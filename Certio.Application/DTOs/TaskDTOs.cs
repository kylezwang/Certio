namespace Certio.Application.DTOs
{
    public class CreateTaskDto
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string Status { get; set; } = "Pending";
        public string Priority { get; set; } = "Medium";
        public string? Location { get; set; }
        public DateTime? DueDate { get; set; }
        public int Order { get; set; } = 0;
        public List<int>? AssignedUserIds { get; set; }
    }

    public class UpdateTaskDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public string? Location { get; set; }
        public DateTime? DueDate { get; set; }
        public int? Order { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class TaskDto
    {
        public int Id { get; set; }
        public int OrgId { get; set; }
        public int MatterId { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string Status { get; set; } = "";
        public string Priority { get; set; } = "";
        public string? Location { get; set; }
        public int Order { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? LastModifiedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<TaskAssignmentDto> Assignments { get; set; } = new();
        public List<TaskCommentDto> Comments { get; set; } = new();
        public List<SubTaskDto> SubTasks { get; set; } = new();
        public string? MatterTitle { get; set; }
    }

    public class TaskAssignmentDto
    {
        public int Id { get; set; }
        public int TaskItemId { get; set; }
        public int UserId { get; set; }
        public string AssignmentType { get; set; } = "";
        public string? Role { get; set; }
        public bool IsNotifyRecipient { get; set; }
        public DateTime AssignedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public UserSummaryDto? User { get; set; }
    }

    public class TaskCommentDto
    {
        public int Id { get; set; }
        public int TaskItemId { get; set; }
        public int UserId { get; set; }
        public string Content { get; set; } = "";
        public int? ParentCommentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastModifiedAt { get; set; }
        public UserSummaryDto? User { get; set; }
        public List<CommentMentionDto> Mentions { get; set; } = new();
        public List<CommentReactionDto> Reactions { get; set; } = new();
    }

    public class CommentMentionDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public UserSummaryDto? User { get; set; }
    }

    public class CommentReactionDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Emoji { get; set; } = "";
        public UserSummaryDto? User { get; set; }
    }

    public class AddTaskCommentDto
    {
        public string Content { get; set; } = "";
        public int? ParentCommentId { get; set; }
        public List<int>? MentionedUserIds { get; set; }
    }

    public class AssignTaskDto
    {
        public int UserId { get; set; }
        public string AssignmentType { get; set; } = "Assignee";
        public string? Role { get; set; }
        public bool IsNotifyRecipient { get; set; } = true;
    }
}

