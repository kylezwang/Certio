using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;

namespace Certio.Domain.Calendar
{
    public class CalendarEventAttendee
    {
        public int Id { get; set; }
        
        [Required]
        public int CalendarEventId { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [Required]
        [StringLength(20)]
        public string AttendeeType { get; set; } = "Required"; // Organizer, Required, Optional
        
        [Required]
        [StringLength(20)]
        public string ResponseStatus { get; set; } = "Pending"; // Pending, Accepted, Declined, Tentative
        
        public bool IsNotifyRecipient { get; set; } = true;
        
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResponseAt { get; set; }
        
        // Navigation properties
        public virtual CalendarEvent CalendarEvent { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}

