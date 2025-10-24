# Communications Sidebar Implementation

## Overview
A duplicate sidebar chat system has been implemented for the Communications page functionality. This sidebar allows users to view channels, direct messages, and chat with team members directly from any page in the application.

## Features Implemented

### 1. **Sidebar Toggle Buttons**
- **Sparkles Button (AI Chat)**: Toggles the AI chat sidebar on the right
- **Comments Button (Communications)**: Toggles the communications sidebar on the right
- Only one sidebar can be open at a time
- Clicking an active button will close that sidebar
- Clicking an inactive button will switch to that sidebar

### 2. **Communications Sidebar Structure**

#### Channel List View
- Shows all communication channels organized by categories:
  - **Text Channels**: Public channels for general communication
  - **Matter Channels**: Channels associated with specific legal matters
  - **Private Channels**: Private/restricted channels
  - **Direct Messages**: One-on-one conversations with team members

- Category toggles to expand/collapse sections
- Channel icons indicate type (hashtag, folder, lock)
- Unread message badges
- Active member status indicators (online/offline/away)

#### Chat View
- Channel header with back button to return to channel list
- Channel name with appropriate icon
- Action buttons: Voice Call, Video Call, Search, Settings
- Active Members section showing online users
- Messages list with:
  - User avatars
  - Message grouping (messages within 15 minutes from same user)
  - Timestamps
  - Current user highlighting
  - Message reactions support
- Message input with:
  - File attachment button
  - Emoji picker button
  - Auto-resizing textarea
  - Send button

### 3. **File Structure**

#### New Files Created:
1. **`Certio.Web/wwwroot/css/communications-sidebar.css`**
   - Complete styling for the communications sidebar
   - Responsive design
   - Smooth animations and transitions
   - Theme-consistent colors and typography

2. **`Certio.Web/wwwroot/js/communications-sidebar.js`**
   - Channel loading and rendering
   - Message display and sending
   - SignalR integration for real-time updates
   - View switching logic (channels ↔ chat)
   - Event handlers for all interactions

#### Modified Files:
1. **`Certio.Web/Views/Shared/_ClientLayout.cshtml`**
   - Added communications sidebar HTML structure
   - Added user/org context data attributes
   - Linked CSS and JavaScript files
   - Updated header button logic for sidebar switching
   - Prevented display on actual Communications page

## How It Works

### User Flow:
1. User clicks the **Comments button** in the header
2. Communications sidebar slides in from the right
3. Channels list is displayed, organized by categories
4. User clicks a channel to view messages
5. Chat view is displayed with message history and input
6. User can click the back arrow to return to channels list
7. User can click the Comments button again to close the sidebar
8. User can click the **Sparkles button** to switch to AI chat

### Technical Flow:
1. **Initialization** (`communications-sidebar.js`):
   - Gets organization and user info from `#appContext` data attributes
   - Sets up event listeners for buttons and inputs
   - Initializes SignalR connection for real-time updates

2. **Channel Loading**:
   - Fetches channels from `/Client/{orgId}/Communications`
   - Parses HTML to extract channel data
   - Renders channels grouped by category
   - Sets up click handlers for channel selection

3. **Message Loading**:
   - When channel is selected, fetches messages from API
   - Renders messages with proper grouping and formatting
   - Scrolls to bottom of message list
   - Displays demo messages if API is not available

4. **Message Sending**:
   - User types message and presses Enter or clicks Send
   - POST request to `/api/communications/channels/{channelId}/messages`
   - Message is added to UI immediately
   - SignalR broadcasts message to other users in real-time

5. **Sidebar State Management**:
   - `localStorage` tracks which sidebar is active ('ai', 'comms', or 'none')
   - State persists across page refreshes
   - Only one sidebar can be active at a time
   - Visual active states on header buttons

## Integration Points

### Data Sources:
- **Channels**: Loaded from existing Communications page HTML
- **Messages**: API endpoint `/api/communications/channels/{channelId}/messages`
- **Team Members**: Extracted from Communications page HTML
- **User Context**: From `ViewBag` and `currentUser` in layout

### SignalR Integration:
- Hub URL: `/hubs/communications`
- Receives real-time message updates
- Updates unread badges
- Handles message delivery confirmation

## Styling Details

### Theme Consistency:
- Uses existing brand colors (`#3d1019` primary, `#6d2932` hover)
- Matches current UI patterns and spacing
- Consistent border-radius and shadows
- Smooth animations (0.3s ease transitions)

### Responsive Design:
- Full-width on mobile (<768px)
- Fixed 400px width on desktop
- Scrollable message and channel lists
- Auto-resizing textarea (max 120px height)

## Configuration

### Hide on Communications Page:
```javascript
// Automatically hides sidebar and floating button on Communications page
if (currentAction === 'Communications') {
    commsSidebarPanel.style.display = 'none';
    commsToggleBtn.style.display = 'none';
}
```

### Active Sidebar Persistence:
```javascript
// Stored in localStorage
localStorage.getItem('activeSidebar') // 'ai', 'comms', or 'none'
```

## Future Enhancements

### Potential Additions:
1. **Real-time typing indicators**
2. **File upload and sharing**
3. **Message editing and deletion**
4. **Thread replies**
5. **@mentions and notifications**
6. **Search within messages**
7. **Message pinning**
8. **Voice/video call integration**
9. **Emoji reactions**
10. **Channel creation and management**

## API Endpoints Required

To fully integrate with backend:

1. **GET** `/api/communications/channels`
   - Returns list of channels user has access to
   - Response: `Array<ChannelDto>`

2. **GET** `/api/communications/channels/{channelId}/messages`
   - Returns messages for specific channel
   - Query params: `skip`, `take` for pagination
   - Response: `Array<MessageDto>`

3. **POST** `/api/communications/channels/{channelId}/messages`
   - Creates new message in channel
   - Body: `{ content: string, channelId: number }`
   - Response: `MessageDto`

4. **GET** `/api/communications/direct-messages/{userId}`
   - Returns DM history with specific user
   - Response: `Array<MessageDto>`

5. **SignalR Hub**: `/hubs/communications`
   - Methods:
     - `JoinChannel(channelId)`
     - `LeaveChannel(channelId)`
     - `SendMessage(channelId, message)`
   - Events:
     - `ReceiveMessage(message)`
     - `UserTyping(userId, channelId)`
     - `MessageRead(messageId, userId)`

## Testing Checklist

- [ ] Click Comments button to open sidebar
- [ ] Click Sparkles button to switch to AI chat
- [ ] Click Comments button again to switch back
- [ ] Verify only one sidebar open at a time
- [ ] Test channel list loading
- [ ] Test channel selection
- [ ] Test back button to return to channel list
- [ ] Test message input auto-resize
- [ ] Test message sending (Enter key)
- [ ] Test message sending (Send button)
- [ ] Verify state persists across page refresh
- [ ] Test responsive behavior on mobile
- [ ] Verify hidden on Communications page
- [ ] Test category expand/collapse
- [ ] Test DM list display

## Notes

- The sidebar automatically loads channels when first opened
- Demo messages are displayed if API endpoints are not yet implemented
- The floating toggle button is hidden (using header buttons instead)
- SignalR is optional - sidebar works without real-time updates
- All animations use CSS transitions for smooth performance
- Active sidebar state is saved to localStorage for persistence

## Support

For questions or issues, refer to:
- `communications-sidebar.js` for JavaScript logic
- `communications-sidebar.css` for styling
- `_ClientLayout.cshtml` for HTML structure and button handling

