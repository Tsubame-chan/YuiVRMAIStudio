@echo off
setlocal
cd /d "%~dp0\..\backend"
"%~dp0\..\backend\.venv\Scripts\python.exe" -m uvicorn main:app --host 127.0.0.1 --port 8000 --no-use-colors
echo Backend exited with code %ERRORLEVEL%
pause
