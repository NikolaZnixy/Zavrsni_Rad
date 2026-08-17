namespace Data.Model.Data
{
    /// <summary>
    /// Fixed GUIDs for the seeded <see cref="TransactionCategory"/> rows, so EF's HasData seeding
    /// produces a stable migration instead of a new random Id every time the model is rebuilt.
    /// </summary>
    public static class TransactionCategorySeedIds
    {
        public static readonly Guid Car = new("f47e9b1a-0f2e-4a3c-9a1d-000000000001");
        public static readonly Guid Gift = new("f47e9b1a-0f2e-4a3c-9a1d-000000000002");
        public static readonly Guid Luxury = new("f47e9b1a-0f2e-4a3c-9a1d-000000000003");
        public static readonly Guid Groceries = new("f47e9b1a-0f2e-4a3c-9a1d-000000000004");
        public static readonly Guid Subscriptions = new("f47e9b1a-0f2e-4a3c-9a1d-000000000005");
    }
}
