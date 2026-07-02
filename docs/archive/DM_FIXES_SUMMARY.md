# Direct Messaging - All Fixes Applied

## Summary
Fixed both the UserId=0 issue and the SignalR DirectHub disconnection issue.

## Fix 1: User ID showing as 0 ✅

### File: `Certio.Web/Controllers/HomeController.cs` (Line 1402)

**Problem:** `UserOrganization.UserId` field is 0 in the database, causing all team members to show `data-user-id="0"`.

**Solution:** Use the actual User entity's Id instead:
```csharp
// Before:
UserId = uo.UserId,  // This was 0

// After:
UserId = uo.User.Id,  // Uses the related User entity's Id
```

## Fix 2: DirectHub SignalR Disconnection ✅

### File: `Certio.Web/Hubs/DirectHub.cs`

**Problem:** DirectHub was disconnecting immediately with error:
```
Error: Connection disconnected with error 'Error: Server returned an error on close: Connection closed with an error.'
```

**Root Cause:** 
- `GetCurrentUserId()` and `GetCurrentOrganizationId()` were throwing exceptions
- `OnConnectedAsync()` was calling these methods and crashing the connection
- Methods returned `int` instead of `int?` (nullable)

**Solutions Applied:**

### 2a. Made helper methods return nullable types
```csharp
// Before:
private int GetCurrentUserId()
{
    var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
    {
        throw new UnauthorizedAccessException("User ID not found in claims");
    }
    return userId;
}

// After:
private int? GetCurrentUserId()
{
    var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var userId))
    {
        return userId;
    }
    return null;  // No exception, just return null
}
```

### 2b. Added try-catch to OnConnectedAsync
```csharp
public override async Task OnConnectedAsync()
{
    try
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("User {UserId} connected to DirectHub", userId);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in DirectHub OnConnectedAsync");
    }
    await base.OnConnectedAsync();  // Connection still succeeds
}
```

### 2c. Updated all Hub methods to check for null
```csharp
public async Task JoinThread(string threadId)
{
    // ... validation ...
    
    var userId = GetCurrentUserId();
    var orgId = GetCurrentOrganizationId();

    // NEW: Check for null before using
    if (!userId.HasValue || !orgId.HasValue)
    {
        await Clients.Caller.SendAsync("Error", "Authentication required");
        return;
    }

    // Use .Value to access the int
    var isParticipant = await _directMessageService.IsParticipantAsync(orgId.Value, userId.Value, threadGuid, Context.ConnectionAborted);
    // ...
}
```

Applied to all methods:
- ✅ `JoinThread()`
- ✅ `SendMessage()`
- ✅ `Typing()`
- ✅ `MarkRead()`

## Testing Steps

### 1. Restart the Application
The hub changes require a server restart:
```powershell
# Stop if running
# Start the app
```

### 2. Hard Refresh the Browser
Clear cache and reload:
```
Ctrl + Shift + R  (or Ctrl + F5)
```

### 3. Check Console Logs
Should now see:
```
✅ Direct Messaging SignalR Connected
❌ NO disconnection error
```

### 4. Check User IDs in HTML
Inspect a team member in DevTools:
```html
✅ <div class="team-member dm-user-item" data-user-id="1" data-user-name="Kyle Wang">
❌ NOT data-user-id="0"
```

### 5. Click a Team Member
Click on any team member and check:
- Console should show correct user ID (not 0)
- Network tab should show POST to `/api/dm/threads`
- Check if API still returns HTML or now returns JSON

## Expected Results

After these fixes:

1. **SignalR DirectHub**: ✅ Should connect and stay connected
2. **User IDs**: ✅ Should show correct IDs (1, 3, 5, 8, etc.)
3. **API Call**: Still needs investigation - may return HTML due to authorization issue

## Next Issue: API Authorization

If the API still returns HTML instead of JSON, it's an authorization issue with `DirectMessagesController`.

**Check:**
1. Is "OrgMember" policy defined in `Program.cs`?
2. Does the user have required claims?
3. Try temporarily removing `[Authorize(Policy = "OrgMember")]` to test

## All Modified Files

1. ✅ `Certio.Web/Controllers/HomeController.cs` - Fixed UserId mapping
2. ✅ `Certio.Web/Hubs/DirectHub.cs` - Fixed SignalR connection
3. ✅ `Certio.Web/Views/Home/Communications.cshtml` - Already has clickable DM users
4. ✅ `Certio.Web/wwwroot/js/direct-messages.js` - Already integrated with UI
5. ✅ `Certio.Web/ViewModels/CommunicationsViewModel.cs` - Has UserId property

## Pattern Match with ChatHub

DirectHub now follows the same pattern as the working ChatHub:
- ✅ Returns `int?` from helper methods (not `int`)
- ✅ No exceptions thrown on connection
- ✅ Null checks before using values
- ✅ Try-catch in OnConnectedAsync

This should resolve the SignalR disconnection issue!

