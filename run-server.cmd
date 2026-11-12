@echo off
cd /d "%~dp0Server"
dotnet run --urls http://0.0.0.0:5080
pause
