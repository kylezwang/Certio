# Calendar OAuth Integration Implementation

**Date**: November 22, 2025  
**Status**: Implementation Complete ✅

---

## Overview

Successfully implemented OAuth integrations for Google Calendar and Outlook Calendar with seamless integration into the existing CalendarEvents architecture. Users can now connect their external calendars and automatically sync events into Certio.

---

## ✅ Completed Components

### **1. Domain Layer**

#### New Domain Entity: `Certio.Domain/Calendar/CalendarIntegration.cs`
- Tracks calendar OAuth connections per user/organization
- Fields:
  - `Id`, `OrgId`, `UserId`, `Provider` (Google/Outlook)
  - `AccessToken`, `RefreshToken`, `TokenExpiresAt`
  - `Scopes`, `Email`, `DisplayName`
  - `ConnectedAt`, `LastSyncedAt`
  - `Status`, `LastErrorMessage`, `LastErrorAt`
- Relationships with Organization and User entities

### **2. Infrastructure Layer**

#### Database Migration: `AddCalendarIntegrations`
- Created `CalendarIntegrations` table
- Added indexes for performance (OrgId, UserId, Provider unique constraint)
- Configured relationships in `ApplicationDbContext`

### **3. Application Layer**

#### New Service Interface: `ICalendarSyncService`
```csharp
- SyncGoogleCalendarAsync()
- SyncOutlookCalendarAsync()
- RefreshTokenIfNeededAsync()
```

#### New Service Implementation: `CalendarSyncService`
- Fetches events from Google Calendar API
- Fetches events from Microsoft Graph API (Outlook)
- Automatically creates/updates CalendarEvents from external sources
- Handles token refresh for expired access tokens
- Error handling and status tracking
- Stores external calendar ID and source for tracking

### **4. Web/API Layer**

#### New Controller: `CalendarOAuthController`
**Google Calendar Endpoints:**
- `GET /api/calendar-oauth/google/authorize` - Initiates OAuth flow
- `GET /api/calendar-oauth/google/callback` - Handles OAuth callback

**Outlook Calendar Endpoints:**
- `GET /api/calendar-oauth/outlook/authorize` - Initiates OAuth flow
- `GET /api/calendar-oauth/outlook/callback` - Handles OAuth callback

**Management Endpoints:**
- `GET /api/calendar-oauth/status` - Get current integration status
- `POST /api/calendar-oauth/disconnect` - Disconnect calendar integration

#### OAuth Callback View: `CalendarOAuthCallback.cshtml`
- Beautiful success/error page
- Auto-closes and notifies parent window
- Provides user feedback during OAuth flow

### **5. UI Components**

#### Calendar Integrations Card (in `_ClientLayout.cshtml`)
- Positioned at the top of calendar sidebar (above "Upcoming Events")
- Styled exactly like the documents page integration card
- Shows connection status for both Google Calendar and Outlook Calendar
- Not Connected State:
  - Two buttons side by side: "Google Calendar" and "Outlook"
  - Descriptive text: "Connect your calendar to sync events"
- Connected State:
  - Shows provider icon, name, and connected email
  - Individual "Disconnect" buttons for each provider
  - Ability to connect additional providers

#### JavaScript OAuth Handler
- Loads integration status on page load
- Opens OAuth popup window for authentication
- Listens for OAuth completion messages
- Handles disconnect with confirmation
- Automatically refreshes calendar after sync
- Dynamic UI updates based on connection status

### **6. Configuration**

#### Added to `appsettings.json`:
```json
"CalendarIntegration": {
  "GoogleCalendar": {
    "ClientId": "${GOOGLE_CALENDAR_CLIENT_ID}",
    "ClientSecret": "${GOOGLE_CALENDAR_CLIENT_SECRET}",
    "RedirectUri": "${GOOGLE_CALENDAR_REDIRECT_URI}"
  },
  "OutlookCalendar": {
    "ClientId": "${OUTLOOK_CALENDAR_CLIENT_ID}",
    "ClientSecret": "${OUTLOOK_CALENDAR_CLIENT_SECRET}",
    "RedirectUri": "${OUTLOOK_CALENDAR_REDIRECT_URI}"
  }
}
```

### **7. Service Registration**

Registered `ICalendarSyncService` in `Program.cs`:
```csharp
builder.Services.AddScoped<ICalendarSyncService, CalendarSyncService>();
```

---

## 🔄 Integration with Existing CalendarEvents

The implementation seamlessly integrates with the existing `CalendarEvent` entity:

- **External Calendar ID**: Each synced event stores the external calendar ID (e.g., `google:abc123`)
- **External Calendar Source**: Tracks the source provider ("Google" or "Outlook")
- **Sync Status**: Tracks sync state ("Synced", "Pending", "Failed")
- **Last Synced At**: Timestamp of last successful sync
- **Bidirectional Tracking**: Events can be identified and updated during subsequent syncs

