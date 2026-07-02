# Audit Logging Testing Guide

## Quick Testing Steps

### 1. Test View Audit Logging

#### **Test Matter Views:**
1. Navigate to a matter: `/Client/1/Matter/Details/1`
2. Check audit logs:
```bash
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 10 Id, EntityType, EntityId, Action, UserName, Description, Timestamp FROM CertioLocal.dbo.AuditLogs WHERE EntityType = 'Matter' AND Action = 'View' ORDER BY Timestamp DESC"
```

**Expected:** New row with Action='View', EntityType='Matter', your username, IP address

#### **Test Calendar Event Views:**
1. Click on a calendar event to view details
2. Check audit logs:
```bash
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 10 Id, EntityType, EntityId, Action, UserName, Description, Timestamp FROM CertioLocal.dbo.AuditLogs WHERE EntityType = 'CalendarEvent' AND Action = 'View' ORDER BY Timestamp DESC"
```

**Expected:** New row with Action='View', EntityType='CalendarEvent'

---

### 2. Test AI Chat Audit Logging

#### **Generate AI Chat Message:**
1. Go to AI Chat: `/Client/1/Chat`
2. Send a message to Notal AI
3. Wait for AI response
4. Check audit logs:
```bash
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 20 Id, EntityType, EntityId, Action, IsAIAction, AIAgentType, SourceConversationId, UserName, Description, Timestamp FROM CertioLocal.dbo.AuditLogs WHERE EntityType = 'ChatMessage' OR IsAIAction = 1 ORDER BY Timestamp DESC"
```

**Expected:** 
- One row for user message (Action='Create', IsAIAction=0)
- One row for AI response (Action='Create', IsAIAction=1, AIAgentType set)

---

### 3. Check History Page

#### **View in History Page:**
1. Go to: `/Client/1/History`
2. Should see new view events in the activity list
3. Filter by "View" action type
4. Should see matter views, calendar views, etc.

**Expected:** All view events visible with proper formatting

---

### 4. Comprehensive Audit Log Check

#### **Check All Recent Activity:**
```bash
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 50 Id, EntityType, EntityId, Action, IsAIAction, UserName, IPAddress, Description, Timestamp FROM CertioLocal.dbo.AuditLogs ORDER BY Timestamp DESC"
```

**Expected Types:**
- ✅ Create (Matter, Task, Document, CalendarEvent, ChatMessage)
- ✅ Update (Matter, Task, Document, CalendarEvent)
- ✅ Delete / SoftDelete (any entity)
- ✅ **View (Matter, CalendarEvent)** ← NEW!
- ✅ FirmAccess
- ✅ Conversation
- ✅ Login / Logout

#### **Check Action Distribution:**
```bash
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT Action, COUNT(*) AS Count FROM CertioLocal.dbo.AuditLogs GROUP BY Action ORDER BY Count DESC"
```

**Expected:** Should now see "View" in the list

#### **Check AI Actions:**
```bash
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT AIAgentType, COUNT(*) AS Count FROM CertioLocal.dbo.AuditLogs WHERE IsAIAction = 1 GROUP BY AIAgentType ORDER BY Count DESC"
```

**Expected:** AI agent types with counts

---

### 5. Performance Testing

#### **Check Response Times:**
1. Open browser DevTools (F12)
2. Navigate to matter details page
3. Check Network tab - page should load quickly
4. ViewAudit attribute should NOT add noticeable delay

**Expected:** < 50ms additional overhead (audit logging is async)

---

### 6. Error Handling Test

#### **Test Invalid Entity ID:**
1. Try to view non-existent matter: `/Client/1/Matter/Details/99999`
2. Check audit logs - should NOT have view event for non-existent entity
3. Check logs for errors

**Expected:** No audit log created, no errors thrown

---

## Common Issues & Solutions

### ❌ No Audit Logs Created
**Symptom:** View events not appearing in AuditLogs table

**Check:**
1. Is user authenticated? (CustomUser in HttpContext.Items)
2. Is entity ID valid and > 0?
3. Check application logs for ViewAudit errors

