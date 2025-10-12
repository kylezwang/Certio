# Docker SQL Command Update Summary
**Date:** October 11, 2025  
**Update Type:** Testing Documentation Enhancement  

---

## 🎯 What Changed

All testing documentation has been updated to use Docker SQL commands instead of raw SQL queries for database verification.

### **Command Format**

**Old Style (Raw SQL):**
```sql
SELECT TOP 10 * FROM AuditLogs ORDER BY Timestamp DESC;
```

**New Style (Docker Command):**
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT TOP 10 * FROM CertioLocal.dbo.AuditLogs ORDER BY Timestamp DESC"
```

---

## 📄 Files Updated

### 1. **PHASE_1_TESTING_GUIDE.md**
**Changes:** 20+ SQL queries converted to Docker commands

**Sections Updated:**
- ✅ Pre-Testing Setup → Database State Verification
- ✅ TEST-IDOR-001 → Audit log checks
- ✅ TEST-IDOR-002 → Matter verification
- ✅ TEST-IDOR-003 → Task verification
- ✅ TEST-IDOR-004 → Task deletion checks
- ✅ TEST-AUTH-001 → Matter edit audit logs
- ✅ TEST-AUTH-002 → Task update audit logs
- ✅ TEST-MATTER-001 → Database verification
- ✅ TEST-MATTER-002 → MatterPermissions checks
- ✅ TEST-MATTER-003 → Permission verification
- ✅ TEST-INPUT-001 → Title length checks
- ✅ TEST-INPUT-002 → Status validation
- ✅ TEST-AUDIT-001 → Create operation logging
- ✅ TEST-AUDIT-002 → Update operation logging
- ✅ TEST-AUDIT-003 → Delete operation logging
- ✅ TEST-AUDIT-004 → Authorization failure logging
- ✅ TEST-FIRM-001 → Organization relationship checks
- ✅ Common Issues & Solutions → Troubleshooting queries

### 2. **TESTING_QUICK_START.md**
**Changes:** 4 SQL queries converted to Docker commands

**Sections Updated:**
- ✅ Step 3: Verify Audit Logging
- ✅ Issue: 404 on everything → User organization check
- ✅ Getting Help → Database State checks (Matters, Tasks, UserOrganizations)

### 3. **PHASE_1_DEPLOYMENT_CHECKLIST.md**
**Changes:** 5+ SQL queries converted to Docker commands

**Sections Updated:**
- ✅ Database Preparation → AuditLogs table verification
- ✅ Database Preparation → Table structure check
- ✅ Smoke Tests → AuditLogs check
- ✅ Monitoring Queries → Recent audit activity
- ✅ Monitoring Queries → Authorization failures
- ✅ Monitoring Queries → Activity by user

---

## 🔧 Command Anatomy

Understanding the Docker SQL command structure:

```powershell
docker exec -it certio-sqlserver                    # Execute in SQL Server container
  /opt/mssql-tools18/bin/sqlcmd                     # Use sqlcmd tool
  -S localhost,1433                                  # Server and port
  -U sa                                              # Username
  -P $env:SQL_PASSWORD                               # Password from environment
  -C                                                 # Trust certificate
  -N                                                 # Encrypt connection
  -W                                                 # Remove trailing spaces
  -s','                                              # CSV output format
  -Q "SELECT ... FROM CertioLocal.dbo.TableName"    # Query with full DB name