---

## 🎨 UI Design

The Calendar Integrations card follows the exact styling of the Documents page:

- **Card Style**: White background, rounded corners, subtle shadow
- **OAuth Buttons**: Custom styled buttons with provider icons
- **Connected State**: Clean display with provider info and disconnect option
- **Responsive**: Adapts to different screen sizes
- **Professional**: Matches the overall Certio design system

---

## 🔒 Security Features

1. **Data Protection**: Uses ASP.NET Data Protection API for state parameter
2. **CSRF Protection**: State parameter includes timestamp and is validated
3. **Token Security**: Access and refresh tokens stored securely in database
4. **Session Tokens**: Uses Google Places session tokens to optimize API usage
5. **Authorization**: Requires OrgMember policy for all endpoints (except callbacks)
6. **Organization Scoping**: All integrations are scoped to user and organization

---

## 🔄 Sync Behavior

1. **Initial Connection**: Automatically syncs next 30 days of events
2. **Event Creation**: Creates new CalendarEvent records for each external event
3. **Event Updates**: Updates existing events on subsequent syncs
4. **Event Identification**: Uses external calendar ID to prevent duplicates
5. **Background Sync**: Runs sync in background after successful OAuth
6. **Error Handling**: Captures and logs sync errors, updates integration status

---

## 📋 Setup Requirements

To enable calendar integrations, configure the following:

### Google Calendar API:
1. Create project in Google Cloud Console
2. Enable Google Calendar API
3. Create OAuth 2.0 credentials
4. Set redirect URI: `https://yourdomain.com/api/calendar-oauth/google/callback`
5. Add scopes:
   - `https://www.googleapis.com/auth/calendar.readonly`
   - `https://www.googleapis.com/auth/calendar.events.readonly`

### Outlook Calendar API:
1. Register app in Azure AD
2. Add Microsoft Graph permissions:
   - `Calendars.Read`
   - `Calendars.ReadWrite`
   - `offline_access`
3. Set redirect URI: `https://yourdomain.com/api/calendar-oauth/outlook/callback`

### Environment Variables:
```bash
GOOGLE_CALENDAR_CLIENT_ID=your_client_id
GOOGLE_CALENDAR_CLIENT_SECRET=your_client_secret
GOOGLE_CALENDAR_REDIRECT_URI=https://yourdomain.com/api/calendar-oauth/google/callback

OUTLOOK_CALENDAR_CLIENT_ID=your_client_id
OUTLOOK_CALENDAR_CLIENT_SECRET=your_client_secret
OUTLOOK_CALENDAR_REDIRECT_URI=https://yourdomain.com/api/calendar-oauth/outlook/callback
```

---

## 🎯 Key Features

✅ Google Calendar OAuth integration  
✅ Outlook Calendar OAuth integration  
✅ Automatic event syncing (next 30 days)  
✅ Token refresh handling  
✅ Connection status display  
✅ Disconnect functionality  
✅ Integration with existing CalendarEvents  
✅ Beautiful UI matching documents page  
✅ Error handling and status tracking  
✅ Background sync after connection  
✅ Support for multiple calendar providers per user  
✅ Organization-scoped integrations  

---

## 🚀 Usage Flow

1. **User navigates to Calendar page**
2. **Sees "Calendar Integrations" card at top of sidebar**
3. **Clicks "Google Calendar" or "Outlook" button**
4. **OAuth popup opens for authentication**
5. **User grants permissions**
6. **Popup closes, calendar syncs automatically**
7. **Synced events appear in the calendar**
8. **Integration status shows connected state**

---

## 📝 Notes

- The implementation follows the same pattern as the existing Document OAuth integrations
- Uses the existing `CalendarEvent` architecture without modifications
- Calendar sync service is optional - if not registered, OAuth still works but won't sync
- Token refresh is automatic and transparent to users
- Each user can have one Google and one Outlook calendar connected per organization
- External events are read-only in Certio (synced from external source)

---

## 🔮 Future Enhancements

Potential future improvements:
- Two-way sync (create/edit events in Certio, push to external calendar)
- Support for multiple calendars per provider
- Selective calendar syncing
- Real-time sync via webhooks
- Calendar event conflict detection
- Sync settings and preferences
- Calendar color mapping from external sources

---

## ✅ Testing Checklist

- [ ] Configure Google Calendar API credentials
- [ ] Configure Outlook Calendar API credentials
- [ ] Test Google Calendar OAuth flow
- [ ] Test Outlook Calendar OAuth flow
- [ ] Verify events sync correctly
- [ ] Test disconnect functionality
- [ ] Verify token refresh works
- [ ] Test with multiple users
- [ ] Test error scenarios (invalid credentials, denied permissions)
- [ ] Verify UI matches documents page styling

---

**Implementation Status**: ✅ COMPLETE AND READY FOR TESTING

