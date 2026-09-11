using Data.Model.Data;
using Data.Model.Interfaces;
using Data.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    [Authorize(Roles = Constants.AppRoles.ADMIN)]
    [Route("Dashboard/[controller]/{action=Index}/{id?}")]
    public class AdminController : Controller
    {
        private readonly EnableBankingClient _enableBankingClient;
        private readonly ICategorizationService _categorizationService;

        public AdminController(EnableBankingClient enableBankingClient, ICategorizationService categorizationService)
        {
            _enableBankingClient = enableBankingClient;
            _categorizationService = categorizationService;
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
        public async Task<IActionResult> CheckCategorization()
        {
            if (_categorizationService is IAiCategorization aiCategorization)
                return Ok(await aiCategorization.PingAsync(HttpContext.RequestAborted));

            return Ok(new ServiceHealthResult
            {
                Healthy = true,
                Configured = false,
                Message = "No external AI provider is configured; local categorization remains available."
            });
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
