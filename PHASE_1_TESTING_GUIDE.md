# PHASE 1 Security Testing Guide
**Date:** October 11, 2025  
**Testing Phase:** Security Hardening Validation  
**Priority:** HIGH  
**Estimated Time:** 2-3 hours

---

## 🎯 Testing Objectives

This guide will walk you through comprehensive testing of all PHASE 1 security fixes to ensure:
1. ✅ IDOR vulnerabilities are completely eliminated
2. ✅ Cross-organization data leakage is prevented
3. ✅ Authorization checks work correctly
4. ✅ Audit logging captures all security events
5. ✅ Input validation prevents malicious inputs
6. ✅ Legitimate users can still perform authorized operations

---

## 📋 Pre-Testing Setup

### 1. Verify Database State

**Check if AuditLogs table exists and is ready:**
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 10 * FROM CertioLocal.dbo.AuditLogs ORDER BY Timestamp DESC"
```

**Verify existing test data:**
```powershell
# Check Organizations
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT Id, Name, Type FROM CertioLocal.dbo.Organizations ORDER BY Id"

# Check Users
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT Id, Email, FirstName, LastName FROM CertioLocal.dbo.Users ORDER BY Id"

# Check Matters
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT Id, Title, OrganizationId, AccessLevel FROM CertioLocal.dbo.Matters ORDER BY Id"

# Check Tasks
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT Id, Title, OrgId, MatterId FROM CertioLocal.dbo.TaskItems ORDER BY Id"
```

**Expected:** You should see existing organizations, users, matters, and tasks.

### 2. Test Environment Setup

**Option A: Local Development**
```bash
# Navigate to project
cd C:\Projects\Certio\Certio.Web

# Build the project
dotnet build

# Run the application
dotnet run
```

**Option B: Use existing running instance**
- If server is already running, skip to testing

**Verify Application is Running:**
- Open browser: `https://localhost:5001` (or your configured port)
- You should see the Certio login page

### 3. Prepare Test Users and Organizations

You'll need at least:
- **Organization A** with **User A** (member)
- **Organization B** with **User B** (member)
- At least 1 matter in each organization
- At least 1 task in each organization

**If you don't have test data, create it:**
1. Register two different users
2. Create an organization for each user
3. Create a matter in each organization
4. Create a task in each organization

**Document your test data:**
```
Organization A:
- ID: _____
- Name: _____
- User A ID: _____
- User A Email: _____
- Matter A ID: _____
- Task A ID: _____

Organization B:
- ID: _____
- Name: _____
- User B ID: _____
- User B Email: _____
- Matter B ID: _____
- Task B ID: _____
```

---

## 🧪 Test Suite 1: Critical IDOR Tests

### TEST-IDOR-001: Cross-Organization Matter Edit Prevention ⚠️ CRITICAL

**Objective:** Verify User A cannot edit Matter B (from Organization B)

**Steps:**
1. Login as **User A**
2. Note a Matter ID from **Organization B**: `_____`
3. Manually navigate to: `/Matter/Edit/{Matter_B_ID}`
   - Example: If Matter B ID is 5, go to `/Matter/Edit/5`

**Expected Result:** 
- ❌ 404 Not Found page
- OR redirected away from edit page
- Matter does NOT load for editing

**Actual Result:** _____________________

**Check Audit Log:**
```powershell
# First, check ALL recent audit logs to see what was logged
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 10 Id, EntityType, EntityId, Action, Result, UserId, IPAddress, UserAgent, Description, Timestamp FROM CertioLocal.dbo.AuditLogs ORDER BY Id DESC"
```
**Expected:** 
- For cross-org access (like `/Client/2/...`): Look for `Action = 'AUTH_FAILURE'` with `EntityType = 'FirmAccess'` and `Result = 'FAILURE'`
- For within-org matter access (like `/Client/1/Matter/Edit/3` where matter 3 is in org 2): Look for `Action = 'AUTH_FAILURE'` with `EntityType = 'Matter'` and `Result = 'FAILURE'`

