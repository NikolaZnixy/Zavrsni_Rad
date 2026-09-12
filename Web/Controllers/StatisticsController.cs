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

        // Reads the globally selected account (top bar dropdown) from its cookie, set by
        // Api/AccountSelectionController.
        private bool TryGetSelectedAccountId(out Guid accountId) =>
            Guid.TryParse(Request.Cookies[Api.AccountSelectionController.CookieName], out accountId);

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User)!;

            // SQLite can't translate ORDER BY on a DateTimeOffset column, so sort client-side after
            // materializing (same workaround used by TransactionsController/CalendarViewComponent).
            var accounts = (await _db.LinkedBankAccounts
                .Where(a => a.UserId == userId)
                .ToListAsync())
                .OrderBy(a => a.LinkedAt)
                .ToList();

            // The page no longer has its own account picker - it always follows the global top bar
            // switcher (falling back to the most recently linked account if nothing's selected yet).
            var account = (TryGetSelectedAccountId(out var cid) ? accounts.FirstOrDefault(a => a.Id == cid) : null)
                ?? accounts.OrderByDescending(a => a.LinkedAt).FirstOrDefault();

            if (account is null)
                return View(new StatisticsViewModel { Accounts = accounts });

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var windowStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-5);
            var previousWindowStart = windowStart.AddMonths(-6);

            // Pull both the visible 6-month window and the 6 months before it in one query, so
            // top-merchant deltas can compare "this window" vs "the window before it".
            var allTransactions = await _db.BankAccountTransactions
                .Include(t => t.TransactionCategory)
                .Where(t => t.LinkedBankAccountId == account.Id && t.TransactionDate >= previousWindowStart)
                .OrderBy(t => t.TransactionDate)
                .ToListAsync();

            var transactions = allTransactions.Where(t => t.TransactionDate >= windowStart).ToList();
            var previousWindowTransactions = allTransactions.Where(t => t.TransactionDate < windowStart).ToList();

            var totalTransactionCount = await _db.BankAccountTransactions
                .CountAsync(t => t.LinkedBankAccountId == account.Id);
            var categorizedCount = await _db.BankAccountTransactions
                .CountAsync(t => t.LinkedBankAccountId == account.Id && t.TransactionCategoryId != null);

            var model = BuildViewModel(account, accounts, transactions, previousWindowTransactions, windowStart, today);
            model.CategorizedPercent = totalTransactionCount > 0 ? Math.Round(100.0 * categorizedCount / totalTransactionCount, 1) : 0;

            return View(model);
        }

        private static StatisticsViewModel BuildViewModel(LinkedBankAccount account, List<LinkedBankAccount> accounts,
            List<BankAccountTransaction> transactions, List<BankAccountTransaction> previousWindowTransactions,
            DateOnly windowStart, DateOnly today)
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
                    Color = CategoryColors.TryGetValue(g.Key, out var color) ? color : FallbackCategoryColor,
                    CategoryId = g.FirstOrDefault(t => t.TransactionCategoryId != null)?.TransactionCategoryId
                })
                .OrderByDescending(c => c.Amount)
                .ToList();

            var previousMerchantTotals = previousWindowTransactions
                .Where(t => t.Amount < 0)
                .GroupBy(t => string.IsNullOrWhiteSpace(t.Description) ? "(no description)" : t.Description!.Trim())
                .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));

            var topMerchants = transactions
                .Where(t => t.Amount < 0)
                .GroupBy(t => string.IsNullOrWhiteSpace(t.Description) ? "(no description)" : t.Description!.Trim())
                .Select(g =>
                {
                    var amount = g.Sum(t => Math.Abs(t.Amount));
                    var previousAmount = previousMerchantTotals.GetValueOrDefault(g.Key);
                    double? delta = previousAmount > 0 ? (double)((amount - previousAmount) / previousAmount) * 100 : null;
                    return new MerchantViewModel { Description = g.Key, Amount = amount, PreviousAmount = previousAmount, DeltaPercent = delta };
                })
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

            string? highestWeekdayLabel = null;
            if (weekdayAverages.Any(a => a > 0))
            {
                var maxIndex = weekdayAverages.IndexOf(weekdayAverages.Max());
                highestWeekdayLabel = weekdayLabels[maxIndex];
            }

            // "Biggest mover": the category whose spend changed the most (in absolute terms) between
            // this month and last month, as a quick "what changed recently" callout.
            var thisMonthStart = new DateOnly(today.Year, today.Month, 1);
            var lastMonthStart = thisMonthStart.AddMonths(-1);
            var thisMonthByCategory = transactions
                .Where(t => t.Amount < 0 && t.TransactionDate >= thisMonthStart)
                .GroupBy(t => t.TransactionCategory?.Name ?? "Uncategorized")
                .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));
            var lastMonthByCategory = transactions
                .Where(t => t.Amount < 0 && t.TransactionDate >= lastMonthStart && t.TransactionDate < thisMonthStart)
                .GroupBy(t => t.TransactionCategory?.Name ?? "Uncategorized")
                .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));

            string? biggestMoverCategory = null;
            double? biggestMoverChangePercent = null;
            var allCategoryNames = thisMonthByCategory.Keys.Union(lastMonthByCategory.Keys);
            decimal biggestAbsChange = 0;
            foreach (var name in allCategoryNames)
            {
                var thisAmt = thisMonthByCategory.GetValueOrDefault(name);
                var lastAmt = lastMonthByCategory.GetValueOrDefault(name);
                if (lastAmt <= 0) continue; // need a baseline to talk about % change meaningfully

                var absChange = Math.Abs(thisAmt - lastAmt);
                if (absChange > biggestAbsChange)
                {
                    biggestAbsChange = absChange;
                    biggestMoverCategory = name;
                    biggestMoverChangePercent = (double)((thisAmt - lastAmt) / lastAmt) * 100;
                }
            }

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
                WeekdayAverages = weekdayAverages,
                HighestWeekdayLabel = highestWeekdayLabel,
                BiggestMoverCategory = biggestMoverCategory,
                BiggestMoverChangePercent = biggestMoverChangePercent
            };
        }
    }
}
