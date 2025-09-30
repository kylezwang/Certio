using System.ComponentModel.DataAnnotations;

namespace Certio.Domain.Organizations
{
    public class OrganizationRelationshipAssignedUser
    {
        public int Id { get; set; }

        [Required]
        public int RelationshipId { get; set; }

        [Required]
        public int UserId { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        public int? AssignedById { get; set; }

        // Navigation properties
        public virtual OrganizationRelationship Relationship { get; set; } = null!;
    }
}


