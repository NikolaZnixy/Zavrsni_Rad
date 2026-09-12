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
    public class TransactionsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<AppUser> _userManager;

        public TransactionsController(AppDbContext db, UserManager<AppUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Accounts()
        {
            var userId = _userManager.GetUserId(User)!;

            var accounts = (await _db.LinkedBankAccounts
                .Where(a => a.UserId == userId)
                .ToListAsync())
                .OrderBy(a => a.LinkedAt)
                .ToList();

            return View(accounts);
        }
        public async Task<IActionResult> List(
            Guid accountId,
            int daysAgo = 14,
            string type = "all",
            decimal? minAmount = null,
            decimal? maxAmount = null,
            Guid? categoryId = null,
            string sortBy = "date",
            string sortDir = "desc")
        {
            var userId = _userManager.GetUserId(User)!;

            var account = await _db.LinkedBankAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

            if (account is null)
                return NotFound();

            var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-daysAgo));

            var query = _db.BankAccountTransactions
                .Include(t => t.TransactionCategory)
                .Where(t => t.LinkedBankAccountId == accountId && t.TransactionDate >= cutoff);

            if (type == "income")
                query = query.Where(t => t.Amount >= 0);
            else if (type == "expense")
                query = query.Where(t => t.Amount < 0);

            if (minAmount.HasValue)
                query = query.Where(t => t.Amount >= minAmount.Value);
            if (maxAmount.HasValue)
                query = query.Where(t => t.Amount <= maxAmount.Value);
            if (categoryId.HasValue)
                query = query.Where(t => t.TransactionCategoryId == categoryId.Value);

            var ascending = sortDir == "asc";
            query = sortBy == "amount"
                ? (ascending ? query.OrderBy(t => t.Amount) : query.OrderByDescending(t => t.Amount))
                : (ascending ? query.OrderBy(t => t.TransactionDate) : query.OrderByDescending(t => t.TransactionDate));

            var transactions = await query.ToListAsync();

            var categories = await _db.TransactionCategories
                .OrderBy(c => c.Name)
                .ToListAsync();

            // Last-sync / missed-transactions estimate: how many transactions the user is
            // probably missing out on, based on how many normally show up per day.
            var totalTransactionCount = await _db.BankAccountTransactions
                .CountAsync(t => t.LinkedBankAccountId == accountId);

            double dailyAverage = 0;
            int? estimatedMissed = null;

            if (totalTransactionCount > 0)
            {
                var earliestDate = await _db.BankAccountTransactions
                    .Where(t => t.LinkedBankAccountId == accountId)
                    .MinAsync(t => t.TransactionDate);

                var historyDays = Math.Max(1, DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - earliestDate.DayNumber + 1);
                dailyAverage = (double)totalTransactionCount / historyDays;

                if (account.LastSyncedAt is { } lastSyncedAt)
                {
                    var daysSinceSync = Math.Max(0, (DateTime.UtcNow - lastSyncedAt.UtcDateTime).TotalDays);
                    estimatedMissed = (int)Math.Round(dailyAverage * daysSinceSync);
                }
            }

            return View(new TransactionsListViewModel
            {
                Account = account,
                Transactions = transactions,
                Categories = categories,
                DaysAgo = daysAgo,
                Type = type,
                MinAmount = minAmount,
                MaxAmount = maxAmount,
                CategoryId = categoryId,
                SortBy = sortBy,
                SortDir = sortDir,
                TotalTransactionCount = totalTransactionCount,
                DailyAverageTransactions = dailyAverage,
                EstimatedMissedTransactions = estimatedMissed
            });
        }

        [HttpPost]
        public async Task<IActionResult> Clear(Guid accountId)
        {
            var userId = _userManager.GetUserId(User)!;

            var account = await _db.LinkedBankAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

            if (account is null)
                return NotFound();

            await _db.BankAccountTransactions
                .Where(t => t.LinkedBankAccountId == accountId)
                .ExecuteDeleteAsync();

            account.LastSyncedAt = null;
            await _db.SaveChangesAsync();

            return RedirectToAction("List", new { accountId });
        }
    }
}
