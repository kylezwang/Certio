using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;
using Certio.Domain.Teams;
using System.Text.Json;

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
        
        // Audit fields
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedById { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public int? ModifiedById { get; set; }
        public DateTime? LastModifiedDate { get; set; } // Legacy - use ModifiedAt
        
        // Soft delete fields
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public int? DeletedById { get; set; }
        
        // Navigation properties
        public virtual User Owner { get; set; } = null!;
        public virtual ICollection<UserOrganization> UserOrganizations { get; set; } = new List<UserOrganization>();
        public virtual ICollection<Team> Teams { get; set; } = new List<Team>();
        public virtual ICollection<OrganizationJoinCode> JoinCodes { get; set; } = new List<OrganizationJoinCode>();
        public virtual ICollection<OrganizationRelationship> OrganizationRelationships { get; set; } = new List<OrganizationRelationship>();
        public virtual ICollection<OrganizationRelationship> RelatedOrganizations { get; set; } = new List<OrganizationRelationship>();
        
        // Helper methods
        public bool HasUser(int userId)
        {
            return UserOrganizations.Any(uo => uo.UserId == userId && uo.IsActive);
        }
        
        public UserOrganization? GetUserMembership(int userId)
        {
            return UserOrganizations.FirstOrDefault(uo => uo.UserId == userId && uo.IsActive);
        }

        // AI Model Tier helpers
        public AIModelTier GetAIModelTier()
        {
            if (string.IsNullOrWhiteSpace(Settings))
                return AIModelTier.Auto; // Default to Auto

            try
            {
                var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Settings);
                if (settings != null && settings.TryGetValue("AIModelTier", out var tierElement))
                {
                    if (tierElement.ValueKind == JsonValueKind.String)
                    {
                        var tierStr = tierElement.GetString();
                        if (Enum.TryParse<AIModelTier>(tierStr, true, out var tier))
                            return tier;
                    }
                    else if (tierElement.ValueKind == JsonValueKind.Number)
                    {
                        var tierInt = tierElement.GetInt32();
                        if (Enum.IsDefined(typeof(AIModelTier), tierInt))
                            return (AIModelTier)tierInt;
                    }
                }
            }
            catch
            {
                // If parsing fails, return default
            }

            return AIModelTier.Auto;
        }

        public void SetAIModelTier(AIModelTier tier)
        {
            Dictionary<string, object> settings;
            
            if (string.IsNullOrWhiteSpace(Settings))
            {
                settings = new Dictionary<string, object>();
            }
            else
            {
                try
                {
                    settings = JsonSerializer.Deserialize<Dictionary<string, object>>(Settings) 
                        ?? new Dictionary<string, object>();
                }
                catch
                {
                    settings = new Dictionary<string, object>();
                }
            }

            settings["AIModelTier"] = tier.ToString();
            Settings = JsonSerializer.Serialize(settings);
        }
    }
    
    public enum OrganizationType
    {
        Client,       // Client organization (default)
        LawFirm,      // Law firm organization
        Government,   // Government agency
        NonProfit     // Non-profit organization
    }

    public enum AIModelTier
    {
        Auto = 0,      // Dynamic selection based on complexity (current behavior)
        Basic = 1,     // gpt-4o-mini
        Advanced = 2,  // gpt-4o
        Premium = 3    // gpt-5
    }
}
