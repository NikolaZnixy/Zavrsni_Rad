using Data.Model;
using Data.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Controllers.Api;
using Web.Models;

namespace Web.Controllers
{
    // Landing page for signed-in users. Left panel: an at-a-glance overview of the globally
    // selected account (top bar dropdown). Right panel: the compact calendar widget.
    // Everything reachable from the floating sidebar hangs off this /Dashboard/ prefix.
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<AppUser> _userManager;

        public DashboardController(AppDbContext db, UserManager<AppUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User)!;

            var accounts = (await _db.LinkedBankAccounts
                .Where(a => a.UserId == userId)
                .ToListAsync())
                .OrderBy(a => a.LinkedAt)
                .ToList();

            var hasSelectedCookie = Guid.TryParse(Request.Cookies[AccountSelectionController.CookieName], out var selectedId);
            var account = (hasSelectedCookie ? accounts.FirstOrDefault(a => a.Id == selectedId) : null)
                ?? accounts.OrderByDescending(a => a.LinkedAt).FirstOrDefault();

            if (account is null)
                return View(new DashboardViewModel { Accounts = accounts });

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var thisMonthStart = new DateOnly(today.Year, today.Month, 1);
            var lastMonthStart = thisMonthStart.AddMonths(-1);
            var historyStart = thisMonthStart.AddMonths(-6);

            var transactions = await _db.BankAccountTransactions
                .Include(t => t.TransactionCategory)
                .Where(t => t.LinkedBankAccountId == account.Id && t.TransactionDate >= historyStart)
                .ToListAsync();

            var totalTransactionCount = await _db.BankAccountTransactions
                .CountAsync(t => t.LinkedBankAccountId == account.Id);
            var categorizedCount = await _db.BankAccountTransactions
                .CountAsync(t => t.LinkedBankAccountId == account.Id && t.TransactionCategoryId != null);

            var thisMonthTx = transactions.Where(t => t.TransactionDate >= thisMonthStart).ToList();
            var lastMonthTx = transactions.Where(t => t.TransactionDate >= lastMonthStart && t.TransactionDate < thisMonthStart).ToList();

            var spentThisMonth = thisMonthTx.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));
            var spentLastMonth = lastMonthTx.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));

            double? momChange = spentLastMonth > 0
                ? (double)((spentThisMonth - spentLastMonth) / spentLastMonth) * 100
                : null;

            var topCategory = thisMonthTx
                .Where(t => t.Amount < 0 && t.TransactionCategory != null)
                .GroupBy(t => t.TransactionCategory!.Name)
                .OrderByDescending(g => g.Sum(t => Math.Abs(t.Amount)))
                .Select(g => g.Key)
                .FirstOrDefault();

            var recurringCount = RecurringTransactionDetector.Detect(transactions.OrderBy(t => t.TransactionDate).ToList()).Count;

            var model = new DashboardViewModel
            {
                Account = account,
                Accounts = accounts,
                TransactionCount = totalTransactionCount,
                LastSyncedAt = account.LastSyncedAt,
                CategorizedCount = categorizedCount,
                CategorizedPercent = totalTransactionCount > 0 ? Math.Round(100.0 * categorizedCount / totalTransactionCount, 1) : 0,
                SpentThisMonth = spentThisMonth,
                SpentLastMonth = spentLastMonth,
                MonthOverMonthChangePercent = momChange,
                TopCategoryThisMonth = topCategory,
                RecurringCount = recurringCount
            };

            return View(model);
        }
    }
}
