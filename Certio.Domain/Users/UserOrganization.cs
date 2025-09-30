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
        public string UserType { get; set; } = "Client";
        
        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "Member";
        
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
    
    // OrganizationRole and UserType are now string constants
    public static class OrganizationRoles
    {
        // Client roles
        public const string Owner = "Owner";
        public const string Manager = "Manager";
        public const string Member = "Member";
        public const string Lawyer = "Lawyer";
        
        // External roles  
        public const string OpposingCounsel = "OpposingCounsel";
        public const string ExpertWitness = "ExpertWitness";
        public const string CourtPersonnel = "CourtPersonnel";
        public const string RegulatoryBody = "RegulatoryBody";
        public const string Other = "Other";
        
        // Certio roles
        public const string Admin = "Admin";
        public const string MatterManager = "MatterManager";
        public const string Support = "Support";
        public const string Legal = "Legal";
        
        // Law Firm roles
        public const string Partner = "Partner";
        public const string Associate = "Associate";
        public const string Paralegal = "Paralegal";
        public const string Staff = "Staff";
        
        // General roles
        public const string Guest = "Guest";
    }
    
    public static class UserTypes
    {
        public const string Client = "Client";
        public const string External = "External";
        public const string Certio = "Certio";
        public const string LawFirm = "LawFirm";
    }
}
