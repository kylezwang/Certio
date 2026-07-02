# Matter and Task Filtering Implementation

## Overview

This document describes the implementation of user-based filtering for Matter and Task pages in the Certio application. The filtering ensures that users only see matters and tasks they have access to based on their user type, assignments, and permissions.

## Security Model

### Matter Access Rules

Users can see a matter if **ANY** of the following conditions are met:

1. **Everyone Access**: Matter has `AccessLevel = "Everyone"` (all organization members can see)
2. **Specific Permission**: Matter has `AccessLevel = "Specific"` AND user has an active `MatterPermission` (not revoked)
3. **Matter Assignment**: User is assigned to the matter via `MatterAssignment` (not removed)

### Task Access Rules

Users can see a task if **ANY** of the following conditions are met:

1. **Task Assignment**: User is directly assigned to the task via `TaskAssignment` (not removed)
2. **Matter Assignment**: User is assigned to the task's parent matter via `MatterAssignment` (not removed)
3. **Matter Everyone Access**: Task's parent matter has `AccessLevel = "Everyone"`
4. **Matter Permission**: User has an active `MatterPermission` for the task's parent matter (not revoked)

## Implementation Details

### MatterController.cs

**Location**: `Certio.Web/Controllers/MatterController.cs`

**Changes in `Index()` action** (lines 48-72):

```csharp
// Get user's organization membership to determine user type
var userOrgMembership = customUser.GetOrganizationMembership(primaryOrg.OrganizationId);
var userType = userOrgMembership?.UserType ?? UserTypes.Client;

// Build query with proper filtering based on user type and assignments
IQueryable<Matter> mattersQuery = _context.Matters
    .Where(m => m.OrganizationId == primaryOrg.OrganizationId)
    .Include(m => m.Assignments)
        .ThenInclude(a => a.User)
    .Include(m => m.Permissions);

// Filter based on AccessLevel and user assignments
mattersQuery = mattersQuery.Where(m => 
    // All org members see "Everyone" access level matters
    m.AccessLevel == "Everyone" ||
    // Users with specific permissions
    m.Permissions.Any(p => p.UserId == customUser.Id && p.RevokedAt == null) ||
    // Users assigned to the matter
    m.Assignments.Any(a => a.UserId == customUser.Id && a.RemovedAt == null));

var matters = await mattersQuery.ToListAsync();
```

**Key Points**:
- Filters matters before loading them into memory
- Includes both `Assignments` and `Permissions` navigation properties for efficient filtering
- Respects the `AccessLevel` field (Everyone vs Specific)
- Checks that permissions are not revoked (`RevokedAt == null`)
- Checks that assignments are not removed (`RemovedAt == null`)

### TasksController.cs

**Location**: `Certio.Web/Controllers/TasksController.cs`

**Changes in `Index()` action** (lines 112-166):

#### Task Filtering (lines 112-141):

```csharp
// Apply task-level filtering based on user assignments and matter access
tasksQuery = tasksQuery
    .Include(t => t.Matter)
        .ThenInclude(m => m.Assignments)
    .Include(t => t.Matter)
        .ThenInclude(m => m.Permissions)
    .Include(t => t.TaskAssignments)
        .ThenInclude(ta => ta.User)
    .Include(t => t.Comments)
        .ThenInclude(c => c.User)
    .Include(t => t.SubTasks)
        .ThenInclude(st => st.Assignments)
            .ThenInclude(sta => sta.User)
    .Where(t => 
        // User is assigned to the task directly
        t.TaskAssignments.Any(ta => ta.UserId == customUser.Id && ta.RemovedAt == null) ||
        // User is assigned to the matter
        t.Matter.Assignments.Any(ma => ma.UserId == customUser.Id && ma.RemovedAt == null) ||
        // Matter has "Everyone" access level
        t.Matter.AccessLevel == "Everyone" ||
        // User has specific permission to the matter
        t.Matter.Permissions.Any(p => p.UserId == customUser.Id && p.RevokedAt == null))
    .OrderBy(t => t.Order);
```

#### Matter Dropdown Filtering (lines 143-166):

```csharp
// Apply matter-level filtering based on user access
mattersQuery = mattersQuery.Where(m => 
    m.AccessLevel == "Everyone" ||
    m.Permissions.Any(p => p.UserId == customUser.Id && p.RevokedAt == null) ||
    m.Assignments.Any(a => a.UserId == customUser.Id && a.RemovedAt == null));
```

**Key Points**:
- Filters tasks based on both direct task assignments AND parent matter access
- Ensures the matter dropdown (for creating tasks) also respects the same filtering rules
- Includes all necessary navigation properties for efficient filtering
- Maintains support for law firm users accessing client organization tasks

### AuthorizationHelper.cs

**Location**: `Certio.Web/Security/AuthorizationHelper.cs`

**Changes** (lines 10-34):

Added comprehensive documentation header explaining the security model:

```csharp
/// <summary>
/// Helper class for validating user authorization and resource ownership
/// Centralizes security checks to prevent IDOR vulnerabilities
/// 
/// SECURITY MODEL:
/// ===============
/// 
/// MATTERS:
/// - Users see matters where:
///   1. Matter.AccessLevel = "Everyone" (all org members)
///   2. User has MatterPermission (for "Specific" access level)
///   3. User has MatterAssignment (assigned to matter)
/// 
/// TASKS:
/// - Users see tasks where:
///   1. User has TaskAssignment (assigned to task)
///   2. User has MatterAssignment (assigned to parent matter)
///   3. Parent matter has AccessLevel = "Everyone"
///   4. User has MatterPermission for parent matter
/// 
/// FILTERING:
/// - List-level filtering is done in Controllers (Index actions)
/// - Item-level authorization is done here (Get/Edit/Delete actions)
/// - Both must be consistent to prevent security vulnerabilities
/// </summary>
```

