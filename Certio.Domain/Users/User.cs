using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Certio.Domain.Teams;
using Certio.Domain.Matters;
using Certio.Domain.Services;
using Certio.Domain.Organizations;

namespace Certio.Domain.Users
{
    public class User
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = "";
        
        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = "";
        
        [Required]
        [StringLength(200)]
        public string Email { get; set; } = "";
        
        [StringLength(20)]
        public string? PhoneNumber { get; set; }
        
        [StringLength(100)]
        public string? Company { get; set; }
        
        [StringLength(100)]
        public string? JobTitle { get; set; }
        
        [StringLength(50)]
        public string? Department { get; set; }
        
        [StringLength(100)]
        public string? Location { get; set; }
        
        // Removed global UserType, ClientType, ExternalType, CertioType
        // These are now handled at the organization level in UserOrganization

        public List<int> MatterIds { get; set; } = new();
        
        // Removed single OrganizationId - now using many-to-many relationship
        // public int OrganizationId { get; set; }  // DELETED
        
        public bool IsPersonalOrganization { get; set; } = false;

        [StringLength(50)]
        public string? Avatar { get; set; }
        
        [StringLength(7)]
        public string Color { get; set; } = "#007bff";
        
        [StringLength(100)]
        public string? TimeZone { get; set; } = "America/New_York";
        
        [StringLength(10)]
        public string? Language { get; set; } = "en";
        
        [StringLength(20)]
        public string? Theme { get; set; } = "light";
        
        public bool IsActive { get; set; } = true;
        