**Status:** [ ] PASS, but testing using User A's original organization to edit other organization's matter does not log in the AuditLog.

---

### TEST-IDOR-002: Cross-Organization Matter Delete Prevention ⚠️ CRITICAL

**Objective:** Verify User A cannot delete Matter B

**Steps:**
1. Still logged in as **User A**
2. Try to access: `/Matter/Delete/{Matter_B_ID}`

**Expected Result:**
- ❌ 404 Not Found page
- OR redirected away

**Actual Result:** _____________________

**Advanced Test (if you have API access):**
```javascript
// In browser console on Organization A page:
fetch('/Matter/DeleteConfirmed', {
    method: 'POST',
    headers: {
        'Content-Type': 'application/json',
        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
    },
    body: JSON.stringify({ id: MATTER_B_ID })
});
```

**Expected:** Error response, matter NOT deleted

**Verify Matter Still Exists:**
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT * FROM CertioLocal.dbo.Matters WHERE Id = {Matter_B_ID}"
```
**Expected:** Matter still exists with all data intact

**Status:** [ ] PASS

---

### TEST-IDOR-003: Cross-Organization Task Update Prevention ⚠️ CRITICAL

**Objective:** Verify User A cannot update Task B

**Steps:**
1. Still logged in as **User A**
2. Open browser developer console (F12)
3. Go to Network tab
4. Execute this in Console:
```javascript
// Replace TASK_B_ID with actual Task B ID
fetch('/Tasks/Update', {
    method: 'POST',
    headers: {
        'Content-Type': 'application/json',
    },
    body: JSON.stringify({
        Id: TASK_B_ID,
        Title: 'HACKED - This should not work',
        Status: 'Completed'
    })
})
.then(r => r.json())
.then(data => console.log('Response:', data));
```

**Expected Response:**
```json
{
    "success": false,
    "message": "Task not found"
}
```

**Verify Task Unchanged:**
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT Id, Title, Status FROM CertioLocal.dbo.TaskItems WHERE Id = {Task_B_ID}"
```
**Expected:** Title is NOT "HACKED - This should not work"

**Check Audit Log:**
```powershell
# Check ALL recent audit logs
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 10 Id, EntityType, EntityId, Action, Result, UserId, IPAddress, Description, Timestamp FROM CertioLocal.dbo.AuditLogs ORDER BY Id DESC"
```
**Expected:** New entry with `Action = 'AUTH_FAILURE'` and `Result = 'FAILURE'` where access was blocked

**Status:** [ ] PASS

---

### TEST-IDOR-004: Cross-Organization Task Delete Prevention ⚠️ CRITICAL

**Objective:** Verify User A cannot delete Task B

**Steps:**
1. Still logged in as **User A**
2. In browser console:
```javascript
fetch('/Tasks/Delete', {
    method: 'POST',
    headers: {
        'Content-Type': 'application/json',
    },
    body: JSON.stringify({ id: TASK_B_ID })
})
.then(r => r.json())
.then(data => console.log('Response:', data));
```

**Expected Response:**
```json
{
    "success": false,
    "message": "Task not found"
}
```

