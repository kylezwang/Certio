using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Certio.Infrastructure.Data;
using Certio.Domain.Audit;
using Certio.Domain.Users;

namespace Certio.Web.Attributes
{
    /// <summary>
    /// Attribute to automatically log view/access events for resources
    /// Usage: [ViewAudit("Matter")] or [ViewAudit("Document", includeDetails: true)]
    /// </summary>
    public class ViewAuditAttribute : ActionFilterAttribute
    {
        private readonly string _entityType;
        private readonly bool _includeDetails;
        private readonly string _entityIdParameter;

        public ViewAuditAttribute(string entityType, string entityIdParameter = "id", bool includeDetails = false)
        {
            _entityType = entityType;
            _entityIdParameter = entityIdParameter;
            _includeDetails = includeDetails;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Execute the action first
            var executedContext = await next();

            // Only log if the action was successful
            if (executedContext.Result is Microsoft.AspNetCore.Mvc.ViewResult ||
                executedContext.Result is Microsoft.AspNetCore.Mvc.JsonResult ||
                executedContext.Result is Microsoft.AspNetCore.Mvc.PartialViewResult)
            {
                try
                {
                    // Get services from DI
                    var dbContext = context.HttpContext.RequestServices.GetService<ApplicationDbContext>();
                    var httpContextAccessor = context.HttpContext.RequestServices.GetService<IHttpContextAccessor>();

                    if (dbContext == null || httpContextAccessor == null)
                        return;

                    // Get current user
                    var user = httpContextAccessor.HttpContext?.Items["CustomUser"] as User;
                    if (user == null)
                        return;

                    // Get entity ID from route parameters or action arguments (supports int or Guid)
                    int entityId = 0;
                    if (context.ActionArguments.ContainsKey(_entityIdParameter))
                    {
                        var idValue = context.ActionArguments[_entityIdParameter];
                        if (idValue is int intId)
                        {
                            entityId = intId;
                        }
                        else if (idValue is Guid guidId)
                        {
                            unchecked { entityId = guidId.GetHashCode(); }
                        }
                        else if (int.TryParse(idValue?.ToString(), out var parsedId))
                        {
                            entityId = parsedId;
                        }
                        else if (Guid.TryParse(idValue?.ToString(), out var parsedGuid))
                        {
                            unchecked { entityId = parsedGuid.GetHashCode(); }
                        }
                    }

                    // If no entity ID found, skip logging
                    if (entityId == 0)
                        return;

                    // Get organization ID and matter ID from context or entity
                    int? organizationId = null;
                    int? matterId = null;

                    // Try to get orgId from route or parameters
                    if (context.ActionArguments.ContainsKey("orgId"))
                    {
                        var orgIdValue = context.ActionArguments["orgId"];
                        if (orgIdValue is int orgInt)
                            organizationId = orgInt;
                        else if (int.TryParse(orgIdValue?.ToString(), out var parsedOrgId))
                            organizationId = parsedOrgId;
                    }

                    // Try to get matterId from route or parameters
                    if (context.ActionArguments.ContainsKey("matterId"))
                    {
                        var matterIdValue = context.ActionArguments["matterId"];
                        if (matterIdValue is int matterInt)
                            matterId = matterInt;
                        else if (int.TryParse(matterIdValue?.ToString(), out var parsedMatterId))
                            matterId = parsedMatterId;
                    }

                    // For Matter views, get the actual organization ID from the Matter entity
                    if (_entityType == "Matter" && entityId > 0 && !organizationId.HasValue)
                    {
                        var matter = await dbContext.Set<Certio.Domain.Matters.Matter>()
                            .Where(m => m.Id == entityId)
                            .Select(m => new { m.OrganizationId })
                            .FirstOrDefaultAsync();
                        
                        if (matter != null)
                        {
                            organizationId = matter.OrganizationId;
                        }
                    }

                    // For CalendarEvent views, get the organization ID from the event
                    if (_entityType == "CalendarEvent" && entityId > 0 && !organizationId.HasValue)
                    {
                        var calendarEvent = await dbContext.Set<Certio.Domain.Calendar.CalendarEvent>()
                            .Where(e => e.Id == entityId)
                            .Select(e => new { e.OrgId, e.MatterId })
                            .FirstOrDefaultAsync();
                        
                        if (calendarEvent != null)
                        {
                            organizationId = calendarEvent.OrgId;
                            matterId = calendarEvent.MatterId;
                        }
                    }

                    // Create audit log entry
                    var auditLog = new AuditLog
                    {
                        EntityType = _entityType,
                        EntityId = entityId,
                        Action = AuditActions.View,
                        Result = AuditResults.Success,
                        UserId = user.Id,
                        UserName = $"{user.FirstName} {user.LastName}",
                        IPAddress = httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                        UserAgent = httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString(),
                        Description = $"Viewed {_entityType} with ID {entityId}",
                        Timestamp = DateTime.UtcNow,
                        SessionId = httpContextAccessor.HttpContext?.Session?.Id,
                        RequestUrl = httpContextAccessor.HttpContext?.Request?.Path.ToString(),
                        HttpMethod = httpContextAccessor.HttpContext?.Request?.Method,
                        OrganizationId = organizationId,
                        MatterId = matterId
                    };

                    // Add details if requested
                    if (_includeDetails && executedContext.Result is Microsoft.AspNetCore.Mvc.JsonResult jsonResult)
                    {
                        auditLog.NewValues = System.Text.Json.JsonSerializer.Serialize(jsonResult.Value);
                    }

                    dbContext.AuditLogs.Add(auditLog);
                    await dbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // Log error but don't fail the request
                    var logger = context.HttpContext.RequestServices.GetService<ILogger<ViewAuditAttribute>>();
                    logger?.LogError(ex, "Error logging view audit for {EntityType} {EntityId}", _entityType, _entityIdParameter);
                }
            }
        }
    }
}

