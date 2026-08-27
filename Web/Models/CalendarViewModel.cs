using Data.Model;

namespace Web.Models
{
    public class CalendarViewModel
    {
        public List<LinkedBankAccount> Accounts { get; set; } = new();
        public LinkedBankAccount? Account { get; set; }

        public int Year { get; set; }
        public int Month { get; set; }
        public DateOnly MonthStart => new(Year, Month, 1);

        public List<CalendarDayViewModel> Days { get; set; } = new();
        public CalendarSummaryViewModel Summary { get; set; } = new();

        // Compact = the Home dashboard widget: no account switcher/month nav/legend, days link out to the
        // full /Calendar page instead of opening the inline detail panel in place.
        public bool Compact { get; set; }

        // Day-of-month to auto-select the detail panel for on load (used when arriving here from a compact
        // widget's day link). Null just defaults to today (if visible) or nothing.
        public int? OpenDay { get; set; }
    }
}
