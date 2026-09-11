using Data.Model.Data;

namespace Data.Model.Interfaces
{
    public interface IAiCategorization : ICategorizationService
    {
        string ProviderName { get; }

        Task<ServiceHealthResult> PingAsync(CancellationToken cancellationToken = default);
    }
}
