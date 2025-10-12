# Phase 1 Security Hardening - Deployment Checklist

**Date:** October 11, 2025  
**Phase:** Emergency Security Fixes  
**Priority:** HIGH  

---

## ✅ Pre-Deployment Verification

### Code Review
- [ ] Review `AuthorizationHelper.cs` implementation
- [ ] Review `AuditService.cs` implementation
- [ ] Review `InputValidator.cs` implementation
- [ ] Verify all IDOR fixes in `MatterController.cs`
- [ ] Verify all IDOR fixes in `TasksController.cs`
- [ ] Verify critical `[AllowAnonymous]` removal in `ChatController.cs`
- [ ] Check `Program.cs` DI registrations

### Testing
- [ ] Run unit tests: `dotnet test`
- [ ] Manual test: Login and navigate through app
- [ ] Manual test: Try to access matter from different org (should fail)
- [ ] Manual test: Try to update task from different org (should fail)
- [ ] Manual test: Verify unauthorized access returns 404 (not 403 or exception)
- [ ] Manual test: Create a matter and verify audit log entry
- [ ] Manual test: Delete a task and verify audit log entry
- [ ] Check `AuditLogs` table has recent entries

### Build Verification
- [ ] Clean build: `dotnet clean && dotnet build`
- [ ] No compiler errors
- [ ] No linter errors
- [ ] Publish build: `dotnet publish -c Release`

---

## 🚀 Deployment Steps

### 1. Database Preparation

**Verify AuditLogs table exists:**
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 10 * FROM CertioLocal.dbo.AuditLogs ORDER BY Timestamp DESC"
```

**Check table structure:**
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT COLUMN_NAME, DATA_TYPE FROM CertioLocal.INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AuditLogs'"
```

- [ ] AuditLogs table exists
- [ ] Table has columns: UserId, Action, EntityType, EntityId, Timestamp, IPAddress, Description

### 2. Backup Current System
```bash
# Backup database
# (Your backup command here)

# Tag current git commit
git tag pre-phase1-security-fixes
git push --tags
```
- [ ] Database backed up
- [ ] Git tagged
- [ ] Rollback plan documented

### 3. Deploy to Staging
```bash
# Build release
dotnet publish -c Release -o ./publish

# Deploy to staging
# (Your staging deployment process)
```
- [ ] Code deployed to staging
- [ ] Staging environment accessible
- [ ] No deployment errors

### 4. Staging Smoke Tests
- [ ] Application starts successfully
- [ ] Login works
- [ ] Matter list loads
- [ ] Can create a new matter
- [ ] Can edit a matter (authorized user)
- [ ] Cannot edit matter from different org (returns 404)
- [ ] Task list loads
- [ ] Can create a new task
- [ ] Can update a task (authorized user)
- [ ] Cannot update task from different org (returns 404)
- [ ] Chat/Communications accessible
- [ ] Channel messages require authentication
- [ ] Anonymous access to `/Chat/channel/{id}/messages` returns 401
- [ ] Check AuditLogs table for new entries:
  ```powershell
  docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 20 Id, EntityType, EntityId, Action, Result, UserId, IPAddress, Description, Timestamp FROM CertioLocal.dbo.AuditLogs ORDER BY Id DESC"
  ```

### 5. Security Validation Tests
- [ ] **Test 1:** User A tries to access `/Matter/Edit/[Matter_from_Org_B]`
  - Expected: 404 Not Found
  - Actual: ___________
  - [ ] PASS

- [ ] **Test 2:** User A tries to POST `/Tasks/Update` with task from Org B
  - Expected: JSON `{ success: false, message: "Task not found" }`
  - Actual: ___________
  - [ ] PASS

- [ ] **Test 3:** User A tries to POST `/Tasks/Delete` with task from Org B
  - Expected: JSON `{ success: false, message: "Task not found" }`
  - Actual: ___________
  - [ ] PASS

- [ ] **Test 4:** Unauthenticated user accesses `/Client/1/Chat/channel/1/messages`
  - Expected: 401 Unauthorized
  - Actual: ___________
  - [ ] PASS

- [ ] **Test 5:** Create a matter and verify audit log
  - Expected: New entry in AuditLogs with Action='CREATE', EntityType='Matter'
  - Actual: ___________
  - [ ] PASS

### 6. Performance Monitoring
Monitor staging for 2-4 hours:
- [ ] Response times normal (<10ms increase acceptable)
- [ ] Error rates stable or lower
- [ ] No unexpected exceptions in logs
- [ ] Database performance acceptable
- [ ] Audit log growth rate reasonable

### 7. Production Deployment
**Only proceed if all staging tests pass!**

```bash
# Deploy to production
# (Your production deployment process)
```
- [ ] Code deployed to production
- [ ] Production environment accessible
- [ ] No deployment errors

### 8. Production Smoke Tests
Repeat critical tests from staging:
- [ ] Application starts successfully
- [ ] Login works
- [ ] Matter CRUD operations work
- [ ] Task CRUD operations work
- [ ] Chat/Communications work
- [ ] Unauthorized access returns 404
- [ ] AuditLogs table receiving entries

