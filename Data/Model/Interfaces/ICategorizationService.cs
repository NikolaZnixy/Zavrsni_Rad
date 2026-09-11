using static Data.Model.Data.CategorizationDtos;

namespace Data.Model.Interfaces
{
    public interface ICategorizationService
    {
        Task<IReadOnlyList<CategorizationResult>> CategorizeAsync(
            IReadOnlyList<CategorizationInput> transactions,
            IReadOnlyList<string> categoryNames,
            CancellationToken cancellationToken = default);
    }
}
