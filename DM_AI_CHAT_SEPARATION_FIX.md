# Direct Messaging - AI Chat Separation Fix

## Problem

When sending a Direct Message, the AI Chat panel was being activated/triggered on the right side of the screen. The DM and AI chat should be completely separate systems.

## Root Cause

The `DirectHub` was broadcasting messages using the event name `"ReceiveMessage"`, which is the SAME event name used by:
1. AI Chat system (`chat.js`) listening on ChatHub
2. Channel messages (`communications.js`) listening on ChatHub

Even though DirectHub and ChatHub are different hubs, having multiple handlers for the same event name can cause confusion and cross-triggering.

## Solution

Changed the DirectHub to use a **unique event name** for direct messages:

### Backend Change (`DirectHub.cs`)

```csharp
// BEFORE:
await Clients.Group($"dm:{threadId}").SendAsync("ReceiveMessage", new { ... });

// AFTER:
await Clients.Group($"dm:{threadId}").SendAsync("ReceiveDirectMessage", new { ... });
```

### Frontend Change (`direct-messages.js`)

```javascript
// BEFORE:
directConnection.on("ReceiveMessage", function (message) {
    appendDirectMessage(message);
    // ...
});

// AFTER:
directConnection.on("ReceiveDirectMessage", function (message) {
    appendDirectMessage(message);
    // ...
});
```

## Event Naming Convention

Now each system has its own distinct events:

| System | Hub | Event Name | Handler Location |
|--------|-----|------------|------------------|
| **AI Chat** | ChatHub | `ReceiveMessage` | `chat.js` |
| **Channel Messages** | ChatHub | `ReceiveChannelMessage` | `communications.js` |
| **Direct Messages** | DirectHub | `ReceiveDirectMessage` | `direct-messages.js` |

## Benefits

✅ **Isolation**: DM events won't trigger AI chat
✅ **Clarity**: Event names clearly indicate their purpose
✅ **Debugging**: Easier to track which system is sending what
✅ **Maintainability**: Prevents future conflicts

## Testing

1. **Restart the application** (hub changes require restart)
2. **Open Communications page**
3. **Send a Direct Message**
4. **Verify**:
   - ✅ DM appears in main chat area
   - ❌ AI chat panel should NOT activate
   - ✅ Message is delivered in real-time

## Other Event Names in DirectHub

For consistency, other DirectHub events already use unique names:
- ✅ `UserTyping` - Typing indicator
- ✅ `ReadReceipt` - Read receipts
- ✅ `UserJoined` - User joined thread
- ✅ `Error` - Error messages

Only `ReceiveMessage` was conflicting, which is now fixed as `ReceiveDirectMessage`.

## Summary

🎯 **Direct Messages and AI Chat are now properly separated!**

The AI chat panel will only activate when you explicitly interact with it, not when you send or receive direct messages.

