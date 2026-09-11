using Data.Model.Interfaces;
using static Data.Model.Data.CategorizationDtos;

namespace Data.Services
{
    public sealed class NoOpCategorizationService : ICategorizationService
    {
        public Task<IReadOnlyList<CategorizationResult>> CategorizeAsync(
            IReadOnlyList<CategorizationInput> transactions,
            IReadOnlyList<string> categoryNames,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CategorizationResult>>([]);
    }
}
