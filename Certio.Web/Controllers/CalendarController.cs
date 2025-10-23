using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Certio.Application.Interfaces;
using Certio.Application.DTOs;
using Certio.Domain.Users;
using Certio.Infrastructure.Data;
using Certio.Web.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Certio.Web.Configuration;

namespace Certio.Web.Controllers
{
    public class CalendarController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GoogleMapsConfiguration _googleMapsConfig;
        private readonly ICalendarService _calendarService;
        private readonly IMatterService _matterService;
        private readonly ITeamService _teamService;
        private readonly ILogger<CalendarController> _logger;

        public CalendarController(
            ApplicationDbContext context,
            IOptions<GoogleMapsConfiguration> googleMapsConfig,
            ICalendarService calendarService,
            IMatterService matterService,
            ITeamService teamService,
            ILogger<CalendarController> logger)
        {
            _context = context;
            _googleMapsConfig = googleMapsConfig.Value;
            _calendarService = calendarService;
            _matterService = matterService;
            _teamService = teamService;
            _logger = logger;
        }

        // GET: /Client/{orgId}/Calendar
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Calendar")]
        public async Task<IActionResult> Index(int orgId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // Set ViewBag for layout and client-side
            ViewBag.OrganizationId = orgId;
            ViewBag.GoogleMapsApiKey = _googleMapsConfig.ApiKey;
            ViewBag.GoogleMapsEnabled = _googleMapsConfig.Enabled;
            ViewBag.CurrentUserId = user.Id;
            ViewBag.CurrentUserName = $"{user.FirstName} {user.LastName}";
            ViewBag.CurrentUserInitials = $"{user.FirstName[0]}{user.LastName[0]}".ToUpper();
            ViewBag.CurrentUserEmail = user.Email ?? "";

            // Get organization name
            var org = await _context.Organizations
                .Where(o => o.Id == orgId)
                .FirstOrDefaultAsync();
            ViewBag.OrganizationName = org?.Name ?? "Client";

            return View("~/Views/Client/Calendar.cshtml");
        }

        // GET: /Client/{orgId}/Calendar/Events
        [Authorize]  // Basic auth only - custom authorization below
        [HttpGet("/Client/{orgId:int}/Calendar/Events")]
        public async Task<IActionResult> GetEvents(
            int orgId,
            [FromQuery] DateTime? start,
            [FromQuery] DateTime? end,
            [FromQuery] int? matterId,
            [FromQuery] string? eventTypes)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            // If filtering by matter, use ListEventsForMatterAsync (handles cross-org relationships)
            ServiceResult<List<CalendarEventDto>> result;
            
