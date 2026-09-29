@echo off
cd /d "%~dp0.."
REM Run the AI service from the repo root.

cd ai_agents
call venv\Scripts\activate.bat
python main.py
pause
