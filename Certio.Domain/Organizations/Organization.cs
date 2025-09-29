using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;
using Certio.Domain.Teams;

namespace Certio.Domain.Organizations
{
    public class Organization
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = "";
        
        [StringLength(1000)]
        public string? Description { get; set; }
        
        [Required]
        public int OwnerId { get; set; }
        
        // Add organization type for better categorization
        [Required]
        [StringLength(20)]
        public OrganizationType Type { get; set; } = OrganizationType.Client;
        
        public bool IsPersonal { get; set; } = false;
        public bool IsActive { get; set; } = true;
        
        // Add organization settings
        [StringLength(50)]
        public string? Color { get; set; } = "#007bff";
        
        [StringLength(50)]
        public string? Logo { get; set; }
        
        // Add organization preferences
        public string? Settings { get; set; } // JSON for custom settings
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedDate { get; set; }
        
        // Navigation properties
        public virtual User Owner { get; set; } = null!;
        public virtual ICollection<UserOrganization> UserOrganizations { get; set; } = new List<UserOrganization>();
        public virtual ICollection<Team> Teams { get; set; } = new List<Team>();
        public virtual ICollection<OrganizationJoinCode> JoinCodes { get; set; } = new List<OrganizationJoinCode>();
        
        // Helper methods
        public bool HasUser(int userId)
        {
            return UserOrganizations.Any(uo => uo.UserId == userId && uo.IsActive);
        }
        
        public UserOrganization? GetUserMembership(int userId)
        {
            return UserOrganizations.FirstOrDefault(uo => uo.UserId == userId && uo.IsActive);
        }
    }
    
    public enum OrganizationType
    {
        Client,       // Client organization (default)
        LawFirm,      // Law firm organization
        Government,   // Government agency
        NonProfit     // Non-profit organization
    }
}
