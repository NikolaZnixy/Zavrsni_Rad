using Data.Model;
using Data.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    }
}
