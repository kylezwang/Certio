using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Certio.Application.Interfaces;

namespace Certio.Web.Security
{
    /// <summary>
    /// Authorization attribute that validates user has access to a task
    /// Usage: [RequireTaskAccess("taskId")]
    /// </summary>
    public class RequireTaskAccessAttribute : TypeFilterAttribute
    {
        public RequireTaskAccessAttribute(string taskIdParameter = "taskId") 
            : base(typeof(RequireTaskAccessFilter))
        {
            Arguments = new object[] { taskIdParameter };
        }
    }

    /// <summary>
    /// Filter implementation for RequireTaskAccess attribute
    /// </summary>
    public class RequireTaskAccessFilter : IAsyncAuthorizationFilter
    {
        private readonly string _taskIdParameter;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<RequireTaskAccessFilter> _logger;

        public RequireTaskAccessFilter(
            string taskIdParameter,
            IPermissionService permissionService,
            ILogger<RequireTaskAccessFilter> logger)
        {
            _taskIdParameter = taskIdParameter;
            _permissionService = permissionService;
            _logger = logger;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // Check if user is authenticated
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Get user ID
            var userIdClaim = context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Get task ID from route data, query string, or form data
            int? taskId = null;

            // Try route data first
            if (context.RouteData.Values.TryGetValue(_taskIdParameter, out var routeTaskId))
            {
                if (routeTaskId is int taskIdInt)
                {
                    taskId = taskIdInt;
                }
                else if (int.TryParse(routeTaskId?.ToString(), out int parsedTaskId))
                {
                    taskId = parsedTaskId;
                }
            }

            // Try query string
            if (!taskId.HasValue && context.HttpContext.Request.Query.TryGetValue(_taskIdParameter, out var queryTaskId))
            {
                if (int.TryParse(queryTaskId.FirstOrDefault(), out int parsedQueryTaskId))
                {
                    taskId = parsedQueryTaskId;
                }
            }

            // Try form data (for POST requests)
            if (!taskId.HasValue && context.HttpContext.Request.HasFormContentType)
            {
                if (context.HttpContext.Request.Form.TryGetValue(_taskIdParameter, out var formTaskId))
                {
                    if (int.TryParse(formTaskId.FirstOrDefault(), out int parsedFormTaskId))
                    {
                        taskId = parsedFormTaskId;
                    }
                }
            }

            if (!taskId.HasValue)
            {
                _logger.LogWarning("Task ID parameter '{Parameter}' not found in request. User: {UserId}",
                    _taskIdParameter, userId);
                context.Result = new BadRequestObjectResult(new { error = $"Task ID parameter '{_taskIdParameter}' is required" });
                return;
            }

            // Check task access
            var hasAccess = await _permissionService.CanAccessTaskAsync(userId, taskId.Value);
            
            if (!hasAccess)
            {
                _logger.LogWarning("Task access denied. User: {UserId}, Task: {TaskId}",
                    userId, taskId.Value);
                context.Result = new ForbidResult();
                return;
            }

            _logger.LogDebug("Task access granted. User: {UserId}, Task: {TaskId}",
                userId, taskId.Value);
        }
    }
}

