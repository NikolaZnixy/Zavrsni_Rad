using Data.Model;
using Data.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Web.Controllers.Api
{
    [ApiController]
    [Route("api/bank")]
    public class BankController : ControllerBase
    {
        private readonly EnableBankingClient _client;
        private readonly AppDbContext _db;
        private readonly UserManager<AppUser> _userManager;

        public BankController(EnableBankingClient client, AppDbContext db, UserManager<AppUser> userManager)
        {
            _client = client;
            _db = db;
            _userManager = userManager;
        }

        private record LinkState(string UserId, string DisplayName, string AspspName, string Country);

        [HttpGet("connect")]
        [Authorize]
        public async Task<IActionResult> Connect(string bank, string country, string displayName, [FromServices] IConfiguration config)
        {
            var redirectUrl = config["EnableBanking:RedirectUrl"]!;
            var userId = _userManager.GetUserId(User)!;

            var state = Convert.ToBase64String(
                JsonSerializer.SerializeToUtf8Bytes(new LinkState(userId, displayName, bank, country)));

            var auth = await _client.StartAuthorizationAsync(bank, country, redirectUrl, state);
            return Redirect(auth.Url);
        }

        [HttpGet("callback")]
        [AllowAnonymous]
        public async Task<IActionResult> Callback(string code, string state)
        {
            var linkState = JsonSerializer.Deserialize<LinkState>(Convert.FromBase64String(state))!;

            var session = await _client.CreateSessionAsync(code);
            var consentValidUntil = DateTimeOffset.UtcNow.AddDays(90);
            var linkedAt = DateTimeOffset.UtcNow;

            for (var i = 0; i < session.Accounts.Count; i++)
            {
                var account = session.Accounts[i];
                var displayName = i == 0 ? linkState.DisplayName : $"{linkState.DisplayName} ({i + 1})";

                _db.LinkedBankAccounts.Add(new LinkedBankAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = linkState.UserId,
                    DisplayName = displayName,
                    AspspName = linkState.AspspName,
                    Country = linkState.Country,
                    EnableBankingAccountId = account.Uid,
                    Iban = account.AccountId?.Iban ?? string.Empty,
                    ConsentValidUntil = consentValidUntil,
                    LinkedAt = linkedAt
                });
            }

            await _db.SaveChangesAsync();

            return RedirectToAction("Accounts", "Transactions");
        }

        [HttpGet("transactions/{linkedAccountId}")]
        [Authorize]
        public async Task<IActionResult> GetTransactions(Guid linkedAccountId)
        {
            var userId = _userManager.GetUserId(User)!;
            var account = await _db.LinkedBankAccounts
                .FirstOrDefaultAsync(a => a.Id == linkedAccountId && a.UserId == userId);

            if (account is null)
                return NotFound();

            var transactions = await _client.GetTransactionsAsync(account.EnableBankingAccountId);
            return Ok(transactions);
        }

        [HttpGet("banks")]
        [Authorize]
        public async Task<IActionResult> GetBanks(string country)
        {
            var result = await _client.GetAspspsAsync(country);
            return Ok(result.Aspsps.Select(a => new { a.Name, a.Country, a.Bic }));
        }
    }
}
