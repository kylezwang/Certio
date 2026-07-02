# Matter Details Page - Routing Fix

## Issue
Getting HTTP 401 error when trying to navigate to Matter Details page at `/Client/1/Matter/Details/2`.

## Root Cause
The `MatterController.Details` action did not have an explicit route attribute configured. Without the `[HttpGet]` attribute, ASP.NET Core was using default routing which didn't match the `/Client/{orgId}/Matter/Details/{matterId}` pattern.

## Solution Applied

### 1. Added Explicit Route Attribute
**File**: `Certio.Web/Controllers/MatterController.cs`

**Before:**
```csharp
// GET: Matter/Details/5
[RequireMatterAccess("id")]
public async Task<IActionResult> Details(int? id)
{
    var (user, orgId) = GetUserContext();
    // ...
}
```

**After:**
```csharp
// GET: /Client/{orgId}/Matter/Details/{id}
[HttpGet("/Client/{orgId:int}/Matter/Details/{id:int}")]
[RequireMatterAccess("id")]
public async Task<IActionResult> Details(int orgId, int? id)
{
    var user = HttpContext.Items["CustomUser"] as User;
    // ...
}
```

### 2. Fixed Parameter Handling
- Added `orgId` parameter to method signature (from route)
- Changed from `GetUserContext()` to direct `HttpContext.Items["CustomUser"]` access
- Updated `ViewBag.OrganizationId` to use route parameter instead of DTO value

## Routing Pattern Consistency
This matches the existing routing pattern used throughout the application:

| Controller | Route Pattern | Example |
|------------|---------------|---------|
| ClientController.Matter | `/Client/{orgId}/Matter` | `/Client/1/Matter` |
| ClientController.Dashboard | `/Client/{orgId}/Dashboard` | `/Client/1/Dashboard` |
| ClientController.Communications | `/Client/{orgId}/Communications` | `/Client/1/Communications` |
| TasksController.Index | `/Client/{orgId}/Tasks` | `/Client/1/Tasks` |
| **MatterController.Details** | **`/Client/{orgId}/Matter/Details/{id}`** | **`/Client/1/Matter/Details/2`** |

## Testing
After this fix:
1. Navigate to Matter Index: `/Client/{orgId}/Matter`
2. Click on any matter card
3. Should successfully navigate to: `/Client/{orgId}/Matter/Details/{matterId}`
4. Details page should load with all statistics and tabs

## Additional Notes
- The `[RequireMatterAccess("id")]` attribute remains in place for permission checking
- The `[Authorize(Policy = "OrgMember")]` attribute on the controller ensures user is authenticated
- All existing permission checks continue to work as expected
- The route parameter validation uses `:int` constraints to ensure valid IDs

## Files Modified
- `Certio.Web/Controllers/MatterController.cs` (lines 144-160, 271)

## Status
✅ **FIXED** - Routing should now work correctly for Matter Details page.

