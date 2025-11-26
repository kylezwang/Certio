@echo off
echo Starting Certio Services...
echo.

REM Load environment variables from .env file if it exists
if exist .env (
    for /f "usebackq tokens=1,2 delims==" %%a in (.env) do (
        if not "%%a"=="" if not "%%a:~0,1%"=="#" (
            set %%a=%%b
        )
    )
)

REM Start SQL Server (Docker)
echo [1/4] Starting SQL Server...
docker ps --filter "name=certio-sqlserver" --format "{{.Names}}" 2>nul | findstr /C:"certio-sqlserver" >nul
if errorlevel 1 (
    REM Container doesn't exist or isn't running
    docker start certio-sqlserver 2>nul
    if errorlevel 1 (
        REM Container doesn't exist, create it via docker-compose
        docker-compose up -d sqlserver 2>nul
        if errorlevel 1 (
            echo WARNING: Could not start SQL Server. Check Docker and docker-compose.yml
        ) else (
            echo SQL Server started: localhost:1433
            echo Waiting for SQL Server to be ready...
            timeout /t 10 /nobreak >nul
        )
    ) else (
        echo SQL Server started: localhost:1433
    )
) else (
    echo SQL Server already running: localhost:1433
)
echo.

REM Start Redis (Docker)
echo [2/4] Starting Redis cache...
docker ps --filter "name=redis-certio" --format "{{.Names}}" 2>nul | findstr /C:"redis-certio" >nul
if errorlevel 1 (
    REM Container doesn't exist or isn't running
    docker start redis-certio 2>nul
    if errorlevel 1 (
        REM Container doesn't exist, create it
        docker run -d --name redis-certio -p 6379:6379 redis:latest >nul 2>&1
        if errorlevel 1 (
            echo WARNING: Could not start Redis. App will use in-memory cache.
        ) else (
            echo Redis started: localhost:6379
        )
    ) else (
        echo Redis started: localhost:6379
    )
) else (
    echo Redis already running: localhost:6379
)
echo.

REM Start Python AI Service
echo [3/4] Starting AI Service...
cd ai_agents
call venv\Scripts\activate.bat
pip install -r requirements-minimal.txt >nul 2>&1
start /B python main.py
cd ..
echo AI Service starting on: http://localhost:8000
echo.

REM Wait briefly for AI service to start
timeout /t 2 /nobreak >nul

REM Start ASP.NET Core Web Application
echo [4/4] Starting Web Application...
cd Certio.Web
start /B dotnet run
cd ..
echo Web App starting on: http://localhost:5092
echo.

echo ========================================
echo All Services Started:
echo ========================================
echo SQL Server:   localhost:1433
echo Redis Cache:  localhost:6379
echo AI Service:   http://localhost:8000
echo Web App:      http://localhost:5092
echo ========================================
echo.
pause