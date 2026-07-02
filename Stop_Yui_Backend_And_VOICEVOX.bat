@echo off
setlocal
cd /d "%~dp0"

set "PWSH=pwsh.exe"
where pwsh.exe >nul 2>nul
if errorlevel 1 set "PWSH=powershell.exe"

"%PWSH%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\stop_local_services.ps1"
echo.
pause
