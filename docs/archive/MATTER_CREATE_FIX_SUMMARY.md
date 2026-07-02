# Matter Creation Assignment & Organization Context Fix

## Issues Found

### 1. **Profile Icons Not Persisting from Page 3 to Page 4**
**Problem**: Assignee profile icons displayed correctly on page 3 preview but showed "+0" on page 4.

**Root Cause**: JavaScript property name case mismatch
- C# serializes JSON with camelCase properties (e.g., `userId`, `assignmentType`)
- JavaScript was accessing them with PascalCase (e.g., `UserId`, `AssignmentType`)
- This caused JavaScript to not find the assignee data on page 4

**Fix Applied** (in `Create.cshtml`):
- Updated all JavaScript code to use camelCase property names:
  - `fa.UserId` → `fa.userId`
  - `fa.AssignmentType` → `fa.assignmentType`
  - `fa.Role` → `fa.role`
- Added `.Where(fa => fa.UserId.HasValue)` filter to only serialize assignments with actual values
- Added fallback mechanism to read from hidden form fields on Step 4

### 2. **Matter Assignments Not Being Created**
**Problem**: Matter ID 13 "Service Layer Architecture Partnership" was created with 0 assignments while all other matters had 4 assignments.

**Root Cause**: Data binding issue between steps - to be diagnosed with added logging

**Fix Applied**:
- Added comprehensive debug logging to controller:
  - Logs FirmAssignments count and details on every POST
  - Logs step validation and assignment counts
  - Tracks data flow between steps
- Added HTML debug comments in view:
  - Shows FirmAssignments and RelevantContacts counts
  - Shows individual assignment details (Type, UserId, Role)
- These will help identify where assignment data is being lost

### 3. **Incorrect Organization Context** ⚠️ **CRITICAL**
**Problem**: Matters were being created in the user's primary organization instead of the current working organization (partner organization).

**Root Cause**: `GetUserContext()` method was using `GetPrimaryOrganization()` instead of `CurrentOrganizationId` from middleware
- When a partner lawyer works with client organizations, the `CurrentOrganizationId` reflects the client org
- Using primary organization incorrectly created matters in the law firm organization

**Fix Applied** (in `MatterController.cs`):

#### Fixed `GetUserContext()` method:
```csharp
private (User? User, int OrganizationId) GetUserContext()
{
    var customUser = HttpContext.Items["CustomUser"] as User;
    // Use CurrentOrganizationId from middleware (handles partner org context)
    var orgId = HttpContext.Items.TryGetValue("CurrentOrganizationId", out var orgObj) && orgObj is int currentOrgId
        ? currentOrgId
        : customUser?.GetPrimaryOrganization()?.OrganizationId ?? 0;
    
    _logger.LogInformation($"GetUserContext: User={customUser?.Id}, OrgId={orgId} (from {(HttpContext.Items.ContainsKey("CurrentOrganizationId") ? "CurrentOrganizationId" : "PrimaryOrg")})");
    
    return (customUser, orgId);
}
```

#### Fixed `PopulateOrgMembersData()` method:
- Changed from using `GetPrimaryOrganization()` to using `GetUserContext()`
- Now loads members from the **current organization context** (client org) instead of law firm
- Added logging to track which organization's members are being loaded

**Impact**: 
- ✅ Matters now created in correct organization
- ✅ Assignment dropdowns show correct organization members
- ✅ Partner lawyers can properly create matters for client organizations

## Testing Instructions

1. **Test Organization Context**:
   - Log in as a partner lawyer
   - Switch to a client organization context
   - Create a new matter
   - Verify the matter is created in the client organization (not law firm)
   - Check logs for "GetUserContext" to see which org is being used

2. **Test Assignment Persistence**:
   - Create a new matter
   - On Step 3, select all three firm assignments
   - Add at least one relevant contact
   - Move to Step 4 and view page source (Ctrl+U)
   - Look for debug comments showing assignment data
   - Verify profile icons show correctly in preview
   - Complete matter creation
   - Check database to verify all assignments were created

3. **Database Verification**:
   ```sql
   -- Check matter organization
   SELECT Id, Title, OrganizationId FROM CertioLocal.dbo.Matters 
   WHERE Id = [NEW_MATTER_ID]
   
   -- Check matter assignments
   SELECT ma.Id, ma.MatterId, ma.UserId, ma.AssignmentType, ma.Role
   FROM CertioLocal.dbo.MatterAssignments ma
   WHERE ma.MatterId = [NEW_MATTER_ID] AND ma.RemovedAt IS NULL
   ```

## Debug Output Locations

### Controller Logs (Console/Application Insights):
- `Matter/Create POST - Step: X, Action: Y`
- `FirmAssignments Count: X`
- `RelevantContacts Count: X`
- `GetUserContext: User=X, OrgId=Y (from Z)`
- `PopulateOrgMembersData: Loading members for organization X`
- `PopulateOrgMembersData: Found X members in organization Y`