**Verify Task Still Exists:**
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT * FROM CertioLocal.dbo.TaskItems WHERE Id = {Task_B_ID}"
```
**Expected:** Task exists and is NOT deleted

**Status:** [ ] PASS

---

### TEST-IDOR-005: Matter ID Enumeration Prevention ⚠️ HIGH

**Objective:** Verify User A can only see matters from authorized organizations

**Steps:**
1. Still logged in as **User A**
2. Try accessing sequential matter IDs: `/Matter/Details/1`, `/Matter/Details/2`, etc.
3. For matters NOT in User A's organization:

**Expected Result:**
- ❌ 404 Not Found for unauthorized matters
- ✅ Matter details page ONLY for matters in User A's organizations

**Document Results:**
```
Matter ID 1: Organization ID ____ → [ ] Accessible  [ ] 404
Matter ID 2: Organization ID ____ → [ ] Accessible  [ ] 404
Matter ID 3: Organization ID ____ → [ ] Accessible  [ ] 404
```

**Key Validation:** User should NEVER see matter details from organizations they don't belong to

**Status:** [ ] PASS

---

### TEST-IDOR-006: Unauthenticated Channel Access Prevention ⚠️ CRITICAL

**Objective:** Verify anonymous users cannot access channel messages

**Steps:**
1. **Logout** completely from the application
2. Clear cookies (or use incognito/private window)
3. Try to access: `/Client/1/Chat/channel/1/messages`

**Expected Result:**
- ❌ 401 Unauthorized OR redirect to login page
- NO message data should be visible

**Advanced Test (using curl or Postman):**
```bash
curl -i https://localhost:5001/Client/1/Chat/channel/1/messages
```

**Expected Headers:**
```
HTTP/1.1 401 Unauthorized
or
HTTP/1.1 302 Found
Location: /Identity/Account/Login
```

**CRITICAL:** If you see message data WITHOUT being logged in, this is a FAILED test!

**Status:** [ ] PASS

---

## 🧪 Test Suite 2: Authorization Success Tests

### TEST-AUTH-001: Authorized Matter Edit ✅ Positive Test

**Objective:** Verify User A CAN edit Matter A (from their own org)

**Steps:**
1. Login as **User A**
2. Navigate to `/Matter/Edit/{Matter_A_ID}`

**Expected Result:**
- ✅ Edit form loads successfully
- ✅ Matter details are visible
- ✅ Can modify and save changes

**Test Edit:**
1. Change matter title to: "Test Edit - {Current DateTime}"
2. Click Save
3. Verify change persisted

**Check Audit Log:**
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 1 * FROM CertioLocal.dbo.AuditLogs WHERE Action = 'UPDATE' AND EntityType = 'Matter' AND EntityId = {Matter_A_ID} ORDER BY Timestamp DESC"
```
**Expected:** New audit entry showing the update

**Status:** [ ] PASS  [ ] FAIL

---

### TEST-AUTH-002: Authorized Task Update ✅ Positive Test

**Objective:** Verify User A CAN update Task A

**Steps:**
1. Login as **User A**
2. Navigate to task list for Matter A
3. Click edit on Task A

**Expected Result:**
- ✅ Task details load
- ✅ Can modify title, status, etc.
- ✅ Changes save successfully

**Check Audit Log:**
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 1 * FROM CertioLocal.dbo.AuditLogs WHERE Action = 'UPDATE' AND EntityType = 'Task' AND EntityId = {Task_A_ID} ORDER BY Timestamp DESC"
```

**Status:** [ ] PASS

---

### TEST-AUTH-003: Authorized Channel Access ✅ Positive Test

**Objective:** Verify User A CAN access channels in their organization

**Steps:**
1. Login as **User A**
2. Navigate to `/Client/{Org_A_ID}/Chat/channel/{Channel_ID}/messages`

**Expected Result:**
- ✅ Messages load successfully
- ✅ Can see channel history
- ✅ Can send new messages

**Status:** [ ] PASS

---

## 🧪 Test Suite 3: Matter Access Level Tests

These tests verify the "Everyone" vs "Specific" access level functionality.

### TEST-MATTER-001: "Everyone" Access Level ✅

**Objective:** Verify organization members can access "Everyone" matters

**Setup:**
1. Login as Owner of Organization A
2. Create a new matter with:
   - Title: "Test Everyone Access"
   - Access Level: "Everyone"
3. Note the Matter ID: _____

**Test:**
1. Login as a different member of Organization A (not the creator)
2. Navigate to `/Matter/Details/{Matter_ID}`

**Expected Result:**
- ✅ Matter details load successfully
- ✅ No permission error

**Verify in Database:**
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT Id, Title, AccessLevel FROM CertioLocal.dbo.Matters WHERE Id = {Matter_ID}"
```

