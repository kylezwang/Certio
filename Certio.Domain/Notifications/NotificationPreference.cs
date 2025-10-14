using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;

namespace Certio.Domain.Notifications
{
    public class NotificationPreference
    {
        public int Id { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        // Notification type preferences
        public bool EmailNotifications { get; set; } = true;
        public bool PushNotifications { get; set; } = true;
        public bool InAppNotifications { get; set; } = true;
        
        // Event type preferences
        public bool NotifyOnMatterChanges { get; set; } = true;
        public bool NotifyOnTaskAssignment { get; set; } = true;
        public bool NotifyOnTaskCompletion { get; set; } = true;
        public bool NotifyOnDocumentShared { get; set; } = true;
        public bool NotifyOnMentions { get; set; } = true;
        public bool NotifyOnAIGeneration { get; set; } = true;
        public bool NotifyOnComments { get; set; } = true;
        
        // AI-specific preferences
        public bool NotifyOnAIPendingApproval { get; set; } = true;
        public bool NotifyOnAIApproved { get; set; } = false;
        public bool NotifyOnAIRejected { get; set; } = true;
        
        // Quiet hours
        public bool EnableQuietHours { get; set; } = false;
        public TimeSpan? QuietHoursStart { get; set; }
        public TimeSpan? QuietHoursEnd { get; set; }
        
        // Digest preferences
        public bool EnableDailyDigest { get; set; } = false;
        public bool EnableWeeklyDigest { get; set; } = false;
        public TimeSpan? DailyDigestTime { get; set; }
        public DayOfWeek? WeeklyDigestDay { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedAt { get; set; }
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
    }
}

