namespace Data.Model
{
    public enum ActivityLogLevel
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    /// <summary>
    /// A single application activity record (sign-ins, bank syncs, categorization runs, failures, ...).
    /// Rendered in the admin panel's log window.
    /// </summary>
    public class ActivityLogEntry
    {
        public long Id { get; set; }

        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        public ActivityLogLevel Level { get; set; } = ActivityLogLevel.Info;

        /// <summary>Coarse grouping shown as a filter in the admin log window, e.g. "Auth", "Bank".</summary>
        public string Category { get; set; } = "General";

        /// <summary>Short machine-ish name of what happened, e.g. "SignIn", "SyncTransactions".</summary>
        public string Action { get; set; } = "";

        public string Message { get; set; } = "";

        public string? UserId { get; set; }

        public string? UserName { get; set; }

        public string? IpAddress { get; set; }

        public string? Path { get; set; }

        public string? Method { get; set; }

        public int? StatusCode { get; set; }

        public long? DurationMs { get; set; }

        /// <summary>Optional extra context (exception text, counts, ids) shown when a row is expanded.</summary>
        public string? Detail { get; set; }
    }
}