        public bool Enable2FA { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedDate { get; set; }
        public DateTime? LastLoginDate { get; set; }
        
        // Soft delete properties
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public int? DeletedById { get; set; }
        public string? DeletionReason { get; set; }
        
        // Navigation properties
        public virtual ICollection<UserOrganization> UserOrganizations { get; set; } = new List<UserOrganization>();
        public virtual ICollection<TeamMembership> TeamMemberships { get; set; } = new List<TeamMembership>();
        public virtual ICollection<MatterAssignment> MatterAssignments { get; set; } = new List<MatterAssignment>();
        public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
        public virtual ICollection<OrganizationJoinCode> CreatedJoinCodes { get; set; } = new List<OrganizationJoinCode>();

        // Custom permissions (overrides default role permissions)
        public List<string> CustomPermissions { get; set; } = new();
        
        // Helper methods
        public bool IsInOrganization(int organizationId)
        {
            return UserOrganizations.Any(uo => uo.OrganizationId == organizationId && uo.IsActive);
        }
        
        public UserOrganization? GetOrganizationMembership(int organizationId)
        {
            return UserOrganizations.FirstOrDefault(uo => uo.OrganizationId == organizationId && uo.IsActive);
        }
        
        public bool IsOrganizationOwner(int organizationId)
        {
            var membership = GetOrganizationMembership(organizationId);
            return membership?.Role == OrganizationRoles.Owner;
        }
        
        public bool IsOrganizationAdmin(int organizationId)
        {
            var membership = GetOrganizationMembership(organizationId);
            return membership?.Role == OrganizationRoles.Admin || membership?.Role == OrganizationRoles.Owner;
        }
        
        public bool IsCertioStaff(int organizationId)
        {
            var membership = GetOrganizationMembership(organizationId);
            return membership?.UserType == UserTypes.Certio;
        }
        
        public bool IsClient(int organizationId)
        {
            var membership = GetOrganizationMembership(organizationId);
            return membership?.UserType == UserTypes.Client;
        }
        
        public bool IsExternal(int organizationId)
        {
            var membership = GetOrganizationMembership(organizationId);
            return membership?.UserType == UserTypes.External;
        }
        
        public UserOrganization? GetPrimaryOrganization()
        {
            return UserOrganizations.FirstOrDefault(uo => uo.IsPrimary && uo.IsActive);
        }
        
        public bool CanJoinLawFirm()
        {
            // Check if user is already part of a LawFirm organization
            return !UserOrganizations.Any(uo => uo.IsActive && uo.Organization.Type == OrganizationType.LawFirm);
        }
        
        public UserOrganization? GetLawFirmMembership()
        {
            return UserOrganizations.FirstOrDefault(uo => uo.IsActive && uo.Organization.Type == OrganizationType.LawFirm);
        }
        
        public bool IsLawFirmMember()
        {
            return GetLawFirmMembership() != null;
        }
        
        public bool IsLawFirmPartner()
        {
            var membership = GetLawFirmMembership();
            return membership?.Role == OrganizationRoles.Partner;
        }
        
        public bool IsLawFirmAssociate()
        {
            var membership = GetLawFirmMembership();
            return membership?.Role == OrganizationRoles.Associate;
        }
        
        public bool IsLawFirmParalegal()
        {
            var membership = GetLawFirmMembership();
            return membership?.Role == OrganizationRoles.Paralegal;
        }
        
        public bool IsLawFirmStaff()
        {
            var membership = GetLawFirmMembership();
            return membership?.Role == OrganizationRoles.Staff;
        }
        
        public bool CanAccessClientThroughFirm(int clientOrganizationId)
        {
            var lawFirmMembership = GetLawFirmMembership();
            if (lawFirmMembership == null) return false;
            
            // Check if there's an active relationship between the law firm and client
            return lawFirmMembership.Organization.OrganizationRelationships
                .Any(rel => rel.TargetOrganizationId == clientOrganizationId && 
                           rel.IsValid() && 
                           rel.RelationshipType == RelationshipTypes.LawFirmClient);
        }
        
        public List<Organization> GetAccessibleClientOrganizations()
        {
            var lawFirmMembership = GetLawFirmMembership();
            if (lawFirmMembership == null) return new List<Organization>();
            
            return lawFirmMembership.Organization.OrganizationRelationships
                .Where(rel => rel.IsValid() && rel.RelationshipType == RelationshipTypes.LawFirmClient)
                .Select(rel => rel.TargetOrganization)
                .ToList();
        }
        
        public bool HasMatterAccess(int matterId, int organizationId)
        {
            var membership = GetOrganizationMembership(organizationId);
            if (membership == null) return false;
            
            if (membership.UserType == UserTypes.Certio)
                return true; // Certio staff have access to all matters
                
            // Check if user has access through their organization membership
            return MatterIds.Contains(matterId);
        }
        
        public bool CanCreateJoinCodes(int organizationId)
        {
            var membership = GetOrganizationMembership(organizationId);
            if (membership == null) return false;
            
            return membership.UserType == UserTypes.Certio || 
                   (membership.UserType == UserTypes.Client && 
                    (membership.Role == OrganizationRoles.Owner || 
                     membership.Role == OrganizationRoles.Manager || 
                     membership.Role == OrganizationRoles.Lawyer)) ||
                   (membership.UserType == UserTypes.LawFirm && 
                    (membership.Role == OrganizationRoles.Partner || 
                     membership.Role == OrganizationRoles.Associate));
        }
        
        public bool CanInviteUserType(string userType, int organizationId)
        {
            var membership = GetOrganizationMembership(organizationId);
            if (membership == null) return false;
            
            if (membership.UserType == UserTypes.Certio)
                return true; // Certio can invite anyone
                
            if (membership.UserType == UserTypes.Client)
            {
                if (membership.Role == OrganizationRoles.Owner)
                    return true; // Owners can invite anyone
                    
                if (membership.Role == OrganizationRoles.Manager)
                    return userType == UserTypes.Client || userType == UserTypes.External; // Managers can invite clients and externals
                    
                if (membership.Role == OrganizationRoles.Lawyer)
                    return userType == UserTypes.External; // Lawyers can only invite externals
            }
            
            return false;
        }
        
        public List<Permission> GetEffectivePermissions(int organizationId, int? matterId = null)
        {
            var membership = GetOrganizationMembership(organizationId);
            if (membership == null) return new List<Permission>();
            
            var basePermissions = GetBasePermissions(membership);
            var customPermissions = CustomPermissions.Select(p => Enum.Parse<Permission>(p)).ToList();
            var effectivePermissions = basePermissions.Union(customPermissions).ToList();
            
            // Matter-specific permission filtering
            if (matterId.HasValue && !HasMatterAccess(matterId.Value, organizationId))
            {
                return new List<Permission>(); // No permissions if no matter access
            }
            
            return effectivePermissions;
        }
        
        private List<Permission> GetBasePermissions(UserOrganization membership)
        {
            return membership.UserType switch
            {
                UserTypes.Client => membership.Role switch
                {
                    OrganizationRoles.Owner => PermissionSets.ClientOwner,
                    OrganizationRoles.Manager => PermissionSets.ClientManager,
                    OrganizationRoles.Member => PermissionSets.ClientMember,
                    OrganizationRoles.Lawyer => PermissionSets.ClientLawyer,
                    _ => new List<Permission>()
                },
                UserTypes.External => membership.Role switch
                {
                    OrganizationRoles.OpposingCounsel => PermissionSets.OpposingCounsel,
                    OrganizationRoles.ExpertWitness => PermissionSets.ExpertWitness,
                    OrganizationRoles.CourtPersonnel => PermissionSets.CourtPersonnel,
                    OrganizationRoles.RegulatoryBody => PermissionSets.RegulatoryBody,
                    OrganizationRoles.Other => PermissionSets.Other,
                    _ => new List<Permission>()
                },
                UserTypes.Certio => membership.Role switch
                {
                    OrganizationRoles.Admin => PermissionSets.CertioAdmin,
                    OrganizationRoles.MatterManager => PermissionSets.CertioMatterManager,
                    OrganizationRoles.Support => PermissionSets.CertioSupport,
                    OrganizationRoles.Legal => PermissionSets.CertioLegal,
                    _ => PermissionSets.CertioAdmin // Default to admin for Certio
                },
                UserTypes.LawFirm => membership.Role switch
                {
                    OrganizationRoles.Partner => PermissionSets.Partner,
                    OrganizationRoles.Associate => PermissionSets.Associate,
                    OrganizationRoles.Paralegal => PermissionSets.Paralegal,
                    OrganizationRoles.Staff => PermissionSets.Staff,
                    _ => new List<Permission>()
                },
                _ => new List<Permission>()
            };
        }
    }
    
