@echo off
cd /d "%~dp0.."
echo Loading environment variables from .env file...

REM Check if .env file exists
if not exist .env (
    echo ERROR: .env file not found!
    echo Please create a .env file with your environment variables.
    pause
    exit /b 1
)

REM Load environment variables from .env file
for /f "usebackq tokens=1,2 delims==" %%a in (.env) do (
    if not "%%a"=="" if not "%%a:~0,1%"=="#" (
        set %%a=%%b
    )
)

echo.
echo  Environment variables loaded successfully!
echo.
echo You can now use Docker commands with environment variables.
echo.
pause
