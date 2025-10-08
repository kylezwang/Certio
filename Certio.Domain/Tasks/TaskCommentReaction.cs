using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;

namespace Certio.Domain.Tasks
{
    public class TaskCommentReaction
    {
        public int Id { get; set; }

        [Required]
        public int CommentId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(24)]
        public string ReactionType { get; set; } = "like"; // like, heart, laugh, etc.

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual TaskItemComment Comment { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}


