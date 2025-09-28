@echo off
REM Quick script to run AI agents with proper environment setup

cd ai_agents
call venv\Scripts\activate.bat
python main.py
pause
