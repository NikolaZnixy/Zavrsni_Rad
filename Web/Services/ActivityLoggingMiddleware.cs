using Data.Model;
using Data.Model.Interfaces;
using System.Diagnostics;

namespace Web.Services
{
    /// <summary>
    /// Records every meaningful user interaction (page visits, API calls, failures) into the activity log
    /// that the admin panel displays. Static files, framework noise and the log window's own polling are
    /// skipped so the log stays readable.
    /// </summary>
    public sealed class ActivityLoggingMiddleware
    {
        private static readonly string[] IgnoredPrefixes =
        [
            "/css/", "/js/", "/lib/", "/images/", "/img/", "/fonts/", "/favicon", "/_framework", "/_vs"
        ];

        private readonly RequestDelegate _next;

        public ActivityLoggingMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, IActivityLogger activityLogger)
        {
            var path = context.Request.Path.Value ?? "/";

            if (ShouldIgnore(context, path))
            {
                await _next(context);
                return;
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await activityLogger.LogAsync(
                    Categorize(path),
                    "UnhandledException",
                    $"{context.Request.Method} {path} threw {ex.GetType().Name}.",
                    ActivityLogLevel.Error,
                    detail: ex.ToString(),
                    durationMs: stopwatch.ElapsedMilliseconds,
                    statusCode: 500);
                throw;
            }

            stopwatch.Stop();

            var status = context.Response.StatusCode;
            var level = status >= 500 ? ActivityLogLevel.Error
                : status >= 400 ? ActivityLogLevel.Warning
                : ActivityLogLevel.Info;

            await activityLogger.LogAsync(
                Categorize(path),
                Describe(context, path),
                $"{context.Request.Method} {path} responded {status}.",
                level,
                durationMs: stopwatch.ElapsedMilliseconds,
                statusCode: status,
                cancellationToken: CancellationToken.None);
        }

        private static bool ShouldIgnore(HttpContext context, string path)
        {
            // The admin log window polls itself; logging those requests would flood the log with noise.
            if (path.StartsWith("/Dashboard/Admin/Logs", StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var prefix in IgnoredPrefixes)
            {
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Anything that looks like an asset request rather than a user interaction.
            var extension = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(extension) && !extension.Equals(".cshtml", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static string Categorize(string path)
        {
            if (path.Contains("/Identity/Account", StringComparison.OrdinalIgnoreCase))
                return "Auth";
            if (path.StartsWith("/api/bank", StringComparison.OrdinalIgnoreCase))
                return "Bank";
            if (path.Contains("/Admin", StringComparison.OrdinalIgnoreCase))
                return "Admin";
            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                return "Api";
            return "Navigation";
        }

        private static string Describe(HttpContext context, string path)
        {
            var trimmed = path.Trim('/');
            if (string.IsNullOrEmpty(trimmed))
                return "Home";

            var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var name = string.Join('/', segments.Take(3));
            return context.Request.Method == "GET" ? $"View {name}" : $"{context.Request.Method} {name}";
        }
    }
}
