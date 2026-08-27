namespace Data.Model.Data
{
    /// <summary>Result of a lightweight, non-destructive "ping" against an external service.</summary>
    public class ServiceHealthResult
    {
        public bool Healthy { get; set; }

        // False for services that exist in the admin panel but aren't wired up yet (e.g. Azure Document
        // Intelligence) - lets the UI show a neutral "not configured" state instead of a red failure.
        public bool Configured { get; set; } = true;

        public string Message { get; set; } = "";
        public long LatencyMs { get; set; }
    }
}
