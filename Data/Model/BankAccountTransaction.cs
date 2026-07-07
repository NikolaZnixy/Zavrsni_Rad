using Data.Model.Interfaces;

namespace Data.Model
{
    public class BankAccountTransaction : IBankAccountTransaction
    {
        public Guid Id { get; set; }
        public Guid LinkedBankAccountId { get; set; }
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateOnly TransactionDate { get; set; }
        public string? ExternalTransactionId { get; set; } // this is enable banking transaction id, only way to know if transaction has already been fetched

        //Ef navigation property
        public virtual LinkedBankAccount LinkedBankAccount { get; set; } = null!;
    }
}
