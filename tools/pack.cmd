@echo off
setlocal
set ROOT=%~dp0..
set SUFFIX=%1
set OUT=%ROOT%\artifacts
set DOTNET_CLI_DISABLE_NODE_REUSE=1
set MSBUILDDISABLENODEREUSE=1

dotnet build-server shutdown
dotnet test "%ROOT%\NetOpenEditor.slnx" -c Release || exit /b 1
if exist "%OUT%" rmdir /s /q "%OUT%"

if "%SUFFIX%"=="" (
  dotnet pack "%ROOT%\src\NetOpenEditor\NetOpenEditor.csproj" -c Release -o "%OUT%" || exit /b 1
) else (
  dotnet pack "%ROOT%\src\NetOpenEditor\NetOpenEditor.csproj" -c Release -o "%OUT%" --version-suffix %SUFFIX% || exit /b 1
)
dir /b "%OUT%"
