using Certio.Domain.Matters;
using Certio.Domain.Tasks;

namespace Certio.Web.ViewModels
{
    public class MatterDetailsViewModel
    {
        // Matter Information
        public Matter Matter { get; set; } = null!;
        
        // Task Statistics (Last 7 Days)
        public int TasksCompletedLast7Days { get; set; }
        public int TasksUpdatedLast7Days { get; set; }
        public int TasksCreatedLast7Days { get; set; }
        public int TasksDueSoon { get; set; } // Next 7 days
        
        // Priority Breakdown
        public int HighPriorityCount { get; set; }
        public int MediumPriorityCount { get; set; }
        public int LowPriorityCount { get; set; }
        public int CriticalPriorityCount { get; set; }
        public int TotalTasksCount { get; set; }
        
        // Task Type/Status Distribution
        public int TasksPending { get; set; }
        public int TasksInProgress { get; set; }
        public int TasksInReview { get; set; }
        public int TasksCompleted { get; set; }
        
        // Recent Activity
        public List<RecentActivityItem> RecentActivity { get; set; } = new List<RecentActivityItem>();
        
        // Computed Properties
        public double HighPriorityPercentage => TotalTasksCount > 0 ? (double)HighPriorityCount / TotalTasksCount * 100 : 0;
        public double MediumPriorityPercentage => TotalTasksCount > 0 ? (double)MediumPriorityCount / TotalTasksCount * 100 : 0;
        public double LowPriorityPercentage => TotalTasksCount > 0 ? (double)LowPriorityCount / TotalTasksCount * 100 : 0;
        public double CriticalPriorityPercentage => TotalTasksCount > 0 ? (double)CriticalPriorityCount / TotalTasksCount * 100 : 0;
        
        public double InProgressPercentage => TotalTasksCount > 0 ? (double)TasksInProgress / TotalTasksCount * 100 : 0;
    }
    
    public class RecentActivityItem
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string UserName { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string ActivityType { get; set; } = ""; // Created, Updated, Completed, Commented
    }
}

