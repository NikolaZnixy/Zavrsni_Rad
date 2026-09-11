namespace Data.Model.Data
{
    public static class CategorizationDtos
    {
        public sealed record CategorizationInput(Guid Id, string Description, decimal Amount);

        public sealed record CategorizationResult(Guid Id, string? Category);
    }
}
