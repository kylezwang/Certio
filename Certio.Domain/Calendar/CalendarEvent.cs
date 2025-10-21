using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;
using Certio.Domain.Matters;
using Certio.Domain.Organizations;

namespace Certio.Domain.Calendar
{
    public class CalendarEvent
    {
        public int Id { get; set; }
        
        [Required]
        public int OrgId { get; set; }
        
        public int? MatterId { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = "";
        
        [StringLength(2000)]
        public string? Description { get; set; }
        
        [StringLength(500)]
        public string? Location { get; set; }
        
        [Required]
        public DateTime StartDateTime { get; set; }
        
        [Required]
        public DateTime EndDateTime { get; set; }
        
        public bool IsAllDayEvent { get; set; } = false;
        
        [StringLength(50)]
        public string EventType { get; set; } = "Meeting"; // Meeting, Deadline, CourtDate, Consultation, Conference, etc.
        
        [StringLength(20)]
        public string Color { get; set; } = "blue"; // blue, purple, red, green, orange, etc.
        
        // Audit fields
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedById { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public int? ModifiedById { get; set; }
        
        // Soft delete fields
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public int? DeletedById { get; set; }
        
        // Future Integration Fields - Google/Outlook Calendar Sync
        [StringLength(200)]
        public string? ExternalCalendarId { get; set; }
        
        [StringLength(50)]
        public string? ExternalCalendarSource { get; set; } // "Google", "Outlook"
        
        [StringLength(20)]
        public string? SyncStatus { get; set; } // "Synced", "Pending", "Failed"
        
        public DateTime? LastSyncedAt { get; set; }
        
        // Future Recurrence Support
        public bool IsRecurring { get; set; } = false;
        
        [StringLength(1000)]
        public string? RecurrenceRule { get; set; } // JSON for recurrence pattern (iCal RRULE format)
        
        public DateTime? RecurrenceEndDate { get; set; }
        
        // Navigation properties
        public virtual Organization? Organization { get; set; }
        public virtual Matter? Matter { get; set; }
        public virtual User? CreatedBy { get; set; }
        public virtual User? ModifiedBy { get; set; }
        public virtual ICollection<CalendarEventAttendee> Attendees { get; set; } = new List<CalendarEventAttendee>();
    }
}

