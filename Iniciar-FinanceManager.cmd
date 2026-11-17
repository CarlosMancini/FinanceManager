@echo off
cd /d "%~dp0"
set "PWSH_EXE=pwsh"
where pwsh >nul 2>&1
if errorlevel 1 (
    set "PWSH_EXE=%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe"
    if not exist "%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe" (
        echo O PowerShell 7.5 ou superior precisa estar instalado.
        pause
        exit /b 1
    )
)
"%PWSH_EXE%" -NoProfile -File "%~dp0scripts\container\Start.ps1"
if errorlevel 1 (
    echo.
    echo Nao foi possivel iniciar o FinanceManager.
    pause
    exit /b 1
)
