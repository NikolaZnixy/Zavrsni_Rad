using Data.Model;
using Data.Model.Data;
using Data.Model.Interfaces;
using Data.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Web.Controllers
{
    [Authorize(Roles = Constants.AppRoles.ADMIN)]
    [Route("Dashboard/[controller]/{action=Index}/{id?}")]
    public class AdminController : Controller
    {
        private readonly EnableBankingClient _enableBankingClient;
        private readonly ICategorizationService _categorizationService;
        private readonly AppDbContext _db;
        private readonly IActivityLogger _activityLogger;

        public AdminController(
            EnableBankingClient enableBankingClient,
            ICategorizationService categorizationService,
            AppDbContext db,
            IActivityLogger activityLogger)
        {
            _enableBankingClient = enableBankingClient;
            _categorizationService = categorizationService;
            _db = db;
            _activityLogger = activityLogger;
        }

        public IActionResult Index() => View();

        // Each check is its own POST, fired only when the admin clicks "Check now" for that specific
        // service - nothing here runs automatically on page load.

        [HttpPost]
        public async Task<IActionResult> CheckEnableBanking()
        {
            var result = await _enableBankingClient.PingAsync();
            await _activityLogger.LogAsync(
                "Admin",
                "HealthCheck",
                $"Enable Banking health check: {(result.Healthy ? "healthy" : "unhealthy")}.",
                result.Healthy ? ActivityLogLevel.Info : ActivityLogLevel.Warning,
                detail: result.Message,
                durationMs: result.LatencyMs);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> CheckCategorization()
        {
            if (_categorizationService is IAiCategorization aiCategorization)
            {
                var result = await aiCategorization.PingAsync(HttpContext.RequestAborted);
                await _activityLogger.LogAsync(
                    "Admin",
                    "HealthCheck",
                    $"{aiCategorization.ProviderName} health check: {(result.Healthy ? "healthy" : "unhealthy")}.",
                    result.Healthy ? ActivityLogLevel.Info : ActivityLogLevel.Warning,
                    detail: result.Message,
                    durationMs: result.LatencyMs);
                return Ok(result);
            }

            return Ok(new ServiceHealthResult
            {
                Healthy = true,
                Configured = false,
                Message = "No external AI provider is configured; local categorization remains available."
            });
        }

        /// <summary>Feeds the admin activity log window. Excluded from activity logging itself.</summary>
        [HttpGet]
        public async Task<IActionResult> Logs(string? level, string? category, string? search, int take = 200)
        {
            take = Math.Clamp(take, 1, 500);

            var query = _db.ActivityLogEntries.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(level) && Enum.TryParse<ActivityLogLevel>(level, true, out var parsedLevel))
                query = query.Where(e => e.Level == parsedLevel);

            if (!string.IsNullOrWhiteSpace(category) && !category.Equals("all", StringComparison.OrdinalIgnoreCase))
                query = query.Where(e => e.Category == category);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(e =>
                    EF.Functions.Like(e.Message, $"%{term}%")
                    || EF.Functions.Like(e.Action, $"%{term}%")
                    || (e.UserName != null && EF.Functions.Like(e.UserName, $"%{term}%"))
                    || (e.Path != null && EF.Functions.Like(e.Path, $"%{term}%")));
            }

            var rows = await query
                .OrderByDescending(e => e.Id)
                .Take(take)
                .ToListAsync();

            var entries = rows.Select(e => new
            {
                e.Id,
                e.Timestamp,
                Level = e.Level.ToString(),
                e.Category,
                e.Action,
                e.Message,
                e.UserName,
                e.IpAddress,
                e.Path,
                e.Method,
                e.StatusCode,
                e.DurationMs,
                e.Detail
            });

            var categories = await _db.ActivityLogEntries
                .AsNoTracking()
                .Select(e => e.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return Ok(new { entries, categories });
        }

        [HttpPost]
        public async Task<IActionResult> ClearLogs()
        {
            var removed = await _db.ActivityLogEntries.ExecuteDeleteAsync();
            await _activityLogger.LogAsync(
                "Admin",
                "ClearLogs",
                $"Activity log cleared ({removed} entries removed).",
                ActivityLogLevel.Warning);
            return Ok(new { removed });
        }
    }
}
