@echo off
setlocal

cd /d "%~dp0"

if not exist "backend\.venv\Scripts\python.exe" (
  echo [Yui Backend] Backend virtual environment is missing.
  echo [Yui Backend] Running first-time backend setup. Python 3.12+ is required.
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\setup_backend_byok.ps1" -ProjectRoot "%~dp0"
  if errorlevel 1 (
    echo.
    echo [Yui Backend] Setup failed. Install Python 3.12+ and run this file again.
    pause
    exit /b 1
  )
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\start_local_services.ps1"
echo.
echo [Yui Backend] Backend window closed.
pause
