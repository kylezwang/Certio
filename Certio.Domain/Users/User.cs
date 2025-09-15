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
        
        [Required]
        [StringLength(20)]
        public UserType UserType { get; set; } = UserType.Client;
        
        public ClientType? ClientType { get; set; }
        public ExternalType? ExternalType { get; set; }
        public CertioType? CertioType { get; set; }

        public List<int> ProjectIds { get; set; } = new();
        
        [Required]
        public int OrganizationId { get; set; }
        
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
        public virtual Organization Organization { get; set; } = null!;
        public virtual ICollection<TeamMembership> TeamMemberships { get; set; } = new List<TeamMembership>();
        public virtual ICollection<ProjectAssignment> ProjectAssignments { get; set; } = new List<ProjectAssignment>();
        public virtual ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();
        public virtual ICollection<Document> CreatedDocuments { get; set; } = new List<Document>();
        public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
        public virtual ICollection<OrganizationJoinCode> CreatedJoinCodes { get; set; } = new List<OrganizationJoinCode>();

        // Custom permissions (overrides default role permissions)
        public List<string> CustomPermissions { get; set; } = new();
        
        // Helper methods
        public bool HasProjectAccess(int projectId)
        {
            if (UserType == UserType.Certio)
                return true; // Certio staff have access to all projects
                
            if (UserType == UserType.Client && ClientType == Domain.Users.ClientType.Owner)
                return true; // Client owners have access to all their org's projects
                
            return ProjectIds.Contains(projectId);
        }
        
        public bool IsOrganizationOwner()
        {
            return UserType == UserType.Client && ClientType == Domain.Users.ClientType.Owner;
        }
        
        public bool CanCreateJoinCodes()
        {
            return UserType == UserType.Certio || 
                   (UserType == UserType.Client && 
                    (ClientType == Domain.Users.ClientType.Owner || ClientType == Domain.Users.ClientType.Manager || ClientType == Domain.Users.ClientType.Lawyer));
        }
        
        public bool CanInviteUserType(UserType userType)
        {
            if (UserType == UserType.Certio)
                return true; // Certio can invite anyone
                
            if (UserType == UserType.Client)
            {
                if (ClientType == Domain.Users.ClientType.Owner)
                    return true; // Owners can invite anyone
                    
                if (ClientType == Domain.Users.ClientType.Manager)
                    return userType == UserType.Client || userType == UserType.External; // Managers can invite clients and externals
                    
                if (ClientType == Domain.Users.ClientType.Lawyer)
                    return userType == UserType.External; // Lawyers can only invite externals
            }
            
            return false;
        }
        
        public List<Permission> GetEffectivePermissions(int? projectId = null)
        {
            var basePermissions = GetBasePermissions();
            var customPermissions = CustomPermissions.Select(p => Enum.Parse<Permission>(p)).ToList();
            var effectivePermissions = basePermissions.Union(customPermissions).ToList();
            
            // Project-specific permission filtering
            if (projectId.HasValue && !HasProjectAccess(projectId.Value))
            {
                return new List<Permission>(); // No permissions if no project access
            }
            
            return effectivePermissions;
        }
        
        private List<Permission> GetBasePermissions()
        {
            return UserType switch
            {
                UserType.Client => ClientType switch
                {
                    Domain.Users.ClientType.Owner => PermissionSets.ClientOwner,
                    Domain.Users.ClientType.Manager => PermissionSets.ClientManager,
                    Domain.Users.ClientType.Member => PermissionSets.ClientMember,
                    Domain.Users.ClientType.Lawyer => PermissionSets.ClientLawyer,
                    _ => new List<Permission>()
                },
                UserType.External => ExternalType switch
                {
                    Domain.Users.ExternalType.OpposingCounsel => PermissionSets.OpposingCounsel,
                    Domain.Users.ExternalType.ExpertWitness => PermissionSets.ExpertWitness,
                    Domain.Users.ExternalType.CourtPersonnel => PermissionSets.CourtPersonnel,
                    Domain.Users.ExternalType.RegulatoryBody => PermissionSets.RegulatoryBody,
                    Domain.Users.ExternalType.Other => PermissionSets.Other,
                    _ => new List<Permission>()
                },
                UserType.Certio => PermissionSets.CertioAdmin, // Full access
                _ => new List<Permission>()
            };
        }
    }
    
    public enum UserType
    {
        Client,
        External,
        Certio
    }

    public enum ClientType
    {
        Owner,        // (full access to all projects)
        Manager,      // (full access to assigned projects)
        Member,       // (limited access to assigned projects)
        Lawyer        // (oversight role for legal work)
    }

    public enum ExternalType
    {
        OpposingCounsel,     // Opposing party's lawyers 
        ExpertWitness,       // Expert witnesses 
        CourtPersonnel,      // Judges, clerks, court staff 
        RegulatoryBody,      // Government agencies 
        Other               // Catch-all for other external parties 
    }

    public enum CertioType
    {
        Admin,              // System administrators
        ProjectManager,     // Project management
        Support,            // Certio Customer support
        Legal              // Certio Legal team
    }

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
}
}
