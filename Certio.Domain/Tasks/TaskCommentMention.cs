using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;

namespace Certio.Domain.Tasks
{
    public class TaskCommentMention
    {
        public int Id { get; set; }

        [Required]
        public int CommentId { get; set; }

        [Required]
        public int MentionedUserId { get; set; }

        // Navigation properties
        public virtual TaskItemComment Comment { get; set; } = null!;
        public virtual User MentionedUser { get; set; } = null!;
    }
}


