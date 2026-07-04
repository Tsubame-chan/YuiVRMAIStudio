@echo off
setlocal

cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\stop_local_services.ps1"
echo.
echo [Yui Backend] Stop request sent.
pause
