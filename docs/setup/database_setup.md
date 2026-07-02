# Database Setup Guide for Certio

## 🗄️ Database Options

### Option 1: Local SQLite (Recommended for Development)
- **Pros**: No internet required, fast, easy setup
- **Cons**: Limited features compared to SQL Server
- **Best for**: Local development, offline work

### Option 2: Azure SQL Database (Production)
- **Pros**: Full SQL Server features, cloud-based, scalable
- **Cons**: Requires internet, costs money
- **Best for**: Production, team collaboration

## 🚀 Quick Setup

### Local SQLite Setup
```bash
# The app.db file is already configured
# Just run the application - it will create the database automatically
dotnet run --project Certio.Web
```

### Azure SQL Database Setup
1. **Install Azure Data Studio** (already installed)
2. **Connect to your database**:
   - Server: `your-server.database.windows.net`
   - Database: `Certio`
   - Authentication: SQL Login
   - Username: `wangzonghao`
   - Password: `{DB_PASSWORD}`

### Database Management Tools

#### Azure Data Studio (Recommended)
```bash
# Already installed, launch with:
open -a "Azure Data Studio"
```

#### Command Line Tools
```bash
# For SQLite
sqlite3 app.db

# For Azure SQL (if you install sqlcmd)
sqlcmd -S your-server.database.windows.net -d Certio -U sql-login -P {DB_PASSWORD}
```

## 🔧 Configuration

### Development (Local SQLite)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=app.db"
  }
}
```

### Production (Azure SQL)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:your-server.database.windows.net,1433;Initial Catalog=Certio;Persist Security Info=False;User ID=sql-login;Password=${DB_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }
}
```

## 📊 Database Operations

### Run Migrations
```bash
# Update database schema
dotnet ef database update --project Certio.Web

# Add new migration
dotnet ef migrations add MigrationName --project Certio.Web
```

### View Database
```bash
# SQLite
sqlite3 app.db
.tables
.schema

# Azure SQL (via Azure Data Studio)
# Connect and browse tables
```

## 🔍 Troubleshooting

### Common Issues
1. **Connection refused**: Check if the database server is running
2. **Authentication failed**: Verify credentials
3. **Timeout**: Check network connectivity
4. **Migration errors**: Check for data conflicts

### Debug Commands
```bash
# Test database connection
dotnet run --project Certio.Web --environment Development

# Check migration status
dotnet ef migrations list --project Certio.Web

# Reset database (careful!)
dotnet ef database drop --project Certio.Web
dotnet ef database update --project Certio.Web
```

## 💡 Pro Tips

1. **Use local SQLite for development** - faster and offline
2. **Use Azure SQL for production** - full features and scalability
3. **Always backup before migrations** in production
4. **Use Azure Data Studio** for visual database management
5. **Test migrations locally** before applying to production
