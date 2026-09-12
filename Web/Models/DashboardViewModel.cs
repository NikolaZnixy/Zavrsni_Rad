using Data.Model;

namespace Web.Models
{
    public class DashboardViewModel
    {
        public LinkedBankAccount? Account { get; set; }
        public List<LinkedBankAccount> Accounts { get; set; } = new();

        public int TransactionCount { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }

        public int CategorizedCount { get; set; }
        public double CategorizedPercent { get; set; }

        public decimal SpentThisMonth { get; set; }
        public decimal SpentLastMonth { get; set; }
        public double? MonthOverMonthChangePercent { get; set; }

        public string? TopCategoryThisMonth { get; set; }
        public int RecurringCount { get; set; }
    }
}
