# Calendar Attendees Database Save Fix

## Issue Description
The calendar attendees popup was working correctly for displaying users, but attendees were not being saved to the database when creating or updating events. This was happening because:

1. The `UpdateCalendarEventDto` didn't have an `AttendeeUserIds` property
2. The CalendarService wasn't handling attendee updates in the `UpdateEventAsync` method
3. Law firm members weren't being validated properly for attendee access

## Root Cause Analysis

### 1. Missing AttendeeUserIds in Update DTO
The `UpdateCalendarEventDto` was missing the `AttendeeUserIds` property, so when updating events, attendee changes weren't being processed.

### 2. No Attendee Update Logic in Service
The `UpdateEventAsync` method in CalendarService didn't handle attendee updates, only basic event properties.

### 3. Law Firm Member Validation Issue
The attendee validation was only checking direct organization membership, not matter-based access for law firm members.

## Solution Implemented

### 1. Updated UpdateCalendarEventDto
**File**: `Certio.Application/DTOs/CalendarDTOs.cs`

**Changes Made**:
- Added `AttendeeUserIds` property to `UpdateCalendarEventDto`

```csharp
public class UpdateCalendarEventDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public bool? IsAllDayEvent { get; set; }
    public string? EventType { get; set; }
    public string? Color { get; set; }
    public List<int>? AttendeeUserIds { get; set; }  // ✅ Added this
    // MatterId is not changeable after creation
}
```

### 2. Enhanced CalendarService UpdateEventAsync Method
**File**: `Certio.Application/Services/CalendarService.cs`

**Changes Made**:
- Added attendee update logic to `UpdateEventAsync` method
- Enhanced attendee validation to support law firm members
- Proper cleanup of existing attendees before adding new ones

```csharp
// Update attendees if specified
if (updateDto.AttendeeUserIds != null)
{
    // Remove existing attendees (except organizer)
    var existingAttendees = calendarEvent.Attendees.Where(a => a.AttendeeType != "Organizer").ToList();
    foreach (var attendee in existingAttendees)
    {
        _context.CalendarEventAttendees.Remove(attendee);
    }

    // Add new attendees
    foreach (var attendeeUserId in updateDto.AttendeeUserIds.Distinct())
    {
        // Skip if already organizer
        if (attendeeUserId == userId)
            continue;

        // Verify attendee is in organization or has matter access
        var attendeeInOrg = await _orgContextService.ValidateUserInOrganizationAsync(attendeeUserId, calendarEvent.OrgId);
        if (!attendeeInOrg && calendarEvent.MatterId.HasValue)
        {
            // Check if attendee has access through matter relationships
            var matterResult = await _matterService.GetMatterAsync(attendeeUserId, calendarEvent.MatterId.Value);
            attendeeInOrg = matterResult.Success;
        }

        if (attendeeInOrg)
        {
            var attendee = new CalendarEventAttendee
            {
                CalendarEventId = calendarEvent.Id,
                UserId = attendeeUserId,
                AttendeeType = "Required",
                ResponseStatus = "Pending",
                IsNotifyRecipient = true,
                AddedAt = DateTime.UtcNow
            };
            _context.CalendarEventAttendees.Add(attendee);
        }
    }
}
```

### 3. Enhanced CreateEventAsync Method
**File**: `Certio.Application/Services/CalendarService.cs`

**Changes Made**:
- Updated attendee validation to support law firm members with matter access
- Improved validation logic for cross-organization attendees

```csharp
// Verify attendee is in organization or has matter access
var attendeeInOrg = await _orgContextService.ValidateUserInOrganizationAsync(attendeeUserId, orgId);
if (!attendeeInOrg && createDto.MatterId.HasValue)
{
    // Check if attendee has access through matter relationships
    var matterResult = await _matterService.GetMatterAsync(attendeeUserId, createDto.MatterId.Value);
    attendeeInOrg = matterResult.Success;
}
```

## Technical Details

### Attendee Validation Logic
The enhanced validation now supports two scenarios:

1. **Direct Organization Members**: Users who are direct members of the organization
2. **Law Firm Members**: Users from law firms that have relationships with the matter's organization

### Update Process
When updating an event with attendees:
1. **Remove Existing**: All existing attendees (except organizer) are removed
2. **Add New**: New attendees are added based on the provided `AttendeeUserIds`
3. **Validate Access**: Each attendee is validated for proper access
4. **Save Changes**: All changes are saved in a single transaction

### Data Flow
1. **Frontend**: Sends `attendeeUserIds` array in the update request
2. **Controller**: Maps to `UpdateCalendarEventDto.AttendeeUserIds`
3. **Service**: Processes attendee updates with proper validation
4. **Database**: Attendees are saved to `CalendarEventAttendees` table

## Security Considerations

### Access Validation
- **Organization Members**: Validated through `_orgContextService.ValidateUserInOrganizationAsync`
- **Law Firm Members**: Validated through matter access via `_matterService.GetMatterAsync`
- **Cross-Organization**: Proper handling of law firm relationships

### Data Integrity
- **Transaction Safety**: All attendee changes are saved in a single transaction
- **Organizer Protection**: Event organizer cannot be removed from attendees
- **Duplicate Prevention**: Uses `Distinct()` to prevent duplicate attendees

## Testing Verification

### Expected Behavior
When creating or updating calendar events with attendees:

1. ✅ **Create Event**: Attendees are properly saved to database
2. ✅ **Update Event**: Attendee changes are properly saved
3. ✅ **Law Firm Members**: Can be added as attendees if they have matter access
4. ✅ **Direct Members**: Can be added as attendees if they're organization members
5. ✅ **Organizer Protection**: Event creator remains as organizer
6. ✅ **Database Consistency**: All attendee data is properly stored

### Database Verification
The fix ensures that:
- `CalendarEventAttendees` table is properly populated
- Attendee types are correctly set (Organizer vs Required)
- Response statuses are properly initialized
- Audit timestamps are recorded

## Files Modified

### Backend Changes
**File**: `Certio.Application/DTOs/CalendarDTOs.cs`
- Added `AttendeeUserIds` property to `UpdateCalendarEventDto`

**File**: `Certio.Application/Services/CalendarService.cs`
- Enhanced `UpdateEventAsync` method with attendee update logic
- Improved `CreateEventAsync` method with better law firm member validation
- Added proper attendee cleanup and validation

## Conclusion

This fix resolves the issue where calendar attendees weren't being saved to the database. The implementation:

1. ✅ **Fixes Core Issue**: Attendees are now properly saved when creating/updating events
2. ✅ **Supports Law Firm Members**: Law firm members can be added as attendees
3. ✅ **Maintains Security**: Proper validation and access control
4. ✅ **Ensures Data Integrity**: Transaction-safe attendee updates
5. ✅ **Backward Compatible**: Existing functionality remains intact

The calendar attendees functionality now works end-to-end, from the UI popup to the database storage, supporting both direct organization members and law firm members with proper matter access.
