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

        /// <summary>Name of the provider actually behind the check, when it can vary (e.g. "OpenAI").</summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Ordered extra detail rendered as a small table in the admin panel - quotas, rate-limit windows,
        /// token usage, model availability, per-probe latencies, and so on.
        /// </summary>
        public List<ServiceHealthMetric> Metrics { get; set; } = [];
    }

    public class ServiceHealthMetric
    {
        public ServiceHealthMetric() { }

        public ServiceHealthMetric(string label, string value, string status = "neutral")
        {
            Label = label;
            Value = value;
            Status = status;
        }

        public string Label { get; set; } = "";
        public string Value { get; set; } = "";

        /// <summary>One of "good", "warn", "bad", "neutral" - drives the colour of the value in the UI.</summary>
        public string Status { get; set; } = "neutral";
    }
}
