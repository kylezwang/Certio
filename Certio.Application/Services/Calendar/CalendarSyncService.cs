using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Certio.Application.Interfaces;
using Certio.Domain.Calendar;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Certio.Application.Services.Calendar
{
    public class CalendarSyncService : ICalendarSyncService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CalendarSyncService> _logger;

        public CalendarSyncService(
            ApplicationDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<CalendarSyncService> logger)
        {
            _dbContext = dbContext;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SyncGoogleCalendarAsync(int orgId, int userId, CancellationToken cancellationToken)
        {
            try
            {
                var integration = await _dbContext.CalendarIntegrations
                    .FirstOrDefaultAsync(ci => ci.OrgId == orgId && ci.UserId == userId && ci.Provider == "Google", cancellationToken);

                if (integration == null)
                {
                    _logger.LogWarning("No Google Calendar integration found for Org {OrgId} User {UserId}", orgId, userId);
                    return;
                }

                // Refresh token if needed
                await RefreshTokenIfNeededAsync(integration.Id, cancellationToken);

                // Reload integration after potential token refresh
                await _dbContext.Entry(integration).ReloadAsync(cancellationToken);

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {integration.AccessToken}");

                // Get events from the next 30 days
                var timeMin = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
                var timeMax = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

                var url = $"https://www.googleapis.com/calendar/v3/calendars/primary/events?timeMin={timeMin}&timeMax={timeMax}&singleEvents=true&orderBy=startTime";
                var response = await httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to sync Google Calendar: {StatusCode} - {Error}", response.StatusCode, errorBody);
                    
                    integration.Status = "Error";
                    integration.LastErrorMessage = $"API Error: {response.StatusCode}";
                    integration.LastErrorAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    return;
                }

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var calendarData = JsonSerializer.Deserialize<GoogleCalendarResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (calendarData?.Items != null)
                {
                    foreach (var item in calendarData.Items)
                    {
                        await UpsertCalendarEventFromGoogleAsync(orgId, userId, item, cancellationToken);
                    }
                }

                integration.LastSyncedAt = DateTime.UtcNow;
                integration.Status = "Active";
                integration.LastErrorMessage = null;
                integration.LastErrorAt = null;
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Successfully synced {Count} events from Google Calendar for Org {OrgId} User {UserId}", 
                    calendarData?.Items?.Count ?? 0, orgId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing Google Calendar for Org {OrgId} User {UserId}", orgId, userId);
                throw;
            }
        }

        public async Task SyncOutlookCalendarAsync(int orgId, int userId, CancellationToken cancellationToken)
        {
            try
            {
                var integration = await _dbContext.CalendarIntegrations
                    .FirstOrDefaultAsync(ci => ci.OrgId == orgId && ci.UserId == userId && ci.Provider == "Outlook", cancellationToken);

                if (integration == null)
                {
                    _logger.LogWarning("No Outlook Calendar integration found for Org {OrgId} User {UserId}", orgId, userId);
                    return;
                }

                // Refresh token if needed
                await RefreshTokenIfNeededAsync(integration.Id, cancellationToken);

                // Reload integration after potential token refresh
                await _dbContext.Entry(integration).ReloadAsync(cancellationToken);

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {integration.AccessToken}");

                // Get events from the next 30 days
                var startDateTime = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
                var endDateTime = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

                var url = $"https://graph.microsoft.com/v1.0/me/calendarview?startDateTime={startDateTime}&endDateTime={endDateTime}&$orderby=start/dateTime";
                var response = await httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to sync Outlook Calendar: {StatusCode} - {Error}", response.StatusCode, errorBody);
                    
                    integration.Status = "Error";
                    integration.LastErrorMessage = $"API Error: {response.StatusCode}";
                    integration.LastErrorAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    return;
                }

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var calendarData = JsonSerializer.Deserialize<OutlookCalendarResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (calendarData?.Value != null)
                {
                    foreach (var item in calendarData.Value)
                    {
                        await UpsertCalendarEventFromOutlookAsync(orgId, userId, item, cancellationToken);
                    }
                }

                integration.LastSyncedAt = DateTime.UtcNow;
                integration.Status = "Active";
                integration.LastErrorMessage = null;
                integration.LastErrorAt = null;
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Successfully synced {Count} events from Outlook Calendar for Org {OrgId} User {UserId}", 
                    calendarData?.Value?.Count ?? 0, orgId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing Outlook Calendar for Org {OrgId} User {UserId}", orgId, userId);
                throw;
            }
        }

        public async Task RefreshTokenIfNeededAsync(int integrationId, CancellationToken cancellationToken)
        {
            var integration = await _dbContext.CalendarIntegrations.FindAsync(new object[] { integrationId }, cancellationToken);
            
            if (integration == null)
            {
                return;
            }

            // Check if token is expired or will expire in the next 5 minutes
            if (integration.TokenExpiresAt.HasValue && integration.TokenExpiresAt.Value <= DateTime.UtcNow.AddMinutes(5))
            {
                if (string.IsNullOrEmpty(integration.RefreshToken))
                {
                    _logger.LogWarning("Token expired but no refresh token available for integration {IntegrationId}", integrationId);
                    integration.Status = "Error";
                    integration.LastErrorMessage = "Token expired and no refresh token available";
                    integration.LastErrorAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    return;
                }

                try
                {
                    if (integration.Provider == "Google")
                    {
                        await RefreshGoogleTokenAsync(integration, cancellationToken);
                    }
                    else if (integration.Provider == "Outlook")
                    {
                        await RefreshOutlookTokenAsync(integration, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refresh token for integration {IntegrationId}", integrationId);
                    integration.Status = "Error";
                    integration.LastErrorMessage = "Failed to refresh token";
                    integration.LastErrorAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }
        }

        private async Task RefreshGoogleTokenAsync(CalendarIntegration integration, CancellationToken cancellationToken)
        {
            var clientId = _configuration["CalendarIntegration:GoogleCalendar:ClientId"];
            var clientSecret = _configuration["CalendarIntegration:GoogleCalendar:ClientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                throw new InvalidOperationException("Google Calendar credentials not configured");
            }

            using var httpClient = _httpClientFactory.CreateClient();
            var parameters = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["refresh_token"] = integration.RefreshToken!,
                ["grant_type"] = "refresh_token"
            };

            var content = new FormUrlEncodedContent(parameters);
            var response = await httpClient.PostAsync("https://oauth2.googleapis.com/token", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to refresh Google token: {StatusCode} - {Error}", response.StatusCode, errorBody);
                throw new InvalidOperationException($"Token refresh failed: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenResponse = JsonSerializer.Deserialize<TokenRefreshResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (tokenResponse != null)
            {
                integration.AccessToken = tokenResponse.Access_Token;
                if (!string.IsNullOrEmpty(tokenResponse.Refresh_Token))
                {
                    integration.RefreshToken = tokenResponse.Refresh_Token;
                }
                if (tokenResponse.Expires_In.HasValue)
                {
                    integration.TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.Expires_In.Value);
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Successfully refreshed Google token for integration {IntegrationId}", integration.Id);
            }
        }

        private async Task RefreshOutlookTokenAsync(CalendarIntegration integration, CancellationToken cancellationToken)
        {
            var clientId = _configuration["CalendarIntegration:OutlookCalendar:ClientId"];
            var clientSecret = _configuration["CalendarIntegration:OutlookCalendar:ClientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                throw new InvalidOperationException("Outlook Calendar credentials not configured");
            }

            using var httpClient = _httpClientFactory.CreateClient();
            var parameters = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["refresh_token"] = integration.RefreshToken!,
                ["grant_type"] = "refresh_token"
            };

            var content = new FormUrlEncodedContent(parameters);
            var response = await httpClient.PostAsync("https://login.microsoftonline.com/common/oauth2/v2.0/token", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to refresh Outlook token: {StatusCode} - {Error}", response.StatusCode, errorBody);
                throw new InvalidOperationException($"Token refresh failed: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenResponse = JsonSerializer.Deserialize<TokenRefreshResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (tokenResponse != null)
            {
                integration.AccessToken = tokenResponse.Access_Token;
                if (!string.IsNullOrEmpty(tokenResponse.Refresh_Token))
                {
                    integration.RefreshToken = tokenResponse.Refresh_Token;
                }
                if (tokenResponse.Expires_In.HasValue)
                {
                    integration.TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.Expires_In.Value);
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Successfully refreshed Outlook token for integration {IntegrationId}", integration.Id);
            }
        }

        private async Task UpsertCalendarEventFromGoogleAsync(int orgId, int userId, GoogleCalendarEvent googleEvent, CancellationToken cancellationToken)
        {
            var externalId = $"google:{googleEvent.Id}";
            
            var existingEvent = await _dbContext.CalendarEvents
                .FirstOrDefaultAsync(ce => ce.ExternalCalendarId == externalId && ce.OrgId == orgId, cancellationToken);

            var startDateTime = ParseGoogleDateTime(googleEvent.Start);
            var endDateTime = ParseGoogleDateTime(googleEvent.End);
            var isAllDay = !string.IsNullOrEmpty(googleEvent.Start?.Date);

            if (existingEvent == null)
            {
                var newEvent = new CalendarEvent
                {
                    OrgId = orgId,
                    Title = googleEvent.Summary ?? "Untitled Event",
                    Description = googleEvent.Description,
                    Location = googleEvent.Location,
                    StartDateTime = startDateTime,
                    EndDateTime = endDateTime,
                    IsAllDayEvent = isAllDay,
                    EventType = "Meeting",
                    Color = "blue",
                    ExternalCalendarId = externalId,
                    ExternalCalendarSource = "Google",
                    SyncStatus = "Synced",
                    LastSyncedAt = DateTime.UtcNow,
                    CreatedById = userId,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.CalendarEvents.Add(newEvent);
            }
            else
            {
                existingEvent.Title = googleEvent.Summary ?? "Untitled Event";
                existingEvent.Description = googleEvent.Description;
                existingEvent.Location = googleEvent.Location;
                existingEvent.StartDateTime = startDateTime;
                existingEvent.EndDateTime = endDateTime;
                existingEvent.IsAllDayEvent = isAllDay;
                existingEvent.SyncStatus = "Synced";
                existingEvent.LastSyncedAt = DateTime.UtcNow;
                existingEvent.ModifiedById = userId;
                existingEvent.ModifiedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task UpsertCalendarEventFromOutlookAsync(int orgId, int userId, OutlookCalendarEvent outlookEvent, CancellationToken cancellationToken)
        {
            var externalId = $"outlook:{outlookEvent.Id}";
            
            var existingEvent = await _dbContext.CalendarEvents
                .FirstOrDefaultAsync(ce => ce.ExternalCalendarId == externalId && ce.OrgId == orgId, cancellationToken);

            var startDateTime = ParseOutlookDateTime(outlookEvent.Start);
            var endDateTime = ParseOutlookDateTime(outlookEvent.End);
            var isAllDay = outlookEvent.IsAllDay ?? false;

            if (existingEvent == null)
            {
                var newEvent = new CalendarEvent
                {
                    OrgId = orgId,
                    Title = outlookEvent.Subject ?? "Untitled Event",
                    Description = outlookEvent.BodyPreview,
                    Location = outlookEvent.Location?.DisplayName,
                    StartDateTime = startDateTime,
                    EndDateTime = endDateTime,
                    IsAllDayEvent = isAllDay,
                    EventType = "Meeting",
                    Color = "blue",
                    ExternalCalendarId = externalId,
                    ExternalCalendarSource = "Outlook",
                    SyncStatus = "Synced",
                    LastSyncedAt = DateTime.UtcNow,
                    CreatedById = userId,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.CalendarEvents.Add(newEvent);
            }
            else
            {
                existingEvent.Title = outlookEvent.Subject ?? "Untitled Event";
                existingEvent.Description = outlookEvent.BodyPreview;
                existingEvent.Location = outlookEvent.Location?.DisplayName;
                existingEvent.StartDateTime = startDateTime;
                existingEvent.EndDateTime = endDateTime;
                existingEvent.IsAllDayEvent = isAllDay;
                existingEvent.SyncStatus = "Synced";
                existingEvent.LastSyncedAt = DateTime.UtcNow;
                existingEvent.ModifiedById = userId;
                existingEvent.ModifiedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private DateTime ParseGoogleDateTime(GoogleEventDateTime? dateTime)
        {
            if (dateTime == null)
            {
                return DateTime.UtcNow;
            }

            if (!string.IsNullOrEmpty(dateTime.DateTime))
            {
                return DateTime.Parse(dateTime.DateTime).ToUniversalTime();
            }

            if (!string.IsNullOrEmpty(dateTime.Date))
            {
                return DateTime.Parse(dateTime.Date).ToUniversalTime();
            }

            return DateTime.UtcNow;
        }

        private DateTime ParseOutlookDateTime(OutlookEventDateTime? dateTime)
        {
            if (dateTime == null || string.IsNullOrEmpty(dateTime.DateTime))
            {
                return DateTime.UtcNow;
            }

            return DateTime.Parse(dateTime.DateTime).ToUniversalTime();
        }

        #region DTOs

        private class GoogleCalendarResponse
        {
            public List<GoogleCalendarEvent>? Items { get; set; }
        }

        private class GoogleCalendarEvent
        {
            public string Id { get; set; } = "";
            public string? Summary { get; set; }
            public string? Description { get; set; }
            public string? Location { get; set; }
            public GoogleEventDateTime? Start { get; set; }
            public GoogleEventDateTime? End { get; set; }
        }

        private class GoogleEventDateTime
        {
            public string? DateTime { get; set; }
            public string? Date { get; set; }
            public string? TimeZone { get; set; }
        }

        private class OutlookCalendarResponse
        {
            public List<OutlookCalendarEvent>? Value { get; set; }
        }

        private class OutlookCalendarEvent
        {
            public string Id { get; set; } = "";
            public string? Subject { get; set; }
            public string? BodyPreview { get; set; }
            public OutlookLocation? Location { get; set; }
            public OutlookEventDateTime? Start { get; set; }
            public OutlookEventDateTime? End { get; set; }
            public bool? IsAllDay { get; set; }
        }

        private class OutlookLocation
        {
            public string? DisplayName { get; set; }
        }

        private class OutlookEventDateTime
        {
            public string? DateTime { get; set; }
            public string? TimeZone { get; set; }
        }

        private class TokenRefreshResponse
        {
            public string Access_Token { get; set; } = "";
            public string? Refresh_Token { get; set; }
            public int? Expires_In { get; set; }
        }

        #endregion
    }
}

