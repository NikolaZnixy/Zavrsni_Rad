using Data.Model;
using Data.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Web.Controllers.Api
{
    /// <summary>
    /// Backs the global account switcher in the top bar. The chosen account is stored in a cookie so
    /// Dashboard/Calendar/Statistics can all default to it without the user re-selecting it on every page.
    /// </summary>
    [ApiController]
    [Route("api/account-selection")]
    [Authorize]
    public class AccountSelectionController : ControllerBase
    {
        public const string CookieName = "insightify_selected_account";

        private readonly AppDbContext _db;
        private readonly UserManager<AppUser> _userManager;

        public AccountSelectionController(AppDbContext db, UserManager<AppUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [HttpPost("{accountId:guid}")]
        public async Task<IActionResult> Select(Guid accountId)
        {
            var userId = _userManager.GetUserId(User)!;
            var owned = await _db.LinkedBankAccounts.AnyAsync(a => a.Id == accountId && a.UserId == userId);
            if (!owned)
                return NotFound();

            Response.Cookies.Append(CookieName, accountId.ToString(), new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            });

            return Ok();
        }
    }
}
