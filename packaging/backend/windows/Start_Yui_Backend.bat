@echo off
setlocal

cd /d "%~dp0"

if not exist "backend\.venv\Scripts\python.exe" (
  echo [Yui Backend] Backend virtual environment is missing.
  echo [Yui Backend] Running fallback backend setup. Public releases should already include Python.
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\setup_backend_byok.ps1" -ProjectRoot "%~dp0"
  if errorlevel 1 (
    echo.
    echo [Yui Backend] Setup failed. Reinstall the public backend bundle, or install Python 3.12+ for source builds.
    pause
    exit /b 1
  )
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\start_local_services.ps1"
echo.
echo [Yui Backend] Backend window closed.
pause
