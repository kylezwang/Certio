@echo off
echo Stopping Certio services...

REM Stop .NET web application
for /f "tokens=5" %%a in ('netstat -ano ^| findstr :5092') do (
    taskkill /PID %%a /F 2>nul
)

REM Stop AI agents (Python) on port 8000
for /f "tokens=5" %%a in ('netstat -ano ^| findstr :8000') do (
    taskkill /PID %%a /F 2>nul
)

REM Stop any dotnet processes
taskkill /IM dotnet.exe /F 2>nul

REM Stop any python processes that might be running the AI agents
for /f "tokens=2" %%a in ('tasklist /FI "IMAGENAME eq python.exe" /FO CSV ^| findstr /V "PID"') do (
    taskkill /PID %%a /F 2>nul
)

echo Services stopped.
pause