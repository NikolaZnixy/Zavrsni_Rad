using Data.Model;
using Data.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Models;

namespace Web.Controllers
{
    [Authorize]
    [Route("Dashboard/[controller]/{action=Index}/{id?}")]
    public class StatisticsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<AppUser> _userManager;

        private static readonly Dictionary<string, string> CategoryColors = new(StringComparer.OrdinalIgnoreCase)
        {
            ["car"] = "#4dabf7",
            ["gift"] = "#f783ac",
            ["luxury"] = "#ffd43b",
            ["groceries"] = "#51cf66",
            ["subscriptions"] = "#b197fc"
        };
        private const string FallbackCategoryColor = "#9a9a9a";

        public StatisticsController(AppDbContext db, UserManager<AppUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(Guid? accountId)
        {
            var userId = _userManager.GetUserId(User)!;

            // SQLite can't translate ORDER BY on a DateTimeOffset column, so sort client-side after
            // materializing (same workaround used by TransactionsController/CalendarViewComponent).
            var accounts = (await _db.LinkedBankAccounts
                .Where(a => a.UserId == userId)
                .ToListAsync())
                .OrderBy(a => a.LinkedAt)
                .ToList();

            var account = accountId is { } id
                ? accounts.FirstOrDefault(a => a.Id == id)
                : accounts.OrderByDescending(a => a.LinkedAt).FirstOrDefault();

            if (account is null)
                return View(new StatisticsViewModel { Accounts = accounts });

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var windowStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-5);

            var transactions = await _db.BankAccountTransactions
                .Include(t => t.TransactionCategory)
                .Where(t => t.LinkedBankAccountId == account.Id && t.TransactionDate >= windowStart)
                .OrderBy(t => t.TransactionDate)
                .ToListAsync();

            var model = BuildViewModel(account, accounts, transactions, windowStart, today);
            return View(model);
        }

        private static StatisticsViewModel BuildViewModel(LinkedBankAccount account, List<LinkedBankAccount> accounts,
            List<BankAccountTransaction> transactions, DateOnly windowStart, DateOnly today)
        {
            var monthLabels = new List<string>();
            var monthlySpending = new List<decimal>();
            var monthlyIncome = new List<decimal>();

            for (var month = windowStart; month <= today; month = month.AddMonths(1))
            {
                var monthTransactions = transactions.Where(t => t.TransactionDate.Year == month.Year && t.TransactionDate.Month == month.Month);
                monthLabels.Add(month.ToString("MMM yyyy"));
                monthlySpending.Add(monthTransactions.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount)));
                monthlyIncome.Add(monthTransactions.Where(t => t.Amount > 0).Sum(t => t.Amount));
            }

            var categoryBreakdown = transactions
                .Where(t => t.Amount < 0)
                .GroupBy(t => t.TransactionCategory?.Name ?? "Uncategorized")
                .Select(g => new CategorySliceViewModel
                {
                    Name = g.Key,
                    Amount = g.Sum(t => Math.Abs(t.Amount)),
                    Color = CategoryColors.TryGetValue(g.Key, out var color) ? color : FallbackCategoryColor
                })
                .OrderByDescending(c => c.Amount)
                .ToList();

            var topMerchants = transactions
                .Where(t => t.Amount < 0)
                .GroupBy(t => string.IsNullOrWhiteSpace(t.Description) ? "(no description)" : t.Description!.Trim())
                .Select(g => new MerchantViewModel { Description = g.Key, Amount = g.Sum(t => Math.Abs(t.Amount)) })
                .OrderByDescending(m => m.Amount)
                .Take(10)
                .ToList();

            // Average spend per weekday, normalized by how many times that weekday actually occurred in
            // the window (not just by transaction count), so a quiet Tuesday still counts as a $0 Tuesday.
            var weekdayTotals = new decimal[7];
            var weekdayOccurrences = new int[7];
            for (var date = windowStart; date <= today; date = date.AddDays(1))
                weekdayOccurrences[((int)date.DayOfWeek + 6) % 7]++;
            foreach (var t in transactions.Where(t => t.Amount < 0))
                weekdayTotals[((int)t.TransactionDate.DayOfWeek + 6) % 7] += Math.Abs(t.Amount);

            var weekdayLabels = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            var weekdayAverages = weekdayTotals
                .Select((total, i) => weekdayOccurrences[i] > 0 ? Math.Round(total / weekdayOccurrences[i], 2) : 0m)
                .ToList();

            return new StatisticsViewModel
            {
                Accounts = accounts,
                Account = account,
                MonthLabels = monthLabels,
                MonthlySpending = monthlySpending,
                MonthlyIncome = monthlyIncome,
                CategoryBreakdown = categoryBreakdown,
                TopMerchants = topMerchants,
                WeekdayLabels = weekdayLabels.ToList(),
                WeekdayAverages = weekdayAverages
            };
        }
    }
}
