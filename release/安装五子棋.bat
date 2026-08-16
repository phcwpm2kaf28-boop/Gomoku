@echo off
title Gomoku Installer

:: ---- request administrator rights (UAC) ----
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting administrator privileges, please click Yes on the UAC prompt...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo ==========================================
echo   Gomoku - MSIX installer
echo ==========================================
echo.
echo This will:
echo   1) Trust the self-signed certificate (machine trusted root, UAC)
echo   2) Install the MSIX package
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-msix.ps1"
echo.
echo ==========================================
pause
