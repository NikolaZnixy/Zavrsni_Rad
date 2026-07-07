using Data.Model;

namespace Web.Models
{
    public class TransactionsListViewModel
    {
        public LinkedBankAccount Account { get; set; } = null!;
        public List<BankAccountTransaction> Transactions { get; set; } = new();
    }
}
