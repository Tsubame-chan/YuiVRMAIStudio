@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\start_irodori_v4_windows.ps1" -PackRoot "%~dp0."
if errorlevel 1 pause
