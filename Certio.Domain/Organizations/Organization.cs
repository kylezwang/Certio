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
        
        public bool IsPersonal { get; set; } = false;
        
        [StringLength(50)]
        public string? Color { get; set; } = "#007bff";
        
        [StringLength(50)]
        public string? Logo { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedDate { get; set; }
        
        // Navigation properties
        public virtual User Owner { get; set; } = null!;
        public virtual ICollection<Team> Teams { get; set; } = new List<Team>();
        public virtual ICollection<User> Members { get; set; } = new List<User>();
        public virtual ICollection<OrganizationJoinCode> JoinCodes { get; set; } = new List<OrganizationJoinCode>();
    }
}
