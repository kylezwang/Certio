# Direct Messaging - User ID Fix Applied

## Problem
All team members were showing `data-user-id="0"` in the HTML, causing DM clicks to fail with:
```
Opening DM with: Kyle Wang ID: 0
Error opening direct thread
```

## Root Cause
The `UserOrganization` entity has a `UserId` property that was `0` in the database. The code was using:
```csharp
UserId = uo.UserId  // This was 0
```

However, since we're already loading the related `User` entity with `.Include(uo => uo.User)`, we can access the actual User's Id directly.

## Fix Applied

### File: `Certio.Web/Controllers/HomeController.cs` (Line 1402)

**Before:**
```csharp
return new CommunicationsTeamMember
{
    UserId = uo.UserId,  // ❌ This was 0
    Name = $"{uo.User.FirstName} {uo.User.LastName}".Trim(),
    // ... other properties
};
```

**After:**
```csharp
return new CommunicationsTeamMember
{
    UserId = uo.User.Id,  // ✅ Use the actual User's Id
    Name = $"{uo.User.FirstName} {uo.User.LastName}".Trim(),
    // ... other properties
};
```

## Expected Result
After refreshing the page, the HTML should now show:
```html
<div class="team-member dm-user-item" data-user-id="1" data-user-name="Kyle Wang">
<div class="team-member dm-user-item" data-user-id="2" data-user-name="Chloe Tang">
<div class="team-member dm-user-item" data-user-id="3" data-user-name="Lawyer Kyle">
```

Instead of all zeros.

## Testing Steps
1. **Refresh the Communications page** (Ctrl+F5 to hard refresh)
2. **Inspect a team member** in DevTools
3. **Verify** `data-user-id` is now a real number (not 0)
4. **Click on a team member**
5. **Check Network tab** for the POST to `/api/dm/threads`
6. **Verify** the request body now has correct `otherUserId`

## Next Issue to Resolve
The API is still returning HTML instead of JSON. This is an **authorization issue**. 

Check the Network tab when clicking a user - if you see:
- **Response starts with `<!DOCTYPE html>`** → Authorization redirect
- **Status 401** → Not authenticated
- **Status 403** → Permission denied

The DirectMessagesController uses `[Authorize(Policy = "OrgMember")]` which may need configuration.

## Database Note
If you want to fix the underlying data issue (UserOrganization.UserId being 0), you can run:

```powershell
# Check current state
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT uo.Id, uo.UserId, u.Id as ActualUserId, u.Email FROM CertioLocal.dbo.UserOrganizations uo LEFT JOIN CertioLocal.dbo.Users u ON u.Id = uo.UserId WHERE uo.OrganizationId = 1"
```

However, this fix makes it work regardless of the database state.

