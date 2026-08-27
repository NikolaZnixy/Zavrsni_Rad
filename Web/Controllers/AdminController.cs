using Data.Model.Data;
using Data.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    [Authorize(Roles = Constants.AppRoles.ADMIN)]
    public class AdminController : Controller
    {
        private readonly EnableBankingClient _enableBankingClient;
        private readonly GroqClient _groqClient;

        public AdminController(EnableBankingClient enableBankingClient, GroqClient groqClient)
        {
            _enableBankingClient = enableBankingClient;
            _groqClient = groqClient;
        }

        public IActionResult Index() => View();

        // Each check is its own POST, fired only when the admin clicks "Check now" for that specific
        // service - nothing here runs automatically on page load.

        [HttpPost]
        public async Task<IActionResult> CheckEnableBanking()
        {
            var result = await _enableBankingClient.PingAsync();
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> CheckGroq()
        {
            var result = await _groqClient.PingAsync();
            return Ok(result);
        }

        [HttpPost]
        public IActionResult CheckAzure()
        {
            // Azure Document Intelligence isn't integrated into this app yet - no client, no config.
            return Ok(new ServiceHealthResult
            {
                Healthy = false,
                Configured = false,
                Message = "Azure Document Intelligence isn't configured yet."
            });
        }
    }
}
