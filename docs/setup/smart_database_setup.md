# Smart Database Setup Options

## Current Setup (Manual)
- **Development**: SQLite (offline)
- **Production**: Azure SQL (online)
- **Switch**: Change environment variable

## Option 1: Internet Detection (Automatic)
```csharp
// In Program.cs - Add internet detection
var hasInternet = await CheckInternetConnection();
var connectionString = hasInternet 
    ? builder.Configuration.GetConnectionString("AzureConnection")
    : builder.Configuration.GetConnectionString("LocalConnection");
```

## Option 2: Hybrid Setup (Best of Both)
- **Primary**: Azure SQL (when online)
- **Fallback**: SQLite (when offline)
- **Sync**: When back online, sync data

## Option 3: Data Migration
- **Export**: Data from Azure SQL
- **Import**: Into SQLite for offline work
- **Sync**: Back to Azure SQL when online

## Recommended Approach
1. **Keep current setup** for simplicity
2. **Use SQLite for offline development**
3. **Use Azure SQL for production**
4. **Add data migration tools** if needed