**Solution:**
```bash
# Check application logs
docker logs certio-web
```

---

### ❌ AI Chat Logs Not Appearing
**Symptom:** ChatMessage audit logs missing or IsAIAction = false

**Check:**
1. Is `IsAIGenerated` flag set on ChatMessage?
2. Is AuditInterceptor properly configured in DbContext?
3. Are AI messages being saved via DbContext.SaveChanges()?

**Solution:** Verify ChatService.CreateAIMessage sets IsAIGenerated = true

---

### ❌ Wrong Entity Type or ID
**Symptom:** Audit logs have EntityType='0' or EntityId=0

**Check:**
1. Is parameter name correct in ViewAudit attribute?
2. Is parameter actually in route/action arguments?
3. Check attribute constructor parameters

**Solution:**
```csharp
// Correct
[ViewAudit("Matter", "id")] // "id" must match parameter name
public async Task<IActionResult> Details(int orgId, int? id)

// Wrong
[ViewAudit("Matter", "matterId")] // ❌ Wrong parameter name
public async Task<IActionResult> Details(int orgId, int? id)
```

---

## Success Criteria

✅ **View Events Logged:**
- Matter views create audit logs
- Calendar event views create audit logs
- Entity ID, user info, IP captured correctly

✅ **AI Chat Logged:**
- AI messages have IsAIAction = true
- AIAgentType is populated
- SourceConversationId is set

✅ **History Page Shows Events:**
- View events visible in History page
- Filter by action type works
- Proper formatting and display

✅ **Performance Acceptable:**
- No noticeable slowdown
- Audit logging is non-blocking
- No errors in logs

✅ **Comprehensive Coverage:**
- All CRUD operations logged
- View operations logged
- AI operations logged
- System operations logged

---

## Quick Reference: All Docker Commands

### View-Only Commands:
```bash
# All View events
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT Id, EntityType, EntityId, UserName, Description, Timestamp FROM CertioLocal.dbo.AuditLogs WHERE Action = 'View' ORDER BY Timestamp DESC"

# Matter View events
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT Id, EntityId, UserName, IPAddress, Timestamp FROM CertioLocal.dbo.AuditLogs WHERE EntityType = 'Matter' AND Action = 'View' ORDER BY Timestamp DESC"

# Calendar Event Views
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT Id, EntityId, UserName, IPAddress, Timestamp FROM CertioLocal.dbo.AuditLogs WHERE EntityType = 'CalendarEvent' AND Action = 'View' ORDER BY Timestamp DESC"
```

### AI-Only Commands:
```bash
# All AI actions
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT Id, EntityType, EntityId, AIAgentType, SourceConversationId, Description, Timestamp FROM CertioLocal.dbo.AuditLogs WHERE IsAIAction = 1 ORDER BY Timestamp DESC"

# AI Chat messages
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT Id, EntityId, AIAgentType, SourceConversationId, Description, Timestamp FROM CertioLocal.dbo.AuditLogs WHERE EntityType = 'ChatMessage' AND IsAIAction = 1 ORDER BY Timestamp DESC"
```

### Summary Commands:
```bash
# Action type distribution
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT Action, COUNT(*) AS Count FROM CertioLocal.dbo.AuditLogs GROUP BY Action ORDER BY Count DESC"

# Entity type distribution
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT EntityType, COUNT(*) AS Count FROM CertioLocal.dbo.AuditLogs GROUP BY EntityType ORDER BY Count DESC"

# Recent activity (last 24 hours)
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT Id, EntityType, Action, UserName, Timestamp FROM CertioLocal.dbo.AuditLogs WHERE Timestamp >= DATEADD(hour, -24, GETDATE()) ORDER BY Timestamp DESC"
```

---

## Expected Results After Full Testing

### Before:
- ~100-150 audit logs
- Only Create, Update, Delete
- No AI chat logs

### After:
- **200-500+ audit logs**
- ✅ Create, Update, Delete, **View**
- ✅ AI chat messages fully logged
- ✅ Complete audit trail

---

**Ready to test!** 🚀

