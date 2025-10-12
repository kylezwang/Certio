namespace Certio.Application.DTOs
{
    public class CreateSubTaskDto
    {
        public string Title { get; set; } = "";
        public DateTime? DueDate { get; set; }
        public List<int>? AssignedUserIds { get; set; }
    }

    public class UpdateSubTaskDto
    {
        public string? Title { get; set; }
        public DateTime? DueDate { get; set; }
        public bool? IsCompleted { get; set; }
    }

    public class SubTaskDto
    {
        public int Id { get; set; }
        public int TaskId { get; set; }
        public int MatterId { get; set; }
        public int OrgId { get; set; }
        public string Title { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? LastModifiedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<SubTaskAssignmentDto> Assignments { get; set; } = new();
    }

    public class SubTaskAssignmentDto
    {
        public int Id { get; set; }
        public int SubTaskItemId { get; set; }
        public int UserId { get; set; }
        public string AssignmentType { get; set; } = "";
        public string? Role { get; set; }
        public bool IsNotifyRecipient { get; set; }
        public DateTime AssignedAt { get; set; }
        public UserSummaryDto? User { get; set; }
    }

    public class AssignSubTaskDto
    {
        public int UserId { get; set; }
        public string AssignmentType { get; set; } = "Assignee";
        public string? Role { get; set; }
        public bool IsNotifyRecipient { get; set; } = true;
    }
}

