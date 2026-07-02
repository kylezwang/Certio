# Windows Development Setup Guide for Certio

This guide helps you set up the Certio project on Windows for development.

## Prerequisites

Before running the setup, ensure you have the following installed:

### Required Software
1. **Visual Studio 2022** or **Visual Studio Code** with C# extension
2. **.NET 9.0 SDK** - Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
3. **Python 3.11** - Download from [python.org](https://python.org/downloads)
4. **Docker Desktop** - Download from [docker.com](https://docker.com/products/docker-desktop)
5. **Git** - Download from [git-scm.com](https://git-scm.com/download/win)

### Optional Software
- **Redis** (if not using Docker) - Download from [redis.io](https://redis.io/download)
- **SQL Server Management Studio** - For database management

## Quick Setup

1. **Clone the repository** (if not already done):
   ```cmd
   git clone <repository-url>
   cd Certio
   ```

2. **Run the Windows setup script**:
   ```cmd
   setup_windows.bat
   ```

3. **Configure environment variables**:
   - Edit the `.env` file created by the setup script
   - Add your API keys and passwords

4. **Start the services**:
   ```cmd
   start_services.bat
   ```

## Manual Setup (Alternative)

If you prefer to set up manually:

### 1. Set up Python Environment
```cmd
cd ai_agents
python -m venv venv
venv\Scripts\activate.bat
pip install -r requirements.txt
```

### 2. Set up .NET Environment
```cmd
dotnet restore
dotnet build
```

### 3. Set up Database
```cmd
docker-compose up -d sqlserver
```

### 4. Configure Environment Variables
Create a `.env` file in the project root:
```env
# Database Configuration
SQL_PASSWORD=YourStrong@Passw0rd
USE_AZURE_SQL=false

# AI Service Configuration
OPENAI_API_KEY=your_openai_api_key_here
ANTHROPIC_API_KEY=your_anthropic_api_key_here

# Azure SQL Configuration (optional)
AZURE_SQL_CONNECTION_STRING=your_azure_connection_string_here
```

## Running the Application

### Start All Services
```cmd
start_services.bat
```

### Start Individual Services
```cmd
REM Start .NET Web Application
cd Certio.Web
dotnet run

REM Start AI Agents (in another terminal)
cd ai_agents
venv\Scripts\activate.bat
python main.py
```

### Stop All Services
```cmd
stop_services.bat
```

## Development Commands

### Build and Test
```cmd
REM Build the solution
dotnet build

REM Run tests
dotnet test

REM Run with specific environment
set SQL_PASSWORD=test123
dotnet run --project Certio.Web
```

### Database Operations
```cmd
REM Apply migrations
dotnet ef database update --project Certio.Web

REM Add new migration
dotnet ef migrations add MigrationName --project Certio.Web
```

## Troubleshooting

### Common Issues

1. **"dotnet command not found"**
   - Ensure .NET 9.0 SDK is installed
   - Restart your terminal/command prompt
   - Check PATH environment variable

2. **"python command not found"**
   - Ensure Python 3.11 is installed
   - Check that Python is added to PATH
   - Restart your terminal/command prompt

3. **"docker command not found"**
   - Ensure Docker Desktop is installed and running
   - Restart Docker Desktop
   - Check that Docker is added to PATH

4. **Database connection issues**
   - Ensure SQL Server container is running: `docker ps`
   - Check SQL_PASSWORD in .env file
   - Verify docker-compose.yml configuration

5. **AI service not starting**
   - Check Python virtual environment is activated
   - Verify all dependencies are installed: `pip list`
   - Check API keys in .env file

### Environment Variables

The application uses these environment variables:

- `SQL_PASSWORD`: Password for local SQL Server (required)
- `USE_AZURE_SQL`: Set to "true" to use Azure SQL (default: "false")
- `AZURE_SQL_CONNECTION_STRING`: Full Azure SQL connection string (optional)
- `OPENAI_API_KEY`: OpenAI API key for AI services
- `ANTHROPIC_API_KEY`: Anthropic API key for AI services (recommended)

### Ports Used

- **5092**: .NET Web Application
- **8000**: AI Agents (Python)
- **1433**: SQL Server (Docker)
- **6379**: Redis (if running locally)

## File Structure

```
Certio/
├── Certio.Web/           # Main web application
├── Certio.Domain/        # Domain models
├── Certio.Application/   # Application services
├── Certio.Infrastructure/ # Infrastructure layer
├── ai_agents/            # Python AI agents
├── start_services.bat    # Start all services
├── stop_services.bat     # Stop all services
├── setup_windows.bat     # Windows setup script
└── .env                  # Environment variables
```

## Next Steps

1. **Configure API Keys**: Add your OpenAI and Anthropic API keys to `.env`
2. **Test the Application**: Visit `http://localhost:5092`
3. **Explore the Code**: Start with `Certio.Web/Program.cs`
4. **Read Documentation**: Check the `README.md` files in each directory

## Support

If you encounter issues:
1. Check the troubleshooting section above
2. Review the console output for error messages
3. Ensure all prerequisites are installed correctly
4. Check that all services are running on the correct ports