**Status:** [ ] PASS  [ ] FAIL

---

### TEST-MATTER-002: "Specific" Access Level - Unauthorized ⚠️ CRITICAL

**Objective:** Verify users NOT in MatterPermissions cannot access "Specific" matters

**Setup:**
1. Login as Owner of Organization A
2. Create a new matter with:
   - Title: "Test Specific Access - Restricted"
   - Access Level: "Specific"
   - Grant permission to ONLY one specific user (not you for this test)
3. Note the Matter ID: _____
4. Verify MatterPermissions:
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT * FROM CertioLocal.dbo.MatterPermissions WHERE MatterId = {Matter_ID}"
```

**Test:**
1. Login as a DIFFERENT member of Organization A who is NOT in MatterPermissions
2. Navigate to `/Matter/Details/{Matter_ID}`

**Expected Result:**
- ❌ 404 Not Found OR Access Denied
- User CANNOT see the matter even though they're in the same organization

**Check Audit Log:**
```powershell
# Check ALL recent audit logs to find the authorization failure
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 10 Id, EntityType, EntityId, Action, Result, UserId, IPAddress, Description, Timestamp FROM CertioLocal.dbo.AuditLogs ORDER BY Id DESC"
```
**Expected:** Authorization failure with `Action = 'AUTH_FAILURE'`, `Result = 'FAILURE'`, `EntityType = 'Matter'`, Description containing "FAILURE: NoSpecificPermission"

**Status:** [ ] PASS  [ ] FAIL

---

### TEST-MATTER-003: "Specific" Access Level - Authorized ✅

**Objective:** Verify users IN MatterPermissions CAN access "Specific" matters

**Setup:**
Using the same "Specific" matter from TEST-MATTER-002

**Test:**
1. Login as the user who WAS granted permission
2. Navigate to `/Matter/Details/{Matter_ID}`

**Expected Result:**
- ✅ Matter details load successfully
- ✅ User can view the matter

**Verify Permission:**
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT * FROM CertioLocal.dbo.MatterPermissions WHERE MatterId = {Matter_ID} AND UserId = {Current_User_ID} AND RevokedAt IS NULL"
```
**Expected:** One row showing active permission

**Status:** [ ] PASS  [ ] FAIL

---

## 🧪 Test Suite 4: Input Validation Tests

### TEST-INPUT-001: Oversized Title Rejection

**Objective:** Verify titles over 200 characters are rejected/truncated

**Steps:**
1. Login as User A
2. Try to create a matter with title of 250 characters:
```
This is a very long title that exceeds the maximum allowed length of two hundred characters and should be truncated or rejected by the input validation system to prevent potential database issues or buffer overflow attacks that could compromise the security of the application
```

**Expected Result:**
- Either: Title truncated to 200 characters
- Or: Validation error message

**Check Database:**
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT Title, LEN(Title) as TitleLength FROM CertioLocal.dbo.Matters ORDER BY Id DESC"
```
**Expected:** Title length <= 200

**Status:** [ ] PASS  [ ] FAIL

---

### TEST-INPUT-002: Invalid Status Rejection

**Objective:** Verify invalid status values are rejected

**Steps:**
1. Open browser console
2. Try to update task with invalid status:
```javascript
fetch('/Tasks/UpdateStatus', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
        Id: TASK_A_ID,
        Status: 'INVALID_STATUS_HACKED'
    })
})
.then(r => r.json())
.then(data => console.log(data));
```

**Expected Response:**
```json
{
    "success": false,
    "message": "Invalid status value"
}
```

**Verify Task Unchanged:**
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT Status FROM CertioLocal.dbo.TaskItems WHERE Id = {Task_A_ID}"
```
**Expected:** Status NOT changed to "INVALID_STATUS_HACKED"

**Status:** [ ] PASS  [ ] FAIL

---

### TEST-INPUT-003: Negative ID Rejection

**Objective:** Verify negative IDs are rejected

