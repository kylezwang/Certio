# Partner Permission Fix

## Issue Description

Law Firm partner users were unable to create matters in client organizations, receiving the error:
```
User 1 is not authorized to create Matter. Lacks CreateMatters permission
```

## Root Causes

### 1. Missing AccessLevel Handlers (Initial Issue - FIXED)
The `PermissionService.GetPartnerPermissions()` method was missing handlers for two AccessLevel values:
- **MatterSpecific** - Defined in domain but not handled in permissions
- **DocumentOnly** - Defined in domain but not handled in permissions

### 2. Permission Resolution Priority Issue (Actual Root Cause - FIXED)
The real issue was that User 1 had **duplicate memberships**:
- **Direct membership** in Organization 4 with Role='Staff' (limited permissions, no CreateMatters)
- **Firm-based access** via OrganizationRelationship with AccessLevel='FullAccess' (includes CreateMatters)

The permission system checked direct membership FIRST and returned early, never checking firm-based access that would grant CreateMatters permission.

**Staff permissions:** ViewDocuments, DownloadDocuments, ViewMatters, ViewMessages, SendMessages (NO CreateMatters)
**FullAccess permissions:** All permissions including CreateMatters

### 3. UI Dropdown Missing Firm Users (Secondary Issue - FIXED)
The `PopulateOrgMembersData()` method only loaded users with direct UserOrganizations membership, causing law firm users to disappear from dropdowns when direct membership was removed.

## Fix Applied

Updated `Certio.Application/Services/PermissionService.cs`:

### 1. Added MatterSpecific AccessLevel Handler
```csharp
"MatterSpecific" => new List<Permission>
{
    // Matter-specific access - can create and manage matters, view documents
    Permission.ViewMatters,
    Permission.CreateMatters,
    Permission.EditMatters,
    Permission.ManageMatterSettings,
    Permission.ViewDocuments,
    Permission.DownloadDocuments,
    Permission.ViewMessages
}
```

### 2. Added DocumentOnly AccessLevel Handler
```csharp
"DocumentOnly" => new List<Permission>
{
    // Document-only access - can view/manage documents but not create matters
    Permission.ViewMatters,
    Permission.ViewDocuments,
    Permission.DownloadDocuments,
    Permission.UploadDocuments,
    Permission.CommentOnDocuments,
    Permission.ViewMessages
}
```

**Note:** DocumentOnly does NOT include `CreateMatters` permission, as that access level is intended for document-only work.

### Fix 2: Fixed Permission Resolution Priority
Updated `Certio.Application/Services/PermissionService.cs` - `GetEffectivePermissionsAsync()` method:

**Changed from:** Check direct membership first, return early if found (ignoring firm-based access)

**Changed to:** Check BOTH direct membership AND firm-based access, then return the **union** of both permission sets

This ensures that if a user has both direct membership (e.g., Staff role with limited permissions) AND firm-based access (e.g., FullAccess via relationship), they get the combined permissions from both sources.

### Fix 3: Include Firm Users in Matter Assignment Dropdowns
Updated `Certio.Web/Controllers/MatterController.cs` - `PopulateOrgMembersData()` method:

**Added:** Query to load users from law firms that have OrganizationRelationships to the client organization
**Added:** Deduplication logic to handle users who have both direct and firm-based access
**Result:** Law firm members now appear in assignment dropdowns even without direct membership

### Fix 4: Include Firm-Accessible Organizations in Global Clients Dropdown
Updated `Certio.Web/Controllers/ClientController.cs` - `List()` action:

**Changed from:** Only loading direct UserOrganizations memberships
**Changed to:** Loading BOTH direct memberships AND organizations accessible via OrganizationRelationships
**Added:** Deduplication by organizationId (direct membership takes precedence for isPrimary flag)
**Result:** Client organizations now appear in the global navbar dropdown for law firm users, even without direct membership

### Fix 5: Enhanced Logging
Added detailed logging to track:
- Which AccessLevel is being used
- What permissions are granted
- Cases where firm access returns true but relationship is null

## AccessLevel Permission Matrix

| AccessLevel | CreateMatters | EditMatters | DeleteMatters | ViewDocs | UploadDocs | ViewMessages |
|------------|---------------|-------------|---------------|----------|------------|--------------|
| FullAccess | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| LimitedAccess | ✓ | ✓ | ✗ | ✓ | ✓ | ✓ |
| MatterSpecific | ✓ | ✓ | ✗ | ✓ | ✗ | ✓ |
| DocumentOnly | ✗ | ✗ | ✗ | ✓ | ✓ | ✓ |
| ReadOnly | ✗ | ✗ | ✗ | ✓ | ✗ | ✓ |

## Diagnostic Queries

### 1. Check for Duplicate/Overlapping Memberships

This shows users who have BOTH direct membership AND firm-based access:

```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT u.Id AS UserId, u.FirstName + ' ' + u.LastName AS UserName, direct.OrganizationId AS DirectOrgId, direct.Role AS DirectRole, rel.TargetOrganizationId AS AccessViaRelationship, rel.AccessLevel FROM CertioLocal.dbo.Users u INNER JOIN CertioLocal.dbo.UserOrganizations direct ON u.Id = direct.UserId AND direct.IsActive = 1 INNER JOIN CertioLocal.dbo.UserOrganizations lawfirm ON u.Id = lawfirm.UserId AND lawfirm.IsActive = 1 AND lawfirm.UserType = 'LawFirm' INNER JOIN CertioLocal.dbo.OrganizationRelationships rel ON rel.SourceOrganizationId = lawfirm.OrganizationId AND rel.TargetOrganizationId = direct.OrganizationId AND rel.IsActive = 1 AND rel.IsDeleted = 0 AND rel.RelationshipType = 'LawFirmClient' ORDER BY u.Id"
```

