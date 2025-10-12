# Phase 2: Controller Refactoring Guide

## Overview
This guide demonstrates how to refactor controllers to use the new service layer. The goal is to make controllers thin (< 50 lines per action) and delegate all business logic to services.

## Refactoring Pattern

### Before (Fat Controller)
```csharp
public async Task<IActionResult> Create([FromForm] CreateMatterViewModel model)
{
    // Get user from context
    var customUser = HttpContext.Items["CustomUser"] as User;
    if (customUser == null) return RedirectToAction("Index", "Home");
    
    // Validate organization membership
    var primaryOrg = customUser.GetPrimaryOrganization();
    if (primaryOrg == null) return RedirectToAction("Index", "Home");
    
    // Check permissions
    if (!customUser.HasPermission(primaryOrg.OrganizationId, Permission.CreateMatters))
    {
        return Forbid();
    }
    
    // Validate model
    if (!ModelState.IsValid) return View(model);
    
    // Create entity
    var matter = new Matter { ... };
    _context.Matters.Add(matter);
    await _context.SaveChangesAsync();
    
    // Handle permissions
    if (model.AccessLevel == "Specific")
    {
        foreach (var userId in model.PermissionUserIds)
        {
            var permission = new MatterPermission { ... };
            _context.MatterPermissions.Add(permission);
        }
        await _context.SaveChangesAsync();
    }
    
    // Audit log
    await _auditService.LogCreateAsync(...);
    
    return RedirectToAction("Index");
}
```

### After (Thin Controller)
```csharp
public async Task<IActionResult> Create([FromForm] CreateMatterViewModel model)
{
    // 1. Get user context (still in controller - it's HTTP concern)
    var customUser = HttpContext.Items["CustomUser"] as User;
    if (customUser == null) return RedirectToAction("Index", "Home");
    
    var orgId = customUser.GetPrimaryOrganization()?.OrganizationId ?? 0;
    if (orgId == 0) return RedirectToAction("Index", "Home");
    
    // 2. Validate model (still in controller - it's HTTP concern)
    if (!ModelState.IsValid) return View(model);
    
    // 3. Map ViewModel to DTO
    var createDto = new CreateMatterDto
    {
        Title = model.Title,
        Description = model.Description,
        // ... map other properties
    };
    
    // 4. Call service (ALL business logic delegated)
    var result = await _matterService.CreateMatterAsync(
        customUser.Id,
        orgId,
        createDto,
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        HttpContext.Request.Headers["User-Agent"]);
    
    // 5. Handle result and return appropriate HTTP response
    if (!result.Success)
    {
        ModelState.AddModelError("", result.ErrorMessage ?? "Failed to create matter");
        return View(model);
    }
    
    TempData["Success"] = "Matter created successfully";
    return RedirectToAction("Index");
}
```

## Key Principles

### 1. Controllers Handle HTTP Concerns Only
- Routing
- Model binding
- Model validation
- Response formatting (JSON, View, Redirect)
- TempData/ViewBag
- HTTP status codes

### 2. Services Handle Business Logic
- Permission checks
- Business rule validation
- Data persistence
- Entity relationships
- Audit logging
- Domain logic

### 3. Exception Handling Pattern

**In Services:**
```csharp
// Services throw domain exceptions
if (!await _permissionService.CanAccessMatterAsync(userId, matterId))
{
    throw new UnauthorizedOperationException(userId, "update", "Matter");
}
```

**In Controllers:**
```csharp
// Controllers catch and convert to HTTP responses
// Option 1: Service returns ServiceResult (recommended for most cases)
var result = await _matterService.UpdateMatterAsync(...);
if (!result.Success)
{
    if (result.ErrorCode == "UNAUTHORIZED_OPERATION")
        return Forbid();
    if (result.ErrorCode == "RESOURCE_NOT_FOUND")
        return NotFound();
    
    return BadRequest(result.ErrorMessage);
}

// Option 2: Try-catch for operations that might fail
try
{
    var result = await _matterService.GetMatterAsync(userId, matterId);
    return View(result.Data);
}
catch (UnauthorizedOperationException)
{
    return Forbid();
}
catch (ResourceNotFoundException)
{
    return NotFound();
}
```

### 4. User Context Pattern

```csharp
// Always extract user context at the start of controller action
private (User?, int) GetUserContext()
{
    var customUser = HttpContext.Items["CustomUser"] as User;
    var orgId = customUser?.GetPrimaryOrganization()?.OrganizationId ?? 0;
    return (customUser, orgId);
}

public async Task<IActionResult> SomeAction()
{
    var (user, orgId) = GetUserContext();
    if (user == null || orgId == 0)
        return RedirectToAction("Index", "Home");
    
    // Use user.Id and orgId
}
```

## Refactored Examples

### MatterController Refactoring

