using Data.Model.Interfaces;

namespace Data.Model
{
    public class LinkedBankAccount : ILinkedBankAccount
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string AspspName { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public Guid EnableBankingAccountId { get; set; }
        public string Iban { get; set; } = string.Empty;
        public DateTimeOffset ConsentValidUntil { get; set; }
        public DateTimeOffset LinkedAt { get; set; }

        //EF navigation property
        public virtual AppUser User { get; set; } = null!;

    }
}