    // UserType, ClientType, ExternalType, and CertioType enums moved to UserOrganization.cs
    // All user types and roles are now organization-specific

public enum Permission
{
    // Document permissions
    ViewDocuments,
    DownloadDocuments,
    UploadDocuments,
    DeleteDocuments,
    CommentOnDocuments,
    
    // Matter permissions
    ViewMatters,
    CreateMatters,
    EditMatters,
    DeleteMatters,
    ManageMatterSettings,
    
    // User management permissions
    InviteUsers,
    RemoveUsers,
    ManageUserPermissions,
    
    // Communication permissions
    ViewMessages,
    SendMessages,
    DeleteMessages,
    ManageThreads,
    
    // System permissions
    ViewAuditLogs,
    ManageSystemSettings,
    AccessAdminPanel
}

public static class PermissionSets
{
    // Client Owner - Full access to all client matters
    public static readonly List<Permission> ClientOwner = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.UploadDocuments,
        Permission.DeleteDocuments, Permission.CommentOnDocuments,
        Permission.ViewMatters, Permission.CreateMatters, Permission.EditMatters,
        Permission.DeleteMatters, Permission.ManageMatterSettings,
        Permission.InviteUsers, Permission.RemoveUsers, Permission.ManageUserPermissions,
        Permission.ViewMessages, Permission.SendMessages, Permission.DeleteMessages,
        Permission.ManageThreads
    };

