@echo off
rem Builds and starts ImageToIcon (GUI). Run from anywhere; paths are relative to this file.
setlocal
cd /d "%~dp0"

set "DOTNET=dotnet"
where dotnet >nul 2>&1
if errorlevel 1 set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
if not exist "%DOTNET%" (
    echo [ERROR] dotnet SDK not found. Install .NET 10 SDK from https://dot.net
    pause
    exit /b 1
)

"%DOTNET%" run --project "src\ImageToIcon\ImageToIcon.csproj" -c Debug
if errorlevel 1 (
    echo.
    echo [ERROR] ImageToIcon exited with an error.
    pause
    exit /b 1
)

endlocal
