using Data.Model;
using Data.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Models;

namespace Web.ViewComponents
{
    /// <summary>
    /// The calendar control itself - queries transactions, runs recurring detection, and builds the month
    /// grid. Used both by the full /Calendar page (accountId/year/month picked by the user) and by the Home
    /// dashboard widget (compact = true, defaults to the most recently linked account, current month only).
    /// </summary>
    public class CalendarViewComponent : ViewComponent
    {
        private readonly AppDbContext _db;
        private readonly UserManager<AppUser> _userManager;

        public CalendarViewComponent(AppDbContext db, UserManager<AppUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync(Guid? accountId, int? year, int? month, int? openDay, bool compact)
        {
            var userId = _userManager.GetUserId(HttpContext.User)!;

            // SQLite can't translate ORDER BY on a DateTimeOffset column, so sort client-side after
            // materializing (same workaround TransactionsController.Accounts already uses).
            var accounts = (await _db.LinkedBankAccounts
                .Where(a => a.UserId == userId)
                .ToListAsync())
                .OrderBy(a => a.LinkedAt)
                .ToList();

            var account = accountId is { } id
                ? accounts.FirstOrDefault(a => a.Id == id)
                : accounts.OrderByDescending(a => a.LinkedAt).FirstOrDefault();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var resolvedYear = year ?? today.Year;
            var resolvedMonth = month ?? today.Month;

            if (account is null)
            {
                return View(new CalendarViewModel
                {
                    Accounts = accounts,
                    Account = null,
                    Year = resolvedYear,
                    Month = resolvedMonth,
                    Compact = compact
                });
            }

            var model = await BuildModel(account, accounts, resolvedYear, resolvedMonth, openDay, compact, today);
            return View(model);
        }

        private async Task<CalendarViewModel> BuildModel(LinkedBankAccount account, List<LinkedBankAccount> accounts,
            int year, int month, int? openDay, bool compact, DateOnly today)
        {
            var monthStart = new DateOnly(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var gridStart = monthStart.AddDays(-(((int)monthStart.DayOfWeek + 6) % 7)); // Monday-start grid
            var gridEnd = gridStart.AddDays(41); // always 6 full weeks

            // Recurring detection needs history beyond the visible month to see repeat cycles.
            var detectionStart = monthStart.AddMonths(-6);

            var transactions = await _db.BankAccountTransactions
                .Include(t => t.TransactionCategory)
                .Where(t => t.LinkedBankAccountId == account.Id && t.TransactionDate >= detectionStart && t.TransactionDate <= gridEnd)
                .OrderBy(t => t.TransactionDate)
                .ToListAsync();

            var recurringIds = RecurringTransactionDetector.Detect(transactions);

            var visibleByDate = transactions
                .Where(t => t.TransactionDate >= gridStart && t.TransactionDate <= gridEnd)
                .ToLookup(t => t.TransactionDate);

            var monthTransactions = transactions
                .Where(t => t.TransactionDate >= monthStart && t.TransactionDate <= monthEnd)
                .ToList();

            var maxDaySpent = monthTransactions
                .Where(t => t.Amount < 0)
                .GroupBy(t => t.TransactionDate)
                .Select(g => g.Sum(t => Math.Abs(t.Amount)))
                .DefaultIfEmpty(0)
                .Max();

            var days = new List<CalendarDayViewModel>();
            for (var date = gridStart; date <= gridEnd; date = date.AddDays(1))
            {
                var dayTransactions = visibleByDate[date].OrderBy(t => t.TransactionDate).ToList();
                var spent = dayTransactions.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));
                var income = dayTransactions.Where(t => t.Amount > 0).Sum(t => t.Amount);

                days.Add(new CalendarDayViewModel
                {
                    Date = date,
                    IsCurrentMonth = date >= monthStart && date <= monthEnd,
                    IsToday = date == today,
                    Transactions = dayTransactions,
                    RecurringTransactionIds = recurringIds,
                    TotalSpent = spent,
                    TotalIncome = income,
                    IntensityPct = maxDaySpent > 0 ? Math.Round(Math.Sqrt((double)(spent / maxDaySpent)) * 90, 0) : 0,
                    HasRecurring = dayTransactions.Any(t => recurringIds.Contains(t.Id))
                });
            }

            var monthSpendDayCount = monthTransactions.Where(t => t.Amount < 0).Select(t => t.TransactionDate).Distinct().Count();
            var totalSpent = monthTransactions.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));

            return new CalendarViewModel
            {
                Accounts = accounts,
                Account = account,
                Year = year,
                Month = month,
                Days = days,
                Summary = new CalendarSummaryViewModel
                {
                    TotalSpent = totalSpent,
                    DailyAverage = monthSpendDayCount > 0 ? totalSpent / monthSpendDayCount : 0,
                    RecurringCount = monthTransactions.Count(t => recurringIds.Contains(t.Id))
                },
                Compact = compact,
                OpenDay = openDay
            };
        }
    }
}
