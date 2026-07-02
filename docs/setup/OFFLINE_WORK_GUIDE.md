# ✈️ Offline Work Guide for Certio

## 🎯 **What Works Offline (100%)**

### ✅ **Cursor AI Integration**
- **Code completion** - Works offline
- **Refactoring suggestions** - Works offline  
- **Code explanations** - Works offline
- **Project understanding** - Works offline (via `.cursorrules`)
- **No internet required** for core Cursor features

### ✅ **Database Operations**
- **Local SQL Server** - Docker container
- **Full CRUD operations** - Create, Read, Update, Delete
- **Entity Framework** - All ORM features work
- **Migrations** - Can create and apply migrations
- **No internet required** for database work

### ✅ **.NET Web Application**
- **Full development** - All C# features work
- **Debugging** - Complete debugging capabilities
- **Testing** - Unit and integration tests
- **Build/Compile** - All build operations work
- **No internet required** for development

### ✅ **Python AI Agents (Limited)**
- **Local text processing** - NLTK, spaCy models
- **Data analysis** - pandas, numpy, matplotlib
- **Machine learning** - scikit-learn
- **File processing** - All file operations
- **No internet required** for local processing

## ❌ **What Requires Internet**

### AI API Calls
- OpenAI API calls
- Anthropic API calls  
- Google Gemini API calls
- Hugging Face model downloads

### Package Management
- Installing new packages
- Updating dependencies

## 🚀 **Quick Start Commands (Offline)**

### Start Development Environment
```bash
# Set environment to use local SQLite
export ASPNETCORE_ENVIRONMENT=Development

# Start web application
dotnet run --project Certio.Web

# Start AI agents (limited functionality)
cd ai_agents && source venv/bin/activate && python main.py

# Start Redis (optional)
redis-server
```

### Database Operations
```bash
# View database
sqlite3 Certio.Web/app.db

# Run migrations
export PATH="$PATH:/Users/chloetang/.dotnet/tools"
dotnet ef database update --project Certio.Web

# Create new migration
dotnet ef migrations add MigrationName --project Certio.Web
```

### Cursor AI Usage
```bash
# Open project in Cursor
cursor .

# Cursor will automatically:
# - Use .cursorrules for project context
# - Provide code completion
# - Suggest refactoring
# - Explain code
# - All without internet!
```

## 💡 **Perfect Offline Tasks**

### High Priority (100% Offline)
1. **Code refactoring and improvements**
2. **Database schema changes**
3. **UI/UX enhancements**
4. **Bug fixes and testing**
5. **Documentation writing**
6. **Performance optimization**

### Medium Priority (Mostly Offline)
1. **Local data analysis**
2. **File processing features**
3. **Basic text processing**
4. **Unit test development**

### Low Priority (Requires Internet)
1. **AI API integration**
2. **Real-time AI features**
3. **Cloud-based features**

## 🔧 **Configuration Summary**

### Database Configuration
- **Development**: SQLite (`app.db`) - Works offline
- **Production**: Azure SQL - Requires internet
- **Automatic detection** based on connection string

### Cursor AI Configuration
- **`.cursorrules`** - Project-specific rules
- **Works offline** - No internet needed
- **Full IDE features** - Code completion, refactoring, etc.

### Environment Setup
- **`.NET 9.0** - Full offline development
- **Python 3.11** - Local processing
- **SQLite** - Local database
- **Redis** - Local caching (optional)

## 🎉 **You're Ready for Offline Development!**

### What You Can Do on the Plane:
1. **Full .NET development** - Code, debug, test
2. **Database work** - Schema changes, queries, migrations
3. **Cursor AI assistance** - Code completion, refactoring
4. **Local data processing** - Analysis, visualization
5. **Documentation** - Write docs, comments, guides

### What to Avoid:
1. **AI API calls** - Will fail gracefully
2. **Package installation** - Do before going offline
3. **Cloud features** - Use local alternatives

## 🚀 **Pro Tips**

1. **Test offline setup** before traveling
2. **Download all packages** beforehand
3. **Use local SQLite** for development
4. **Cursor AI works great offline**
5. **Focus on core development tasks**

**You're all set for productive offline development! 🎉**
