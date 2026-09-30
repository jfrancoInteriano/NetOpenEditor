@echo off
REM Runs the sample app (journal + quote demos) at http://localhost:5199
setlocal
cd /d "%~dp0.."
set PORT=%1
if "%PORT%"=="" set PORT=5199
echo [netopeneditor] http://localhost:%PORT% - Ctrl+C para detener
dotnet run --project samples/NetOpenEditor.Example --urls "http://localhost:%PORT%"
