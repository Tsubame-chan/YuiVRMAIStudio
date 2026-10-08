@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\setup_irodori_v4_windows.ps1" -PackRoot "%~dp0."
if errorlevel 1 pause
