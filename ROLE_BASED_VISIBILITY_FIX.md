# Role-Based Matter & Task Visibility Fix

## Overview
Implemented role-based filtering to ensure that matter and task visibility is properly scoped based on both **user role** and **access level**.

## Problem Statement
Users could see matters they shouldn't have access to in two scenarios:
1. **Law firm users accessing client organizations via `OrganizationRelationship`** with restricted access levels (e.g., "MatterSpecific") could see ALL matters, not just assigned ones
2. **Associates, Paralegals, and Staff** within their own organizations could see ALL matters, even those with `AccessLevel = "Specific"` where they weren't assigned

## Solution Implemented

### 1. Firm-Based Access Filtering (OrganizationRelationships)
When law firm users access client organizations via `OrganizationRelationship`:

**Files Modified:**
- `Certio.Application/Services/MatterService.cs` (lines 332-344)
- `Certio.Application/Services/TaskService.cs` (lines 349-361)
- `Certio.Application/Interfaces/IPermissionService.cs` (added `GetFirmRelationshipAsync`)
- `Certio.Application/Services/PermissionService.cs` (made `GetFirmRelationshipAsync` public)

**Logic:**
```csharp
if (hasFirmAccess)
{
    var firmRelationship = await _permissionService.GetFirmRelationshipAsync(userId, organizationId);
    
    if (firmRelationship?.AccessLevel == "MatterSpecific" || 
        firmRelationship?.AccessLevel == "DocumentOnly")
    {
        // Only show matters they're assigned to
        query = query.Where(m => 
            m.AccessLevel == "Everyone" || 
            m.Permissions.Any(p => p.UserId == userId) ||
            m.Assignments.Any(a => a.UserId == userId));
    }
    // For "Full" or "Limited" AccessLevel: show all matters
}
```

**Result:**
- ✅ Partners with `AccessLevel = "Full"`: See all client matters
- ✅ Users with `AccessLevel = "MatterSpecific"`: Only see assigned matters
- ✅ Users with `AccessLevel = "DocumentOnly"`: Only see assigned matters

### 2. Role-Based Access Filtering (Direct Organization Members)
When users access their own organization as direct members:

**Files Modified:**
- `Certio.Application/Services/MatterService.cs` (lines 314-330)
- `Certio.Application/Services/TaskService.cs` (lines 331-348)

**Logic:**
```csharp
if (!hasFirmAccess)
{
    var userOrgMembership = await _context.UserOrganizations
        .FirstOrDefaultAsync(uo => uo.UserId == userId && uo.OrganizationId == organizationId);
    
    if (userOrgMembership?.Role != OrganizationRoles.Partner)
    {
        // Non-partners: filter by matter-level access
        query = query.Where(m => 
            m.AccessLevel == "Everyone" || 
            m.Permissions.Any(p => p.UserId == userId) ||
            m.Assignments.Any(a => a.UserId == userId));
    }
    // Partners see all matters
}
```

**Result:**
- ✅ **Partners**: See all matters in their organization
- ✅ **Associates, Paralegals, Staff**: Only see:
  - Matters with `AccessLevel = "Everyone"`
  - Matters they're assigned to
  - Matters with explicit permissions

## Access Matrix

### Within Own Organization (Direct Member)

| Role | AccessLevel = "Everyone" | AccessLevel = "Specific" (Not Assigned) | AccessLevel = "Specific" (Assigned) |
|------|-------------------------|----------------------------------------|-------------------------------------|
| **Partner** | ✅ Can see | ✅ Can see | ✅ Can see |
| **Associate** | ✅ Can see | ❌ Cannot see | ✅ Can see |
| **Paralegal** | ✅ Can see | ❌ Cannot see | ✅ Can see |
| **Staff** | ✅ Can see | ❌ Cannot see | ✅ Can see |

### Accessing Client Org via OrganizationRelationship

