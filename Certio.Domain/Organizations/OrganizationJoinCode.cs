using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;

namespace Certio.Domain.Organizations
{
    public class OrganizationJoinCode
    {
        public int Id { get; set; }
        
        [Required]
        public int OrganizationId { get; set; }
        
        [Required]
        public int CreatedByUserId { get; set; }
        
        [Required]
        [StringLength(20)]
        public string Code { get; set; } = "";
        
        [Required]
        [StringLength(20)]
        public UserType InvitedUserType { get; set; }
        
        [Required]
        [StringLength(20)]
        public OrganizationRole InvitedRole { get; set; } = OrganizationRole.Member;
        
        [StringLength(100)]
        public string? TeamName { get; set; }
        
        public int MaxUses { get; set; } = 1;
        public int UsesRemaining { get; set; }
        
        public DateTime ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAt { get; set; }
        
        // Navigation properties
        public virtual Organization Organization { get; set; } = null!;
        public virtual User CreatedBy { get; set; } = null!;
    }
}
