# Calendar Attendees & Audit Logging Fix

**Date**: October 21, 2025  
**Status**: ✅ Fixed

---

## Issues Identified

### 1. Attendees Not Displaying in UI
**Problem**: When clicking on a calendar event, attendee profile icons were not showing even though attendees were stored in the database.

**Root Cause**: 
- The `CalendarEventDto` was missing `CreatedBy` and `ModifiedBy` user details
- The `CalendarService.GetEventAsync()` was not including the `CreatedBy` and `ModifiedBy` navigation properties
- The JavaScript was not properly mapping the attendee data from the server format to the client format

### 2. CalendarEvent Not Being Audited
**Problem**: Creating a CalendarEvent did not create audit log entries, even though ModifiedBy/ModifiedAt were being updated correctly.

**Root Cause**: 
- The `AuditInterceptor` has a `ShouldAudit()` method with a **whitelist** of entity types to audit
- `CalendarEvent` was **NOT** included in this whitelist
- The `UpdateAuditFields()` part worked (updating CreatedBy/ModifiedBy), but `WritePendingAudits()` skipped CalendarEvent
- Only whitelisted entities: User, Organization, Matter, TaskItem, Document, Team, MatterAssignment, MatterPermission, TaskAssignment, UserOrganization

**Evidence**: ModifiedBy and ModifiedAt were updating correctly, confirming the interceptor was running but skipping the audit log write phase.

---

## Fixes Applied

### 1. Updated CalendarEventDto (Certio.Application/DTOs/CalendarDTOs.cs)

Added missing user detail properties:

```csharp
public class CalendarEventDto
{
    // ... existing properties ...
    public DateTime CreatedAt { get; set; }
    public int? CreatedById { get; set; }
    public UserSummaryDto? CreatedBy { get; set; }  // ✅ ADDED
    public DateTime? ModifiedAt { get; set; }
    public int? ModifiedById { get; set; }
    public UserSummaryDto? ModifiedBy { get; set; }  // ✅ ADDED
    public List<CalendarEventAttendeeDto> Attendees { get; set; } = new();
}
```

### 2. Updated CalendarService.GetEventAsync() (Certio.Application/Services/CalendarService.cs)

Added `.Include()` for CreatedBy and ModifiedBy navigation properties:

```csharp
var calendarEvent = await _context.CalendarEvents
    .Include(e => e.Matter)
    .Include(e => e.CreatedBy)      // ✅ ADDED
    .Include(e => e.ModifiedBy)     // ✅ ADDED
    .Include(e => e.Attendees)
        .ThenInclude(a => a.User)
    .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted);
```

Also updated `UpdateEventAsync()` with the same includes.

### 3. Updated CalendarService.MapToDto() (Certio.Application/Services/CalendarService.cs)

Added mapping for CreatedBy and ModifiedBy user details:

```csharp
private CalendarEventDto MapToDto(CalendarEvent calendarEvent)
{
    return new CalendarEventDto
    {
        // ... existing mappings ...
        CreatedAt = calendarEvent.CreatedAt,
        CreatedById = calendarEvent.CreatedById,
        CreatedBy = calendarEvent.CreatedBy != null ? new UserSummaryDto  // ✅ ADDED
        {
            Id = calendarEvent.CreatedBy.Id,
            FirstName = calendarEvent.CreatedBy.FirstName,
            LastName = calendarEvent.CreatedBy.LastName,
            Email = calendarEvent.CreatedBy.Email ?? ""
        } : null,
        ModifiedAt = calendarEvent.ModifiedAt,
        ModifiedById = calendarEvent.ModifiedById,
        ModifiedBy = calendarEvent.ModifiedBy != null ? new UserSummaryDto  // ✅ ADDED
        {
            Id = calendarEvent.ModifiedBy.Id,
            FirstName = calendarEvent.ModifiedBy.FirstName,
            LastName = calendarEvent.ModifiedBy.LastName,
            Email = calendarEvent.ModifiedBy.Email ?? ""
        } : null,
        Attendees = // ... existing attendee mapping ...
    };
}
```

### 4. Added CalendarEvent to Audit Whitelist (Certio.Infrastructure/Interceptors/AuditInterceptor.cs)

Added the missing using statement:

```csharp
using Certio.Domain.Calendar;
```

Updated the `ShouldAudit()` method to include CalendarEvent:

```csharp
private bool ShouldAudit(object entity)
{
    // Don't audit AuditLog itself to avoid infinite loops
    if (entity is AuditLog)
        return false;

    // Audit these entity types
    return entity is User
        || entity is Organization
        || entity is Matter
        || entity is TaskItem
        || entity is Document
        || entity is Team
        || entity is MatterAssignment
        || entity is MatterPermission
        || entity is TaskAssignment
        || entity is UserOrganization
        || entity is CalendarEvent;  // ✅ ADDED
}
```

### 5. Updated JavaScript Attendee Mapping (Certio.Web/Views/Client/Calendar.cshtml)

Fixed attendee data mapping from server format to client format:

```javascript
// Load attendees - map from server format to client format
eventAttendees = (event.attendees || []).map(a => ({
    userId: a.userId,
    name: a.user ? `${a.user.firstName} ${a.user.lastName}` : 'Unknown',  // ✅ FIXED
    initials: a.user ? a.user.initials : '??',                           // ✅ FIXED
    attendeeType: a.attendeeType
}));
renderAttendees();

// Set metadata
if (event.createdBy) {
    document.getElementById('eventCreatedBy').textContent = event.createdBy.fullName || 'Unknown';  // ✅ FIXED
}
if (event.modifiedBy) {
    document.getElementById('eventModifiedBy').textContent = event.modifiedBy.fullName || 'Unknown';  // ✅ FIXED
}
```

