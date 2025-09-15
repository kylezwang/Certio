# 🔄 Data Sync Strategy: Local → Azure SQL

## Overview
This guide explains how to sync your local development data to Azure SQL Database after working offline.

## 🎯 **Sync Methods**

### Method 1: Entity Framework Migrations (Recommended)
```bash
# 1. Create migration for local changes
dotnet ef migrations add OfflineChanges --project Certio.Web

# 2. Apply to Azure SQL (when online)
# Update connection string to Azure SQL
dotnet ef database update --project Certio.Web
```

### Method 2: Data Export/Import
```bash
# 1. Export local data to SQL scripts
dotnet ef dbcontext script --project Certio.Web --output offline_data.sql

# 2. Import to Azure SQL (when online)
# Run the generated SQL script against Azure SQL
```

### Method 3: Programmatic Sync
```csharp
// Create a sync service
public class DataSyncService
{
    public async Task SyncToAzure()
    {
        // Export from local SQLite
        var localData = await GetLocalData();
        
        // Import to Azure SQL
        await ImportToAzure(localData);
    }
}
```

## 🚀 **Quick Sync Commands**

### Before Going Offline
```bash
# 1. Ensure local database is up to date
dotnet ef database update --project Certio.Web

# 2. Backup current state
cp Certio.Web/app.db Certio.Web/app_backup.db
```

### After Coming Back Online
```bash
# 1. Switch to Azure SQL
export ASPNETCORE_ENVIRONMENT=Production

# 2. Apply migrations to Azure
dotnet ef database update --project Certio.Web

# 3. Run data sync (if needed)
dotnet run --project Certio.Web --sync-data
```

## 🔧 **Configuration**

### Environment-Specific Connection Strings
- **Development**: `appsettings.Development.json` → Local SQL Server
- **Production**: `appsettings.json` → Azure SQL Database

### Automatic Environment Detection
```csharp
// In Program.cs
if (builder.Environment.IsDevelopment())
{
    // Use local SQL Server
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
}
else
{
    // Use Azure SQL Database
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
}
```

## 📊 **Data Sync Checklist**

### Before Offline Work
- [ ] Local database is up to date
- [ ] All migrations applied locally
- [ ] Backup current state
- [ ] Test offline functionality

### After Coming Online
- [ ] Switch to Azure SQL environment
- [ ] Apply any new migrations
- [ ] Sync data if needed
- [ ] Test Azure SQL connectivity
- [ ] Verify data integrity

## 🎉 **Benefits**

1. **Seamless Development** - Work offline without losing data
2. **Data Integrity** - Migrations ensure schema consistency
3. **Flexibility** - Multiple sync methods available
4. **Backup Safety** - Local backups before changes
5. **Production Ready** - Easy deployment to Azure

## 🚨 **Important Notes**

- **Always backup** before making changes
- **Test migrations** in development first
- **Use transactions** for data sync operations
- **Monitor connection strings** for environment changes
- **Verify data integrity** after sync

**You're ready for seamless offline-to-online development! 🎉**