**Steps:**
```javascript
fetch('/Tasks/Get/-1', {
    method: 'GET'
})
.then(r => r.json())
.then(data => console.log(data));
```

**Expected Response:**
```json
{
    "success": false,
    "message": "Invalid task ID" 
}
```

**Status:** [ ] PASS  [ ] FAIL

---

## 📋 Understanding Audit Logging

### Two Types of Authorization Failures

**The system logs authorization failures at TWO different levels:**

1. **Middleware Level (Organization Access)**
   - **When:** User tries to access an organization they don't belong to
   - **Action:** `'AccessDenied'`
   - **EntityType:** `'FirmAccess'`
   - **Example:** User from Org A tries to access `/Client/2/Matter/Edit/3` (Org B)
   - **Logged By:** `FirmAccessAuditService` in `ClientAccessMiddleware`

2. **Controller Level (Resource Access)**
   - **When:** User IS in the org but lacks permission to specific resource
   - **Action:** `'AUTHORIZATION_FAILURE'`
   - **EntityType:** `'Matter'`, `'Task'`, etc
   - **Example:** User in Org A tries to access a "Specific" matter without `MatterPermissions`
   - **Logged By:** `AuditService` via `AuthorizationHelper`

**Why Two Types?**
- Middleware blocks at the organization boundary (faster, before controllers run)
- Controllers block at the resource level (specific permissions within an org)

---

## 🧪 Test Suite 5: Audit Logging Verification

### TEST-AUDIT-001: Create Operation Logging

**Objective:** Verify create operations are logged

**Steps:**
1. Login as User A
2. Note current time: _____
3. Create a new matter
4. Note the new Matter ID: _____

**Verify Audit Log:**
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT * FROM CertioLocal.dbo.AuditLogs WHERE Action = 'CREATE' AND EntityType = 'Matter' AND EntityId = {New_Matter_ID} ORDER BY Timestamp DESC"
```

**Expected Fields:**
- UserId: {User_A_ID}
- Action: 'CREATE'
- EntityType: 'Matter'
- EntityId: {New_Matter_ID}
- Timestamp: Recent (within last few minutes)

**Status:** [ ] PASS  [ ] FAIL

---

### TEST-AUDIT-002: Update Operation Logging

**Objective:** Verify update operations are logged

**Steps:**
1. Update an existing matter
2. Check audit log

**Verify:**
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 5 * FROM CertioLocal.dbo.AuditLogs WHERE Action = 'UPDATE' AND EntityType = 'Matter' ORDER BY Timestamp DESC"
```

**Expected:** Recent UPDATE entry

**Status:** [ ] PASS  [ ] FAIL

---

### TEST-AUDIT-003: Delete Operation Logging

**Objective:** Verify delete operations are logged

**Steps:**
1. Create a test matter: "TO DELETE"
2. Delete it
3. Check audit log

**Verify:**
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 5 * FROM CertioLocal.dbo.AuditLogs WHERE Action = 'DELETE' AND EntityType = 'Matter' ORDER BY Timestamp DESC"
```

**Expected:** Recent DELETE entry with correct EntityId

**Status:** [ ] PASS  [ ] FAIL

---

### TEST-AUDIT-004: Authorization Failure Logging

**Objective:** Verify failed access attempts are logged

**Steps:**
1. Perform TEST-IDOR-001 (try to access unauthorized matter)
2. Check audit log

**Verify:**
```powershell
# Check ALL recent audit logs first to see what's being logged
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 20 Id, EntityType, EntityId, Action, Result, UserId, IPAddress, Description, Timestamp FROM CertioLocal.dbo.AuditLogs ORDER BY Id DESC"

# Then filter by specific types if needed:
# All authorization failures:
# docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 10 * FROM CertioLocal.dbo.AuditLogs WHERE Action = 'AUTH_FAILURE' ORDER BY Id DESC"