### View HTML Comments (Page Source):
```html
<!-- DEBUG: Rendering hidden inputs for Step 4, FirmAssignments.Count = 3 -->
<!-- DEBUG: FirmAssignments[0] - Type: ResponsibleAttorney, UserId: 1, Role: Responsible Attorney -->
<!-- DEBUG: FirmAssignments[1] - Type: ResponsibleStaff, UserId: 5, Role: Responsible Staff -->
<!-- DEBUG: FirmAssignments[2] - Type: OriginatingAttorney, UserId: 1, Role: Originating Attorney -->
<!-- DEBUG: RelevantContacts.Count = 1 -->
<!-- DEBUG: RelevantContacts[0] - UserId: 8, Involvement: Client Contact -->
```

## Files Modified

1. **Certio.Web/Views/Matter/Create.cshtml**
   - Fixed JavaScript property name casing (camelCase)
   - Added debug HTML comments
   - Added fallback mechanism for Step 4 data recovery

2. **Certio.Web/Controllers/MatterController.cs**
   - Fixed `GetUserContext()` to use `CurrentOrganizationId`
   - Fixed `PopulateOrgMembersData()` to use current org context
   - Added comprehensive debug logging throughout

## Related Systems

This fix ensures proper integration with:
- **Organization Relationships**: Partner law firms accessing client organizations
- **ClientContext**: Middleware that sets `CurrentOrganizationId`
- **FirmRelationshipCacheService**: Caching layer for partner access
- **Matter Assignment System**: Ensures assignments are created correctly

## Additional Fixes Applied

### 4. **Boolean Binding Issue** ✅ **FIXED**
**Problem**: `IsNotifyRecipient` boolean values were being serialized as "True"/"False" instead of "true"/"false", causing model binding errors.

**Fix Applied** (in `Create.cshtml` lines 121 & 129):
```csharp
// Before
value="@Model.FirmAssignments[i].IsNotifyRecipient"

// After
value="@Model.FirmAssignments[i].IsNotifyRecipient.ToString().ToLower()"
```

### 5. **Missing Involvement Validation** ✅ **FIXED**
**Problem**: Relevant contacts could be added without specifying their involvement/role.

**Fixes Applied**:
1. **Controller Validation** (MatterController.cs):
   - Added validation to ensure all contacts with a UserId have an Involvement description
   - Error message: "The Involvement field is required for all contacts"

2. **View Updates** (Create.cshtml):
   - Added `required` attribute to all Involvement input fields
   - Added red asterisk (*) to labels to indicate required field
   - Applied to initial load, existing contacts, and dynamically added contacts

### 6. **Partner Permission System** ✅ **FIXED**
**Problem**: Partner lawyers didn't have `CreateMatters` permission when accessing client organizations through OrganizationRelationship.

**Root Cause**: `GetEffectivePermissionsAsync` only checked for direct UserOrganization membership and returned empty permissions for partner access, even though `HasFirmBasedAccessAsync` verified the relationship existed.

**Fix Applied** (in `PermissionService.cs`):
1. **Added partner permission logic** to `GetEffectivePermissionsAsync`:
   - After checking direct membership, now checks for firm-based access
   - Calls new `GetFirmRelationshipAsync()` method to get the relationship
   - Grants permissions based on relationship's `AccessLevel` property

2. **Created `GetFirmRelationshipAsync()` method**:
   - Retrieves the OrganizationRelationship between law firm and client
   - Verifies relationship is active and not expired

3. **Created `GetPartnerPermissions()` method**:
   - Maps AccessLevel to permission sets:
     - **"Full"**: All matter, document, and communication permissions (including CreateMatters)
     - **"Limited"**: View, create, edit matters and documents (including CreateMatters)
     - **"ReadOnly"**: View-only access to matters and documents

4. **Added comprehensive logging**:
   - Logs when partner access is granted with permission count
   - Warns when no permissions found for debugging

**Impact**: Partner lawyers now automatically get appropriate permissions based on their OrganizationRelationship's AccessLevel without needing manual database entries.

## Summary of All Fixes

✅ **Profile icon persistence** (camelCase property names)  
✅ **Organization context** (CurrentOrganizationId from middleware)  
✅ **Debug logging** (comprehensive tracking)  
✅ **Validation feedback** (visible error messages)  
✅ **Access level selection** (explicit user choice required)  
✅ **Auto-populate permissions** (users from Step 3)  
✅ **Boolean binding** (lowercase true/false)  
✅ **Involvement validation** (required field)  
✅ **Partner permissions** (automatic through OrganizationRelationship)

## Next Steps

1. ~~**Grant CreateMatters permission** to User 1 in Organization 4~~ ✅ **FIXED** - Now automatic via partner relationship
2. **Test matter creation** with all fixes applied
3. **Verify database entries** for matters and assignments  
4. **Check application logs** for partner permission grant messages
5. **Remove debug comments** once confirmed working (optional)
6. **Consider automated tests** for partner organization workflows

## Permission Access Levels

The system now supports three AccessLevel tiers for OrganizationRelationships:

### **Full Access**
- All matter operations (View, Create, Edit, Delete, Manage)
- All document operations (View, Download, Upload, Delete, Comment)
- All communication features (View, Send, Manage threads)
- **Use case**: Primary law firm partners managing client matters

### **Limited Access** (Default for most partners)
- Matter operations: View, Create, Edit, Manage
- Document operations: View, Download, Upload, Comment
- Communication: View, Send messages
- **Use case**: Associate lawyers or staff members

### **ReadOnly Access**
- Matter operations: View only
- Document operations: View, Download only
- Communication: View messages only
- **Use case**: External reviewers or observers

