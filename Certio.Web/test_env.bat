@echo off
echo Testing dotnet run behavior...

echo 1. Testing WITHOUT environment variables:
set SQL_PASSWORD=
set AZURE_SQL_CONNECTION_STRING=
timeout /t 5 /nobreak > nul
dotnet run --no-build 2>&1 | more
if %errorlevel% neq 0 echo Command failed or timed out

echo.
echo 2. Testing WITH environment variables:
set SQL_PASSWORD=test123
set AZURE_SQL_CONNECTION_STRING=test-connection
timeout /t 5 /nobreak > nul
dotnet run --no-build 2>&1 | more
if %errorlevel% neq 0 echo Command failed or timed out

pause
