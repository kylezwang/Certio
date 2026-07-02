# Communications Real Data Integration

## Changes Made

### 1. Updated ClientController.Communications Action

**Before:** Always used sample data (Sarah Johnson, Mike Chen, etc.)

**After:** Loads real data from database:
```csharp
// Load real channels from database
var channels = await _channelManagementService.GetOrganizationChannelsAsync(orgId);

// Load team members from organization  
var teamMembers = await _db.UserOrganizations
    .Where(uo => uo.OrganizationId == orgId && uo.IsActive)
    .Include(uo => uo.User)
    .Select(uo => new CommunicationsTeamMember { ... })
    .ToListAsync();
```

### 2. How It Works

#### Default Channels Created Automatically
The `ChannelInitializationMiddleware` automatically creates these default channels when you first visit the Communications page:

1. **general** - General organization discussions and announcements (Public)
2. **urgent-matters** - Time-sensitive legal matters (Public)
3. **client-onboarding** - New client onboarding discussions (Public)

#### Real Team Members
Shows actual users from your organization:
- Loads from `UserOrganizations` table
- Shows real names, roles, and avatars
- Status updated via SignalR

#### Real Messages
- Loads actual messages from `ChatMessages` table where `IsChannelMessage = true`
- Shows last 50 messages per channel
- Includes sender names, timestamps, and content

### 3. Data Flow

```
User visits Communications page
         ↓
Middleware checks if default channels exist
         ↓
If not, creates: general, urgent-matters, client-onboarding
         ↓
Controller loads real channels from Conversations table
         ↓
Controller loads real messages from ChatMessages table
         ↓
Controller loads real team members from UserOrganizations
         ↓
View renders with REAL DATA
         ↓
SignalR connects for real-time updates
```

### 4. Database Schema

Channels are stored as `Conversations` with:
- `IsChannel = true`
- `ChannelType = "Public"` or `"Private"` or `"Voice"`
- `OrganizationId` for organization scoping

Messages are stored as `ChatMessages` with:
- `IsChannelMessage = true`
- `ChannelId` pointing to the Conversation
- `OrganizationId` for organization scoping

### 5. Testing

To see real data:
1. Visit the Communications page
2. First visit creates default channels automatically
3. Send messages through the UI
4. Messages save to database
5. Refresh page to see persisted messages

### 6. Sample Data Fallback

Sample data only shows if:
- `Features:UseSampleData` is `true` in appsettings.json
- AND no real channels exist yet

Otherwise, always uses real data from database.

### 7. What You'll See Now

Instead of placeholder data, you'll see:
- **Channels:** Real channels from your organization's database
- **Team Members:** Actual users in your organization with real names
- **Messages:** Empty at first, then real messages as you send them
- **Online Status:** Updated via SignalR in real-time

### 8. To Populate More Data

Add more channels programmatically:
```csharp
await _channelManagementService.CreateDefaultChannelAsync(
    organizationId,
    createdById,
    "channel-name",
    "Channel description",
    "Public" // or "Private"
);
```

Send messages via ChatHub:
```javascript
await communicationsConnection.invoke(
    "SendChannelMessage",
    channelId.toString(),
    userId.toString(),
    "Member",
    content,
    "Text"
);
```


