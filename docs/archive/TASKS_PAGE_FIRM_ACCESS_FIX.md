# Tasks Page Firm-Based Access Fix

## Issues Fixed

### 1. Matter Dropdown Not Showing Client Organization Matters
**Problem:** Law firm users couldn't see matters from client organizations in the task page matter switcher dropdowns.

**Root Cause:** `ListMattersAsync` in `MatterService` was filtering matters to only show those where the user had specific permissions or assignments:
```csharp
query = query.Where(m => 
    m.AccessLevel == "Everyone" || 
    m.Permissions.Any(p => p.UserId == userId && p.RevokedAt == null) ||
    m.Assignments.Any(a => a.UserId == userId && a.RemovedAt == null));
```

This is correct for direct members (they should only see matters they're assigned to or have explicit permission for), but too restrictive for law firm users who should see ALL matters in client organizations based on their firm relationship AccessLevel.

**Fix:** Check if user has firm-based access, and if so, skip the matter-level filtering:
```csharp
var hasFirmAccess = await _permissionService.HasFirmBasedAccessAsync(userId, organizationId);

if (!hasFirmAccess)
{
    // Direct members: filter by matter-level access
    query = query.Where(m => 
        m.AccessLevel == "Everyone" || 
        m.Permissions.Any(p => p.UserId == userId && p.RevokedAt == null) ||
        m.Assignments.Any(a => a.UserId == userId && a.RemovedAt == null));
}
// Firm-based users: can see all matters in the client organization
```

### 2. User Assignment Dropdowns Not Showing Firm Users
**Problem:** When viewing task details in a client organization, the assignee dropdowns didn't show law firm users.

**Root Cause:** `TasksController.Index` was only querying direct UserOrganizations:
```csharp
var users = await _context.UserOrganizations
    .Where(uo => uo.OrganizationId == organizationId && uo.IsActive)
    .Select(uo => new UserOption {...})
    .ToListAsync();
```

**Fix:** Load BOTH direct users AND firm-based users, then deduplicate:
```csharp
// Get direct users
var directUsers = await _context.UserOrganizations
    .Where(uo => uo.OrganizationId == organizationId && uo.IsActive)
    .Select(uo => new UserOption {...})
    .ToListAsync();

// Get firm users via OrganizationRelationships
var firmUsers = await _context.UserOrganizations
    .Where(uo => uo.IsActive && uo.UserType == UserTypes.LawFirm)
    .Include(uo => uo.User)
    .Include(uo => uo.Organization)
        .ThenInclude(o => o.OrganizationRelationships)
    .Where(uo => uo.Organization.OrganizationRelationships.Any(rel =>
        rel.TargetOrganizationId == organizationId &&
        rel.IsActive &&
        !rel.IsDeleted &&
        rel.RelationshipType == RelationshipTypes.LawFirmClient &&
        (!rel.ExpiresAt.HasValue || rel.ExpiresAt.Value > DateTime.UtcNow)))
    .Select(uo => new UserOption {...})
    .ToListAsync();

// Combine and deduplicate
var users = directUsers
    .Union(firmUsers, new UserOptionComparer())
    .OrderBy(u => u.Name)
    .ToList();
```

## Files Modified

1. **`Certio.Application/Services/MatterService.cs`**
   - Updated `ListMattersAsync` to conditionally apply matter-level filtering based on membership type
   - Firm users can see all matters, direct members see only assigned/permitted matters

2. **`Certio.Web/Controllers/TasksController.cs`**
   - Updated user loading logic to include firm-based users
   - Added `UserOptionComparer` helper class for deduplication

## Testing

After restart, verify:

1. **Matters Dropdown:**
   - Login as law firm user
   - Navigate to client organization tasks page
   - Check matter dropdown shows all client matters (not just assigned ones)

2. **User Dropdowns:**
   - Create/edit a task in client organization
   - Check assignee dropdowns include law firm users
   - Verify subtask assignment dropdowns also show firm users

## Related Fixes

This completes the firm-based access pattern across:
- ✅ Permission checks (`CanAccessMatterAsync`, `CanAccessTaskAsync`)
- ✅ Matter creation
- ✅ Matter assignments  
- ✅ Matter listing (with proper filtering)
- ✅ User dropdowns across all pages
- ✅ Task page matter dropdowns
- ✅ Task page user dropdowns

## Pattern for Future Development

When querying users for any organization:
```csharp
// 1. Get direct members
var directUsers = _context.UserOrganizations
    .Where(uo => uo.OrganizationId == orgId && uo.IsActive)
    .Select(...);

// 2. Get firm-based members
var firmUsers = _context.UserOrganizations
    .Where(uo => uo.IsActive && uo.UserType == UserTypes.LawFirm)
    .Include(uo => uo.Organization.OrganizationRelationships)
    .Where(uo => uo.Organization.OrganizationRelationships.Any(rel =>
        rel.TargetOrganizationId == orgId &&
        rel.IsActive && !rel.IsDeleted &&
        rel.RelationshipType == RelationshipTypes.LawFirmClient))
    .Select(...);

// 3. Combine
var allUsers = directUsers.Union(firmUsers).ToList();
```

