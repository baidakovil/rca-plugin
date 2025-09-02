@echo off
REM RCA Plugin Hot-Reload Batch Script
REM Simple wrapper around the PowerShell script for command-line users

setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
set "PS_SCRIPT=%SCRIPT_DIR%hot-reload.ps1"

if not exist "%PS_SCRIPT%" (
    echo Error: hot-reload.ps1 not found at %PS_SCRIPT%
    pause
    exit /b 1
)

echo RCA Plugin Hot-Reload
echo =====================

REM Check if PowerShell is available
powershell -Command "Get-Host" >nul 2>&1
if errorlevel 1 (
    echo Error: PowerShell is not available or accessible
    pause
    exit /b 1
)

REM Parse command line arguments
set "ARGS="
set "SHOW_HELP=false"

:parse_args
if "%~1"=="" goto execute
if /i "%~1"=="--help" set "SHOW_HELP=true"
if /i "%~1"=="-h" set "SHOW_HELP=true"
if /i "%~1"=="help" set "SHOW_HELP=true"

set "ARGS=%ARGS% %~1"
shift
goto parse_args

:execute
if "%SHOW_HELP%"=="true" (
    echo.
    echo Usage: %~nx0 [options]
    echo.
    echo Options:
    echo   -BuildOnly       Build solution only, don't reload
    echo   -ReloadOnly      Reload only, don't build
    echo   -Verbose         Show detailed output
    echo   -Configuration   Build configuration (Debug/Release^)
    echo   -PipeName        Custom named pipe name
    echo   --help, -h       Show this help
    echo.
    echo Examples:
    echo   %~nx0
    echo   %~nx0 -BuildOnly
    echo   %~nx0 -Verbose
    echo   %~nx0 -Configuration Release
    echo.
    pause
    exit /b 0
)

echo Running PowerShell script with arguments: %ARGS%
echo.

powershell -ExecutionPolicy Bypass -File "%PS_SCRIPT%" %ARGS%

set "EXIT_CODE=%errorlevel%"

if %EXIT_CODE% equ 0 (
    echo.
    echo Operation completed successfully!
) else (
    echo.
    echo Operation failed with exit code %EXIT_CODE%
)

pause
exit /b %EXIT_CODE%