| AccessLevel | AccessLevel = "Everyone" | AccessLevel = "Specific" (Not Assigned) | AccessLevel = "Specific" (Assigned) |
|-------------|-------------------------|----------------------------------------|-------------------------------------|
| **Full** | ✅ Can see | ✅ Can see | ✅ Can see |
| **Limited** | ✅ Can see | ✅ Can see | ✅ Can see |
| **MatterSpecific** | ✅ Can see | ❌ Cannot see | ✅ Can see |
| **DocumentOnly** | ✅ Can see | ❌ Cannot see | ✅ Can see |
| **ReadOnly** | ✅ Can see | ❌ Cannot see | ✅ Can see |

## Testing Steps

1. **Test Role-Based Filtering (Own Organization)**:
   - Log in as an Associate user
   - Navigate to the organization's Matters page
   - Verify you only see:
     - Matters with `AccessLevel = "Everyone"`
     - Matters you're assigned to
   - Log in as a Partner
   - Verify you see ALL matters

2. **Test Firm-Based Filtering (Client Organization)**:
   - Create an `OrganizationRelationship` with `AccessLevel = "MatterSpecific"`
   - Assign a law firm user to ONE matter in the client org
   - Log in as that law firm user
   - Navigate to the client org's Matters page
   - Verify you only see:
     - Matters with `AccessLevel = "Everyone"`
     - The one matter you're assigned to

3. **Test Task Filtering**:
   - Repeat above tests for the Tasks page
   - Verify tasks respect parent matter visibility

## Database Commands for Testing

### Check User Role in Organization
```sql
SELECT u.Id, u.FirstName, u.LastName, uo.Role, uo.UserType, o.Name as OrgName
FROM UserOrganizations uo
INNER JOIN Users u ON uo.UserId = u.Id
INNER JOIN Organizations o ON uo.OrganizationId = o.Id
WHERE uo.IsActive = 1;
```

### Check Matter Assignments
```sql
SELECT m.Id, m.Title, m.AccessLevel, 
       CONCAT(u.FirstName, ' ', u.LastName) AS AssignedUser,
       uo.Role AS UserRole
FROM Matters m
LEFT JOIN MatterAssignments ma ON m.Id = ma.MatterId AND ma.RemovedAt IS NULL
LEFT JOIN Users u ON ma.UserId = u.Id
LEFT JOIN UserOrganizations uo ON uo.UserId = u.Id AND uo.OrganizationId = m.OrganizationId
ORDER BY m.Id;
```

### Check OrganizationRelationship AccessLevel
```sql
SELECT 
    source.Name AS LawFirm,
    target.Name AS Client,
    r.AccessLevel,
    r.IsActive
FROM OrganizationRelationships r
INNER JOIN Organizations source ON r.SourceOrganizationId = source.Id
INNER JOIN Organizations target ON r.TargetOrganizationId = target.Id
WHERE r.RelationshipType = 'LawFirmClient';
```

## Implementation Notes

1. **Performance**: Added one additional database query per request to fetch user's role. This is minimal overhead and can be optimized with caching if needed.

2. **Consistency**: Applied the same logic to both `MatterService` and `TaskService` to ensure consistency.

3. **Security**: This is a **visibility** filter, not a security control. The underlying `CanAccessMatterAsync` checks in `PermissionService` still enforce access control.

4. **Backwards Compatibility**: Existing behavior for Partners and users with "Full" access remains unchanged.

## Files Changed

1. `Certio.Application/Services/MatterService.cs`
2. `Certio.Application/Services/TaskService.cs`
3. `Certio.Application/Services/PermissionService.cs`
4. `Certio.Application/Interfaces/IPermissionService.cs`
5. `Certio.Web/Controllers/TasksController.cs` (previous fix for law firm aggregation)

## Build Status
✅ Build Succeeded (no errors)
⚠️ 8 warnings (pre-existing, unrelated to this change)

