using Data.Model;
using Data.Model.Interfaces;
using Data.Services;
using Microsoft.EntityFrameworkCore;

namespace Web.Services
{
    /// <summary>
    /// Persists activity records to the database, enriching them with whoever/whatever triggered the
    /// current request. Failures here are swallowed on purpose - logging must never break a user action.
    /// </summary>
    public sealed class ActivityLogger : IActivityLogger
    {
        // Keeps the log window (and the SQLite file) from growing without bound.
        private const int MaxEntries = 2000;
        private const int TrimBatch = 200;
        private static int _writesSinceTrim;

        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ActivityLogger> _logger;

        public ActivityLogger(AppDbContext db, IHttpContextAccessor httpContextAccessor, ILogger<ActivityLogger> logger)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task LogAsync(
            string category,
            string action,
            string message,
            ActivityLogLevel level = ActivityLogLevel.Info,
            string? detail = null,
            long? durationMs = null,
            int? statusCode = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var http = _httpContextAccessor.HttpContext;
                var user = http?.User;
                var isAuthenticated = user?.Identity?.IsAuthenticated == true;

                var entry = new ActivityLogEntry
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Level = level,
                    Category = category,
                    Action = action,
                    Message = Truncate(message, 1000) ?? "",
                    Detail = Truncate(detail, 4000),
                    DurationMs = durationMs,
                    StatusCode = statusCode,
                    UserId = isAuthenticated
                        ? user!.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                        : null,
                    UserName = isAuthenticated ? user!.Identity!.Name : "anonymous",
                    IpAddress = http?.Connection.RemoteIpAddress?.ToString(),
                    Path = http?.Request.Path.Value,
                    Method = http?.Request.Method
                };

                _db.ActivityLogEntries.Add(entry);
                await _db.SaveChangesAsync(cancellationToken);

                if (Interlocked.Increment(ref _writesSinceTrim) >= TrimBatch)
                {
                    Interlocked.Exchange(ref _writesSinceTrim, 0);
                    await TrimAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write activity log entry {Category}/{Action}.", category, action);
            }
        }

        private async Task TrimAsync(CancellationToken cancellationToken)
        {
            var total = await _db.ActivityLogEntries.CountAsync(cancellationToken);
            if (total <= MaxEntries)
                return;

            var stale = await _db.ActivityLogEntries
                .OrderBy(e => e.Id)
                .Take(total - MaxEntries)
                .ToListAsync(cancellationToken);

            _db.ActivityLogEntries.RemoveRange(stale);
            await _db.SaveChangesAsync(cancellationToken);
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return value.Length <= maxLength ? value : value[..maxLength] + "…";
        }
    }
}
