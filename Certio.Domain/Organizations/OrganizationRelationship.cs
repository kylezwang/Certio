using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Certio.Domain.Users;

namespace Certio.Domain.Organizations
{
    public class OrganizationRelationship
    {
        public int Id { get; set; }
        
        [Required]
        public int SourceOrganizationId { get; set; }
        
        [Required]
        public int TargetOrganizationId { get; set; }
        
        [Required]
        [StringLength(50)]
        public string RelationshipType { get; set; } = "";
        
        [Required]
        [StringLength(50)]
        public string AccessLevel { get; set; } = "";
        
        [StringLength(1000)]
        public string? Description { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public DateTime? ExpiresAt { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedDate { get; set; }
        
        [Required]
        public int CreatedById { get; set; }
        
        public int? ModifiedById { get; set; }
        
        // Soft delete properties
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public int? DeletedById { get; set; }
        public string? DeletionReason { get; set; }
        
        // Navigation properties
        public virtual Organization SourceOrganization { get; set; } = null!;
        public virtual Organization TargetOrganization { get; set; } = null!;
        public virtual User CreatedBy { get; set; } = null!;
        public virtual User? ModifiedBy { get; set; }
        public virtual User? DeletedBy { get; set; }
        
        // Helper methods
        public bool IsExpired()
        {
            return ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
        }
        
        public bool IsValid()
        {
            return IsActive && !IsDeleted && !IsExpired();
        }
        
        public bool CanAccess(int userId)
        {
            // Check if user is part of the source organization
            return SourceOrganization.UserOrganizations.Any(uo => 
                uo.UserId == userId && 
                uo.IsActive && 
                uo.UserType == UserTypes.LawFirm);
        }
    }
    
    public static class RelationshipTypes
    {
        public const string LawFirmClient = "LawFirmClient";
        public const string PartnerFirm = "PartnerFirm";
        public const string Subsidiary = "Subsidiary";
        public const string Vendor = "Vendor";
        public const string Consultant = "Consultant";
    }
    
    public static class AccessLevels
    {
        public const string FullAccess = "FullAccess";
        public const string ReadOnly = "ReadOnly";
        public const string LimitedAccess = "LimitedAccess";
        public const string MatterSpecific = "MatterSpecific";
        public const string DocumentOnly = "DocumentOnly";
    }
}
