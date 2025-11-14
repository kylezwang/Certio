using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Certio.Domain.Calendar;
using Certio.Domain.Exceptions;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Certio.Application.Services
{
    public class CalendarService : ICalendarService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermissionService _permissionService;
        private readonly IOrganizationContextService _orgContextService;
        private readonly IMatterService _matterService;
        private readonly ILogger<CalendarService> _logger;

        public CalendarService(
            ApplicationDbContext context,
            IPermissionService permissionService,
            IOrganizationContextService orgContextService,
            IMatterService matterService,
            ILogger<CalendarService> logger)
        {
            _context = context;
            _permissionService = permissionService;
            _orgContextService = orgContextService;
            _matterService = matterService;
            _logger = logger;
        }

        public async Task<ServiceResult<CalendarEventDto>> CreateEventAsync(
            int userId,
            int orgId,
            CreateCalendarEventDto createDto,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                // If matter-scoped, validate matter access (handles cross-org via OrganizationRelationship)
                if (createDto.MatterId.HasValue)
                {
                    var matterResult = await _matterService.GetMatterAsync(userId, createDto.MatterId.Value);
                    if (!matterResult.Success)
                    {
                        throw new UnauthorizedOperationException(userId, "access", "Matter");
                    }

                    // Verify matter belongs to the same organization
                    if (matterResult.Data!.OrganizationId != orgId)
                    {
                        throw new OrganizationMismatchException(userId, orgId, "Matter", createDto.MatterId.Value);
                    }
                    // Matter access validated - skip org membership check for cross-org matters
                }
                else
                {
                    // Org-level event (no matter) - validate user is in organization
                    var isInOrg = await _orgContextService.ValidateUserInOrganizationAsync(userId, orgId);
                    if (!isInOrg)
                    {
                        throw new UnauthorizedOperationException(userId, "create event in", "Organization");
                    }
                }

                var normalizedStart = NormalizeToUtc(createDto.StartDateTime);
                var normalizedEnd = NormalizeToUtc(createDto.EndDateTime);

                // Validate dates
                if (normalizedEnd <= normalizedStart)
                {
                    throw new ValidationException("EndDateTime", "End date/time must be after start date/time");
                }

                // Create calendar event
                var calendarEvent = new CalendarEvent
                {
                    OrgId = orgId,
                    MatterId = createDto.MatterId,
                    Title = createDto.Title,
                    Description = createDto.Description,
                    Location = createDto.Location,
                    StartDateTime = normalizedStart,
                    EndDateTime = normalizedEnd,
                    IsAllDayEvent = createDto.IsAllDayEvent,
                    EventType = createDto.EventType,
                    Color = createDto.Color,
                    CreatedById = userId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.CalendarEvents.Add(calendarEvent);
                await _context.SaveChangesAsync();

                // Add attendees if specified
                if (createDto.AttendeeUserIds != null && createDto.AttendeeUserIds.Any())
                {
                    _logger.LogInformation("Processing {Count} attendees for event {EventId}", createDto.AttendeeUserIds.Count, calendarEvent.Id);
                    
                    foreach (var attendeeUserId in createDto.AttendeeUserIds.Distinct())
                    {
                        _logger.LogInformation("Validating attendee {AttendeeUserId} for org {OrgId}, matter {MatterId}", 
                            attendeeUserId, orgId, createDto.MatterId);
                            
                        // Verify attendee is in organization or has matter access
                        var attendeeInOrg = await _orgContextService.ValidateUserInOrganizationAsync(attendeeUserId, orgId);
                        _logger.LogInformation("Direct org membership for user {AttendeeUserId}: {IsInOrg}", attendeeUserId, attendeeInOrg);
                        
                        if (!attendeeInOrg && createDto.MatterId.HasValue)
                        {
                            // Check if attendee has access through matter relationships
                            var matterResult = await _matterService.GetMatterAsync(attendeeUserId, createDto.MatterId.Value);
                            attendeeInOrg = matterResult.Success;
                            _logger.LogInformation("Matter access for user {AttendeeUserId}: {HasAccess}", attendeeUserId, attendeeInOrg);
                        }

                        if (attendeeInOrg)
                        {
                            var attendee = new CalendarEventAttendee
                            {
                                CalendarEventId = calendarEvent.Id,
                                UserId = attendeeUserId,
                                AttendeeType = attendeeUserId == userId ? "Organizer" : "Required",
                                ResponseStatus = attendeeUserId == userId ? "Accepted" : "Pending",
                                IsNotifyRecipient = true,
                                AddedAt = DateTime.UtcNow
                            };
                            _context.CalendarEventAttendees.Add(attendee);
                            _logger.LogInformation("Added attendee {AttendeeUserId} to event {EventId}", attendeeUserId, calendarEvent.Id);
                        }
                        else
                        {
                            _logger.LogWarning("Attendee {AttendeeUserId} validation failed for event {EventId}", attendeeUserId, calendarEvent.Id);
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                // Reload with navigation properties
                var createdEvent = await _context.CalendarEvents
                    .Include(e => e.Matter)
                    .Include(e => e.Attendees)
                        .ThenInclude(a => a.User)
                    .FirstOrDefaultAsync(e => e.Id == calendarEvent.Id);

                var eventDto = MapToDto(createdEvent!);

                _logger.LogInformation("Calendar event {EventId} created in org {OrgId} by user {UserId}",
                    calendarEvent.Id, orgId, userId);

                return ServiceResult<CalendarEventDto>.SuccessResult(eventDto);
            }
            catch (Exception ex) when (ex is DomainException)
            {
                _logger.LogWarning(ex, "Domain exception creating calendar event");
                return ServiceResult<CalendarEventDto>.FailureResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating calendar event in org {OrgId}", orgId);
                return ServiceResult<CalendarEventDto>.FailureResult("An error occurred while creating the event");
            }
        }

        public async Task<ServiceResult<CalendarEventDto>> UpdateEventAsync(
            int userId,
            int eventId,
            UpdateCalendarEventDto updateDto,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var calendarEvent = await _context.CalendarEvents
                    .Include(e => e.Matter)
                    .Include(e => e.CreatedBy)
                    .Include(e => e.ModifiedBy)
                    .Include(e => e.Attendees)
                        .ThenInclude(a => a.User)
                    .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted);

                if (calendarEvent == null)
                {
                    throw new ResourceNotFoundException("CalendarEvent", eventId);
                }

                calendarEvent.StartDateTime = NormalizeToUtc(calendarEvent.StartDateTime);
                calendarEvent.EndDateTime = NormalizeToUtc(calendarEvent.EndDateTime);

                // Validate access
                var canAccess = await CanAccessEventAsync(userId, calendarEvent);
                if (!canAccess)
                {
                    throw new UnauthorizedOperationException(userId, "access", "CalendarEvent");
                }

                // Only creator or organizer can edit
                var isCreator = calendarEvent.CreatedById == userId;
                var isOrganizer = calendarEvent.Attendees.Any(a => a.UserId == userId && a.AttendeeType == "Organizer");
                
                if (!isCreator && !isOrganizer)
                {
                    throw new UnauthorizedOperationException(userId, "edit", "CalendarEvent");
                }

                // Update fields
                if (updateDto.Title != null)
                    calendarEvent.Title = updateDto.Title;

                if (updateDto.Description != null)
                    calendarEvent.Description = updateDto.Description;

                if (updateDto.Location != null)
                    calendarEvent.Location = updateDto.Location;

                if (updateDto.StartDateTime.HasValue)
                    calendarEvent.StartDateTime = NormalizeToUtc(updateDto.StartDateTime.Value);

                if (updateDto.EndDateTime.HasValue)
                    calendarEvent.EndDateTime = NormalizeToUtc(updateDto.EndDateTime.Value);

                if (updateDto.IsAllDayEvent.HasValue)
                    calendarEvent.IsAllDayEvent = updateDto.IsAllDayEvent.Value;

                if (updateDto.EventType != null)
                    calendarEvent.EventType = updateDto.EventType;

                if (updateDto.Color != null)
                    calendarEvent.Color = updateDto.Color;

                // Validate dates after updates
                if (calendarEvent.EndDateTime <= calendarEvent.StartDateTime)
                {
                    throw new ValidationException("EndDateTime", "End date/time must be after start date/time");
                }

                // Update attendees if specified
                if (updateDto.AttendeeUserIds != null)
                {
                    _logger.LogInformation("Updating attendees for event {EventId}, count: {Count}", eventId, updateDto.AttendeeUserIds.Count);
                    
                    // Remove existing attendees (except organizer)
                    var existingAttendees = calendarEvent.Attendees.Where(a => a.AttendeeType != "Organizer").ToList();
                    foreach (var attendee in existingAttendees)
                    {
                        _context.CalendarEventAttendees.Remove(attendee);
                        _logger.LogInformation("Removed existing attendee {AttendeeUserId} from event {EventId}", attendee.UserId, eventId);
                    }

                    // Add new attendees
                    foreach (var attendeeUserId in updateDto.AttendeeUserIds.Distinct())
                    {
                        // Skip if already organizer
                        if (attendeeUserId == userId)
                            continue;

                        _logger.LogInformation("Validating attendee {AttendeeUserId} for event {EventId}, org {OrgId}, matter {MatterId}", 
                            attendeeUserId, eventId, calendarEvent.OrgId, calendarEvent.MatterId);

                        // Verify attendee is in organization or has matter access
                        var attendeeInOrg = await _orgContextService.ValidateUserInOrganizationAsync(attendeeUserId, calendarEvent.OrgId);
                        _logger.LogInformation("Direct org membership for user {AttendeeUserId}: {IsInOrg}", attendeeUserId, attendeeInOrg);
                        
                        if (!attendeeInOrg && calendarEvent.MatterId.HasValue)
                        {
                            // Check if attendee has access through matter relationships
                            var matterResult = await _matterService.GetMatterAsync(attendeeUserId, calendarEvent.MatterId.Value);
                            attendeeInOrg = matterResult.Success;
                            _logger.LogInformation("Matter access for user {AttendeeUserId}: {HasAccess}", attendeeUserId, attendeeInOrg);
                        }

                        if (attendeeInOrg)
                        {
                            var attendee = new CalendarEventAttendee
                            {
                                CalendarEventId = calendarEvent.Id,
                                UserId = attendeeUserId,
                                AttendeeType = "Required",
                                ResponseStatus = "Pending",
                                IsNotifyRecipient = true,
                                AddedAt = DateTime.UtcNow
                            };
                            _context.CalendarEventAttendees.Add(attendee);
                            _logger.LogInformation("Added attendee {AttendeeUserId} to event {EventId}", attendeeUserId, eventId);
                        }
                        else
                        {
                            _logger.LogWarning("Attendee {AttendeeUserId} validation failed for event {EventId}", attendeeUserId, eventId);
                        }
                    }
                }

                calendarEvent.ModifiedById = userId;
                calendarEvent.ModifiedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var eventDto = MapToDto(calendarEvent);

                _logger.LogInformation("Calendar event {EventId} updated by user {UserId}", eventId, userId);

                return ServiceResult<CalendarEventDto>.SuccessResult(eventDto);
            }
            catch (Exception ex) when (ex is DomainException)
            {
                _logger.LogWarning(ex, "Domain exception updating calendar event {EventId}", eventId);
                return ServiceResult<CalendarEventDto>.FailureResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating calendar event {EventId}", eventId);
                return ServiceResult<CalendarEventDto>.FailureResult("An error occurred while updating the event");
            }
        }

        public async Task<ServiceResult> DeleteEventAsync(
            int userId,
            int eventId,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var calendarEvent = await _context.CalendarEvents
                    .Include(e => e.Attendees)
                    .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted);

                if (calendarEvent == null)
                {
                    throw new ResourceNotFoundException("CalendarEvent", eventId);
                }

                // Validate access
                var canAccess = await CanAccessEventAsync(userId, calendarEvent);
                if (!canAccess)
                {
                    throw new UnauthorizedOperationException(userId, "access", "CalendarEvent");
                }

                // Only creator or organizer can delete
                var isCreator = calendarEvent.CreatedById == userId;
                var isOrganizer = calendarEvent.Attendees.Any(a => a.UserId == userId && a.AttendeeType == "Organizer");
                
                if (!isCreator && !isOrganizer)
                {
                    throw new UnauthorizedOperationException(userId, "delete", "CalendarEvent");
                }

                // Soft delete
                calendarEvent.IsDeleted = true;
                calendarEvent.DeletedAt = DateTime.UtcNow;
                calendarEvent.DeletedById = userId;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Calendar event {EventId} deleted by user {UserId}", eventId, userId);

                return ServiceResult.SuccessResult();
            }
            catch (Exception ex) when (ex is DomainException)
            {
                _logger.LogWarning(ex, "Domain exception deleting calendar event {EventId}", eventId);
                return ServiceResult.FailureResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting calendar event {EventId}", eventId);
                return ServiceResult.FailureResult("An error occurred while deleting the event");
            }
        }

        public async Task<ServiceResult<CalendarEventDto>> GetEventAsync(int userId, int eventId)
        {
            try
            {
                var calendarEvent = await _context.CalendarEvents
                    .Include(e => e.Matter)
                    .Include(e => e.CreatedBy)
                    .Include(e => e.ModifiedBy)
                    .Include(e => e.Attendees)
                        .ThenInclude(a => a.User)
                    .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted);

                if (calendarEvent == null)
                {
                    throw new ResourceNotFoundException("CalendarEvent", eventId);
                }

                // Validate access
                var canAccess = await CanAccessEventAsync(userId, calendarEvent);
                if (!canAccess)
                {
                    throw new UnauthorizedOperationException(userId, "access", "CalendarEvent");
                }

                var eventDto = MapToDto(calendarEvent);

                return ServiceResult<CalendarEventDto>.SuccessResult(eventDto);
            }
            catch (Exception ex) when (ex is DomainException)
            {
                _logger.LogWarning(ex, "Domain exception getting calendar event {EventId}", eventId);
                return ServiceResult<CalendarEventDto>.FailureResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting calendar event {EventId}", eventId);
                return ServiceResult<CalendarEventDto>.FailureResult("An error occurred while retrieving the event");
            }
        }

        public async Task<ServiceResult<List<CalendarEventDto>>> ListEventsAsync(
            int userId,
            int organizationId,
            CalendarFilterDto? filter = null)
        {
            try
            {
                // Validate user is in organization
                var isInOrg = await _orgContextService.ValidateUserInOrganizationAsync(userId, organizationId);
                if (!isInOrg)
                {
                    throw new UnauthorizedOperationException(userId, "list events in", "Organization");
                }

                var query = _context.CalendarEvents
                    .Include(e => e.Matter)
                    .Include(e => e.Attendees)
                        .ThenInclude(a => a.User)
                    .Where(e => e.OrgId == organizationId && !e.IsDeleted);

                // Apply filters
                if (filter != null)
                {
                    if (filter.StartDate.HasValue)
                    {
                        query = query.Where(e => e.EndDateTime >= filter.StartDate.Value);
                    }

                    if (filter.EndDate.HasValue)
                    {
                        query = query.Where(e => e.StartDateTime <= filter.EndDate.Value);
                    }

                    if (filter.MatterId.HasValue)
                    {
                        query = query.Where(e => e.MatterId == filter.MatterId.Value);
                    }

                    if (filter.EventTypes != null && filter.EventTypes.Any())
                    {
                        query = query.Where(e => filter.EventTypes.Contains(e.EventType));
                    }

                    if (!filter.IncludeOrgEvents)
                    {
                        query = query.Where(e => e.MatterId != null);
                    }

                    if (!filter.IncludeMatterEvents)
                    {
                        query = query.Where(e => e.MatterId == null);
                    }
                }

                // Filter by matter access - only show matter events user can access
                var events = await query.ToListAsync();
                var accessibleEvents = new List<CalendarEvent>();

                foreach (var evt in events)
                {
                    if (await CanAccessEventAsync(userId, evt))
                    {
                        accessibleEvents.Add(evt);
                    }
                }

                var eventDtos = accessibleEvents.Select(MapToDto).ToList();

                return ServiceResult<List<CalendarEventDto>>.SuccessResult(eventDtos);
            }
            catch (Exception ex) when (ex is DomainException)
            {
                _logger.LogWarning(ex, "Domain exception listing calendar events for org {OrgId}", organizationId);
                return ServiceResult<List<CalendarEventDto>>.FailureResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing calendar events for org {OrgId}", organizationId);
                return ServiceResult<List<CalendarEventDto>>.FailureResult("An error occurred while retrieving events");
            }
        }

        public async Task<ServiceResult<List<CalendarEventDto>>> ListEventsForMatterAsync(
            int userId,
            int matterId,
            CalendarFilterDto? filter = null)
        {
            try
            {
                // Validate matter access
                var matterResult = await _matterService.GetMatterAsync(userId, matterId);
                if (!matterResult.Success)
                {
                    throw new UnauthorizedOperationException(userId, "access", "Matter");
                }

                var query = _context.CalendarEvents
                    .Include(e => e.Matter)
                    .Include(e => e.Attendees)
                        .ThenInclude(a => a.User)
                    .Where(e => e.MatterId == matterId && !e.IsDeleted);

                // Apply date filters
                if (filter != null)
                {
                    if (filter.StartDate.HasValue)
                    {
                        query = query.Where(e => e.EndDateTime >= filter.StartDate.Value);
                    }

                    if (filter.EndDate.HasValue)
                    {
                        query = query.Where(e => e.StartDateTime <= filter.EndDate.Value);
                    }

                    if (filter.EventTypes != null && filter.EventTypes.Any())
                    {
                        query = query.Where(e => filter.EventTypes.Contains(e.EventType));
                    }
                }

                var events = await query.OrderBy(e => e.StartDateTime).ToListAsync();
                var eventDtos = events.Select(MapToDto).ToList();

                return ServiceResult<List<CalendarEventDto>>.SuccessResult(eventDtos);
            }
            catch (Exception ex) when (ex is DomainException)
            {
                _logger.LogWarning(ex, "Domain exception listing calendar events for matter {MatterId}", matterId);
                return ServiceResult<List<CalendarEventDto>>.FailureResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing calendar events for matter {MatterId}", matterId);
                return ServiceResult<List<CalendarEventDto>>.FailureResult("An error occurred while retrieving events");
            }
        }

        public async Task<ServiceResult> AddAttendeeAsync(
            int userId,
            int eventId,
            int attendeeUserId,
            string attendeeType,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var calendarEvent = await _context.CalendarEvents
                    .Include(e => e.Attendees)
                    .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted);

                if (calendarEvent == null)
                {
                    throw new ResourceNotFoundException("CalendarEvent", eventId);
                }

                // Validate access
                var canAccess = await CanAccessEventAsync(userId, calendarEvent);
                if (!canAccess)
                {
                    throw new UnauthorizedOperationException(userId, "access", "CalendarEvent");
                }

                // Verify attendee is in organization
                var attendeeInOrg = await _orgContextService.ValidateUserInOrganizationAsync(attendeeUserId, calendarEvent.OrgId);
                if (!attendeeInOrg)
                {
                    throw new ValidationException("AttendeeUserId", "Attendee is not a member of the organization");
                }

                // Check if already an attendee
                if (calendarEvent.Attendees.Any(a => a.UserId == attendeeUserId))
                {
                    throw new ValidationException("AttendeeUserId", "User is already an attendee of this event");
                }

                var attendee = new CalendarEventAttendee
                {
                    CalendarEventId = eventId,
                    UserId = attendeeUserId,
                    AttendeeType = attendeeType,
                    ResponseStatus = "Pending",
                    IsNotifyRecipient = true,
                    AddedAt = DateTime.UtcNow
                };

                _context.CalendarEventAttendees.Add(attendee);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {AttendeeUserId} added to calendar event {EventId} by user {UserId}",
                    attendeeUserId, eventId, userId);

                return ServiceResult.SuccessResult();
            }
            catch (Exception ex) when (ex is DomainException)
            {
                _logger.LogWarning(ex, "Domain exception adding attendee to event {EventId}", eventId);
                return ServiceResult.FailureResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding attendee to event {EventId}", eventId);
                return ServiceResult.FailureResult("An error occurred while adding the attendee");
            }
        }

        public async Task<ServiceResult> RemoveAttendeeAsync(
            int userId,
            int eventId,
            int attendeeUserId,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var calendarEvent = await _context.CalendarEvents
                    .Include(e => e.Attendees)
                    .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted);

                if (calendarEvent == null)
                {
                    throw new ResourceNotFoundException("CalendarEvent", eventId);
                }

                // Validate access
                var canAccess = await CanAccessEventAsync(userId, calendarEvent);
                if (!canAccess)
                {
                    throw new UnauthorizedOperationException(userId, "access", "CalendarEvent");
                }

                var attendee = calendarEvent.Attendees.FirstOrDefault(a => a.UserId == attendeeUserId);
                if (attendee == null)
                {
                    throw new ResourceNotFoundException("CalendarEventAttendee", attendeeUserId);
                }

                // Prevent removing the organizer
                if (attendee.AttendeeType == "Organizer")
                {
                    throw new ValidationException("AttendeeType", "Cannot remove the organizer from the event");
                }

                _context.CalendarEventAttendees.Remove(attendee);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {AttendeeUserId} removed from calendar event {EventId} by user {UserId}",
                    attendeeUserId, eventId, userId);

                return ServiceResult.SuccessResult();
            }
            catch (Exception ex) when (ex is DomainException)
            {
                _logger.LogWarning(ex, "Domain exception removing attendee from event {EventId}", eventId);
                return ServiceResult.FailureResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing attendee from event {EventId}", eventId);
                return ServiceResult.FailureResult("An error occurred while removing the attendee");
            }
        }

        public async Task<ServiceResult> UpdateAttendeeResponseAsync(
            int userId,
            int eventId,
            string responseStatus,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var calendarEvent = await _context.CalendarEvents
                    .Include(e => e.Attendees)
                    .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted);

                if (calendarEvent == null)
                {
                    throw new ResourceNotFoundException("CalendarEvent", eventId);
                }

                var attendee = calendarEvent.Attendees.FirstOrDefault(a => a.UserId == userId);
                if (attendee == null)
                {
                    throw new ResourceNotFoundException("CalendarEventAttendee", userId);
                }

                attendee.ResponseStatus = responseStatus;
                attendee.ResponseAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} updated response status to {Status} for event {EventId}",
                    userId, responseStatus, eventId);

                return ServiceResult.SuccessResult();
            }
            catch (Exception ex) when (ex is DomainException)
            {
                _logger.LogWarning(ex, "Domain exception updating attendee response for event {EventId}", eventId);
                return ServiceResult.FailureResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating attendee response for event {EventId}", eventId);
                return ServiceResult.FailureResult("An error occurred while updating the response");
            }
        }

        // Helper methods

        private async Task<bool> CanAccessEventAsync(int userId, CalendarEvent calendarEvent)
        {
            // Matter-scoped events require matter access (handles cross-org via OrganizationRelationship)
            if (calendarEvent.MatterId.HasValue)
            {
                var matterResult = await _matterService.GetMatterAsync(userId, calendarEvent.MatterId.Value);
                return matterResult.Success;
            }

            // Org-scoped events (no matter) require org membership
            var isInOrg = await _orgContextService.ValidateUserInOrganizationAsync(userId, calendarEvent.OrgId);
            return isInOrg;
        }

        private CalendarEventDto MapToDto(CalendarEvent calendarEvent)
        {
            return new CalendarEventDto
            {
                Id = calendarEvent.Id,
                OrgId = calendarEvent.OrgId,
                MatterId = calendarEvent.MatterId,
                MatterTitle = calendarEvent.Matter?.Title,
                Title = calendarEvent.Title,
                Description = calendarEvent.Description,
                Location = calendarEvent.Location,
                StartDateTime = NormalizeToUtc(calendarEvent.StartDateTime),
                EndDateTime = NormalizeToUtc(calendarEvent.EndDateTime),
                IsAllDayEvent = calendarEvent.IsAllDayEvent,
                EventType = calendarEvent.EventType,
                Color = calendarEvent.Color,
                CreatedAt = NormalizeToUtc(calendarEvent.CreatedAt),
                CreatedById = calendarEvent.CreatedById,
                CreatedBy = calendarEvent.CreatedBy != null ? new UserSummaryDto
                {
                    Id = calendarEvent.CreatedBy.Id,
                    FirstName = calendarEvent.CreatedBy.FirstName,
                    LastName = calendarEvent.CreatedBy.LastName,
                    Email = calendarEvent.CreatedBy.Email ?? ""
                } : null,
                ModifiedAt = NormalizeToUtc(calendarEvent.ModifiedAt),
                ModifiedById = calendarEvent.ModifiedById,
                ModifiedBy = calendarEvent.ModifiedBy != null ? new UserSummaryDto
                {
                    Id = calendarEvent.ModifiedBy.Id,
                    FirstName = calendarEvent.ModifiedBy.FirstName,
                    LastName = calendarEvent.ModifiedBy.LastName,
                    Email = calendarEvent.ModifiedBy.Email ?? ""
                } : null,
                Attendees = calendarEvent.Attendees.Select(a => new CalendarEventAttendeeDto
                {
                    Id = a.Id,
                    CalendarEventId = a.CalendarEventId,
                    UserId = a.UserId,
                    AttendeeType = a.AttendeeType,
                    ResponseStatus = a.ResponseStatus,
                    IsNotifyRecipient = a.IsNotifyRecipient,
                    AddedAt = NormalizeToUtc(a.AddedAt),
                    ResponseAt = NormalizeToUtc(a.ResponseAt),
                    User = a.User != null ? new UserSummaryDto
                    {
                        Id = a.User.Id,
                        FirstName = a.User.FirstName,
                        LastName = a.User.LastName,
                        Email = a.User.Email ?? ""
                    } : null
                }).ToList()
            };
        }

        private static DateTime NormalizeToUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        private static DateTime? NormalizeToUtc(DateTime? value)
        {
            return value.HasValue ? NormalizeToUtc(value.Value) : null;
        }
    }
}

