# 🎯 Smart Database System - Complete Guide

## ✅ **What We've Built**

Your app now has a **smart database system** that automatically chooses the right database:

### **Default Behavior (Offline Mode)**
- **Uses**: Local SQL Server (Docker container)
- **Message**: "📱 Using local SQL Server (default)"
- **Auto-starts**: SQL Server container if not running
- **Perfect for**: Working offline, on the plane, etc.

### **Online Mode (When You Want Azure SQL)**
- **Uses**: Azure SQL Database (production data)
- **Message**: "🌐 Using Azure SQL Database (explicitly requested)"
- **Requires**: Internet connection + explicit request

## 🚀 **How to Use**

### **Default (Offline Mode)**
```bash
dotnet run --project Certio.Web
```
**Result**: Uses local SQL Server automatically

### **Online Mode (Azure SQL)**
```bash
USE_AZURE_SQL=true dotnet run --project Certio.Web
```
**Result**: Uses Azure SQL Database (if internet is available)

## 🎉 **Current Status**

Your app is currently running and working perfectly:
- **Database**: Local SQL Server (Docker)
- **URL**: http://localhost:5092
- **Status**: ✅ Working perfectly
- **Mode**: Offline (default)

## 🧪 **Test Both Modes**

### **Test Offline Mode (Default)**
```bash
dotnet run --project Certio.Web
# Should show: "📱 Using local SQL Server (default)"
```

### **Test Online Mode**
```bash
USE_AZURE_SQL=true dotnet run --project Certio.Web
# Should show: "🌐 Using Azure SQL Database (explicitly requested)"
```

## 🔧 **How It Works**

1. **Default**: Always uses local SQL Server (reliable, works offline)
2. **Online**: Only uses Azure SQL when you explicitly request it
3. **Auto-start**: Automatically starts Docker SQL Server if needed
4. **Smart detection**: Checks internet connectivity before using Azure SQL

## 🎯 **Perfect for Your Needs**

- **On the plane**: Just `dotnet run` - works offline
- **In the office**: `USE_AZURE_SQL=true dotnet run` - uses production data
- **No configuration**: It figures everything out automatically
- **No scripts**: Just one command

**This is exactly what you wanted! 🚀**

## 📝 **Quick Reference**

| Command | Database | Use Case |
|---------|----------|----------|
| `dotnet run` | Local SQL Server | Offline work, development |
| `USE_AZURE_SQL=true dotnet run` | Azure SQL | Production data, online work |

**You're all set! ✈️**
