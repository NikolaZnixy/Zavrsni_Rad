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
        public async Task<IActionResult> List(Guid accountId, int daysAgo = 14)
        {
            var userId = _userManager.GetUserId(User)!;

            var account = await _db.LinkedBankAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

            if (account is null)
                return NotFound();

            var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-daysAgo));

            var transactions = await _db.BankAccountTransactions
                .Include(t => t.TransactionCategory)
                .Where(t => t.LinkedBankAccountId == accountId && t.TransactionDate >= cutoff)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            var categories = await _db.TransactionCategories
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(new TransactionsListViewModel
            {
                Account = account,
                Transactions = transactions,
                Categories = categories,
                DaysAgo = daysAgo
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
