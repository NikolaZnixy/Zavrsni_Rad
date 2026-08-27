using Data.Model;

namespace Web.Models
{
    public class CalendarDayViewModel
    {
        public DateOnly Date { get; set; }
        public bool IsCurrentMonth { get; set; }
        public bool IsToday { get; set; }
        public List<BankAccountTransaction> Transactions { get; set; } = new();

        // Ids (from the wider detection window) that are part of a recurring group - a subset of
        // Transactions on any given day, kept as a set so the view can just call .Contains(t.Id).
        public HashSet<Guid> RecurringTransactionIds { get; set; } = new();

        public decimal TotalSpent { get; set; }
        public decimal TotalIncome { get; set; }

        // 0-100, spend intensity relative to this month's busiest day. Drives the day cell's heat color.
        public double IntensityPct { get; set; }

        public bool HasRecurring { get; set; }
    }
}
