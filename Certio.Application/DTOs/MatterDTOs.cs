namespace Certio.Application.DTOs
{
    public class CreateMatterDto
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Location { get; set; } = "";
        public string Status { get; set; } = "Planning";
        public string PracticeArea { get; set; } = "";
        public int? GuestCount { get; set; }
        public decimal? Budget { get; set; }
        public string AccessLevel { get; set; } = "Everyone";
        public int? TeamId { get; set; }
        public int? ClientId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? PendingDate { get; set; }
        public DateTime? StatuteOfLimitationsDate { get; set; }
        public string? ClientGoals { get; set; }
        public string? LegalRequirements { get; set; }
        public string? Notes { get; set; }
        public List<int>? PermissionUserIds { get; set; }
    }

    public class UpdateMatterDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Location { get; set; }
        public string? Status { get; set; }
        public string? PracticeArea { get; set; }
        public int? GuestCount { get; set; }
        public decimal? Budget { get; set; }
        public string? AccessLevel { get; set; }
        public int? TeamId { get; set; }
        public int? ClientId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public DateTime? PendingDate { get; set; }
        public DateTime? StatuteOfLimitationsDate { get; set; }
        public bool? StatuteOfLimitationsSatisfied { get; set; }
        public string? ClientGoals { get; set; }
        public string? LegalRequirements { get; set; }
        public string? Notes { get; set; }
    }

    public class MatterDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Location { get; set; } = "";
        public string Status { get; set; } = "";
        public string PracticeArea { get; set; } = "";
        public int? GuestCount { get; set; }
        public decimal? Budget { get; set; }
        public string AccessLevel { get; set; } = "";
        public int OrganizationId { get; set; }
        public int? TeamId { get; set; }
        public int? ClientId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public DateTime? PendingDate { get; set; }
        public DateTime? StatuteOfLimitationsDate { get; set; }
        public bool StatuteOfLimitationsSatisfied { get; set; }
        public string? ClientGoals { get; set; }
        public string? LegalRequirements { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastModifiedDate { get; set; }
        public int TasksCompleted { get; set; }
        public int TotalTasks { get; set; }
        public List<MatterAssignmentDto> Assignments { get; set; } = new();
        public List<MatterPermissionDto> Permissions { get; set; } = new();
    }

    public class MatterAssignmentDto
    {
        public int Id { get; set; }
        public int MatterId { get; set; }
        public int? UserId { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string AssignmentType { get; set; } = "";
        public string? Role { get; set; }
        public string? VendorCategory { get; set; }
        public string? VendorStatus { get; set; }
        public decimal? ContractAmount { get; set; }
        public decimal? AmountPaid { get; set; }
        public string? RsvpStatus { get; set; }
        public int? PartySize { get; set; }
        public int? TableNumber { get; set; }
        public string? DietaryRestrictions { get; set; }
        public string? MealChoice { get; set; }
        public bool IsNotifyRecipient { get; set; }
        public DateTime AssignedAt { get; set; }
        public UserSummaryDto? User { get; set; }
    }

    public class MatterPermissionDto
    {
        public int Id { get; set; }
        public int MatterId { get; set; }
        public int UserId { get; set; }
        public DateTime GrantedAt { get; set; }
        public int? GrantedById { get; set; }
        public DateTime? RevokedAt { get; set; }
        public UserSummaryDto? User { get; set; }
    }

    public class AssignUserToMatterDto
    {
        public int UserId { get; set; }
        public string AssignmentType { get; set; } = "RelevantContact";
        public string Role { get; set; } = "";
        public bool IsNotifyRecipient { get; set; } = true;
    }

    public class AddContactToMatterDto
    {
        public string AssignmentType { get; set; } = "Guest";
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Role { get; set; }
        public string? VendorCategory { get; set; }
        public string? VendorStatus { get; set; }
        public decimal? ContractAmount { get; set; }
        public decimal? AmountPaid { get; set; }
        public string? RsvpStatus { get; set; }
        public int? PartySize { get; set; }
        public int? TableNumber { get; set; }
        public string? DietaryRestrictions { get; set; }
        public string? MealChoice { get; set; }
    }
}

