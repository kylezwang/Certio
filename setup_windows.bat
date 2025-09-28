@echo off
REM Certio Project Setup Script for Windows
REM This script sets up the development environment for the Certio project

echo 🚀 Setting up Certio development environment on Windows...
echo.

REM Check if .NET is installed
echo Checking .NET installation...
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ .NET is not installed. Please install .NET 9.0 SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)
echo ✅ .NET is installed
dotnet --version

echo.

REM Check if Python is installed
echo Checking Python installation...
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ Python is not installed. Please install Python 3.11 from https://python.org/downloads
    pause
    exit /b 1
)
echo ✅ Python is installed
python --version

echo.

REM Check if Docker is installed
echo Checking Docker installation...
docker --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ Docker is not installed. Please install Docker Desktop from https://docker.com/products/docker-desktop
    pause
    exit /b 1
)
echo ✅ Docker is installed
docker --version

echo.

REM Set up Python virtual environment
echo Setting up Python virtual environment...
cd ai_agents
if not exist "venv" (
    echo Creating Python virtual environment...
    python -m venv venv
)

echo Activating virtual environment and installing dependencies...
call venv\Scripts\activate.bat
pip install --upgrade pip
pip install -r requirements.txt

echo ✅ Python dependencies installed

echo.

REM Test .NET project build
echo Testing .NET project build...
cd ..
if dotnet build (
    echo ✅ .NET project builds successfully
) else (
    echo ❌ .NET project build failed
    pause
    exit /b 1
)

echo.

REM Create .env file if it doesn't exist
if not exist ".env" (
    echo Creating .env file with default values...
    echo # Database Configuration > .env
    echo SQL_PASSWORD=YourStrong@Passw0rd >> .env
    echo USE_AZURE_SQL=false >> .env
    echo. >> .env
    echo # AI Service Configuration >> .env
    echo OPENAI_API_KEY=your_openai_api_key_here >> .env
    echo ANTHROPIC_API_KEY=your_anthropic_api_key_here >> .env
    echo. >> .env
    echo # Azure SQL Configuration (optional) >> .env
    echo AZURE_SQL_CONNECTION_STRING=your_azure_connection_string_here >> .env
    echo ✅ Created .env file with default values
) else (
    echo ✅ .env file already exists
)

echo.
echo 🎉 Setup complete!
echo.
echo Next steps:
echo 1. Edit .env file with your actual API keys and passwords
echo 2. Run start_services.bat to start all services
echo 3. Or run individual services:
echo    - dotnet run --project Certio.Web
echo    - cd ai_agents ^&^& venv\Scripts\activate.bat ^&^& python main.py
echo.
echo Environment variables needed:
echo - SQL_PASSWORD: Password for local SQL Server
echo - OPENAI_API_KEY: Your OpenAI API key
echo - ANTHROPIC_API_KEY: Your Anthropic API key (recommended)
echo.
pause