# Failures with IP addresses (for forensics):
# docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 10 Id, EntityType, EntityId, Result, UserId, IPAddress, UserAgent, Description, Timestamp FROM CertioLocal.dbo.AuditLogs WHERE Result = 'FAILURE' ORDER BY Id DESC"
```

**Expected Fields:**
- **All authorization failures:** Action = 'AUTH_FAILURE', Result = 'FAILURE'
- **EntityType:** 'FirmAccess' (middleware) or 'Matter'/'Task'/etc (controllers)
- **Description:** Contains failure reason in format "FAILURE: {ReasonCode}"
- **IPAddress:** Client IP address
- **UserAgent:** Browser/client user agent

**Note:** The Id column auto-increments, so higher Id numbers = more recent logs

**Important:** This allows security monitoring for attack attempts

**Status:** [ ] PASS  [ ] FAIL

---

## 🧪 Test Suite 6: Law Firm Access Tests

### TEST-FIRM-001: Law Firm Access to Client Matter ✅

**Objective:** Verify law firm users can access client matters when there's a valid relationship

**Prerequisites:**
- Organization C: Law Firm
- Organization D: Client
- Active OrganizationRelationship between C and D
- User C: Member of Law Firm (Organization C)
- Matter D: Belongs to Client (Organization D)

**Steps:**
1. Verify relationship exists:
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT * FROM CertioLocal.dbo.OrganizationRelationships WHERE OrganizationId = {Org_C_ID} AND TargetOrganizationId = {Org_D_ID} AND IsActive = 1 AND IsDeleted = 0"
```

2. Login as User C (law firm member)
3. Try to access Matter D (client matter)

**Expected Result:**
- ✅ Law firm user CAN access client matter
- ✅ Access granted through firm relationship

**This tests the `HasFirmBasedAccessAsync()` method**

**Status:** [ ] PASS  [ ] FAIL  [ ] SKIP (no firm data)

---

## 📊 Test Results Summary

### Critical Tests Results

| Test ID | Test Name | Priority | Status | Notes |
|---------|-----------|----------|--------|-------|
| TEST-IDOR-001 | Cross-Org Matter Edit | CRITICAL | [ ] P [ ] F | |
| TEST-IDOR-002 | Cross-Org Matter Delete | CRITICAL | [ ] P [ ] F | |
| TEST-IDOR-003 | Cross-Org Task Update | CRITICAL | [ ] P [ ] F | |
| TEST-IDOR-004 | Cross-Org Task Delete | CRITICAL | [ ] P [ ] F | |
| TEST-IDOR-005 | Matter ID Enumeration | HIGH | [ ] P [ ] F | |
| TEST-IDOR-006 | Unauth Channel Access | CRITICAL | [ ] P [ ] F | |
| TEST-MATTER-002 | Specific Access Block | CRITICAL | [ ] P [ ] F | |

### Positive Tests Results

| Test ID | Test Name | Status | Notes |
|---------|-----------|--------|-------|
| TEST-AUTH-001 | Authorized Matter Edit | [ ] P [ ] F | |
| TEST-AUTH-002 | Authorized Task Update | [ ] P [ ] F | |
| TEST-AUTH-003 | Authorized Channel Access | [ ] P [ ] F | |
| TEST-MATTER-001 | Everyone Access | [ ] P [ ] F | |
| TEST-MATTER-003 | Specific Access Allow | [ ] P [ ] F | |

### Input Validation Results

| Test ID | Test Name | Status | Notes |
|---------|-----------|--------|-------|
| TEST-INPUT-001 | Oversized Title | [ ] P [ ] F | |
| TEST-INPUT-002 | Invalid Status | [ ] P [ ] F | |
| TEST-INPUT-003 | Negative ID | [ ] P [ ] F | |

### Audit Logging Results

| Test ID | Test Name | Status | Notes |
|---------|-----------|--------|-------|
| TEST-AUDIT-001 | Create Logging | [ ] P [ ] F | |
| TEST-AUDIT-002 | Update Logging | [ ] P [ ] F | |
| TEST-AUDIT-003 | Delete Logging | [ ] P [ ] F | |
| TEST-AUDIT-004 | Failure Logging | [ ] P [ ] F | |

