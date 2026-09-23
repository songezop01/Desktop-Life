@echo off
set DOTNET_CLI_TELEMETRY_OPTOUT=1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run.ps1"
if errorlevel 1 pause