            if (matterId.HasValue)
            {
                var filter = new CalendarFilterDto
                {
                    StartDate = start,
                    EndDate = end,
                    EventTypes = string.IsNullOrEmpty(eventTypes) 
                        ? null 
                        : eventTypes.Split(',').ToList()
                };
                
                result = await _calendarService.ListEventsForMatterAsync(user.Id, matterId.Value, filter);
            }
            else
            {
                // No matter context - use standard org-scoped list (requires org membership)
                var (_, currentOrgId) = GetUserContext();
                if (currentOrgId != orgId)
                {
                    _logger.LogWarning("User {UserId} attempted to access calendar for unauthorized org {OrgId}", 
                        user.Id, orgId);
                    return NotFound();
                }
                
                var filter = new CalendarFilterDto
                {
                    StartDate = start,
                    EndDate = end,
                    EventTypes = string.IsNullOrEmpty(eventTypes) 
                        ? null 
                        : eventTypes.Split(',').ToList()
                };
                
                result = await _calendarService.ListEventsAsync(user.Id, orgId, filter);
            }

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            return Json(new { success = true, events = result.Data });
        }

        // GET: /Client/{orgId}/Calendar/Events/{eventId}
        [Authorize]  // Basic auth only - custom authorization below
        [HttpGet("/Client/{orgId:int}/Calendar/Events/{eventId:int}")]
        [ViewAudit("CalendarEvent", "eventId")]
        public async Task<IActionResult> GetEvent(int orgId, int eventId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            // Get the event first to check if it belongs to a matter
            var result = await _calendarService.GetEventAsync(user.Id, eventId);

            if (!result.Success)
            {
                return NotFound(new { success = false, message = result.ErrorMessage });
            }

            var eventData = result.Data;
            
            // If event belongs to a matter, verify matter access (handles cross-org relationships)
            if (eventData.MatterId.HasValue)
            {
                var matterResult = await _matterService.GetMatterAsync(user.Id, eventData.MatterId.Value);
                if (!matterResult.Success)
                {
                    _logger.LogWarning("User {UserId} attempted to access event {EventId} for unauthorized matter {MatterId}", 
                        user.Id, eventId, eventData.MatterId.Value);
                    return NotFound(new { success = false, message = "Event not found or access denied" });
                }
            }
            else
            {
                // No matter - verify direct org membership
                var (_, currentOrgId) = GetUserContext();
                if (currentOrgId != orgId)
                {
                    _logger.LogWarning("User {UserId} attempted to access event for unauthorized org {OrgId}", 
                        user.Id, orgId);
                    return NotFound();
                }
            }

            return Json(new { success = true, @event = eventData });
        }

        // POST: /Client/{orgId}/Calendar/Events
        [Authorize]  // Basic auth only - custom authorization below
        [HttpPost("/Client/{orgId:int}/Calendar/Events")]
        public async Task<IActionResult> CreateEvent(int orgId, [FromBody] CreateCalendarEventDto dto)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid data", errors = ModelState });
            }

            // If event belongs to a matter, verify matter access (handles cross-org relationships)
            if (dto.MatterId.HasValue)
            {
                var matterResult = await _matterService.GetMatterAsync(user.Id, dto.MatterId.Value);
                if (!matterResult.Success)
                {
                    _logger.LogWarning("User {UserId} attempted to create event for unauthorized matter {MatterId}", 
                        user.Id, dto.MatterId.Value);
                    return NotFound(new { success = false, message = "Matter not found or access denied" });
                }
            }
            else
            {
                // No matter - verify direct org membership
                var (_, currentOrgId) = GetUserContext();
                if (currentOrgId != orgId)
                {
                    _logger.LogWarning("User {UserId} attempted to create event for unauthorized org {OrgId}", 
                        user.Id, orgId);
                    return NotFound();
                }
            }

            var result = await _calendarService.CreateEventAsync(
                user.Id,
                orgId,
                dto,
                GetIpAddress(),
                GetUserAgent()
            );

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            return Json(new { success = true, @event = result.Data });
        }

        // PUT: /Client/{orgId}/Calendar/Events/{eventId}
        [Authorize]  // Basic auth only - custom authorization below
        [HttpPut("/Client/{orgId:int}/Calendar/Events/{eventId:int}")]
        public async Task<IActionResult> UpdateEvent(int orgId, int eventId, [FromBody] UpdateCalendarEventDto dto)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid data", errors = ModelState });
            }

            // Get existing event to check matter access
            var existingEventResult = await _calendarService.GetEventAsync(user.Id, eventId);
            if (!existingEventResult.Success)
            {
                return NotFound(new { success = false, message = "Event not found" });
            }

            // If event belongs to a matter, verify matter access (handles cross-org relationships)
            // Note: MatterId is not changeable after creation
            if (existingEventResult.Data.MatterId.HasValue)
            {
                var matterResult = await _matterService.GetMatterAsync(user.Id, existingEventResult.Data.MatterId.Value);
                if (!matterResult.Success)
                {
                    _logger.LogWarning("User {UserId} attempted to update event {EventId} for unauthorized matter {MatterId}", 
                        user.Id, eventId, existingEventResult.Data.MatterId.Value);
                    return NotFound(new { success = false, message = "Matter not found or access denied" });
                }
            }
            else
            {
                // No matter - verify direct org membership
                var (_, currentOrgId) = GetUserContext();
                if (currentOrgId != orgId)
                {
                    _logger.LogWarning("User {UserId} attempted to update event for unauthorized org {OrgId}", 
                        user.Id, orgId);
                    return NotFound();
                }
            }

            var result = await _calendarService.UpdateEventAsync(
                user.Id,
                eventId,
                dto,
                GetIpAddress(),
                GetUserAgent()
            );

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            return Json(new { success = true, @event = result.Data });
        }

        // DELETE: /Client/{orgId}/Calendar/Events/{eventId}
        [Authorize]  // Basic auth only - custom authorization below
        [HttpDelete("/Client/{orgId:int}/Calendar/Events/{eventId:int}")]
        public async Task<IActionResult> DeleteEvent(int orgId, int eventId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            // Get existing event to check matter access
            var existingEventResult = await _calendarService.GetEventAsync(user.Id, eventId);
            if (!existingEventResult.Success)
            {
                return NotFound(new { success = false, message = "Event not found" });
            }

            // If event belongs to a matter, verify matter access (handles cross-org relationships)
            if (existingEventResult.Data.MatterId.HasValue)
            {
                var matterResult = await _matterService.GetMatterAsync(user.Id, existingEventResult.Data.MatterId.Value);
                if (!matterResult.Success)
                {
                    _logger.LogWarning("User {UserId} attempted to delete event {EventId} for unauthorized matter {MatterId}", 
                        user.Id, eventId, existingEventResult.Data.MatterId.Value);
                    return NotFound(new { success = false, message = "Matter not found or access denied" });
                }
            }
            else
            {
                // No matter - verify direct org membership
                var (_, currentOrgId) = GetUserContext();
                if (currentOrgId != orgId)
                {
                    _logger.LogWarning("User {UserId} attempted to delete event for unauthorized org {OrgId}", 
                        user.Id, orgId);
                    return NotFound();
                }
            }

            var result = await _calendarService.DeleteEventAsync(
                user.Id,
                eventId,
                GetIpAddress(),
                GetUserAgent()
            );

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            return Json(new { success = true, message = "Event deleted successfully" });
        }

        // POST: /Client/{orgId}/Calendar/Events/{eventId}/Attendees
        [Authorize(Policy = "OrgMember")]
        [HttpPost("/Client/{orgId:int}/Calendar/Events/{eventId:int}/Attendees")]
        public async Task<IActionResult> AddAttendee(int orgId, int eventId, [FromBody] AddCalendarAttendeeDto dto)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid data", errors = ModelState });
            }

            var result = await _calendarService.AddAttendeeAsync(
                user.Id,
                eventId,
                dto.UserId,
                dto.AttendeeType,
                GetIpAddress(),
                GetUserAgent()
            );

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            return Json(new { success = true, message = "Attendee added successfully" });
        }

        // DELETE: /Client/{orgId}/Calendar/Events/{eventId}/Attendees/{userId}
        [Authorize(Policy = "OrgMember")]
        [HttpDelete("/Client/{orgId:int}/Calendar/Events/{eventId:int}/Attendees/{userId:int}")]
        public async Task<IActionResult> RemoveAttendee(int orgId, int eventId, int userId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            var result = await _calendarService.RemoveAttendeeAsync(
                user.Id,
                eventId,
                userId,
                GetIpAddress(),
                GetUserAgent()
            );

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            return Json(new { success = true, message = "Attendee removed successfully" });
        }

        // PUT: /Client/{orgId}/Calendar/Events/{eventId}/Response
        [Authorize(Policy = "OrgMember")]
        [HttpPut("/Client/{orgId:int}/Calendar/Events/{eventId:int}/Response")]
        public async Task<IActionResult> UpdateResponse(int orgId, int eventId, [FromBody] UpdateResponseDto dto)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            var result = await _calendarService.UpdateAttendeeResponseAsync(
                user.Id,
                eventId,
                dto.ResponseStatus,
                GetIpAddress(),
                GetUserAgent()
            );

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            return Json(new { success = true, message = "Response updated successfully" });
        }

        // GET: /Client/{orgId}/Calendar/Matters - Get matters for dropdown
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Calendar/Matters")]
        public async Task<IActionResult> GetMatters(int orgId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            var result = await _matterService.ListMattersAsync(user.Id, orgId);

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            var matters = result.Data!.Select(m => new
            {
                id = m.Id,
                title = m.Title,
                organizationId = m.OrganizationId
            }).ToList();

            return Json(new { success = true, matters });
        }

        // GET: /Client/{orgId}/Users - Get org users for attendee selection
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Users")]
        public async Task<IActionResult> GetUsers(int orgId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            // Use TeamService to get organization users (follows service layer pattern)
            var result = await _teamService.GetTeamMembersAsync(orgId, user.Id);

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            // Map TeamMemberDto to format expected by calendar UI
            var users = result.Data!
                .Where(tm => tm.IsActive)
                .Select(tm => new
                {
                    id = tm.UserId,
                    name = $"{tm.FirstName} {tm.LastName}".Trim(),
                    initials = GetInitials(tm.FirstName, tm.LastName),
                    email = tm.Email
                })
                .ToList();

            return Json(new { success = true, users });
        }

        // Helper method to generate user initials
        private string GetInitials(string firstName, string lastName)
        {
            var first = !string.IsNullOrWhiteSpace(firstName) ? firstName[0].ToString().ToUpper() : "";
            var last = !string.IsNullOrWhiteSpace(lastName) ? lastName[0].ToString().ToUpper() : "";
            return first + last;
        }

        // GET: /Client/{orgId}/Matter/{matterId}/Calendar - AJAX partial for Matter Details
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Matter/{matterId:int}/Calendar")]
        public async Task<IActionResult> MatterCalendar(int orgId, int matterId)
        {
            var (user, organizationId) = GetUserContext();
            if (user == null || organizationId == 0)
            {
                return Unauthorized();
            }

            // Verify matter access
            var matterResult = await _matterService.GetMatterAsync(user.Id, matterId);
            if (!matterResult.Success)
            {
                _logger.LogWarning("User {UserId} attempted to access calendar for unauthorized matter {MatterId}", 
                    user.Id, matterId);
                return NotFound();
            }

            // Set ViewBag for the partial view
            // Note: orgId should already be the matter's actual organization ID thanks to MatterController.Details redirect
            ViewBag.OrganizationId = orgId;
            ViewBag.MatterId = matterId;
            ViewBag.GoogleMapsApiKey = _googleMapsConfig.ApiKey;
            ViewBag.GoogleMapsEnabled = _googleMapsConfig.Enabled;
            ViewBag.CurrentUserId = user.Id;
            ViewBag.CurrentUserName = $"{user.FirstName} {user.LastName}";
            ViewBag.CurrentUserInitials = $"{user.FirstName[0]}{user.LastName[0]}".ToUpper();
            ViewBag.CurrentUserEmail = user.Email ?? "";

            return PartialView("~/Views/Matter/_MatterCalendar.cshtml");
        }

        // Helper methods

        private (User?, int) GetUserContext()
        {
            var customUser = HttpContext.Items["CustomUser"] as User;
            var orgId = customUser?.GetPrimaryOrganization()?.OrganizationId ?? 0;
            return (customUser, orgId);
        }

        private string? GetIpAddress() =>
            HttpContext.Connection.RemoteIpAddress?.ToString();

        private string? GetUserAgent() =>
            HttpContext.Request.Headers["User-Agent"].ToString();
    }

    // Helper DTO for response update
    public class UpdateResponseDto
    {
        public string ResponseStatus { get; set; } = "";
    }
}

