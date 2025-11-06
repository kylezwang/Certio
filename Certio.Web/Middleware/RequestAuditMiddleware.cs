using System.Diagnostics;
using Certio.Domain.Audit;
using Certio.Domain.Users;
using Certio.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Certio.Web.Middleware
{
    public class RequestAuditMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestAuditMiddleware> _logger;

        public RequestAuditMiddleware(RequestDelegate next, ILogger<RequestAuditMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            // Skip static and noisy endpoints
            var path = context.Request.Path.ToString();
            if (ShouldSkipPath(path) || string.Equals(context.Request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase) || string.Equals(context.Request.Method, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            Exception? requestException = null;

            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                requestException = ex;
                throw;
            }
            finally
            {
                stopwatch.Stop();
            }

            try
            {
                using var scope = context.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
                if (dbContext == null)
                {
                    return;
                }

                var user = context.Items["CustomUser"] as User;

                // Try to enrich with route context
                int? organizationId = TryParseRouteInt(context, "orgId");
                int? matterId = TryParseRouteInt(context, "matterId");

                string? sessionId = null;
                var sessionFeature = context.Features.Get<ISessionFeature>();
                if (sessionFeature?.Session != null && sessionFeature.Session.IsAvailable)
                {
                    sessionId = sessionFeature.Session.Id;
                }

                var requestUrl = context.Request.QueryString.HasValue
                    ? string.Concat(path, context.Request.QueryString.ToString())
                    : path;

                var auditAction = GetActionForMethod(context.Request.Method);
                var description = GetFriendlyDescription(context, path);

                var auditLog = new AuditLog
                {
                    EntityType = "Http",
                    EntityId = 0,
                    Action = auditAction,
                    Result = context.Response.StatusCode >= 400 || requestException != null ? AuditResults.Failure : AuditResults.Success,
                    UserId = user?.Id,
                    UserName = user != null ? ($"{user.FirstName} {user.LastName}") : null,
                    IPAddress = context.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = context.Request.Headers["User-Agent"].ToString(),
                    Description = description,
                    Timestamp = DateTime.UtcNow,
                    SessionId = sessionId,
                    RequestUrl = requestUrl,
                    HttpMethod = context.Request.Method,
                    OrganizationId = organizationId,
                    MatterId = matterId,
                    ResponseCode = context.Response.StatusCode,
                    DurationMs = stopwatch.ElapsedMilliseconds
                };

                dbContext.AuditLogs.Add(auditLog);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write request audit for {Path}", path);
            }
        }

        private static int? TryParseRouteInt(HttpContext context, string key)
        {
            if (context.Request.RouteValues.TryGetValue(key, out var value) && value != null)
            {
                if (value is int i)
                {
                    return i;
                }
                if (int.TryParse(value.ToString(), out var parsed))
                {
                    return parsed;
                }
            }
            return null;
        }

        private static bool ShouldSkipPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return true;
            // Basic static/infra endpoints to skip
            return path.StartsWith("/css/") ||
                   path.StartsWith("/js/") ||
                   path.StartsWith("/images/") ||
                   path.StartsWith("/lib/") ||
                   path.StartsWith("/hubs/") ||
                   path.StartsWith("/swagger") ||
                   path.StartsWith("/favicon") ||
                   path.Equals("/healthz", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetActionForMethod(string method)
        {
            return method.ToUpperInvariant() switch
            {
                "POST" => AuditActions.Update,
                "PUT" => AuditActions.Update,
                "PATCH" => AuditActions.Update,
                "DELETE" => AuditActions.Delete,
                _ => AuditActions.View
            };
        }

        private static string GetFriendlyDescription(HttpContext context, string path)
        {
            var method = context.Request.Method.ToUpperInvariant();
            var routeValues = context.Request.RouteValues;
            var controller = routeValues["controller"]?.ToString();
            var action = routeValues["action"]?.ToString();

            string? description = controller switch
            {
                "Client" when string.Equals(action, "List", StringComparison.OrdinalIgnoreCase) => "Viewed client list",
                "Client" when string.Equals(action, "History", StringComparison.OrdinalIgnoreCase) => "Viewed client history",
                "History" when string.Equals(action, "Index", StringComparison.OrdinalIgnoreCase) => "Viewed activity history",
                "History" when string.Equals(action, "GetActivities", StringComparison.OrdinalIgnoreCase) => "Fetched activity history",
                "Documents" when string.Equals(action, "Index", StringComparison.OrdinalIgnoreCase) => "Viewed documents",
                "Documents" when string.Equals(action, "View", StringComparison.OrdinalIgnoreCase) => "Opened document",
                "DocumentsApi" when string.Equals(action, "Search", StringComparison.OrdinalIgnoreCase) => "Searched documents",
                "DocumentsApi" when string.Equals(action, "GetDocuments", StringComparison.OrdinalIgnoreCase) => "Loaded documents",
                "DocumentsApi" when string.Equals(action, "GetEmbedUrl", StringComparison.OrdinalIgnoreCase) => "Requested document embed URL",
                "DocumentsApi" when string.Equals(action, "UpdateStatus", StringComparison.OrdinalIgnoreCase) => "Updated document status",
                "DocumentsApi" when string.Equals(action, "Reindex", StringComparison.OrdinalIgnoreCase) => "Queued document reindex",
                "DriveOAuth" when string.Equals(action, "AuthorizeGoogle", StringComparison.OrdinalIgnoreCase) => "Initiated Google Drive connection",
                "DriveOAuth" when string.Equals(action, "AuthorizeOneDrive", StringComparison.OrdinalIgnoreCase) => "Initiated OneDrive connection",
                "DriveOAuth" when string.Equals(action, "GoogleCallback", StringComparison.OrdinalIgnoreCase) => "Connected Google Drive",
                "DriveOAuth" when string.Equals(action, "OneDriveCallback", StringComparison.OrdinalIgnoreCase) => "Connected OneDrive",
                "DriveOAuth" when string.Equals(action, "DisconnectGoogleDrive", StringComparison.OrdinalIgnoreCase) => "Disconnected Google Drive",
                "DriveOAuth" when string.Equals(action, "DisconnectOneDrive", StringComparison.OrdinalIgnoreCase) => "Disconnected OneDrive",
                "DriveOAuth" when string.Equals(action, "SyncDocuments", StringComparison.OrdinalIgnoreCase) => "Triggered document sync",
                _ => null
            };

            if (description != null)
            {
                return description;
            }

            // Provide a generic friendly fallback based on method
            return method switch
            {
                "POST" => $"Updated via {path}",
                "PUT" => $"Updated via {path}",
                "PATCH" => $"Updated via {path}",
                "DELETE" => $"Deleted via {path}",
                _ => $"Viewed {path}"
            };
        }
    }
}