### 9. Production Monitoring (First 24 Hours)
- [ ] Hour 1: Check error logs, no critical errors
- [ ] Hour 2: Verify audit logs growing normally
- [ ] Hour 4: Check performance metrics
- [ ] Hour 8: Review security logs for suspicious activity
- [ ] Hour 24: Full health check

---

## 🔍 Monitoring Queries

### Check Audit Log Activity

**Recent audit activity:**
```powershell
# Show all recent audit logs (ordered by Id for reliability)
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 50 Id, EntityType, EntityId, Action, Result, UserId, IPAddress, Description, Timestamp FROM CertioLocal.dbo.AuditLogs ORDER BY Id DESC"
```

**Authorization failures (potential attacks):**
```powershell
# All authorization failures (unified query)
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT UserId, EntityType, EntityId, IPAddress, Description AS Reason, COUNT(*) as FailureCount FROM CertioLocal.dbo.AuditLogs WHERE Action = 'AUTH_FAILURE' AND Result = 'FAILURE' AND Timestamp > DATEADD(hour, -24, GETUTCDATE()) GROUP BY UserId, EntityType, EntityId, IPAddress, Description HAVING COUNT(*) > 5 ORDER BY FailureCount DESC"

# Failures by IP address (for blocking)
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT IPAddress, COUNT(*) as FailureCount, MIN(Timestamp) as FirstFailure, MAX(Timestamp) as LastFailure FROM CertioLocal.dbo.AuditLogs WHERE Action = 'AUTH_FAILURE' AND Timestamp > DATEADD(hour, -24, GETUTCDATE()) GROUP BY IPAddress HAVING COUNT(*) > 10 ORDER BY FailureCount DESC"
```

**Activity by user:**
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT UserId, Action, COUNT(*) as ActionCount FROM CertioLocal.dbo.AuditLogs WHERE Timestamp > DATEADD(hour, -24, GETUTCDATE()) GROUP BY UserId, Action ORDER BY UserId, ActionCount DESC"
```

### Check Application Logs
```bash
# Check for errors
grep -i "error\|exception\|security" /var/log/certio/app.log | tail -50

# Check for authorization failures
grep "SECURITY:" /var/log/certio/app.log | tail -20
```

---

## 🚨 Rollback Plan

### If Critical Issues Detected

**Symptoms requiring immediate rollback:**
- Application crashes or doesn't start
- Users unable to perform normal operations
- Widespread 404 errors for legitimate access
- Excessive error rates (>5% of requests)
- Database performance degradation

**Rollback Steps:**
```bash
# 1. Revert to previous deployment
git checkout pre-phase1-security-fixes

# 2. Rebuild and redeploy
dotnet clean
dotnet build -c Release
dotnet publish -c Release -o ./publish
# (Your deployment process)

# 3. Verify rollback successful
# - Test login
# - Test matter access
# - Test task access
```
- [ ] Rollback completed
- [ ] Application functional
- [ ] Incident documented
- [ ] Root cause analysis scheduled

---

## 📊 Success Metrics

### Required for Sign-Off

- [ ] Zero critical errors in production logs (first 24 hours)
- [ ] Response time increase <10ms (P95)
- [ ] Error rate unchanged or lower
- [ ] All security tests passing
- [ ] Audit logs capturing all CUD operations
- [ ] No legitimate user lockouts reported
- [ ] Unauthorized access attempts properly blocked

### Optional Improvements
- [ ] Setup monitoring dashboard for AuditLogs
- [ ] Configure alerts for authorization failures spike
- [ ] Document common security events for SOC team
- [ ] Schedule Phase 2 implementation kickoff

---

## 📝 Post-Deployment

### Communication
- [ ] Notify team of successful deployment
- [ ] Update status page (if applicable)
- [ ] Email stakeholders with summary
- [ ] Update security documentation

### Documentation
- [ ] Mark this checklist as complete
- [ ] File deployment report
- [ ] Update runbook with new security features
- [ ] Schedule Phase 2 planning meeting

### Follow-Up Actions
- [ ] Review audit logs after 1 week
- [ ] Conduct security review after 1 month
- [ ] Plan Phase 2 implementation (Service Layer)
- [ ] Consider penetration testing

---

## ✅ Sign-Off

**Deployment Completed By:**  
Name: ________________  
Date: ________________  
Signature: ________________  

**Staging Verification:**  
QA Lead: ________________  
Date: ________________  

**Production Sign-Off:**  
Engineering Lead: ________________  
Security Lead: ________________  
Date: ________________  

---

## 🎯 Next Steps

After successful Phase 1 deployment:

1. **Monitor for 1 week** - Watch for any issues or edge cases
2. **Review audit logs** - Look for patterns and suspicious activity
3. **Plan Phase 2** - Service Layer & Repository Pattern implementation
4. **Schedule penetration test** - External security audit

**Phase 2 Target Start Date:** _________________

---

**Document Version:** 1.0  
**Last Updated:** October 11, 2025

