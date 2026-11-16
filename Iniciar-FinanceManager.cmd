@echo off
cd /d "%~dp0"
where pwsh >nul 2>&1
if errorlevel 1 (
    echo O PowerShell 7.5 ou superior precisa estar instalado.
    pause
    exit /b 1
)
pwsh -NoProfile -File "%~dp0scripts\container\Start.ps1"
if errorlevel 1 (
    echo.
    echo Nao foi possivel iniciar o FinanceManager.
    pause
    exit /b 1
)
