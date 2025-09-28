using System.ComponentModel.DataAnnotations;

namespace Certio.Domain.Users
{
    public class UserDeletionRequest
    {
        public int Id { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public UserDeletionType DeletionType { get; set; }
        
        [StringLength(500)]
        public string Reason { get; set; } = "";
        
        public bool ConfirmDataLoss { get; set; }
        
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? ProcessedAt { get; set; }
        
        public int? ProcessedById { get; set; }
        
        [StringLength(500)]
        public string? ProcessingNotes { get; set; }
        
        public bool IsProcessed { get; set; } = false;
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual User? ProcessedBy { get; set; }
    }
    
    public enum UserDeletionType
    {
        Deactivate,        // Just deactivate account, keep all data
        CompleteDeletion   // Remove personal data, keep anonymized metrics
    }
}
