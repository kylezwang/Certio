namespace Certio.Application.DTOs
{
    public class CreateCalendarEventDto
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? Location { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public bool IsAllDayEvent { get; set; } = false;
        public string EventType { get; set; } = "Meeting";
        public string Color { get; set; } = "blue";
        public int? MatterId { get; set; }
        public List<int>? AttendeeUserIds { get; set; }
    }

    public class UpdateCalendarEventDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Location { get; set; }
        public DateTime? StartDateTime { get; set; }
        public DateTime? EndDateTime { get; set; }
        public bool? IsAllDayEvent { get; set; }
        public string? EventType { get; set; }
        public string? Color { get; set; }
        // MatterId is not changeable after creation
    }

    public class CalendarEventDto
    {
        public int Id { get; set; }
        public int OrgId { get; set; }
        public int? MatterId { get; set; }
        public string? MatterTitle { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? Location { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public bool IsAllDayEvent { get; set; }
        public string EventType { get; set; } = "";
        public string Color { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public int? CreatedById { get; set; }
        public UserSummaryDto? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public int? ModifiedById { get; set; }
        public UserSummaryDto? ModifiedBy { get; set; }
        public List<CalendarEventAttendeeDto> Attendees { get; set; } = new();
    }

    public class CalendarEventAttendeeDto
    {
        public int Id { get; set; }
        public int CalendarEventId { get; set; }
        public int UserId { get; set; }
        public string AttendeeType { get; set; } = "";
        public string ResponseStatus { get; set; } = "";
        public bool IsNotifyRecipient { get; set; }
        public DateTime AddedAt { get; set; }
        public DateTime? ResponseAt { get; set; }
        public UserSummaryDto? User { get; set; }
    }

    public class CalendarFilterDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? MatterId { get; set; }
        public List<string>? EventTypes { get; set; }
        public bool IncludeOrgEvents { get; set; } = true;
        public bool IncludeMatterEvents { get; set; } = true;
    }

    public class AddCalendarAttendeeDto
    {
        public int UserId { get; set; }
        public string AttendeeType { get; set; } = "Required";
        public bool IsNotifyRecipient { get; set; } = true;
    }
}

