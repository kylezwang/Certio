@echo off
echo Testing dotnet run behavior...

echo 1. Testing WITHOUT environment variables:
set SQL_PASSWORD=
set AZURE_SQL_CONNECTION_STRING=
echo Environment variables cleared

echo.
echo 2. Testing WITH environment variables:
set SQL_PASSWORD=test123
set AZURE_SQL_CONNECTION_STRING=test-connection
echo Environment variables set

pause