### 2. See Who Has Firm-Based Access to an Organization

```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT u.Id, u.FirstName, u.LastName, u.Email, uo.Role, rel.AccessLevel FROM CertioLocal.dbo.Users u INNER JOIN CertioLocal.dbo.UserOrganizations uo ON u.Id = uo.UserId INNER JOIN CertioLocal.dbo.OrganizationRelationships rel ON rel.SourceOrganizationId = uo.OrganizationId WHERE uo.IsActive = 1 AND uo.UserType = 'LawFirm' AND rel.TargetOrganizationId = 4 AND rel.IsActive = 1 AND rel.IsDeleted = 0 AND rel.RelationshipType = 'LawFirmClient'"
```

### 3. Check Relationship AccessLevel

Run this SQL query to see what AccessLevel is configured for your law firm-client relationships:

```sql
SELECT 
    r.Id,
    sf.Name AS LawFirmName,
    tf.Name AS ClientName,
    r.AccessLevel,
    r.RelationshipType,
    r.IsActive,
    r.IsDeleted,
    r.CreatedAt
FROM OrganizationRelationships r
INNER JOIN Organizations sf ON r.SourceOrganizationId = sf.Id
INNER JOIN Organizations tf ON r.TargetOrganizationId = tf.Id
WHERE r.RelationshipType = 'LawFirmClient'
ORDER BY r.CreatedAt DESC;
```

### Check Partner User Access

To verify a specific user's access to a client organization:

```sql
SELECT 
    u.Id AS UserId,
    u.FirstName + ' ' + u.LastName AS UserName,
    uo.UserType,
    lawfirm.Name AS LawFirmName,
    client.Name AS ClientName,
    r.AccessLevel,
    r.IsActive AS RelationshipActive
FROM Users u
INNER JOIN UserOrganizations uo ON u.Id = uo.UserId
INNER JOIN Organizations lawfirm ON uo.OrganizationId = lawfirm.Id
INNER JOIN OrganizationRelationships r ON r.SourceOrganizationId = lawfirm.Id
INNER JOIN Organizations client ON r.TargetOrganizationId = client.Id
WHERE 
    u.Id = 1  -- Replace with your user ID
    AND uo.UserType = 'LawFirm'
    AND uo.IsActive = 1
    AND r.RelationshipType = 'LawFirmClient'
    AND r.IsActive = 1
    AND r.IsDeleted = 0;
```

## Testing

After rebuilding and restarting the application:

1. **Login as a Law Firm Partner User**
2. **Navigate to a Client Organization**
3. **Try to Create a Matter**
4. **Check Application Logs** for the new detailed permission logging:
   ```
   Partner access granted for user {UserId} to organization {OrgId} with {AccessLevel} level ({PermissionCount} permissions): {Permissions}
   ```

## Expected Behavior

- **FullAccess**: Partner can create, edit, delete matters and manage all aspects
- **LimitedAccess**: Partner can create and edit matters but cannot delete
- **MatterSpecific**: Partner can create and edit matters with limited document access
- **DocumentOnly**: Partner CANNOT create matters (this is by design - document-only access)
- **ReadOnly**: Partner can only view, cannot create anything

## What to Do If Issue Persists

1. **Check the logs** to see what AccessLevel is being used
2. **Verify the relationship exists** in the database using the diagnostic queries above
3. **Ensure the relationship is active** (IsActive = true, IsDeleted = false)
4. **Check that the user is a member of the law firm** with UserType = 'LawFirm'
5. **Verify the law firm organization ID** matches the SourceOrganizationId in the relationship

## Solution Summary

This fix resolves a complex permission issue where law firm users couldn't create matters in client organizations. The solution involved:

1. **Permission System**: Added missing AccessLevel handlers and changed permission resolution to use the UNION of direct + firm-based permissions
2. **UI Dropdowns**: Updated both matter assignment dropdowns and global clients dropdown to include firm-based access
3. **Database Cleanup**: Removed duplicate UserOrganizations memberships that were blocking firm-based permissions

## Best Practices Going Forward

### For User Management:
- **Avoid duplicate memberships**: Law firm users should NOT have direct UserOrganizations membership in client organizations
- **Use OrganizationRelationships**: Law firm access to client organizations should be managed through OrganizationRelationships
- **Set appropriate AccessLevel**: Choose FullAccess, LimitedAccess, or MatterSpecific based on what permissions the firm needs

### For Debugging:
- Use the diagnostic queries above to check for duplicate memberships
- Check application logs for permission resolution details
- Verify RelationshipType is set to 'LawFirmClient' for law firm relationships

## Additional Notes

- If a partner needs to create matters, ensure the relationship AccessLevel is set to: FullAccess, LimitedAccess, or MatterSpecific
- DocumentOnly and ReadOnly access levels do not grant CreateMatters permission by design
- The system will now log detailed permission information to help diagnose issues
- The permission system now uses the UNION of all permission sources, so users get the best of both direct and firm-based access