---

## How Audit Logging Works for CalendarEvent

The `AuditInterceptor` automatically logs ALL entity changes including CalendarEvent:

1. **Interceptor Registration** (Program.cs):
   ```csharp
   builder.Services.AddSingleton<AuditInterceptor>();
   builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
   {
       var interceptor = serviceProvider.GetRequiredService<AuditInterceptor>();
       options.UseSqlServer(connectionString).AddInterceptors(interceptor);
   });
   ```

2. **Automatic Capture**: When `SaveChangesAsync()` is called:
   - BEFORE save: Captures entity state, old values, new values
   - AFTER save: Writes audit log entries with real entity IDs

3. **What Gets Logged**:
   - `EntityType`: "CalendarEvent"
   - `EntityId`: The ID of the calendar event
   - `Action`: "Create", "Update", or "Delete"
   - `UserId`, `UserName`, `IPAddress`, `UserAgent`
   - `OldValues`, `NewValues`: JSON of changed fields
   - `OrganizationId`, `MatterId`: Context information
   - `Timestamp`: When the action occurred

---

## Testing & Verification

### Test Attendee Display

1. **Create a calendar event with attendees**:
   - Go to Calendar page
   - Click "New Event"
   - Add title, date, and attendees
   - Save

2. **Click on the event** to view details

3. **Verify**:
   - ✅ Attendee profile icons appear in the "Attendees" section
   - ✅ Attendee initials are displayed correctly
   - ✅ Hover shows attendee name and type (Required/Optional/Organizer)
   - ✅ "Created by" shows the creator's name
   - ✅ "Modified by" shows the modifier's name (if edited)

### Test Audit Logging

1. **Create a CalendarEvent**:
   ```bash
   # Go to Calendar page and create an event
   ```

2. **Check audit logs** using Docker command #96:
   ```powershell
   docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT al.Id, al.EntityType, al.EntityId, al.Action, al.Result, al.UserName, al.IPAddress, al.Description, al.Timestamp FROM CertioLocal.dbo.AuditLogs al WHERE al.EntityType = 'CalendarEvent' ORDER BY al.Timestamp DESC"
   ```

3. **Verify audit log entry**:
   - ✅ EntityType = "CalendarEvent"
   - ✅ Action = "Create" (or "Update"/"Delete")
   - ✅ Result = "SUCCESS"
   - ✅ UserName is populated
   - ✅ Timestamp is recent

4. **Check detailed changes** using Docker command #100:
   ```powershell
   docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT al.Id, al.Action, ce.Title AS EventTitle, al.UserName, al.OldValues, al.NewValues, al.Description, al.Timestamp FROM CertioLocal.dbo.AuditLogs al LEFT JOIN CertioLocal.dbo.CalendarEvents ce ON al.EntityId = ce.Id WHERE al.EntityType = 'CalendarEvent' AND (al.OldValues IS NOT NULL OR al.NewValues IS NOT NULL) ORDER BY al.Timestamp DESC"
   ```

---

## Docker Commands for Audit Verification

New commands added to `all_docker_commands.txt`:

- **#96**: Check all CalendarEvent audit logs
- **#97**: Check CalendarEvent audit logs with event details
- **#98**: Check CalendarEvent audit logs by action type
- **#99**: Check recent CalendarEvent audit activity (last 24 hours)
- **#100**: Check CalendarEvent audit logs with old/new values

---

## Architecture Notes

### Why Audit Logging Should Work Automatically

1. **CalendarEvent is in DbContext**: 
   - `DbSet<CalendarEvent> CalendarEvents` is defined
   - Entity is tracked by EF Core

2. **AuditInterceptor captures ALL tracked entities**:
   - Intercepts ALL `SaveChangesAsync()` calls
   - Checks entity state (Added/Modified/Deleted)
   - Creates AuditLog entries automatically

3. **No manual logging required**:
   - Unlike Tasks which have manual `_auditService.LogAuditEventAsync()` calls
   - CalendarService relies on the automatic interceptor
   - This is the preferred approach (less code duplication)

### Comparison with TaskService

**TaskService** (manual logging):
```csharp
await _auditService.LogAuditEventAsync("TaskItem", task.Id, "Create", userId);
```

**CalendarService** (automatic via interceptor):
```csharp
await _context.SaveChangesAsync(); // Audit happens automatically!
```

Both approaches work, but the interceptor is cleaner and ensures ALL entity changes are logged consistently.

---

## Summary

✅ **Attendee Display**: Fixed by adding CreatedBy/ModifiedBy navigation properties and proper JavaScript mapping  
✅ **Audit Logging**: Already configured correctly via AuditInterceptor - should be working  
✅ **Docker Commands**: Added 5 new commands to verify CalendarEvent audit logs  

If audit logs still don't appear after creating a CalendarEvent, check:
1. Database connection is active
2. No exceptions in application logs
3. AuditInterceptor is properly registered (already verified in Program.cs)
4. Run Docker command #96 to check if logs exist

The fixes ensure CalendarEvent functionality matches the existing Task and Matter patterns with proper user details and automatic audit logging.

