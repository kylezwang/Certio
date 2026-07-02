# Communications Status Text and DM Fixes

## Summary
Fixed multiple issues with status indicators and direct messaging:
1. **Status Text**: Changed from "Available" to "Online" for online users and "Offline" for offline users
2. **Real-Time Status Updates**: Status text now updates live via SignalR
3. **DM with Self**: Fixed duplicate profile icons when DMing yourself
4. **Current User Avatar Styling**: Ensured consistency across all pages

## Issues Fixed

### 1. Status Text Showing "Available" Instead of "Online"/"Offline"

**Problem**: All users showed "Available" as their activity status, even when offline (gray indicator).

**Root Cause**: Backend services and controllers were hardcoding "Available" as the activity text.

**Fix**:
- Updated `ChannelManagementService.cs` to set `Activity = "Offline"` by default
- Updated `HomeController.cs` to set activity based on online status:
  ```csharp
  Activity = isOnline ? "Online" : "Offline"
  ```
- Updated `updateUserStatus()` in `communications-sidebar.js` to update activity text when status changes:
  ```javascript
  member.activity = status === 'online' ? 'Online' : 'Offline';
  ```

### 2. Status Text Not Updating in Real-Time

**Problem**: When users came online/offline, their status indicator changed but the "Available"/"Offline" text didn't update.

**Root Cause**: The `updateUserStatus` function only updated the status indicator CSS class, not the activity text element.

**Fix**: Updated `updateUserStatus()` in `communications-sidebar.js`:
```javascript
function updateUserStatus(userId, status) {
    // Update in team members state
    const member = commsSidebarState.teamMembers.find(m => m.userId == userId);
    if (member) {
        member.status = status;
        member.activity = status === 'online' ? 'Online' : 'Offline';
    }
    
    // Update active members display if currently visible
    if (commsSidebarState.currentView === 'chat') {
        renderActiveMembers();
    }
    
    // Update DM list status indicators and activity text
    const dmItem = document.querySelector(`.comms-dm-item[data-user-id="${userId}"]`);
    if (dmItem) {
        const statusIndicator = dmItem.querySelector('.comms-status-indicator');
        if (statusIndicator) {
            statusIndicator.className = `comms-status-indicator ${status}`;
        }
        // NEW: Update activity text
        const activityText = dmItem.querySelector('.comms-dm-activity');
        if (activityText) {
            activityText.textContent = status === 'online' ? 'Online' : 'Offline';
        }
    }
}
```

### 3. Duplicate Profile Icons When DMing Yourself

**Problem**: When opening a direct message with yourself, the Active Members section showed your profile icon twice.

**Root Cause**: The `renderActiveMembers()` function always added both "current user" and "other user" avatars, even when they were the same person.

**Fix**: Added logic to detect DM with self:
```javascript
if (commsSidebarState.currentChannelType === 'dm') {
    // Don't duplicate if DM with self
    if (dmUserId && dmUserId != commsSidebarState.currentUserId) {
        // Show current user avatar
        // Show other user avatar
    } else {
        // DM with self - show only one avatar
        html += `
            <div class="member-avatar-small" title="${commsSidebarState.currentUserName || 'You'} (Self)">
                ${currentUserInitials}
                <div class="status-indicator ${currentUserStatus}"></div>
            </div>
        `;
    }
}
```

### 4. Current User Avatar Styling

**Issue**: User mentioned "our sidebar current user profile icon should match the Communications and MatterCommunications one"

**Implementation**: The Communications page uses a special `.current-user-avatar` class with a linear gradient background:
```css
.team-member[data-user-id] .member-avatar.current-user-avatar {
  background: linear-gradient(135deg, #a32b43, #3d1019);
  font-size: 0.875rem;
}
```

This styling is consistent across:
- Main Communications page (`/Client/{orgId}/Communications`)
- Matter Communications tab (`_MatterCommunications.cshtml`)
- Communications sidebar (current user's messages use `current-user-message` class)

## Files Modified

### Backend
1. **`Certio.Web/Services/ChannelManagementService.cs`**
   - Changed `Activity = "Available"` to `Activity = "Offline"` (lines 130, 159)

2. **`Certio.Web/Controllers/HomeController.cs`**
   - Changed `Activity = isOnline ? "Available" : $"Last seen..."` to `Activity = isOnline ? "Online" : "Offline"` (2 occurrences)

### Frontend
3. **`Certio.Web/wwwroot/js/communications-sidebar.js`**
   - Updated `updateUserStatus()` to update activity text in DM list
   - Updated `renderActiveMembers()` to handle DM with self (no duplication)

## Testing Checklist

### Status Text
- [ ] Open sidebar, verify offline users show "Offline" (gray indicator)
- [ ] Have another user log in, verify their status changes to "Online" (green indicator)
- [ ] Verify the text updates in real-time without page refresh
- [ ] Check both the DM list and any other status displays

### DM with Self
- [ ] Open sidebar, click on your own name in Direct Messages list
- [ ] Verify Active Members section shows only ONE profile icon with "(Self)" tooltip
- [ ] Verify no duplicate icons appear

### Status Consistency
- [ ] Compare status indicators across:
  - Communications sidebar DM list
  - Main Communications page team members
  - Matter Communications tab
- [ ] Verify all three show consistent online/offline status

### Real-Time Updates
- [ ] Have two users logged in (different browsers/devices)
- [ ] User A goes offline (logs out)
- [ ] Verify User B sees User A's status change from "Online" (green) to "Offline" (gray) immediately
- [ ] Verify User B sees the text change from "Online" to "Offline"

## Technical Implementation

### Status Flow
```
User Connects → ChatHub.OnConnectedAsync()
              → UserPresenceService.SetUserOnline()
              → SignalR broadcasts "UserOnline" event
              → updateUserStatus(userId, 'online') called on all clients
              → Status indicator turns green
              → Activity text changes to "Online"

User Disconnects → ChatHub.OnDisconnectedAsync()
                 → UserPresenceService.SetUserOffline()
                 → SignalR broadcasts "UserOffline" event
                 → updateUserStatus(userId, 'offline') called on all clients
                 → Status indicator turns gray
                 → Activity text changes to "Offline"
```

### Status Text Display Locations
1. **Sidebar DM List** (`.comms-dm-activity`):
   - Shows "Online" or "Offline"
   - Updates in real-time via `updateUserStatus()`

2. **Main Communications Page** (team members section):
   - Shows "Online" or "Offline"
   - Updates in real-time via existing `updateUserStatus()` function in `communications.js`

3. **Matter Communications Tab**:
   - Uses same `communications.js` as main page
   - Inherits all real-time status functionality

## Related Documents
- `COMMUNICATIONS_ACTIVE_MEMBERS_AND_STATUS_FIX.md` - Active members filtering and status indicators
- `DM_FINAL_STATUS.md` - Direct messaging implementation
- `DIRECT_MESSAGING_IMPLEMENTATION_SUMMARY.md` - Overall DM architecture

## Notes
- Status colors are defined in CSS:
  - `.status-indicator.online` → Green (#10b981)
  - `.status-indicator.offline` → Gray (#6b7280)
- Activity text is plain text ("Online" or "Offline"), not "Last seen X minutes ago"
- Current user messages use `.current-user-message` class for special styling
- Current user avatar in main Communications page uses `.current-user-avatar` class with gradient background

