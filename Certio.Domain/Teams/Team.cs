using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;
using Certio.Domain.Projects;
using Certio.Domain.Organizations;

namespace Certio.Domain.Teams
{
    public class Team
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = "";
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        [Required]
        public int OrganizationId { get; set; }
        
        [Required]
        [StringLength(20)]
        public TeamType TeamType { get; set; } = TeamType.Client;
        
        [StringLength(50)]
        public string? Color { get; set; } = "#007bff";
        
        [StringLength(50)]
        public string? Icon { get; set; } = "fas fa-users";
        
        public bool IsActive { get; set; } = true;
        public bool IsPrivate { get; set; } = false; // Private teams within organization
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedDate { get; set; }
        
        // Navigation properties
        public virtual Organization Organization { get; set; } = null!;
        public virtual ICollection<TeamMembership> Memberships { get; set; } = new List<TeamMembership>();
        public virtual ICollection<Project> Projects { get; set; } = new List<Project>();
        
        // Helper methods
        public bool HasUser(int userId)
        {
            return Memberships.Any(tm => tm.UserId == userId && tm.Status == "Active");
        }
    }
    
    public class TeamMembership
    {
        public int Id { get; set; }
        
        public int UserId { get; set; }
        public int TeamId { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Role { get; set; } = "Member";
        
        [StringLength(20)]
        public string Status { get; set; } = "Active"; // Active, Inactive, Pending
        
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LeftAt { get; set; }
        public DateTime? LastActiveAt { get; set; }
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Team Team { get; set; } = null!;
    }
    
    public enum TeamType
    {
        Client,
        Legal,
        External
    }
}
