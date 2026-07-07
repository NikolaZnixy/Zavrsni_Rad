namespace Data.Model.Interfaces
{
    public interface ILinkedBankAccount
    {
        public Guid Id { get; }
        public string UserId { get; }
        public string DisplayName { get; }
        public string AspspName { get; }
        public string Country { get; }
        public Guid EnableBankingAccountId { get; }
        public string Iban { get; }
        public DateTimeOffset ConsentValidUntil { get; }
        public DateTimeOffset LinkedAt { get; }
    }
}
