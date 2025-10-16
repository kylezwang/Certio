# Direct Messaging - Relationship Users Implementation

## Summary

Extended the Direct Messages list to show:
1. ✅ **Law Firm Users** - Can DM each other (same organization)
2. ✅ **Client Organization Users** - Visible but cannot DM (different organization)

## Changes Made

### 1. ViewModel Updated (`CommunicationsViewModel.cs`)

Added properties to `CommunicationsTeamMember`:
```csharp
public int OrganizationId { get; set; }
public bool CanDirectMessage { get; set; } = true;
public string? OrganizationName { get; set; }
```

### 2. Controller Logic (`HomeController.cs`)

#### Current Organization Users
- Query `UserOrganizations` for the current org
- Set `CanDirectMessage = true`
- Show with normal styling

#### Related Organization Users (Law Firms Only)
- Query `OrganizationRelationships` for LawFirmClient relationships
- Load users from each client organization
- Set `CanDirectMessage = false`
- Show with different icon (building) and org name appended

```csharp
// Get client relationships
var clientRelationships = await _context.OrganizationRelationships
    .Include(or => or.TargetOrganization)
    .Where(or => or.SourceOrganizationId == organizationId && 
                 or.IsActive &&
                 or.RelationshipType == RelationshipTypes.LawFirmClient)
    .ToListAsync();

// Load users from each client org
foreach (var relationship in clientRelationships)
{
    var clientUsers = await _context.UserOrganizations
        .Include(uo => uo.User)
        .Where(uo => uo.OrganizationId == clientOrgId && uo.IsActive...)
        .ToListAsync();
}
```

### 3. View Updates (`Communications.cshtml`)

#### Visual Indicators
- **Can DM**: Normal opacity, pointer cursor, no badge
- **Cannot DM**: 60% opacity, not-allowed cursor, ban icon (🚫)
- **Organization Name**: Shown below activity for related org users

```html
<div class="team-member @(member.CanDirectMessage ? "dm-user-item" : "dm-user-disabled")" 
     data-can-dm="@member.CanDirectMessage.ToString().ToLower()"
     data-org-name="@member.OrganizationName"
     style="cursor: @(member.CanDirectMessage ? "pointer" : "not-allowed"); 
            opacity: @(member.CanDirectMessage ? "1" : "0.6");">
```

#### Click Handler
- Checks `data-can-dm` attribute
- If `false`, shows notification: "Cannot send direct messages to [User] ([Org])"
- If `true`, proceeds to open DM thread

### 4. User Experience

#### For Law Firm Members
**Sidebar shows:**
```
DIRECT MESSAGES — 5 ONLINE

✅ Kyle Wang (Partner)          [Can DM]
✅ Associate User (Associate)   [Can DM]
✅ David Wang (Manager)          [Can DM]
🚫 Personal Chloe (Member)      [Cannot DM - Acme Corp]
🚫 Client User (Admin)           [Cannot DM - Tech Startup Inc]
```

#### Click Behavior
- **Kyle Wang**: Opens DM thread ✅
- **Personal Chloe**: Shows warning message ⚠️

## Benefits

1. **Visibility**: Users can see all people they work with
2. **Context**: Organization names help identify client users
3. **Clarity**: Visual indicators prevent confusion
4. **Security**: Permission checks still enforced on backend

## Backend Security

Even though related org users appear in the list, the backend still enforces:

```csharp
// DirectMessageService.GetOrCreateThreadAsync()
var isCurrentUserMember = await _permissionService.IsOrganizationMemberAsync(currentUserId, orgId);
var isOtherUserMember = await _permissionService.IsOrganizationMemberAsync(otherUserId, orgId);

if (!isCurrentUserMember || !isOtherUserMember)
{
    throw new UnauthorizedOperationException("Both users must be organization members");
}
```

So even if someone bypasses the frontend, the API will reject the request.

## Future Enhancement Option

If you want to **enable cross-org DMs** later, you would:

1. Change `CanDirectMessage` logic to check for relationship
2. Update `DirectMessageService` to allow threads across relationships
3. Store both organizations in `DirectThread` table
4. Update permission checks to allow relationship-based DMs

For now, this provides visibility without breaking security!

## Testing

1. **Refresh the browser** (Ctrl+Shift+R)
2. **Check the sidebar** - should show both law firm and client users
3. **Click law firm member** - should open DM ✅
4. **Click client member** - should show warning ⚠️
5. **Check console** - should see DEBUG output showing all users loaded

## Console Output Example

```
[DEBUG] Found 4 users in current org 1
[DEBUG] Found 2 client relationships
[DEBUG] Found 3 users in client org 2 (Acme Corp)
[DEBUG] Found 1 users in client org 3 (Tech Startup Inc)
[DEBUG] Current org member: UserId=1, Name=Kyle Wang, CanDM=true
[DEBUG] Current org member: UserId=3, Name=Associate User, CanDM=true
[DEBUG] Current org member: UserId=5, Name=Another User, CanDM=true
[DEBUG] Current org member: UserId=8, Name=David Wang, CanDM=true
[DEBUG] Related org member: UserId=6, Name=Personal Chloe, Org=Acme Corp, CanDM=false
[DEBUG] Related org member: UserId=7, Name=Client User, Org=Acme Corp, CanDM=false
[DEBUG] Related org member: UserId=9, Name=Startup Admin, Org=Tech Startup Inc, CanDM=false
[DEBUG] Final teamMembers count: 7
```

## Summary

✅ Law firm users can see and DM each other
✅ Client users are visible but grayed out
✅ Clear visual indicators show who can be DMed
✅ Backend security unchanged
✅ Notifications explain why some users can't be DMed

