using System.ComponentModel.DataAnnotations;
using Certio.Domain.Matters;

namespace Certio.Domain.Tasks
{
    public class SubTaskItem
    {
        public int Id { get; set; }

        [Required]
        public int TaskId { get; set; }

        [Required]
        public int MatterId { get; set; }

        [Required]
        public int OrgId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = "";

        [StringLength(100)]
        public string? SubTaskAssignment { get; set; }

        public DateTime? DueDate { get; set; }

        public bool IsCompleted { get; set; } = false;

        // Navigation properties
        public virtual TaskItem Task { get; set; } = null!;
        public virtual Matter Matter { get; set; } = null!;
    }
}
