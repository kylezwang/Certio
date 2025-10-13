namespace Certio.Application.DTOs
{
    public class TeamMemberDto
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Role { get; set; }
        public string? Department { get; set; }
        public string? Location { get; set; }
        public string UserType { get; set; } = "";
        public bool IsActive { get; set; }
        public string? Avatar { get; set; }
        public string? Color { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}

