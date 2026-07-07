using Data.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers.Api
{
    [ApiController]
    [Route("api/bank")]
    public class BankController : ControllerBase
    {
        private readonly EnableBankingClient _client;

        public BankController(EnableBankingClient client)
        {
            _client = client;
        }

        [HttpGet("connect")]
        [Authorize]
        public async Task<IActionResult> Connect(string bank, string country, [FromServices] IConfiguration config)
        {
            var redirectUrl = config["EnableBanking:RedirectUrl"]!;
            var auth = await _client.StartAuthorizationAsync(bank, country, redirectUrl, Guid.NewGuid().ToString());
            return Redirect(auth.Url);
        }

        [HttpGet("callback")]
        [AllowAnonymous]
        public async Task<IActionResult> Callback(string code)
        {
            var session = await _client.CreateSessionAsync(code);
            var accountUid = session.Accounts.First().Uid;
            var transactions = await _client.GetTransactionsAsync(accountUid);
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
