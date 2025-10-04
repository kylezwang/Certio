using Certio.Domain.Matters;
using Certio.Domain.Users;

namespace Certio.Web.ViewModels
{
    public class TasksViewModel
    {
        public List<TaskItemViewModel> AllTasks { get; set; } = new List<TaskItemViewModel>();
        public List<TaskItemViewModel> PlannedTasks { get; set; } = new List<TaskItemViewModel>();
        public List<TaskItemViewModel> InProgressTasks { get; set; } = new List<TaskItemViewModel>();
        public List<TaskItemViewModel> ReviewTasks { get; set; } = new List<TaskItemViewModel>();
        public List<TaskItemViewModel> CompletedTasks { get; set; } = new List<TaskItemViewModel>();
        public List<MatterOption> Matters { get; set; } = new List<MatterOption>();
        public List<UserOption> Users { get; set; } = new List<UserOption>();
    }
    
    public class TaskItemViewModel
    {
        public int Id { get; set; }
        public int MatterId { get; set; }
        public string MatterTitle { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Status { get; set; } = "";
        public string Priority { get; set; } = "";
        public string? Location { get; set; }
        public int Order { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public int TotalSubTasks { get; set; }
        public int CompletedSubTasks { get; set; }
        public List<TaskAssignmentViewModel> Assignments { get; set; } = new List<TaskAssignmentViewModel>();
        public List<TaskCommentViewModel> Comments { get; set; } = new List<TaskCommentViewModel>();
        public List<TaskItemViewModel> SubTasks { get; set; } = new List<TaskItemViewModel>();
        public List<int> RelatedDocumentIds { get; set; } = new List<int>();
        
        // Computed properties
        public string PriorityClass => Priority?.ToLower() switch
        {
            "critical" => "priority-critical",
            "high" => "priority-high",
            "medium" => "priority-medium",
            "low" => "priority-low",
            _ => "priority-medium"
        };
        
        public string DueDateFormatted => DueDate?.ToString("MMM dd, yyyy") ?? "";
        public bool IsOverdue => DueDate.HasValue && DueDate.Value < DateTime.Now && Status != "Completed";
        public string ProgressPercentage => TotalSubTasks > 0 ? $"{(CompletedSubTasks * 100 / TotalSubTasks)}%" : "0%";
    }
    
    public class TaskAssignmentViewModel
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = "";
        public string UserInitials { get; set; } = "";
        public string AssignmentType { get; set; } = "";
        public string Role { get; set; } = "";
    }
    
    public class TaskCommentViewModel
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string TimeAgo { get; set; } = "";
    }
    
    public class MatterOption
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string PracticeArea { get; set; } = "";
    }
    
    public class UserOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Initials { get; set; } = "";
    }
}

