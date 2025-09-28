@echo off
echo Starting Certio Services...

REM Load environment variables from .env file if it exists
if exist .env (
    for /f "usebackq tokens=1,2 delims==" %%a in (.env) do (
        if not "%%a"=="" if not "%%a:~0,1%"=="#" (
            set %%a=%%b
        )
    )
)

REM Start Python AI Service
cd ai_agents
call venv\Scripts\activate.bat
pip install -r requirements-minimal.txt >nul 2>&1
start /B python main.py
cd ..

REM Wait briefly for AI service to start
timeout /t 2 /nobreak >nul

REM Start ASP.NET Core Web Application
cd Certio.Web
start /B dotnet run
cd ..

echo.
echo Services started:
echo AI Service: http://localhost:8000
echo Web App: http://localhost:5092
echo.
pause