```

### Key Components:

| Parameter | Purpose | Notes |
|-----------|---------|-------|
| `docker exec -it certio-sqlserver` | Execute in container | Container name must match your setup |
| `/opt/mssql-tools18/bin/sqlcmd` | SQL command tool | Version 18 with TLS 1.2+ support |
| `-S localhost,1433` | Server address | Comma separates host and port |
| `-U sa` | Admin user | System administrator account |
| `-P $env:SQL_PASSWORD` | Password | Uses PowerShell environment variable |
| `-C` | Trust certificate | Required for self-signed certs |
| `-N` | Encrypt connection | Secure communication |
| `-W` | Trim trailing spaces | Cleaner output |
| `-s','` | Column separator | CSV format with comma |
| `-Q "..."` | Query | Fully qualified table names |

---

## 📝 Key Changes Summary

### 1. Database Schema Fully Qualified

**Before:**
```sql
FROM AuditLogs
FROM Matters
FROM TaskItems
```

**After:**
```sql
FROM CertioLocal.dbo.AuditLogs
FROM CertioLocal.dbo.Matters
FROM CertioLocal.dbo.TaskItems
```

### 2. Column Name Corrections

Updated references to match actual schema:
- ✅ `IpAddress` → `IPAddress` (capital IP)
- ✅ `Changes` → `Description`
- ✅ `Success` field → Removed (not in schema)

### 3. PowerShell Code Blocks

Changed from `sql` to `powershell` for proper syntax highlighting:

**Before:**
````markdown
```sql
SELECT ...
```
````

**After:**
````markdown
```powershell
docker exec -it certio-sqlserver ...
```
````

---

## ✅ Benefits

### 1. **Copy-Paste Ready**
Users can copy entire commands without modifications

### 2. **Environment Consistent**
Matches actual deployment environment (Docker SQL Server)

### 3. **Error Reduction**
Eliminates need to manually wrap SQL in docker commands

### 4. **Environment Variable Support**
Uses `$env:SQL_PASSWORD` for secure password handling

### 5. **Full Database Names**
Explicitly includes `CertioLocal.dbo.` prefix

---

## 🧪 Testing the Commands

### Quick Verification

```powershell
# Test 1: Check if container is running
docker ps | findstr certio-sqlserver

# Test 2: Verify environment variable
echo $env:SQL_PASSWORD

# Test 3: Run a simple query
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT @@VERSION"

# Test 4: Check AuditLogs table
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT COUNT(*) as AuditLogCount FROM CertioLocal.dbo.AuditLogs"
```

---

## 🚨 Troubleshooting

### Issue: "certio-sqlserver" container not found
**Solution:** Check your container name
```powershell
docker ps -a
```
Update commands to use your actual container name.

### Issue: SQL_PASSWORD environment variable not set
**Solution:** Load environment variables
```powershell
# Load from .env or set manually
$env:SQL_PASSWORD = "YourPasswordHere"
```

### Issue: Connection timeout
**Solution:** Verify SQL Server is running
```powershell
docker logs certio-sqlserver
```

### Issue: Database "CertioLocal" not found
**Solution:** Check available databases
```powershell
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -Q "SELECT name FROM sys.databases"
```

---

## 📊 Migration Checklist

When updating additional documentation:

- [ ] Replace bare SQL with Docker commands
- [ ] Use `CertioLocal.dbo.` prefix for all tables
- [ ] Change code blocks from ```sql to ```powershell
- [ ] Use `$env:SQL_PASSWORD` for password
- [ ] Include all sqlcmd flags: `-C -N -W -s','`
- [ ] Test command actually works before documenting

---

## 🔄 Future Considerations

### Alternative Command Formats

**For Linux/Mac:**
```bash
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P "$SQL_PASSWORD" -C -N -W -s',' -Q "SELECT ..."
```

**For Windows CMD:**
```cmd
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P %SQL_PASSWORD% -C -N -W -s"," -Q "SELECT ..."
```

### Bash Script Alternative

Create `query.sh` for complex queries:
```bash
#!/bin/bash
QUERY="$1"
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost,1433 \
  -U sa \
  -P "$SQL_PASSWORD" \
  -C -N -W -s',' \
  -Q "$QUERY"
```

Usage:
```bash
./query.sh "SELECT TOP 10 * FROM CertioLocal.dbo.AuditLogs ORDER BY Timestamp DESC"
```

---

## 📚 Related Documentation

- `PHASE_1_TESTING_GUIDE.md` - Comprehensive testing with all Docker commands
- `TESTING_QUICK_START.md` - Quick validation using Docker commands
- `PHASE_1_DEPLOYMENT_CHECKLIST.md` - Deployment monitoring with Docker commands
- `WINDOWS_SETUP_GUIDE.md` - Docker SQL Server setup instructions

---

**Update Version:** 1.0  
**Last Updated:** October 11, 2025  
**Updated By:** AI Assistant (per user request)


