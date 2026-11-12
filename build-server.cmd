@echo off
cd /d "%~dp0Server"
dotnet restore
if errorlevel 1 (
  echo Restore failed.
  pause
  exit /b 1
)
dotnet build
pause
