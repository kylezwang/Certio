using Certio.Application.DTOs;

namespace Certio.Application.Interfaces
{
    /// <summary>
    /// Service interface for calendar event operations
    /// Enforces organization and matter-level access control
    /// </summary>
    public interface ICalendarService
    {
        /// <summary>
        /// Creates a new calendar event within an organization
        /// Optionally associates with a matter
        /// </summary>
        Task<ServiceResult<CalendarEventDto>> CreateEventAsync(
            int userId,
            int orgId,
            CreateCalendarEventDto createDto,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Updates an existing calendar event
        /// </summary>
        Task<ServiceResult<CalendarEventDto>> UpdateEventAsync(
            int userId,
            int eventId,
            UpdateCalendarEventDto updateDto,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Deletes a calendar event (soft delete)
        /// </summary>
        Task<ServiceResult> DeleteEventAsync(
            int userId,
            int eventId,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Gets a calendar event by ID with permission validation
        /// </summary>
        Task<ServiceResult<CalendarEventDto>> GetEventAsync(
            int userId,
            int eventId);

        /// <summary>
        /// Lists all calendar events accessible to user in an organization
        /// </summary>
        Task<ServiceResult<List<CalendarEventDto>>> ListEventsAsync(
            int userId,
            int organizationId,
            CalendarFilterDto? filter = null);

        /// <summary>
        /// Lists all calendar events for a specific matter
        /// </summary>
        Task<ServiceResult<List<CalendarEventDto>>> ListEventsForMatterAsync(
            int userId,
            int matterId,
            CalendarFilterDto? filter = null);

        /// <summary>
        /// Adds an attendee to a calendar event
        /// </summary>
        Task<ServiceResult> AddAttendeeAsync(
            int userId,
            int eventId,
            int attendeeUserId,
            string attendeeType,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Removes an attendee from a calendar event
        /// </summary>
        Task<ServiceResult> RemoveAttendeeAsync(
            int userId,
            int eventId,
            int attendeeUserId,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Updates the response status for an attendee
        /// </summary>
        Task<ServiceResult> UpdateAttendeeResponseAsync(
            int userId,
            int eventId,
            string responseStatus,
            string? ipAddress = null,
            string? userAgent = null);
    }
}

