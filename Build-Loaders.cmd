@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-Loaders.ps1" -Configuration Debug
if errorlevel 1 (
    echo.
    echo Loader build failed.
    pause
    exit /b 1
)
echo.
echo Loader build completed.
pause
