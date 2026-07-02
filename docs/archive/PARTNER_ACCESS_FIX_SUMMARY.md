# Partner Access Fix - Summary

## What Was Fixed

Law firm partner users can now properly access and create matters in client organizations through OrganizationRelationships.

## Files Changed

### 1. `Certio.Application/Services/PermissionService.cs`
- Added `MatterSpecific` and `DocumentOnly` AccessLevel handlers
- Changed `GetEffectivePermissionsAsync()` to return the UNION of direct + firm-based permissions
- Added detailed logging for permission resolution

### 2. `Certio.Application/Services/MatterService.cs`
- Updated `AssignUserToMatterAsync()` to check BOTH direct membership AND firm-based access for assignees
- **Fixed duplicate assignment check** to allow same user with different AssignmentTypes (e.g., Originating Attorney + Responsible Attorney)
- Now checks for duplicates based on UserId + AssignmentType combination instead of just UserId
- Added error handling and logging for assignment failures

### 3. `Certio.Application/Services/PermissionService.cs` (Additional Fixes)
- Updated `CanAccessMatterAsync()` to check firm-based access (this was blocking assignments!)
- Updated `CanAccessTaskAsync()` to check firm-based access
- Consolidated logic to check both direct and firm access consistently

### 4. `Certio.Web/Controllers/MatterController.cs`
- Updated `PopulateOrgMembersData()` to include law firm users who have access via OrganizationRelationships
- Added deduplication logic for users with both direct and firm-based access
- Added error handling for assignment failures with user feedback
- Law firm users now appear in assignment dropdowns

### 5. `Certio.Web/Controllers/ClientController.cs`
- Updated `List()` action to include organizations accessible via OrganizationRelationships
- Client organizations now appear in the global navbar dropdown for law firm users
- Added deduplication by organizationId

## Key Insights

### Primary Issues:
1. **Duplicate memberships**: Users had both direct `UserOrganizations` membership (Role='Staff', limited permissions) AND firm-based access via `OrganizationRelationship` (AccessLevel='FullAccess')
   - Old code checked direct membership first and returned early
   - New code checks both and returns the union of permissions

2. **Assignment failures**: Multiple permission checks only looked at direct membership:
   - `CanAccessMatterAsync` failed for firm users → blocked all assignments
   - `AssignUserToMatterAsync` validation failed for firm users as assignees
   - No error handling → silent failures

3. **UI missing firm users**: Dropdowns only loaded direct `UserOrganizations` members

## Database Cleanup Commands

Remove duplicate UserOrganizations memberships (User 1 already done):

```powershell
# User 5
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -Q "DELETE FROM CertioLocal.dbo.UserOrganizations WHERE UserId = 5 AND OrganizationId = 4"
```

## Diagnostic Queries

### Check for duplicates:
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT u.Id AS UserId, u.FirstName + ' ' + u.LastName AS UserName, direct.OrganizationId AS DirectOrgId, direct.Role AS DirectRole, rel.TargetOrganizationId AS AccessViaRelationship, rel.AccessLevel FROM CertioLocal.dbo.Users u INNER JOIN CertioLocal.dbo.UserOrganizations direct ON u.Id = direct.UserId AND direct.IsActive = 1 INNER JOIN CertioLocal.dbo.UserOrganizations lawfirm ON u.Id = lawfirm.UserId AND lawfirm.IsActive = 1 AND lawfirm.UserType = 'LawFirm' INNER JOIN CertioLocal.dbo.OrganizationRelationships rel ON rel.SourceOrganizationId = lawfirm.OrganizationId AND rel.TargetOrganizationId = direct.OrganizationId AND rel.IsActive = 1 AND rel.IsDeleted = 0 AND rel.RelationshipType = 'LawFirmClient' ORDER BY u.Id"
```

## Testing

1. Restart the application to pick up the changes
2. Login as a law firm user (e.g., User 1 or User 5)
3. Verify:
   - Client organizations appear in the global navbar "Clients" dropdown
   - Can create matters in client organizations
   - Law firm users appear in assignment dropdowns on page 3-4 of Matter/Create
   - Check logs for detailed permission information

## Best Practices

- **Don't create duplicate memberships**: Law firm users should access client orgs via OrganizationRelationships, not direct UserOrganizations
- **Set appropriate AccessLevel**: FullAccess, LimitedAccess, or MatterSpecific for matter creation rights
- **Use diagnostic queries**: Check for overlapping memberships before adding users

See `PARTNER_PERMISSION_FIX.md` for complete documentation.

