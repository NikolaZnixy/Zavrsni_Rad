namespace Data.Model.Interfaces
{
    public interface IBankAccountTransaction
    {
        public Guid Id { get; }
        public Guid LinkedBankAccountId { get; }
        public string? Description { get; }
        public decimal Amount { get; }
        public string Currency { get; }
        public DateOnly TransactionDate { get; }
        public string? ExternalTransactionId { get; }
    }
}
