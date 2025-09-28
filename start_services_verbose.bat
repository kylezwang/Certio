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
cd ai_agents
start "AI Service" cmd /k "python main.py"
cd ..

echo Waiting for AI service to start...
timeout /t 5 /nobreak > nul

echo Starting ASP.NET Core Web Application on port 5092...
cd Certio.Web
start "Web App" cmd /k "dotnet run"
cd ..

echo.
echo Both services are starting...
echo AI Service: http://localhost:8000
echo Web App: http://localhost:5092
echo.
echo Check the opened windows for any errors or warnings.
echo Press any key to exit this window...
pause > nul
