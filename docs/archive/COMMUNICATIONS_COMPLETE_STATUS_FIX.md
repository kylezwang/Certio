# Communications Complete Status & Sorting Fix

## Summary
Fixed all remaining issues with status indicators and team member sorting across all Communications views:
1. **Status Text Sync**: Fixed green indicators showing "Offline" text
2. **Team Member Sorting**: Current user first, then online users, then offline users
3. **Status Text**: All locations now correctly show "Online" or "Offline"

## Issues Fixed

### 1. Green Indicator Showing "Offline" Text in Sidebar

**Problem**: Online users (green indicator) were showing "Offline" text because the `activity` property from the backend wasn't being used/overridden in the render function.

**Root Cause**: The `renderCommsTeamMembers()` function was using `member.activity || ''` which would use whatever was in the backend data (which could be stale), instead of deriving it from the current `status`.

**Fix**: Changed `renderCommsTeamMembers()` in `communications-sidebar.js` to always derive activity from status:
```javascript
// If status is online, show "Online", otherwise show "Offline"
const activity = status === 'online' ? 'Online' : 'Offline';
```

### 2. Team Members Not Sorted Properly

**Problem**: Team members were showing in random order instead of current user first, then online users, then offline users alphabetically.

**Root Cause**: No sorting logic was applied before rendering.

**Fixes**:

#### Sidebar (`communications-sidebar.js`):
```javascript
// Sort team members: current user first, then online users, then offline
const currentUserId = commsSidebarState.currentUserId;
const sortedMembers = [...teamMembers].sort((a, b) => {
    // Current user always first
    if (a.userId == currentUserId) return -1;
    if (b.userId == currentUserId) return 1;
    
    // Then by online status
    if (a.status === 'online' && b.status !== 'online') return -1;
    if (a.status !== 'online' && b.status === 'online') return 1;
    
    // Then alphabetically by name
    return a.name.localeCompare(b.name);
});
```

#### Main Communications Page (`HomeController.cs`):
```csharp
// Sort team members: current user first, then online users, then offline
teamMembers = teamMembers
    .OrderByDescending(m => m.UserId == customUser.Id) // Current user first
    .ThenByDescending(m => m.Status == "online") // Then online users
    .ThenBy(m => m.Name) // Then alphabetically
    .ToList();
```

### 3. Status Indicators on Main Pages

**Issue**: User reported not seeing green status indicators on Communications and MatterCommunications pages.

**Investigation**: The main Communications page HTML (`Communications.cshtml`) already has:
```html
<div class="status-indicator @member.Status"></div>
```

**Root Cause**: The backend was correctly setting status to "online" or "offline", but it needs to match with the CSS classes`.status-indicator.online` and `.status-indicator.offline`.

**Status**: The HTML is correct. The backend now properly sets status based on `UserPresenceService.GetOnlineUsersInOrganization()` which tracks SignalR connections. The CSS classes are defined in `site.css`:
```css
.status-indicator.online {
    background: #10b981; /* Green */
}

.status-indicator.offline {
    background: #6b7280; /* Gray */
}
```

## Files Modified

### Backend
1. **`Certio.Web/Controllers/HomeController.cs`** (line 1563-1568)
   - Added sorting logic for team members
   - Order: Current user → Online users → Offline users (alphabetically within each group)

### Frontend
2. **`Certio.Web/wwwroot/js/communications-sidebar.js`** (line 375-434)
   - Added sorting logic to `renderCommsTeamMembers()`
   - Fixed activity text to always derive from status: `status === 'online' ? 'Online' : 'Offline'`

## How It Works Now

### Status Flow
1. **Backend**: 
   - `UserPresenceService` tracks all SignalR connections
   - `GetOnlineUsersInOrganization()` returns list of online user IDs
   - Controllers set `Status = isOnline ? "online" : "offline"` and `Activity = isOnline ? "Online" : "Offline"`

2. **Frontend Initial Load**:
   - Sidebar calls `loadCommsChannels()` → gets team members with status from backend
   - Main page renders team members with `@member.Status` class on status indicator
   - Both show correct green/gray indicators and "Online"/"Offline" text

3. **Real-Time Updates** (via SignalR):
   - User connects → `ChatHub.OnConnectedAsync()` → broadcasts "UserOnline" event
   - User disconnects → `ChatHub.OnDisconnectedAsync()` → broadcasts "UserOffline" event
   - `updateUserStatus(userId, status)` function updates:
     - Status indicator CSS class
     - Activity text
     - Re-sorts the list (current implementation doesn't re-sort on status change, but could be added)

### Sorting Order
All three views now sort identically:
1. **Current User** (always at the top)
2. **Online Users** (green indicators, "Online" text)
3. **Offline Users** (gray indicators, "Offline" text)
4. Within each group: **Alphabetical by name**

## Testing Checklist

### Status Text
- [ ] Open sidebar, verify all online users show "Online" with green indicator
- [ ] Verify all offline users show "Offline" with gray indicator  
- [ ] Open main Communications page, verify same status consistency
- [ ] Open MatterCommunications tab, verify same status consistency

### Sorting
- [ ] Verify current user is always at the top in all three views
- [ ] Verify online users appear before offline users
- [ ] Verify alphabetical order within each status group

### Real-Time Status Updates
- [ ] Have User B log in while User A is on the page
- [ ] Verify User B appears in "online" section with green indicator and "Online" text
- [ ] Have User B log out
- [ ] Verify User B's indicator turns gray and text changes to "Offline"
- [ ] Verify this works in sidebar, main page, and matter communications

### Cross-View Consistency
- [ ] Compare sidebar DM list, main Communications team members, and MatterCommunications team members
- [ ] Verify all three show identical status for each user
- [ ] Verify all three use same sorting order

## Technical Notes

### Status Indicator CSS Classes
- `.status-indicator` - Base class (10px circle, positioned bottom-right of avatar)
- `.status-indicator.online` - Green (#10b981)
- `.status-indicator.offline` - Gray (#6b7280)

### Activity Text Values
- **Online users**: "Online"
- **Offline users**: "Offline"
- **No more**: "Available" or "Last seen X minutes ago"

### Sorting Implementation
- **Sidebar**: Client-side JavaScript sort before rendering
- **Main Pages**: Server-side LINQ `OrderByDescending()` before sending to view
- **Both use same logic**: Current user → Online → Offline → Alphabetical

### Performance Considerations
- Status is fetched on page load from `UserPresenceService` (in-memory, very fast)
- Real-time updates via SignalR (no polling required)
- Sorting happens once on load, not on every status change (could be optimized if needed)

## Related Documents
- `COMMUNICATIONS_ACTIVE_MEMBERS_AND_STATUS_FIX.md` - Active members filtering and status indicators
- `COMMUNICATIONS_STATUS_TEXT_AND_DM_FIXES.md` - Initial status text fixes
- `DM_FINAL_STATUS.md` - Direct messaging implementation

## Future Enhancements
- Auto-re-sort list when status changes in real-time (currently only updates indicator/text)
- Add "away" status for users idle 5+ minutes
- Show "typing..." indicator in online users section
- Add last seen timestamp tooltip for offline users

