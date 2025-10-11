@echo off
echo Stopping Certio services...
echo.

REM Stop .NET web application
echo [1/3] Stopping Web Application (port 5092)...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr :5092') do (
    taskkill /PID %%a /F 2>nul
)
taskkill /IM dotnet.exe /F 2>nul
echo Web App stopped.
echo.

REM Stop AI agents (Python) on port 8000
echo [2/3] Stopping AI Service (port 8000)...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr :8000') do (
    taskkill /PID %%a /F 2>nul
)
for /f "tokens=2" %%a in ('tasklist /FI "IMAGENAME eq python.exe" /FO CSV ^| findstr /V "PID"') do (
    taskkill /PID %%a /F 2>nul
)
echo AI Service stopped.
echo.

REM Stop Redis (Docker)
echo [3/3] Stopping Redis cache...
docker ps --filter "name=redis-certio" --format "{{.Names}}" 2>nul | findstr /C:"redis-certio" >nul
if errorlevel 1 (
    echo Redis was not running.
) else (
    docker stop redis-certio >nul 2>&1
    echo Redis stopped.
)
echo.

echo ========================================
echo All services stopped.
echo ========================================
echo.
pause