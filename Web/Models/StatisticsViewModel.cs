using Data.Model;

namespace Web.Models
{
    public class StatisticsViewModel
    {
        public List<LinkedBankAccount> Accounts { get; set; } = new();
        public LinkedBankAccount? Account { get; set; }

        // 6-month window, oldest first - shared x-axis for the spending/income-vs-expenses charts.
        public List<string> MonthLabels { get; set; } = new();
        public List<decimal> MonthlySpending { get; set; } = new();
        public List<decimal> MonthlyIncome { get; set; } = new();

        public List<CategorySliceViewModel> CategoryBreakdown { get; set; } = new();
        public List<MerchantViewModel> TopMerchants { get; set; } = new();

        // Average spend per weekday across the window, Monday first, length 7.
        public List<string> WeekdayLabels { get; set; } = new();
        public List<decimal> WeekdayAverages { get; set; } = new();
    }

    public class CategorySliceViewModel
    {
        public string Name { get; set; } = "";
        public decimal Amount { get; set; }
        public string Color { get; set; } = "";
    }

    public class MerchantViewModel
    {
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
    }
}
