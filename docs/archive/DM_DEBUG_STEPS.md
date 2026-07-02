# Direct Messaging Debug Steps

## Issue: UserId showing as 0

### Step 1: Check View Source
1. Right-click on the Communications page
2. Select "View Page Source"
3. Search for `dm-user-item`
4. Check if `data-user-id="0"` or actual number

**Expected:**
```html
<div class="team-member dm-user-item" data-user-id="1" data-user-name="Kyle Wang">
```

**If you see `data-user-id="0"`:**
- The ViewModel is being populated with UserId = 0
- Database query is returning 0 for UserId
- OR UserOrganization.UserId is actually 0 in database

### Step 2: Check Database
Run this Docker SQL query:
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT Id, UserId, OrganizationId, Role, IsActive FROM CertioLocal.dbo.UserOrganizations WHERE OrganizationId = 1 AND IsActive = 1"
```

**Expected:**
- UserId should NOT be 0
- UserId should match actual User IDs

**If UserId is 0 in database:**
- Data integrity issue
- The UserOrganization.UserId field is 0
- **Fix:** The code now uses `uo.User.Id` instead of `uo.UserId`

### Step 3: Check Controller Mapping
**FIXED:** Changed from `uo.UserId` to `uo.User.Id` in `HomeController.cs` around line 1402:
```csharp
UserId = uo.User.Id, // Use User.Id instead of uo.UserId
```

The issue was that `UserOrganization.UserId` was 0, but the actual User entity has the correct Id.

### Step 4: Check API Response
1. Open Browser DevTools → Network tab
2. Click on a team member
3. Find the POST request to `/api/dm/threads`
4. Check the response

**If Response is HTML (starts with `<!DOCTYPE`):**
- Authorization is redirecting to login
- User is not authenticated
- OR Policy "OrgMember" is failing

**If Response is 401 Unauthorized:**
- Check that user is logged in
- Check that user has claims
- Check DirectMessagesController authorization

**If Response is 403 Forbidden:**
- Permission check is failing
- User might not be org member

**If Response is JSON:**
- API is working!
- Check if `thread` object is in response
- Check if thread has correct IDs

## Quick Test

### Test 1: Hardcode UserId
In `Communications.cshtml`, temporarily hardcode a value:
```html
<div class="team-member dm-user-item" 
     data-user-id="1" 
     data-user-name="Test User"
     style="cursor: pointer;">
```

Click on it - if API works, problem is in ViewModel.

### Test 2: Check JavaScript Console
Run this in browser console:
```javascript
document.querySelectorAll('.dm-user-item').forEach(item => {
    console.log('User:', item.dataset.userName, 'ID:', item.dataset.userId);
});
```

This will show all user IDs being rendered.

### Test 3: Bypass Authorization
Temporarily remove authorization from DirectMessagesController:
```csharp
[ApiController]
[Route("api/dm")]
// [Authorize(Policy = "OrgMember")] // COMMENTED OUT FOR TESTING
public class DirectMessagesController : ControllerBase
```

If it works → Authorization issue
If still fails → Other issue

## Common Fixes

### Fix 1: User ID is actually 0 in database
```powershell
# Check User table
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT Id, Email, FirstName, LastName FROM CertioLocal.dbo.Users"

# Check UserOrganizations linkage  
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT uo.Id, uo.UserId, u.Email, uo.OrganizationId, uo.Role FROM CertioLocal.dbo.UserOrganizations uo LEFT JOIN CertioLocal.dbo.Users u ON u.Id = uo.UserId WHERE uo.OrganizationId = 1"
```

**FIXED:** Changed code to use `uo.User.Id` instead of `uo.UserId`.

### Fix 2: Authorization is failing
Check in `DirectMessagesController.GetCurrentUserId()`:
```csharp
private int GetCurrentUserId()
{
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    _logger.LogInformation("User ID Claim: {Claim}", userIdClaim); // ADD THIS
    
    if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
    {
        throw new UnauthorizedAccessException("User ID not found in claims");
    }
    return userId;
}
```

### Fix 3: Policy "OrgMember" not defined
Check `Program.cs` for:
```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OrgMember", policy =>
        policy.RequireAuthenticatedUser());
});
```

## Expected Behavior

When clicking on a team member:
1. JavaScript reads `data-user-id` and `data-user-name`
2. Calls `openDirectThread(userId, userName)`
3. Makes POST to `/api/dm/threads?orgId=1` with body `{ "otherUserId": userId }`
4. API creates/finds thread
5. Returns JSON: `{ "success": true, "thread": { ... } }`
6. JavaScript joins SignalR group
7. Loads messages
8. UI updates

## Current Status

✅ Backend entities created
✅ Database migration applied
✅ Service layer implemented
✅ API controller created
✅ SignalR hub created
✅ Frontend JS integrated
⚠️ UserId = 0 issue
⚠️ API returning HTML issue
❌ Not fully working yet

