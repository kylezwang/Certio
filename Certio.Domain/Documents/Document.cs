using System.ComponentModel.DataAnnotations;

namespace Certio.Domain.Documents
{
    public class Document
    {
        public int Id { get; set; }
        public string TenantId { get; set; } = "default";
        
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = "";
        
        [Required]
        [StringLength(50)]
        public string Type { get; set; } = ""; // Contract, Report, Research, Presentation, Templates
        
        [StringLength(20)]
        public string Size { get; set; } = ""; // e.g., "2.4 MB"
        
        [Required]
        public DateTime Modified { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Author { get; set; } = "";
        
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Draft"; // Final, Published, Draft, Review, Active
        
        [Required]
        [StringLength(20)]
        public string Visibility { get; set; } = "Private"; // Public, Team, Private
        
        public List<string> Tags { get; set; } = new List<string>();
        
        [StringLength(50)]
        public string Icon { get; set; } = "FileText"; // FileText, File, Image, Video, Archive
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? LastModifiedDate { get; set; }
    }
}
