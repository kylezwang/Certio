# Matter Communications Default Channel Fix

## Issue
When navigating to the MatterCommunications tab, it was defaulting to the "general" channel instead of the current matter's channel, and the displayed messages were generic rather than matter-specific.

## Requirements
- Default to the current matter channel (e.g., "sesame-street-llc") when loading the MatterCommunications tab
- Display matter-specific messages in the message container by default
- Ensure the correct channel is marked as active in the UI
- Handle both firm matters and client matters correctly
- Since this is an AJAX-loaded page, ensure proper initialization

## Solution
Modified the controller, view, and message loading logic to:
1. Search for the matter channel across all categories and subcategories
2. Set it as the active channel
3. Load appropriate messages for that channel
4. Update JavaScript to initialize with the active channel

## Changes Made

### 1. File: `Certio.Web/Controllers/CommunicationsController.cs`

#### A. Updated `MatterCommunications` Method to Find Matter Channel

**Added Channel Detection Logic** (Lines 145-186):
```csharp
// Find the matter channel across all categories and subcategories - this should be the active channel
string activeChannel = "matter-general";
int? activeChannelId = null;

foreach (var category in channelCategories)
{
    // Check direct channels in category
    var directMatterChannel = category.Channels.FirstOrDefault(c => c.MatterId == matterId);
    if (directMatterChannel != null)
    {
        activeChannel = directMatterChannel.Name;
        activeChannelId = directMatterChannel.Id;
        break;
    }
    
    // Check subcategories (CURRENT MATTER for firm matters, CLIENT COMMUNICATIONS for client matters)
    foreach (var subcategory in category.Subcategories)
    {
        var matterChannel = subcategory.Channels.FirstOrDefault(c => c.MatterId == matterId);
        if (matterChannel != null)
        {
            activeChannel = matterChannel.Name;
            activeChannelId = matterChannel.Id;
            break;
        }
    }
    
    if (activeChannelId.HasValue) break;
}

// If no matter channel found, fallback to first available channel
if (!activeChannelId.HasValue)
{
    var firstCategory = channelCategories.FirstOrDefault();
    var firstChannel = firstCategory?.Channels.FirstOrDefault() ?? 
                      firstCategory?.Subcategories.FirstOrDefault()?.Channels.FirstOrDefault();
    if (firstChannel != null)
    {
        activeChannel = firstChannel.Name;
        activeChannelId = firstChannel.Id;
    }
}
```

**Updated Message Loading** (Line 189):
```csharp
// Load demo messages for the active matter channel
var messages = await LoadDemoMessagesAsync(customUser.Id, matterId, activeChannel);
```

**Added ViewBag Property** (Line 196):
```csharp
ViewBag.ActiveChannelId = activeChannelId;
```

#### B. Updated `LoadDemoMessagesAsync` Method

**Added channelName Parameter and Logic** (Lines 570-633):
```csharp
private Task<List<Message>> LoadDemoMessagesAsync(int userId, int? matterId = null, string? channelName = null)
{
    var messages = new List<Message>();
    
    // Known firm/organization channels (not matter-specific)
    var knownGeneralChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
    { 
        "general", 
        "urgent-matters", 
        "client-onboarding" 
    };
    
    // If matterId is provided and channelName is not a known general channel, show matter-specific messages
    if (matterId.HasValue && !string.IsNullOrEmpty(channelName) && !knownGeneralChannels.Contains(channelName))
    {
        // Matter-specific messages for the matter channel
        messages.Add(new Message
        {
            Id = 1,
            UserId = userId,
            User = "Kyle Wang",
            Avatar = "KW",
            Time = "4:42 PM",
            CreatedAt = DateTime.Now.AddMinutes(-30),
            Content = "Starting the conversation for the SLA II Matter Details Communications module here",
            Reactions = new List<Reaction>()
        });
    }
    else if (!string.IsNullOrEmpty(channelName))
    {
        // General channel messages
        messages.Add(new Message
        {
            Id = 1,
            UserId = userId,
            User = "System",
            Avatar = "SY",
            Time = DateTime.Now.ToString("h:mm tt"),
            CreatedAt = DateTime.Now,
            Content = $"Welcome to #{channelName}! This is the general discussion channel.",
            Reactions = new List<Reaction>()
        });
    }
    else
    {
        // Fallback messages
        // ...
    }

    return Task.FromResult(messages);
}
```