    // Client Manager - Full access to assigned matters
    public static readonly List<Permission> ClientManager = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.UploadDocuments,
        Permission.CommentOnDocuments,
        Permission.ViewMatters, Permission.EditMatters, Permission.ManageMatterSettings,
        Permission.InviteUsers, Permission.RemoveUsers,
        Permission.ViewMessages, Permission.SendMessages, Permission.ManageThreads
    };

    // Client Member - Full self-service access
    public static readonly List<Permission> ClientMember = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.UploadDocuments,
        Permission.DeleteDocuments, Permission.CommentOnDocuments,
        Permission.ViewMatters, Permission.CreateMatters, Permission.EditMatters,
        Permission.DeleteMatters, Permission.ManageMatterSettings,
        Permission.ViewMessages, Permission.SendMessages, Permission.ManageThreads
    };

    // Client Lawyer - Oversight role for legal work
    public static readonly List<Permission> ClientLawyer = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.CommentOnDocuments,
        Permission.ViewMatters, Permission.EditMatters, Permission.ManageMatterSettings,
        Permission.InviteUsers, Permission.RemoveUsers,
        Permission.ViewMessages, Permission.SendMessages, Permission.ManageThreads,
        Permission.ViewAuditLogs
    };

    // External - Opposing Counsel (limited shared access)
    public static readonly List<Permission> OpposingCounsel = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.CommentOnDocuments,
        Permission.ViewMatters,
        Permission.ViewMessages, Permission.SendMessages
    };

    // External - Expert Witness (document review)
    public static readonly List<Permission> ExpertWitness = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.CommentOnDocuments,
        Permission.ViewMatters,
        Permission.ViewMessages, Permission.SendMessages
    };

    // External - Court Personnel (read-only)
    public static readonly List<Permission> CourtPersonnel = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments,
        Permission.ViewMatters,
        Permission.ViewMessages
    };

    // External - Regulatory Body (compliance access)
    public static readonly List<Permission> RegulatoryBody = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments,
        Permission.ViewMatters,
        Permission.ViewMessages
    };

    // External - Other (custom permissions)
    public static readonly List<Permission> Other = new()
    {
        Permission.ViewDocuments, Permission.ViewMatters, Permission.ViewMessages
    };

    // Certio Admin - Full access to everything
    public static readonly List<Permission> CertioAdmin = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.UploadDocuments,
        Permission.DeleteDocuments, Permission.CommentOnDocuments,
        Permission.ViewMatters, Permission.CreateMatters, Permission.EditMatters,
        Permission.DeleteMatters, Permission.ManageMatterSettings,
        Permission.InviteUsers, Permission.RemoveUsers, Permission.ManageUserPermissions,
        Permission.ViewMessages, Permission.SendMessages, Permission.DeleteMessages,
        Permission.ManageThreads,
        Permission.ViewAuditLogs, Permission.ManageSystemSettings, Permission.AccessAdminPanel
    };

    // Certio Matter Manager - Matter management access
    public static readonly List<Permission> CertioMatterManager = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.UploadDocuments,
        Permission.CommentOnDocuments,
        Permission.ViewMatters, Permission.CreateMatters, Permission.EditMatters,
        Permission.ManageMatterSettings,
        Permission.InviteUsers, Permission.RemoveUsers,
        Permission.ViewMessages, Permission.SendMessages, Permission.ManageThreads,
        Permission.ViewAuditLogs
    };

    // Certio Support - Customer support access
    public static readonly List<Permission> CertioSupport = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments,
        Permission.ViewMatters,
        Permission.ViewMessages, Permission.SendMessages,
        Permission.ViewAuditLogs
    };

    // Certio Legal - Legal team access
    public static readonly List<Permission> CertioLegal = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.UploadDocuments,
        Permission.CommentOnDocuments,
        Permission.ViewMatters, Permission.EditMatters, Permission.ManageMatterSettings,
        Permission.InviteUsers, Permission.RemoveUsers,
        Permission.ViewMessages, Permission.SendMessages, Permission.ManageThreads,
        Permission.ViewAuditLogs
    };

    // Partner - Full access to firm and client matters
    public static readonly List<Permission> Partner = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.UploadDocuments,
        Permission.DeleteDocuments, Permission.CommentOnDocuments,
        Permission.ViewMatters, Permission.CreateMatters, Permission.EditMatters,
        Permission.DeleteMatters, Permission.ManageMatterSettings,
        Permission.InviteUsers, Permission.RemoveUsers, Permission.ManageUserPermissions,
        Permission.ViewMessages, Permission.SendMessages, Permission.DeleteMessages,
        Permission.ManageThreads, Permission.ViewAuditLogs
    };

    // Associate - Access to assigned matters
    public static readonly List<Permission> Associate = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.UploadDocuments,
        Permission.CommentOnDocuments,
        Permission.ViewMatters, Permission.EditMatters, Permission.ManageMatterSettings,
        Permission.ViewMessages, Permission.SendMessages, Permission.ManageThreads,
        Permission.ViewAuditLogs
    };

    // Paralegal - Document and matter support access
    public static readonly List<Permission> Paralegal = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.UploadDocuments,
        Permission.CommentOnDocuments,
        Permission.ViewMatters, Permission.EditMatters,
        Permission.ViewMessages, Permission.SendMessages, Permission.ManageThreads
    };

    // Staff - Basic access for administrative staff
    public static readonly List<Permission> Staff = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments,
        Permission.ViewMatters,
        Permission.ViewMessages, Permission.SendMessages
    };
}
}
