@echo off
setlocal
cd /d echo Backend Console: http://127.0.0.1:8000/admin/
"%~dp0\..\backend"
echo Backend Console: http://127.0.0.1:8000/admin/
"%~dp0\..\backend\.venv\Scripts\python.exe" -m uvicorn main:app --host 127.0.0.1 --port 8000 --no-use-colors --no-proxy-headers
echo Backend exited with code %ERRORLEVEL%
pause
