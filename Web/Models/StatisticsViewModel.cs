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
        public string? HighestWeekdayLabel { get; set; }

        // Overall categorization completeness (all-time, not just this window) - same metric as
        // the Dashboard overview, repeated here so the charts' accuracy caveat is visible in context.
        public double CategorizedPercent { get; set; }

        // "What changed recently": the category whose spend moved the most between this month and
        // last month, with the signed percentage change.
        public string? BiggestMoverCategory { get; set; }
        public double? BiggestMoverChangePercent { get; set; }
    }

    public class CategorySliceViewModel
    {
        public string Name { get; set; } = "";
        public decimal Amount { get; set; }
        public string Color { get; set; } = "";
        public Guid? CategoryId { get; set; }
    }

    public class MerchantViewModel
    {
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal PreviousAmount { get; set; }
        public double? DeltaPercent { get; set; }
    }
}

