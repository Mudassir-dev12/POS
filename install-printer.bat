@echo off
setlocal
title ShopPOS - Epson Thermal Printer Setup & Detector

:: Ensure running as Administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo ==========================================================
    echo   ShopPOS - Requesting Administrator Privileges...
    echo ==========================================================
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

set "SCRIPT_DIR=%~dp0"
if exist "%SCRIPT_DIR%scripts\install-printer.ps1" (
    set "PS_SCRIPT=%SCRIPT_DIR%scripts\install-printer.ps1"
) else if exist "%SCRIPT_DIR%install-printer.ps1" (
    set "PS_SCRIPT=%SCRIPT_DIR%install-printer.ps1"
) else (
    echo Error: Could not locate install-printer.ps1
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" -Interactive

endlocal

