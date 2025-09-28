@echo off
echo Starting Certio AI-Powered Chat System...
echo.

REM Load environment variables from .env file if it exists
if exist .env (
    echo Loading environment variables from .env file...
    for /f "usebackq tokens=1,2 delims==" %%a in (.env) do (
        if not "%%a"=="" if not "%%a:~0,1%"=="#" (
            set %%a=%%b
        )
    )
) else (
    echo Warning: .env file not found. Make sure environment variables are set.
)
echo.

echo Starting Python AI Service on port 8000...
start "AI Service" cmd /k "cd ai_agents && python main.py"

echo Waiting for AI service to start...
timeout /t 3 /nobreak > nul

echo Starting ASP.NET Core Web Application on port 5092...
start "Web App" cmd /k "cd Certio.Web && dotnet run"

echo.
echo Both services are starting...
echo AI Service: http://localhost:8000
echo Web App: http://localhost:5092
echo.
echo Press any key to exit...
pause > nul