#### C. Updated Regular Communications Method

**Updated to Pass activeChannel** (Lines 73-80):
```csharp
// Set active channel to the first available channel
var firstCategory = channelCategories.FirstOrDefault();
var activeChannel = firstCategory?.Channels.FirstOrDefault()?.Name ?? 
                   firstCategory?.Subcategories.FirstOrDefault()?.Channels.FirstOrDefault()?.Name ?? 
                   "general";

// Load demo messages for the active channel
var messages = await LoadDemoMessagesAsync(customUser.Id, null, activeChannel);
```

### 2. File: `Certio.Web/Views/Matter/_MatterCommunications.cshtml`

#### Updated JavaScript to Find Active Channel

**Modified Channel Detection Logic** (Lines 416-450):
```javascript
// Get active channel from the channel marked as active (should be the matter channel)
const activeChannel = document.querySelector('.channel-item.active');
const activeChannelId = activeChannel ? activeChannel.dataset.channelId : null;
const activeChannelName = activeChannel ? activeChannel.querySelector('.channel-name')?.textContent : 'matter-general';

console.log('Channel info:');
console.log('  activeChannel:', activeChannel);
console.log('  activeChannelId:', activeChannelId, '(type:', typeof activeChannelId, ')');
console.log('  activeChannelName:', activeChannelName);

// If no active channel found, fallback to first channel
if (!activeChannelId) {
    const fallbackChannel = document.querySelector('.channel-item');
    if (fallbackChannel) {
        console.warn('No active channel found, falling back to first channel');
        const fallbackChannelId = fallbackChannel.dataset.channelId;
        const fallbackChannelName = fallbackChannel.querySelector('.channel-name')?.textContent;
        
        if (currentUserId && fallbackChannelId && typeof initializeCommunicationsChat === 'function') {
            initializeCommunicationsChat(fallbackChannelId, currentUserId, currentUserName, currentOrganizationId);
        }
    }
} else {
    // Initialize communications chat if we have required data
    if (currentUserId && activeChannelId && typeof initializeCommunicationsChat === 'function') {
        console.log('Initializing communications chat with active channel...');
        initializeCommunicationsChat(activeChannelId, currentUserId, currentUserName, currentOrganizationId);
    } else {
        console.warn('Cannot initialize communications chat:', {
            hasUserId: !!currentUserId,
            hasChannelId: !!activeChannelId,
            hasFunction: typeof initializeCommunicationsChat === 'function'
        });
    }
}
```

## How It Works

### Backend Flow

1. **Channel Search** (Controller):
   - Searches all categories and subcategories for the channel with matching `matterId`
   - For firm matters: Looks in "CURRENT MATTER" subcategory under firm section
   - For client matters: Looks in "CLIENT COMMUNICATIONS" section
   - Falls back to first available channel if matter channel not found

2. **Message Loading** (Controller):
   - Detects if channel is a known general channel (general, urgent-matters, client-onboarding)
   - If it's a matter channel: Shows matter-specific message
   - If it's a general channel: Shows generic welcome message
   - Passes the channel name to determine message type

3. **Active Channel ID** (Controller):
   - Passes `ViewBag.ActiveChannelId` to the view for future use
   - Can be used for additional JavaScript initialization if needed

### Frontend Flow

1. **Active Channel Detection** (JavaScript):
   - Looks for `.channel-item.active` element (the channel marked with "active" class)
   - Extracts `data-channel-id` from that element
   - Uses that channel ID to initialize SignalR connection

2. **Fallback Mechanism** (JavaScript):
   - If no active channel found, falls back to first `.channel-item`
   - Ensures the page still functions even if marking failed

3. **SignalR Initialization** (JavaScript):
   - Calls `initializeCommunicationsChat` with the correct channel ID
   - Loads messages for that specific channel
   - Connects to SignalR hub for real-time updates

### Message Display Logic

The message display is controlled by three parameters:
- `userId`: Current user (for attribution)
- `matterId`: Current matter (to determine if in matter context)
- `channelName`: Active channel name (to determine message type)

