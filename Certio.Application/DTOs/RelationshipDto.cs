namespace Certio.Application.DTOs
{
    public class RelationshipDto
    {
        public int Id { get; set; }
        public int SourceOrganizationId { get; set; }
        public int TargetOrganizationId { get; set; }
        public string RelationshipType { get; set; } = "";
        public string AccessLevel { get; set; } = "";
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<AssignedUserDto> AssignedUsers { get; set; } = new();
    }

    public class AssignedUserDto
    {
        public int UserId { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Role { get; set; }
        public DateTime AssignedAt { get; set; }
        public int? AssignedById { get; set; }
    }

    public class AssignableUserDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Role { get; set; }
        public string UserType { get; set; } = "";
        public bool IsCurrentUser { get; set; }
        public bool IsAlreadyAssigned { get; set; }
    }

    public class AssignmentResultDto
    {
        public int AssignedCount { get; set; }
        public int AlreadyAssignedCount { get; set; }
        public string Message { get; set; } = "";
    }
}

