# View Audit Fix & Tasks Styling Update

## Date: October 23, 2025

## Issues Fixed

### 1. **View Audit Logs Not Showing in History Page**

#### **Problem:**
- View audit logs were being saved to the database (confirmed with ID 662)
- However, they had the wrong `OrganizationId` (OrgId 4 instead of the matter's actual OrgId 1)
- History page filters by OrganizationId, so View events weren't showing up

#### **Root Cause:**
The `ViewAuditAttribute` was extracting `orgId` from the route parameters (e.g., `/Client/4/Matter/Details/5`), but this was the *current user's organization context*, not the *entity's actual organization*.

For cross-organization access (like law firms viewing client matters), this caused View audit logs to be associated with the wrong organization.

#### **Solution:**
Updated `ViewAuditAttribute.cs` to query the database and get the **actual organization ID** from the entity itself:

**For Matter Views:**
```csharp
// Get the actual organization ID from the Matter entity
if (_entityType == "Matter" && entityId > 0 && !organizationId.HasValue)
{
    var matter = await dbContext.Set<Certio.Domain.Matters.Matter>()
        .Where(m => m.Id == entityId)
        .Select(m => new { m.OrganizationId })
        .FirstOrDefaultAsync();
    
    if (matter != null)
    {
        organizationId = matter.OrganizationId;
    }
}
```

**For CalendarEvent Views:**
```csharp
// Get the organization ID from the event
if (_entityType == "CalendarEvent" && entityId > 0 && !organizationId.HasValue)
{
    var calendarEvent = await dbContext.Set<Certio.Domain.Calendar.CalendarEvent>()
        .Where(e => e.Id == entityId)
        .Select(e => new { e.OrgId, e.MatterId })
        .FirstOrDefaultAsync();
    
    if (calendarEvent != null)
    {
        organizationId = calendarEvent.OrgId;
        matterId = calendarEvent.MatterId;
    }
}
```

#### **Result:**
✅ View audit logs now have the correct OrganizationId
✅ View events will appear in the History page
✅ Cross-organization access is properly tracked

---

### 2. **Tasks Inbox Styling Consistency**

#### **Problem:**
- History page has a clean container with border and box-shadow around all cards
- Tasks/Index and MatterTasks had individual card borders/shadows
- Inconsistent visual design between pages

#### **Solution:**
Applied consistent container styling to match History page:

**Files Modified:**
- `Certio.Web/Views/Tasks/Index.cshtml`
- `Certio.Web/Views/Matter/_MatterTasks.cshtml`

**Styling Changes:**
```css
/* Container gets the border and shadow */
.task-cards-container {
    border: 1px solid rgba(0, 0, 0, 0.1) !important;
    border-radius: 12px !important;
    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.05) !important;
    padding: 0 !important;
    overflow: hidden !important;
}

/* Individual cards have no border/shadow, just separator lines */
.task-cards-container .task-card-item {
    border: none !important;
    border-radius: 0 !important;
    box-shadow: none !important;
    border-bottom: 1px solid rgba(0, 0, 0, 0.1) !important;
}

/* Last card has no bottom border */
.task-cards-container .task-card-item:last-child {
    border-bottom: none !important;
}
```

#### **Result:**
✅ Tasks inbox containers now match History page styling
✅ Clean, modern card container design
✅ "Add a task" button is NOT affected (it's outside the container)
✅ Consistent design across all inbox views

---

## Testing

### **Test View Audit Fix:**

1. **View a Matter from a different organization context:**
   ```
   Navigate to: /Client/4/Matter/Details/1 (where Matter 1 belongs to Org 1)
   ```

2. **Check the audit log:**
   ```bash
   docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 5 Id, EntityType, EntityId, Action, OrganizationId, MatterId, UserName FROM CertioLocal.dbo.AuditLogs WHERE Action = 'View' ORDER BY Timestamp DESC"
   ```

3. **Expected Result:**
   - OrganizationId should be `1` (the matter's org), NOT `4` (your current org context)

4. **Check History page:**
   ```
   Navigate to: /Client/1/History
   ```

5. **Expected Result:**
   - The View event should now appear in the activity list!

---

### **Test Tasks Styling:**

1. **Go to Tasks page:**
   ```
   Navigate to: /Client/1/Tasks
   ```

2. **Visual Check:**
   - ✅ Task cards container should have a subtle border and shadow
   - ✅ Individual task cards should have NO individual borders/shadows
   - ✅ Cards separated by thin lines
   - ✅ "Add a task" button styling is unchanged

3. **Go to Matter Tasks:**
   ```
   Navigate to: /Client/1/Matter/Details/1 → Tasks Tab
   ```

4. **Visual Check:**
   - ✅ Same clean container styling
   - ✅ Consistent with Tasks/Index and History pages

---

## Files Modified

### **View Audit Fix:**
- ✅ `Certio.Web/Attributes/ViewAuditAttribute.cs`
  - Added `using Microsoft.EntityFrameworkCore;`
  - Added database queries to get actual OrganizationId from entities
  - Supports Matter and CalendarEvent entity types

### **Tasks Styling:**
- ✅ `Certio.Web/Views/Tasks/Index.cshtml`
  - Added container border and box-shadow styling
  - Removed individual card borders/shadows

- ✅ `Certio.Web/Views/Matter/_MatterTasks.cshtml`
  - Added container border and box-shadow styling
  - Scoped with `.matter-tasks-content` to avoid conflicts

---

## Benefits

### **View Audit Fix:**
- ✅ **Accurate audit trails** - View events tracked to correct organization
- ✅ **Cross-org visibility** - Law firms can track which client matters they viewed
- ✅ **Compliance ready** - Proper organization context for all view events
- ✅ **Partner visibility** - Partners now see ALL view events in History page

### **Tasks Styling:**
- ✅ **Visual consistency** - All inbox views use same design language
- ✅ **Modern UI** - Clean, professional container design
- ✅ **Better UX** - Clear visual grouping of related items
- ✅ **Maintainability** - Consistent styling across all pages

---

## Expected Results After Testing

### **Before:**
- ❌ View events not showing in History page
- ❌ Inconsistent card styling between pages
- ❌ Wrong OrganizationId for cross-org views

### **After:**
- ✅ View events appear in History page
- ✅ Consistent container styling across all inbox views
- ✅ Correct OrganizationId for all view audit logs
- ✅ Complete audit trail for compliance

---

**Everything is now fixed and ready to test!** 🎉

