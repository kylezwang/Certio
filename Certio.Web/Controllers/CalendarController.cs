using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Certio.Application.Interfaces;
using Certio.Application.DTOs;
using Certio.Domain.Users;
using Certio.Infrastructure.Data;
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
        [Authorize(Policy = "OrgMember")]
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

            var filter = new CalendarFilterDto
            {
                StartDate = start,
                EndDate = end,
                MatterId = matterId,
                EventTypes = string.IsNullOrEmpty(eventTypes) 
                    ? null 
                    : eventTypes.Split(',').ToList()
            };

            var result = await _calendarService.ListEventsAsync(user.Id, orgId, filter);

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            return Json(new { success = true, events = result.Data });
        }

        // GET: /Client/{orgId}/Calendar/Events/{eventId}
        [Authorize(Policy = "OrgMember")]
        [HttpGet("/Client/{orgId:int}/Calendar/Events/{eventId:int}")]
        public async Task<IActionResult> GetEvent(int orgId, int eventId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
            }

            var result = await _calendarService.GetEventAsync(user.Id, eventId);

            if (!result.Success)
            {
                return NotFound(new { success = false, message = result.ErrorMessage });
            }

            return Json(new { success = true, @event = result.Data });
        }

        // POST: /Client/{orgId}/Calendar/Events
        [Authorize(Policy = "OrgMember")]
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
        [Authorize(Policy = "OrgMember")]
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
        [Authorize(Policy = "OrgMember")]
        [HttpDelete("/Client/{orgId:int}/Calendar/Events/{eventId:int}")]
        public async Task<IActionResult> DeleteEvent(int orgId, int eventId)
        {
            var (user, _) = GetUserContext();
            if (user == null)
            {
                return Unauthorized();
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
                title = m.Title
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

