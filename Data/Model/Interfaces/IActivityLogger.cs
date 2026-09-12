using Data.Model;

namespace Data.Model.Interfaces
{
    /// <summary>Writes user-facing activity records that the admin log window reads back.</summary>
    public interface IActivityLogger
    {
        Task LogAsync(
            string category,
            string action,
            string message,
            ActivityLogLevel level = ActivityLogLevel.Info,
            string? detail = null,
            long? durationMs = null,
            int? statusCode = null,
            CancellationToken cancellationToken = default);
    }
}
