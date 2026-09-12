using Data.Model;

namespace Web.Models
{
    public class TransactionsListViewModel
    {
        public LinkedBankAccount Account { get; set; } = null!;
        public List<BankAccountTransaction> Transactions { get; set; } = new();
        public List<TransactionCategory> Categories { get; set; } = new();
        public int DaysAgo { get; set; }

        // Filter/sort state, echoed back into the filter form so it stays in sync with the URL.
        public string Type { get; set; } = "all"; // all | income | expense
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
        public Guid? CategoryId { get; set; }
        public string SortBy { get; set; } = "date"; // date | amount
        public string SortDir { get; set; } = "desc"; // asc | desc

        // Last-sync / missed-transactions estimate, shown above the "Fetch transactions" button.
        public int TotalTransactionCount { get; set; }
        public double DailyAverageTransactions { get; set; }
        public int? EstimatedMissedTransactions { get; set; }
    }
}
