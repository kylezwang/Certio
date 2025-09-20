using System.ComponentModel.DataAnnotations;
using Certio.Domain.Organizations;

namespace Certio.Domain.Users
{
    public class UserOrganization
    {
        public int Id { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public int OrganizationId { get; set; }
        
        [Required]
        [StringLength(20)]
        public UserType UserType { get; set; } = UserType.Client;
        
        [Required]
        [StringLength(20)]
        public OrganizationRole Role { get; set; } = OrganizationRole.Member;
        
        [StringLength(50)]
        public string? Department { get; set; }
        
        [StringLength(100)]
        public string? JobTitle { get; set; }
        
        public bool IsActive { get; set; } = true;
        public bool IsPrimary { get; set; } = false; // Primary organization for UI defaults
        
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LeftAt { get; set; }
        public DateTime? LastActiveAt { get; set; }
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Organization Organization { get; set; } = null!;
    }
    
    public enum OrganizationRole
    {
        // Client roles
        Owner, Manager, Member, Lawyer,
        
        // External roles  
        OpposingCounsel, ExpertWitness, CourtPersonnel, RegulatoryBody, Other,
        
        // Certio roles
        Admin, ProjectManager, Support, Legal,
        
        // General roles
        Guest
    }
    
    public enum UserType
    {
        Client,
        External,
        Certio
    }
}
