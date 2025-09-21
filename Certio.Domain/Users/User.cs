using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Certio.Domain.Teams;
using Certio.Domain.Projects;
using Certio.Domain.Services;
using Certio.Domain.Documents;
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

        public List<int> ProjectIds { get; set; } = new();
        
        // Removed single OrganizationId - now using many-to-many relationship
        // public int OrganizationId { get; set; }  // DELETED
        
        public bool IsPersonalOrganization { get; set; } = false;

        [StringLength(50)]
        public string? Avatar { get; set; }
        
        [StringLength(7)]
        public string Color { get; set; } = "#007bff";
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedDate { get; set; }
        public DateTime? LastLoginDate { get; set; }
        
        // Navigation properties
        public virtual ICollection<UserOrganization> UserOrganizations { get; set; } = new List<UserOrganization>();
        public virtual ICollection<TeamMembership> TeamMemberships { get; set; } = new List<TeamMembership>();
        public virtual ICollection<ProjectAssignment> ProjectAssignments { get; set; } = new List<ProjectAssignment>();
        public virtual ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();
        public virtual ICollection<Document> CreatedDocuments { get; set; } = new List<Document>();
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
        
        public bool HasProjectAccess(int projectId, int organizationId)
        {
            var membership = GetOrganizationMembership(organizationId);
            if (membership == null) return false;
            
            if (membership.UserType == UserTypes.Certio)
                return true; // Certio staff have access to all projects
                
            // Check if user has access through their organization membership
            return ProjectIds.Contains(projectId);
        }
        
        public bool CanCreateJoinCodes(int organizationId)
        {
            var membership = GetOrganizationMembership(organizationId);
            if (membership == null) return false;
            
            return membership.UserType == UserTypes.Certio || 
                   (membership.UserType == UserTypes.Client && 
                    (membership.Role == OrganizationRoles.Owner || membership.Role == OrganizationRoles.Manager || membership.Role == OrganizationRoles.Lawyer));
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
        
        public List<Permission> GetEffectivePermissions(int organizationId, int? projectId = null)
        {
            var membership = GetOrganizationMembership(organizationId);
            if (membership == null) return new List<Permission>();
            
            var basePermissions = GetBasePermissions(membership);
            var customPermissions = CustomPermissions.Select(p => Enum.Parse<Permission>(p)).ToList();
            var effectivePermissions = basePermissions.Union(customPermissions).ToList();
            
            // Project-specific permission filtering
            if (projectId.HasValue && !HasProjectAccess(projectId.Value, organizationId))
            {
                return new List<Permission>(); // No permissions if no project access
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
                    OrganizationRoles.ProjectManager => PermissionSets.CertioProjectManager,
                    OrganizationRoles.Support => PermissionSets.CertioSupport,
                    OrganizationRoles.Legal => PermissionSets.CertioLegal,
                    _ => PermissionSets.CertioAdmin // Default to admin for Certio
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
    
    // Project permissions
    ViewProjects,
    CreateProjects,
    EditProjects,
    DeleteProjects,
    ManageProjectSettings,
    
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
    // Client Owner - Full access to all client projects
    public static readonly List<Permission> ClientOwner = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.UploadDocuments,
        Permission.DeleteDocuments, Permission.CommentOnDocuments,
        Permission.ViewProjects, Permission.CreateProjects, Permission.EditProjects,
        Permission.DeleteProjects, Permission.ManageProjectSettings,
        Permission.InviteUsers, Permission.RemoveUsers, Permission.ManageUserPermissions,
        Permission.ViewMessages, Permission.SendMessages, Permission.DeleteMessages,
        Permission.ManageThreads
    };

    // Client Manager - Full access to assigned projects
    public static readonly List<Permission> ClientManager = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.UploadDocuments,
        Permission.CommentOnDocuments,
        Permission.ViewProjects, Permission.EditProjects, Permission.ManageProjectSettings,
        Permission.InviteUsers, Permission.RemoveUsers,
        Permission.ViewMessages, Permission.SendMessages, Permission.ManageThreads
    };

    // Client Member - Full self-service access
    public static readonly List<Permission> ClientMember = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.UploadDocuments,
        Permission.DeleteDocuments, Permission.CommentOnDocuments,
        Permission.ViewProjects, Permission.CreateProjects, Permission.EditProjects,
        Permission.DeleteProjects, Permission.ManageProjectSettings,
        Permission.ViewMessages, Permission.SendMessages, Permission.ManageThreads
    };

    // Client Lawyer - Oversight role for legal work
    public static readonly List<Permission> ClientLawyer = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.CommentOnDocuments,
        Permission.ViewProjects, Permission.EditProjects, Permission.ManageProjectSettings,
        Permission.InviteUsers, Permission.RemoveUsers,
        Permission.ViewMessages, Permission.SendMessages, Permission.ManageThreads,
        Permission.ViewAuditLogs
    };

    // External - Opposing Counsel (limited shared access)
    public static readonly List<Permission> OpposingCounsel = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.CommentOnDocuments,
        Permission.ViewProjects,
        Permission.ViewMessages, Permission.SendMessages
    };

    // External - Expert Witness (document review)
    public static readonly List<Permission> ExpertWitness = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.CommentOnDocuments,
        Permission.ViewProjects,
        Permission.ViewMessages, Permission.SendMessages
    };

    // External - Court Personnel (read-only)
    public static readonly List<Permission> CourtPersonnel = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments,
        Permission.ViewProjects,
        Permission.ViewMessages
    };

    // External - Regulatory Body (compliance access)
    public static readonly List<Permission> RegulatoryBody = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments,
        Permission.ViewProjects,
        Permission.ViewMessages
    };

    // External - Other (custom permissions)
    public static readonly List<Permission> Other = new()
    {
        Permission.ViewDocuments, Permission.ViewProjects, Permission.ViewMessages
    };

    // Certio Admin - Full access to everything
    public static readonly List<Permission> CertioAdmin = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.UploadDocuments,
        Permission.DeleteDocuments, Permission.CommentOnDocuments,
        Permission.ViewProjects, Permission.CreateProjects, Permission.EditProjects,
        Permission.DeleteProjects, Permission.ManageProjectSettings,
        Permission.InviteUsers, Permission.RemoveUsers, Permission.ManageUserPermissions,
        Permission.ViewMessages, Permission.SendMessages, Permission.DeleteMessages,
        Permission.ManageThreads,
        Permission.ViewAuditLogs, Permission.ManageSystemSettings, Permission.AccessAdminPanel
    };

    // Certio Project Manager - Project management access
    public static readonly List<Permission> CertioProjectManager = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.UploadDocuments,
        Permission.CommentOnDocuments,
        Permission.ViewProjects, Permission.CreateProjects, Permission.EditProjects,
        Permission.ManageProjectSettings,
        Permission.InviteUsers, Permission.RemoveUsers,
        Permission.ViewMessages, Permission.SendMessages, Permission.ManageThreads,
        Permission.ViewAuditLogs
    };

    // Certio Support - Customer support access
    public static readonly List<Permission> CertioSupport = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments,
        Permission.ViewProjects,
        Permission.ViewMessages, Permission.SendMessages,
        Permission.ViewAuditLogs
    };

    // Certio Legal - Legal team access
    public static readonly List<Permission> CertioLegal = new()
    {
        Permission.ViewDocuments, Permission.DownloadDocuments, Permission.UploadDocuments,
        Permission.CommentOnDocuments,
        Permission.ViewProjects, Permission.EditProjects, Permission.ManageProjectSettings,
        Permission.InviteUsers, Permission.RemoveUsers,
        Permission.ViewMessages, Permission.SendMessages, Permission.ManageThreads,
        Permission.ViewAuditLogs
    };
}
}