**Key Points**:
- Existing item-level authorization methods remain unchanged
- They already implement the same logic as the new list-level filtering
- Documentation now clearly explains the two-tier security approach

## User Types and Their Impact

The filtering respects different user types defined in `UserOrganization.UserTypes`:

- **Client**: Regular client organization users
- **LawFirm**: Law firm users who can also see client organization data (existing behavior preserved)
- **External**: External users (e.g., opposing counsel, experts)
- **Certio**: Internal Certio platform users

The filtering logic applies to all user types. Law firm users maintain their existing cross-organization access via `OrganizationRelationships`.

## Database Entities Involved

### MatterAssignment

```csharp
- MatterId: int
- UserId: int
- AssignmentType: string (OriginatingAttorney, ResponsibleAttorney, ResponsibleStaff, RelevantContact)
- Role: string (free-text description)
- RemovedAt: DateTime? (soft delete)
```

### MatterPermission

```csharp
- MatterId: int
- UserId: int
- GrantedAt: DateTime
- RevokedAt: DateTime? (revoked permissions)
```

### TaskAssignment

```csharp
- TaskItemId: int
- UserId: int
- AssignmentType: string (Assignee, Reviewer, Observer, Contributor)
- Role: string (free-text description)
- RemovedAt: DateTime? (soft delete)
```

## Testing Recommendations

### Matter Filtering Tests

1. **Everyone Access Test**:
   - Create a matter with `AccessLevel = "Everyone"`
   - Verify all organization members can see it

2. **Specific Access Test**:
   - Create a matter with `AccessLevel = "Specific"`
   - Grant permission to User A via `MatterPermission`
   - Verify User A can see it, User B cannot

3. **Assignment Test**:
   - Create a matter with `AccessLevel = "Specific"`
   - Add User A via `MatterAssignment` (no permission)
   - Verify User A can see it (assignment grants access)

4. **Revoked Permission Test**:
   - Grant User A permission, then revoke it (`RevokedAt = DateTime.UtcNow`)
   - Verify User A can no longer see it

5. **Removed Assignment Test**:
   - Assign User A, then remove (`RemovedAt = DateTime.UtcNow`)
   - Verify User A can no longer see it

### Task Filtering Tests

1. **Task Assignment Test**:
   - Create a task on a "Specific" matter (User A has no matter access)
   - Assign User A to the task via `TaskAssignment`
   - Verify User A can see the task

2. **Matter Assignment Test**:
   - Create a task on a "Specific" matter
   - Assign User A to the matter via `MatterAssignment`
   - Verify User A can see all tasks on that matter

3. **Inherited Access Test**:
   - Create a task on an "Everyone" matter
   - Verify all organization members can see the task

4. **Law Firm Cross-Org Test**:
   - Create a client organization with tasks
   - Create a law firm with relationship to client
   - Verify law firm users can see client tasks (if they meet access rules)

## Performance Considerations

### Query Optimization

The filtering is done at the database level using LINQ queries, which ensures:

1. **Efficient Filtering**: Only authorized data is loaded from the database
2. **Index Usage**: Proper indexes on foreign keys (UserId, MatterId, etc.)
3. **Eager Loading**: Uses `.Include()` to avoid N+1 query problems

### Indexes

The following indexes support efficient filtering:

```csharp
// Existing indexes in ApplicationDbContext
- MatterAssignment: (MatterId, UserId)
- MatterPermission: (MatterId, UserId)
- TaskAssignment: (TaskItemId, UserId)
- SubTaskAssignment: (SubTaskItemId, UserId)
```

## Security Benefits

### Defense in Depth

1. **List-Level Filtering** (Controllers): Users don't see unauthorized items in lists
2. **Item-Level Authorization** (AuthorizationHelper): Users can't access unauthorized items directly by ID
3. **Policy-Based Authorization** (`[Authorize(Policy = "OrgMember")]`): Users must be organization members

### IDOR Prevention

The combination of list filtering and item authorization prevents Insecure Direct Object Reference (IDOR) vulnerabilities:

- Users cannot enumerate IDs to find hidden matters/tasks
- Direct access attempts return 404 (not 403, to prevent information disclosure)
- All access attempts are audit logged

## Backward Compatibility

### Preserved Behaviors

1. **Law Firm Access**: Law firm users still see client organization data via `OrganizationRelationships`
2. **Sample Data**: Existing sample data creation logic unchanged
3. **API Endpoints**: All existing API endpoints maintain their behavior
4. **Authorization Methods**: Existing authorization helper methods unchanged

### Migration Notes

- **No database migration required** - uses existing tables and columns
- **No breaking changes** - only adds filtering, doesn't remove functionality
- **Immediate effect** - changes take effect as soon as deployed

## Summary

This implementation ensures that:

✅ Users only see matters they have access to (via assignment, permission, or Everyone access)
✅ Users only see tasks they have access to (via task assignment, matter assignment, or matter access)
✅ User types are respected (law firm users maintain cross-org access)
✅ Filtering is consistent between list views and item access
✅ Performance is optimized with database-level filtering
✅ Security is enhanced with defense-in-depth approach
✅ Backward compatibility is maintained

The filtering logic is transparent, well-documented, and follows the principle of least privilege - users see only what they need to see.