---

## 🎯 Success Criteria

**To PASS Phase 1 Testing:**

✅ **ALL Critical Tests MUST PASS** (7/7)
✅ **At least 90% of other tests MUST PASS**
✅ **Audit logging MUST work** for all CUD operations
✅ **No legitimate user operations MUST be blocked**

**If ANY critical test fails:**
1. Document the failure
2. DO NOT proceed to production
3. Review the code for the specific controller/method
4. Re-test after fixes

---

## 🚨 What To Do If Tests Fail

### Failure Analysis Template

```
Test Failed: TEST-IDOR-XXX
Date/Time: ___________
Tester: ___________

Failure Description:
_______________________________________

Expected Behavior:
_______________________________________

Actual Behavior:
_______________________________________

Steps to Reproduce:
1. 
2. 
3. 

Error Messages/Logs:
_______________________________________

Database State (if relevant):
SQL Query Results:
_______________________________________

Browser Console Errors:
_______________________________________

Suspected Cause:
_______________________________________

Priority: [ ] CRITICAL  [ ] HIGH  [ ] MEDIUM  [ ] LOW
```

### Common Issues & Solutions

**Issue:** "Task not found" even for authorized tasks
- **Check:** User organization membership
- **Verify:** Task's OrgId matches user's organization
- **Command:** 
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT t.Id, t.Title, t.OrgId, uo.OrganizationId, uo.IsActive FROM CertioLocal.dbo.TaskItems t CROSS JOIN CertioLocal.dbo.UserOrganizations uo WHERE t.Id = {TaskId} AND uo.UserId = {UserId}"
```

**Issue:** "Matter not found" even for authorized matters
- **Check:** Matter AccessLevel
- **Verify:** If "Specific", check MatterPermissions
- **Command:**
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT m.*, mp.UserId FROM CertioLocal.dbo.Matters m LEFT JOIN CertioLocal.dbo.MatterPermissions mp ON m.Id = mp.MatterId AND mp.RevokedAt IS NULL WHERE m.Id = {MatterId}"
```

**Issue:** Audit logs not appearing
- **Check:** Database connection
- **Verify:** AuditService is registered in DI
- **Check:** No exceptions in application logs

---

## 📝 Testing Completion Checklist

Before signing off:

- [ ] All critical tests executed
- [ ] All test results documented
- [ ] Any failures reported and tracked
- [ ] Audit log verification complete
- [ ] Positive tests confirm authorized access works
- [ ] Database queries confirmed data integrity
- [ ] No unexpected errors in application logs
- [ ] Test data documented for future reference

**Tester Name:** ______________________  
**Test Date:** ______________________  
**Overall Result:** [ ] PASS  [ ] FAIL  
**Ready for Production:** [ ] YES  [ ] NO  

**Notes/Comments:**
_____________________________________________________
_____________________________________________________
_____________________________________________________

---

## 📚 Additional Resources

**Related Documents:**
- `PHASE_0_SECURITY_ARCHITECTURE_ASSESSMENT.md` - Original vulnerability assessment
- `PHASE_1_IMPLEMENTATION_SUMMARY.md` - Implementation details
- `PHASE_1_VERIFICATION_REPORT.md` - Code verification results
- `PHASE_1_DEPLOYMENT_CHECKLIST.md` - Deployment procedures

**Code References:**
- `Certio.Web/Security/AuthorizationHelper.cs` - Authorization logic
- `Certio.Web/Security/InputValidator.cs` - Validation logic
- `Certio.Web/Services/AuditService.cs` - Audit logging
- `Certio.Web/Controllers/MatterController.cs` - Matter operations
- `Certio.Web/Controllers/TasksController.cs` - Task operations
- `Certio.Web/Controllers/ChatController.cs` - Chat operations

---

**Testing Guide Version:** 1.0  
**Last Updated:** October 11, 2025  
**Next Review:** After deployment to staging


