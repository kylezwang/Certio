using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Certio.Infrastructure.Data;
using Certio.Domain.Calendar;
using Certio.Application.Interfaces;
using System.Text.Json;

namespace Certio.Web.Controllers.Api
{
    [Authorize]
    [ApiController]
    [Route("api/calendar-sync")]
    public class CalendarSyncController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICalendarSyncService _calendarSyncService;
        private readonly ILogger<CalendarSyncController> _logger;
        private readonly IDataProtectionProvider _dataProtectionProvider;

        public CalendarSyncController(
            ApplicationDbContext context,
            ICalendarSyncService calendarSyncService,
            IDataProtectionProvider dataProtectionProvider,
            ILogger<CalendarSyncController> logger)
        {
            _context = context;
            _calendarSyncService = calendarSyncService;
            _logger = logger;
            _dataProtectionProvider = dataProtectionProvider;
        }

        /// <summary>
        /// Get count of calendar events that haven't been synced to external calendars
        /// </summary>
        [HttpGet("unsynced-count")]
        public async Task<IActionResult> GetUnsyncedCount([FromQuery] int orgId)
        {
            try
            {
                var userId = GetCurrentUserId();

                // Check if user has any calendar integrations
                var hasIntegrations = await _context.CalendarIntegrations
                    .AnyAsync(ci => ci.OrgId == orgId && ci.UserId == userId);

                if (!hasIntegrations)
                {
                    return Ok(new { success = true, count = 0, hasIntegrations = false });
                }

                // Get count of events that haven't been synced (ExternalCalendarId is null or SyncStatus is not "Synced")
                var unsyncedCount = await _context.CalendarEvents
                    .Where(e => e.OrgId == orgId 
                            && !e.IsDeleted 
                            && (e.ExternalCalendarId == null || e.SyncStatus != "Synced")
                            && e.StartDateTime >= DateTime.UtcNow.AddDays(-30)) // Only count recent/upcoming events
                    .CountAsync();

                return Ok(new { success = true, count = unsyncedCount, hasIntegrations = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unsynced calendar events count for org {OrgId}", orgId);
                return StatusCode(500, new { success = false, error = "Failed to get unsynced events count" });
            }
        }

        /// <summary>
        /// Sync Notal calendar events to connected OAuth calendars
        /// </summary>
        [HttpPost("sync")]
        public async Task<IActionResult> SyncEvents([FromBody] SyncEventsRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();

                // Get user's calendar integrations for this org
                var integrations = await _context.CalendarIntegrations
                    .Where(ci => ci.OrgId == request.OrgId && ci.UserId == userId)
                    .ToListAsync();

                if (!integrations.Any())
                {
                    return BadRequest(new { success = false, error = "No calendar integrations found. Please connect a calendar first." });
                }

                // Get unsynced events
                var unsyncedEvents = await _context.CalendarEvents
                    .Where(e => e.OrgId == request.OrgId 
                            && !e.IsDeleted 
                            && (e.ExternalCalendarId == null || e.SyncStatus != "Synced")
                            && e.StartDateTime >= DateTime.UtcNow.AddDays(-30)) // Only sync recent/upcoming events
                    .OrderBy(e => e.StartDateTime)
                    .ToListAsync();

                if (!unsyncedEvents.Any())
                {
                    return Ok(new { success = true, message = "All events are already synced", syncedCount = 0 });
                }

                var syncedCount = 0;
                var failedCount = 0;
                var errors = new List<string>();

                foreach (var integration in integrations)
                {
                    try
                    {
                        foreach (var calendarEvent in unsyncedEvents)
                        {
                            try
                            {
                                string externalEventId;

                                if (integration.Provider == "Google")
                                {
                                    externalEventId = await SyncToGoogleCalendar(integration, calendarEvent);
                                }
                                else if (integration.Provider == "Outlook")
                                {
                                    externalEventId = await SyncToOutlookCalendar(integration, calendarEvent);
                                }
                                else
                                {
                                    _logger.LogWarning("Unsupported provider: {Provider}", integration.Provider);
                                    continue;
                                }

                                // Update event with external calendar info
                                calendarEvent.ExternalCalendarId = externalEventId;
                                calendarEvent.ExternalCalendarSource = integration.Provider;
                                calendarEvent.SyncStatus = "Synced";
                                calendarEvent.LastSyncedAt = DateTime.UtcNow;

                                syncedCount++;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to sync event {EventId} to {Provider}", calendarEvent.Id, integration.Provider);
                                calendarEvent.SyncStatus = "Failed";
                                failedCount++;
                                errors.Add($"Event '{calendarEvent.Title}' failed to sync to {integration.Provider}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to sync events to {Provider} integration", integration.Provider);
                        errors.Add($"Failed to sync to {integration.Provider}: {ex.Message}");
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Synced {syncedCount} event(s) to your connected calendar(s)",
                    syncedCount,
                    failedCount,
                    errors = errors.Any() ? errors : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing calendar events for org {OrgId}", request.OrgId);
                return StatusCode(500, new { success = false, error = "Failed to sync calendar events" });
            }
        }

        private async Task<string> SyncToGoogleCalendar(CalendarIntegration integration, CalendarEvent calendarEvent)
        {
            // Decrypt the access token using secure helper
            string accessToken;
            try
            {
                accessToken = DecryptToken(integration.AccessToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt access token for integration {IntegrationId}", integration.Id);
                throw new Exception("Failed to decrypt access token");
            }

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Parse location if it's JSON, otherwise use as-is
            string locationString = calendarEvent.Location ?? "";
            if (!string.IsNullOrWhiteSpace(locationString) && locationString.TrimStart().StartsWith("{"))
            {
                try
                {
                    var locationJson = JsonSerializer.Deserialize<JsonElement>(locationString);
                    // Try to extract the name field from JSON location
                    if (locationJson.TryGetProperty("name", out var nameProperty))
                    {
                        locationString = nameProperty.GetString() ?? locationString;
                    }
                }
                catch
                {
                    // If parsing fails, just use the original string
                }
            }

            var googleEvent = new
            {
                summary = calendarEvent.Title,
                description = calendarEvent.Description,
                location = locationString,
                start = new
                {
                    dateTime = calendarEvent.StartDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    timeZone = "UTC"
                },
                end = new
                {
                    dateTime = calendarEvent.EndDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    timeZone = "UTC"
                }
            };

            var json = JsonSerializer.Serialize(googleEvent);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(
                "https://www.googleapis.com/calendar/v3/calendars/primary/events",
                content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Google Calendar API error: {response.StatusCode} - {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);
            
            return responseData.GetProperty("id").GetString() ?? throw new Exception("No event ID returned from Google Calendar");
        }

        private async Task<string> SyncToOutlookCalendar(CalendarIntegration integration, CalendarEvent calendarEvent)
        {
            // Decrypt the access token using secure helper
            string accessToken;
            try
            {
                accessToken = DecryptToken(integration.AccessToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt access token for integration {IntegrationId}", integration.Id);
                throw new Exception("Failed to decrypt access token");
            }

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Parse location if it's JSON, otherwise use as-is
            string locationString = calendarEvent.Location ?? "";
            if (!string.IsNullOrWhiteSpace(locationString) && locationString.TrimStart().StartsWith("{"))
            {
                try
                {
                    var locationJson = JsonSerializer.Deserialize<JsonElement>(locationString);
                    // Try to extract the name field from JSON location
                    if (locationJson.TryGetProperty("name", out var nameProperty))
                    {
                        locationString = nameProperty.GetString() ?? locationString;
                    }
                }
                catch
                {
                    // If parsing fails, just use the original string
                }
            }

            var outlookEvent = new
            {
                subject = calendarEvent.Title,
                body = new
                {
                    contentType = "HTML",
                    content = calendarEvent.Description ?? ""
                },
                start = new
                {
                    dateTime = calendarEvent.StartDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    timeZone = "UTC"
                },
                end = new
                {
                    dateTime = calendarEvent.EndDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    timeZone = "UTC"
                },
                location = new
                {
                    displayName = locationString
                }
            };

            var json = JsonSerializer.Serialize(outlookEvent);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(
                "https://graph.microsoft.com/v1.0/me/events",
                content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Outlook Calendar API error: {response.StatusCode} - {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);
            
            return responseData.GetProperty("id").GetString() ?? throw new Exception("No event ID returned from Outlook Calendar");
        }

        private int GetCurrentUserId()
        {
            // First try CustomUserId from HttpContext.Items (set by middleware)
            if (HttpContext.Items.TryGetValue("CustomUserId", out var customUserIdObj) && customUserIdObj is int customUserId)
            {
                return customUserId;
            }

            // Fallback to ClaimTypes.NameIdentifier
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }

            throw new UnauthorizedAccessException("Invalid user ID format");
        }

        #region Security Helpers

        /// <summary>
        /// Decrypts an OAuth token from storage
        /// </summary>
        private string DecryptToken(string encrypted)
        {
            var protector = _dataProtectionProvider.CreateProtector("CalendarOAuthTokens");
            return protector.Unprotect(encrypted);
        }

        #endregion

        public class SyncEventsRequest
        {
            public int OrgId { get; set; }
        }
    }
}