```csharp
[Authorize(Policy = "OrgMember")]
public class MatterController : Controller
{
    private readonly IMatterService _matterService;
    private readonly ILogger<MatterController> _logger;

    public MatterController(
        IMatterService matterService,
        ILogger<MatterController> logger)
    {
        _matterService = matterService;
        _logger = logger;
    }

    // GET: Matter
    public async Task<IActionResult> Index()
    {
        var (user, orgId) = GetUserContext();
        if (user == null || orgId == 0) 
            return RedirectToAction("Index", "Home");

        var result = await _matterService.ListMattersAsync(user.Id, orgId);
        
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return View(new List<MatterDto>());
        }

        return View(result.Data);
    }

    // GET: Matter/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var (user, _) = GetUserContext();
        if (user == null) return RedirectToAction("Index", "Home");

        var result = await _matterService.GetMatterAsync(user.Id, id);
        
        if (!result.Success)
        {
            if (result.ErrorCode == "RESOURCE_NOT_FOUND")
                return NotFound();
            if (result.ErrorCode == "UNAUTHORIZED_OPERATION")
                return Forbid();
            
            return BadRequest(result.ErrorMessage);
        }

        return View(result.Data);
    }

    // POST: Matter/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] CreateMatterViewModel model)
    {
        var (user, orgId) = GetUserContext();
        if (user == null || orgId == 0) 
            return RedirectToAction("Index", "Home");

        if (!ModelState.IsValid) 
            return View(model);

        var createDto = MapToCreateDto(model);
        
        var result = await _matterService.CreateMatterAsync(
            user.Id, 
            orgId, 
            createDto,
            GetIpAddress(),
            GetUserAgent());

        if (!result.Success)
        {
            ModelState.AddModelError("", result.ErrorMessage ?? "Failed to create matter");
            return View(model);
        }

        TempData["Success"] = "Matter created successfully";
        return RedirectToAction("Index");
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

    private CreateMatterDto MapToCreateDto(CreateMatterViewModel model)
    {
        return new CreateMatterDto
        {
            Title = model.Title,
            Description = model.Description,
            Status = model.Status,
            PracticeArea = model.PracticeArea,
            AccessLevel = model.AccessLevel,
            // ... other properties
        };
    }
}
```

### TasksController Refactoring

```csharp
public class TasksController : Controller
{
    private readonly ITaskService _taskService;
    private readonly ILogger<TasksController> _logger;

    public TasksController(
        ITaskService taskService,
        ILogger<TasksController> logger)
    {
        _taskService = taskService;
        _logger = logger;
    }

    [Authorize(Policy = "OrgMember")]
    [HttpGet("/Client/{orgId:int}/Tasks")]
    public async Task<IActionResult> Index(int orgId)
    {
        var user = GetCurrentUser();
        if (user == null) return RedirectToAction("Index", "Home");

        var result = await _taskService.ListTasksAsync(user.Id, orgId);
        
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return View(new List<TaskDto>());
        }

        // Set ViewBag for client-side scripts
        ViewBag.OrganizationId = orgId;
        ViewBag.Tasks = result.Data;
        
        return View();
    }

    [HttpPost]
    [Authorize(Policy = "OrgMember")]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request)
    {
        var user = GetCurrentUser();
        if (user == null) return Unauthorized();

        var createDto = new CreateTaskDto
        {
            Title = request.Title,
            Description = request.Description,
            // ... other properties
        };

        var result = await _taskService.CreateTaskAsync(
            user.Id,
            request.MatterId,
            createDto,
            GetIpAddress(),
            GetUserAgent());

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Json(new { success = true, task = result.Data });
    }

    private User? GetCurrentUser() => 
        HttpContext.Items["CustomUser"] as User;
}
```

## Migration Checklist

For each controller action:

1. ✅ **Identify business logic** - What needs to move to service?
   - [ ] Permission checks
   - [ ] Database queries
   - [ ] Entity creation/updates
   - [ ] Business rule validation
   - [ ] Audit logging

2. ✅ **Create/update service method** - Does service method exist?
   - [ ] Create new service method if needed
   - [ ] Ensure proper parameter signature
   - [ ] Ensure returns ServiceResult<T>

3. ✅ **Refactor controller action**
   - [ ] Extract user context
   - [ ] Validate ModelState
   - [ ] Map ViewModel → DTO
   - [ ] Call service method
   - [ ] Handle ServiceResult
   - [ ] Return appropriate HTTP response

4. ✅ **Test**
   - [ ] Test happy path
   - [ ] Test unauthorized access
   - [ ] Test not found scenarios
   - [ ] Test validation errors

## Benefits

### Before Refactoring
- ❌ 200+ line controller actions
- ❌ Business logic mixed with HTTP concerns
- ❌ Hard to test
- ❌ Difficult to reuse logic
- ❌ Permission checks scattered
- ❌ Audit logging inconsistent

### After Refactoring
- ✅ 20-40 line controller actions
- ✅ Clear separation of concerns
- ✅ Easy to unit test services
- ✅ Business logic reusable
- ✅ Consistent permission enforcement
- ✅ Centralized audit logging
- ✅ Ready for AI agent integration

## Next Steps

1. Follow this pattern for all controller actions
2. For each action:
   - Identify the corresponding service method
   - If service method doesn't exist, create it
   - Refactor controller to call service
   - Test thoroughly
3. Remove direct DbContext access from controllers
4. Ensure all controllers are thin (< 50 lines per action)

## Notes on AI Agent Integration

With this architecture:
- AI agents can call service methods directly
- No need to go through HTTP layer
- Same permission checks apply
- Same audit logging happens
- Same business rules enforced

Example AI agent code:
```csharp
// AI agent can now use services directly
var result = await matterService.CreateMatterAsync(
    aiUserId,
    organizationId,
    extractedMatterDto,
    "AI Agent",
    "CertioAI/1.0");

if (result.Success)
{
    await SendNotificationToUser(result.Data);
}
```

