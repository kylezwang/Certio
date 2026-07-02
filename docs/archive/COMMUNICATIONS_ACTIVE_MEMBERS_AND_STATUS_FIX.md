# Communications Active Members & Status Indicators Fix

## Summary
Fixed two critical issues with the Communications sidebar and main Communications/MatterCommunications pages:
1. **Active Members Filtering**: Now properly filters members based on actual channel access instead of showing all organization members
2. **Status Indicators**: Implemented real-time online/offline status detection using the UserPresenceService

## Key Issue Resolved
The initial implementation showed no profile icons in matter channels because it only checked `ConversationParticipants`. However, matter channels are automatically accessible to users assigned to the matter via `MatterAssignments`, not through explicit conversation participation. The fix now correctly queries `MatterAssignments` (and `MatterPermissions` for specific-access matters) to show the proper list of users who have access to each matter channel.

## Changes Made

### Backend Changes

#### 1. CommunicationsController.cs
**File**: `Certio.Web/Controllers/CommunicationsController.cs`

**Added UserPresenceService Injection**:
```csharp
private readonly IUserPresenceService _userPresenceService;

public CommunicationsController(
    // ... other parameters
    IUserPresenceService userPresenceService,
    // ...
)
{
    _userPresenceService = userPresenceService;
}
```

**Updated GetChannelsJson** to include real-time online status:
```csharp
// Get team members with online status
var orgTeamMembers = await _channelManagementService.GetOrganizationTeamMembersAsync(orgId);
var onlineUserIds = _userPresenceService.GetOnlineUsersInOrganization(orgId);

// Update online status for team members
foreach (var member in orgTeamMembers)
{
    member.Status = onlineUserIds.Contains(member.UserId) ? "online" : "offline";
}
```

**Added New GetChannelMembers Endpoint**:
```csharp
[Authorize(Policy = "OrgMember")]
[HttpGet("/Client/{orgId:int}/Communications/GetChannelMembers")]
public async Task<IActionResult> GetChannelMembers(int orgId, int channelId)
{
    // Returns actual channel members with online status
    // For matter channels: Shows users assigned to the matter via MatterAssignments
    // For non-matter channels: Shows conversation participants
    
    var conversation = await _db.Conversations
        .Include(c => c.Participants)
        .Include(c => c.Matter)
            .ThenInclude(m => m.Assignments)
        .Include(c => c.Matter)
            .ThenInclude(m => m.Permissions)
        .FirstOrDefaultAsync(c => c.Id == channelId && c.OrganizationId == orgId);
    
    // Get online status
    var onlineUserIds = _userPresenceService.GetOnlineUsersInOrganization(orgId);
    
    if (conversation.MatterId.HasValue && conversation.Matter != null)
    {
        // Matter channel: Show users from MatterAssignments (not removed)
        var assignedUsers = conversation.Matter.Assignments
            .Where(a => a.RemovedAt == null && a.User != null && a.User.IsActive);
            
        // Also include users with specific permissions if AccessLevel is "Specific"
        if (conversation.Matter.AccessLevel == "Specific")
        {
            var permissionedUsers = conversation.Matter.Permissions
                .Where(p => p.RevokedAt == null && p.User != null && p.User.IsActive);
        }
    }
    else
    {
        // Non-matter channel: Show conversation participants
        var participants = conversation.Participants
            .Where(p => p.User != null && p.User.IsActive);
    }
}
```

### Frontend Changes

#### 2. communications-sidebar.js
**File**: `Certio.Web/wwwroot/js/communications-sidebar.js`

**Updated renderActiveMembers** to fetch actual channel members:
```javascript
async function renderActiveMembers(dmUserId, dmUserName) {
    if (commsSidebarState.currentChannelType === 'dm') {
        // Show two DM participants with status
    } else {
        // For channels, fetch actual members from API
        const response = await fetch(
            `/Client/${commsSidebarState.organizationId}/Communications/GetChannelMembers?orgId=${commsSidebarState.organizationId}&channelId=${commsSidebarState.currentChannelId}`
        );
        
        const result = await response.json();
        if (result.success && result.members) {
            // Display members with status indicators
            result.members.forEach(member => {
                html += `
                    <div class="member-avatar-small" title="${member.name}">
                        ${member.avatar}
                        <div class="status-indicator ${member.status}"></div>
                    </div>
                `;
            });
        }
    }
}
```

**Updated SignalR initialization** to track online/offline events:
```javascript
function initializeCommsSignalR() {
    commsSidebarState.signalRConnection = new signalR.HubConnectionBuilder()
        .withUrl(`/hubs/chat?orgId=${commsSidebarState.organizationId}`)
        .withAutomaticReconnect()
        .build();
    
    // Handle user online/offline status
    commsSidebarState.signalRConnection.on("UserOnline", function(data) {
        updateUserStatus(data.UserId, 'online');
    });
    
    commsSidebarState.signalRConnection.on("UserOffline", function(data) {
        updateUserStatus(data.UserId, 'offline');
    });
}
```

**Added updateUserStatus function**:
```javascript
function updateUserStatus(userId, status) {
    // Update in team members state
    const member = commsSidebarState.teamMembers.find(m => m.userId == userId);
    if (member) {
        member.status = status;
    }
    
    // Update active members display
    if (commsSidebarState.currentView === 'chat') {
        renderActiveMembers();
    }
    
    // Update DM list status indicators
    const dmItem = document.querySelector(`.comms-dm-item[data-user-id="${userId}"]`);
    if (dmItem) {
        const statusIndicator = dmItem.querySelector('.comms-status-indicator');
        if (statusIndicator) {
            statusIndicator.className = `comms-status-indicator ${status}`;
        }
    }
}
```

