namespace Certio.Application.DTOs
{
    public class UserSummaryDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public string FullName => $"{FirstName} {LastName}";
        public string Initials => $"{(FirstName.Length > 0 ? FirstName[0] : ' ')}{(LastName.Length > 0 ? LastName[0] : ' ')}";
    }

    public class MatterFilterDto
    {
        public string? Status { get; set; }
        public string? PracticeArea { get; set; }
        public int? AssignedUserId { get; set; }
        public DateTime? StartDateFrom { get; set; }
        public DateTime? StartDateTo { get; set; }
        public DateTime? DueDateFrom { get; set; }
        public DateTime? DueDateTo { get; set; }
        public string? SearchTerm { get; set; }
    }

    public class TaskFilterDto
    {
        public int? MatterId { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public int? AssignedUserId { get; set; }
        public DateTime? DueDateFrom { get; set; }
        public DateTime? DueDateTo { get; set; }
        public string? SearchTerm { get; set; }
    }
}