**Decision Tree:**
```
IF matterId exists AND channelName exists AND channelName is NOT in [general, urgent-matters, client-onboarding]
    → Show matter-specific messages
ELSE IF channelName exists
    → Show generic welcome message for that channel
ELSE
    → Show fallback message
```

## Expected Behavior

### Before Fix:
```
When loading MatterCommunications tab:
- Active Channel: "general" ❌
- Displayed Message: "Welcome to #general!" ❌
- Highlighted Channel: None or first channel ❌
```

### After Fix - Firm Matter:
```
When loading MatterCommunications tab:
- Active Channel: "sesame-street-llc" ✓
- Displayed Message: "Starting the conversation for the SLA II Matter Details Communications module here" ✓
- Highlighted Channel: sesame-street-llc (under CURRENT MATTER) ✓
```

### After Fix - Client Matter:
```
When loading MatterCommunications tab:
- Active Channel: "[matter-channel-name]" ✓
- Displayed Message: "Starting the conversation..." ✓
- Highlighted Channel: [matter-channel-name] (under CLIENT COMMUNICATIONS) ✓
```

## Testing Checklist

### Firm Matter Testing
- [ ] Navigate to a firm matter's Communications tab
- [ ] Verify "sesame-street-llc" (or matter channel) is highlighted/selected
- [ ] Verify channel appears under "CURRENT MATTER" subcategory
- [ ] Verify message container shows "Starting the conversation..." message
- [ ] Verify SignalR initializes with correct channel ID
- [ ] Verify sending messages works in the matter channel
- [ ] Switch to "general" channel and verify it works
- [ ] Switch back to matter channel and verify it persists

### Client Matter Testing
- [ ] Navigate to a client matter's Communications tab
- [ ] Verify client matter channel is highlighted/selected
- [ ] Verify channel appears under "CLIENT COMMUNICATIONS" section
- [ ] Verify message container shows matter-specific message
- [ ] Verify SignalR initializes correctly
- [ ] Test channel switching works properly

### Edge Cases
- [ ] Test with matter that has no channel (should fallback gracefully)
- [ ] Test with very long matter channel names
- [ ] Test with special characters in matter channel names
- [ ] Test AJAX loading multiple times (tab switch back and forth)
- [ ] Verify browser refresh maintains correct channel

### General Channel Testing
- [ ] Switch to "general" channel from matter channel
- [ ] Verify it shows "Welcome to #general!" message
- [ ] Switch to "urgent-matters" channel
- [ ] Verify it shows appropriate welcome message
- [ ] Verify these are treated as general channels (not matter channels)

## Technical Details

### Channel Matching Logic
The matter channel is identified by:
```csharp
c => c.MatterId == matterId
```

This ensures we find the exact channel associated with the current matter, regardless of whether it's a firm matter or client matter.

### Active Class Assignment
The channel gets the "active" class in the Razor view:
```html
class="channel-item @(channel.Name == Model.ActiveChannel ? "active" : "")"
```

Where `Model.ActiveChannel` is set by the controller to the found matter channel name.

### SignalR Integration
The JavaScript passes the `activeChannelId` to `initializeCommunicationsChat()`, which:
1. Joins the SignalR channel group
2. Loads historical messages for that channel
3. Sets up real-time message listeners
4. Enables sending messages to that channel

## Benefits

1. **Contextual Default**: Users immediately see the most relevant channel for the matter
2. **Appropriate Messages**: Message container shows matter-specific content by default
3. **Visual Feedback**: The correct channel is highlighted, providing clear context
4. **No User Action Required**: Automatic selection means no extra clicks needed
5. **Consistent Experience**: Works the same way for firm matters and client matters
6. **Graceful Degradation**: Falls back to first channel if matter channel not found

## Files Modified

1. `Certio.Web/Controllers/CommunicationsController.cs` - Updated channel detection and message loading
2. `Certio.Web/Views/Matter/_MatterCommunications.cshtml` - Updated JavaScript initialization

## Related Components (No Changes)

- `communications.js` - Works with the channel ID passed to it
- `direct-messages.js` - Unaffected by these changes
- Database schema - No changes needed
- SignalR hub - No changes needed

## Conclusion

The MatterCommunications tab now correctly defaults to the current matter's channel when loaded, displaying appropriate messages and highlighting the correct channel in the UI. This provides a more intuitive and contextual user experience, especially since users are navigating directly to a specific matter's communications.

