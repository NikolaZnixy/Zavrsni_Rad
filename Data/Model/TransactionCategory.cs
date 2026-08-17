namespace Data.Model
{
    public class TransactionCategory
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;

        //EF navigation property
        public virtual ICollection<BankAccountTransaction> Transactions { get; set; } = new List<BankAccountTransaction>();
    }
}
