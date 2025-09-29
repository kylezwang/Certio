using Certio.Domain.Matters;
using Certio.Domain.Documents;

namespace Certio.Web.ViewModels
{
    public class DashboardViewModel
    {
        // Firm Summary data
        public int NewMattersThisWeek { get; set; }
        public double NewMattersPercentageChange { get; set; }
        public int BillingBacklogPercentage { get; set; }
        public double BillingBacklogChange { get; set; }
        public int TrustComplianceWarnings { get; set; }
        public string TrustComplianceStatus { get; set; } = "unresolved";

        // Daily Briefing data
        public int FilingsDueToday { get; set; }
        public string FilingsDueDescription { get; set; } = "";
        public int ClientCallsToday { get; set; }
        public string ClientCallsDescription { get; set; } = "";
        public int OverdueInvoices { get; set; }
        public string OverdueInvoicesDescription { get; set; } = "";

        // Next Suggestions data
        public List<NextSuggestion> NextSuggestions { get; set; } = new List<NextSuggestion>();

        // Upcoming Deadlines
        public List<UpcomingDeadline> UpcomingDeadlines { get; set; } = new List<UpcomingDeadline>();

        // Recent Activity
        public List<RecentFile> RecentFiles { get; set; } = new List<RecentFile>();

        // Calendar data - current month/year
        public DateTime CurrentDate { get; set; } = DateTime.Now;
        public List<CalendarEvent> CalendarEvents { get; set; } = new List<CalendarEvent>();
    }

    public class NextSuggestion
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string ActionText { get; set; } = "";
        public string Priority { get; set; } = "Medium"; // High, Medium, Low
    }

    public class UpcomingDeadline
    {
        public string Title { get; set; } = "";
        public DateTime DueDate { get; set; }
        public string Time { get; set; } = "";
        public string Priority { get; set; } = "Medium"; // High, Medium, Low
        public string Category { get; set; } = "";
    }

    public class RecentFile
    {
        public string Name { get; set; } = "";
        public DateTime ModifiedDate { get; set; }
        public string TimeAgo { get; set; } = "";
    }

    public class CalendarEvent
    {
        public DateTime Date { get; set; }
        public string Title { get; set; } = "";
        public string Type { get; set; } = ""; // meeting, deadline, etc.
    }
}