#### 3. direct-messages.js
**File**: `Certio.Web/wwwroot/js/direct-messages.js`

**Added online/offline status handlers**:
```javascript
function setupDirectMessageHandlers() {
    // ... existing handlers
    
    // Handle user online/offline status
    directConnection.on("UserOnline", function (data) {
        if (typeof updateUserStatus === 'function') {
            updateUserStatus(data.UserId, 'online');
        }
    });

    directConnection.on("UserOffline", function (data) {
        if (typeof updateUserStatus === 'function') {
            updateUserStatus(data.UserId, 'offline');
        }
    });
}
```

#### 4. communications.js
**File**: `Certio.Web/wwwroot/js/communications.js`

**Already had UserOnline/UserOffline handlers** (no changes needed):
```javascript
// User online
communicationsConnection.on("UserOnline", function (data) {
    updateUserStatus(data.UserId, 'online');
});

// User offline
communicationsConnection.on("UserOffline", function (data) {
    updateUserStatus(data.UserId, 'offline');
});

// Update user status function
function updateUserStatus(userId, status) {
    const memberElements = document.querySelectorAll(`[data-user-id="${userId}"]`);
    memberElements.forEach(element => {
        const statusIndicator = element.querySelector('.status-indicator');
        if (statusIndicator) {
            statusIndicator.className = `status-indicator ${status}`;
        }
    });
}
```

## How It Works

### Active Members Filtering
1. When a channel is selected, `renderActiveMembers()` is called
2. For DM channels, it shows just the two participants
3. For matter channels, it fetches users via `/GetChannelMembers` API which:
   - Queries `MatterAssignments` to get users assigned to the matter (where `RemovedAt IS NULL`)
   - Includes users with `MatterPermissions` if the matter's `AccessLevel` is "Specific" (where `RevokedAt IS NULL`)
   - Deduplicates users who have both assignments and permissions
4. For non-matter channels, it fetches conversation participants from `ConversationParticipants`
5. Only active users are displayed

### Status Indicators
1. **Backend**: `UserPresenceService` tracks all active SignalR connections
2. When user connects: `SetUserOnline()` is called, adds to presence tracking
3. When user disconnects: `SetUserOffline()` is called, removes from tracking
4. `GetOnlineUsersInOrganization()` returns list of currently online user IDs

5. **Frontend**: SignalR broadcasts `UserOnline`/`UserOffline` events to all connected clients
6. JavaScript handlers update status indicators in real-time:
   - Active Members section (sidebar)
   - DM list (sidebar)
   - Team members list (main page)

### Status Indicator Styling
The status indicators use these CSS classes:
- `.status-indicator` - Base class (10px circle, positioned bottom-right of avatar)
- `.status-indicator.online` - Green (#10b981)
- `.status-indicator.offline` - Gray (#6b7280)
- `.status-indicator.away` - Orange (#f59e0b)

## Testing Checklist

### Active Members Filtering
- [ ] Open Communications sidebar, select a **matter channel**
- [ ] Verify Active Members shows users assigned to that matter via MatterAssignments
- [ ] If the matter has AccessLevel = "Specific", verify users with MatterPermissions also appear
- [ ] Select a **non-matter channel** (e.g., General, Firm Updates)
- [ ] Verify Active Members shows conversation participants
- [ ] Open a **DM**, verify Active Members shows only the two participants
- [ ] Check that member count is accurate (shows +N if more than 5 members)
- [ ] Verify profile icons appear (not empty) for all channel types

### Status Indicators
- [ ] Have two users log in from different browsers/devices
- [ ] Verify status indicators show green for online users
- [ ] When one user logs out, verify their status turns gray in real-time
- [ ] Check status indicators in:
  - [ ] Sidebar Active Members section
  - [ ] Sidebar DM list
  - [ ] Main Communications page team members
  - [ ] Matter Communications tab team members
- [ ] Refresh page, verify status persists correctly

### Cross-Page Consistency
- [ ] Open Communications sidebar, note user statuses
- [ ] Navigate to main Communications page
- [ ] Verify same users show same online/offline status
- [ ] Open Matter Communications tab
- [ ] Verify status consistency across all three views

## Technical Notes

### UserPresenceService Architecture
- Uses `ConcurrentDictionary` for thread-safe presence tracking
- Each SignalR connection is tracked with userId and organizationId
- Users can have multiple connections (multiple tabs/devices)
- User is considered offline only when ALL connections are closed

### SignalR Event Flow
```
User Connects → ChatHub.OnConnectedAsync()
              → UserPresenceService.SetUserOnline()
              → Broadcast "UserOnline" to org group
              → All clients update UI

User Disconnects → ChatHub.OnDisconnectedAsync()
                 → UserPresenceService.SetUserOffline()
                 → Broadcast "UserOffline" to org group
                 → All clients update UI
```

### Performance Considerations
- Active members API call is only made when selecting a channel (not real-time)
- Status updates are real-time via SignalR (no polling)
- Presence data stored in-memory (fast, no database queries)
- Status indicators use CSS classes (no inline styles)

## Related Files
- `Certio.Web/Controllers/CommunicationsController.cs`
- `Certio.Web/Services/UserPresenceService.cs`
- `Certio.Web/Hubs/ChatHub.cs`
- `Certio.Web/wwwroot/js/communications-sidebar.js`
- `Certio.Web/wwwroot/js/communications.js`
- `Certio.Web/wwwroot/js/direct-messages.js`
- `Certio.Web/wwwroot/css/communications-sidebar.css`

## Future Enhancements
- Add "away" status for users idle for 5+ minutes
- Show "typing..." indicator in Active Members section
- Add hover tooltips showing last seen time for offline users
- Consider caching channel members on frontend to reduce API calls

