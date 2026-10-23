@echo off
setlocal EnableDelayedExpansion
chcp 65001 >nul
title Dong bo Git - Pure CMD

set "GIT_CMD=git"
where %GIT_CMD% >nul 2>nul
if %errorlevel% neq 0 (
    if exist "%ProgramFiles%\Git\cmd\git.exe" (
        set "GIT_CMD=%ProgramFiles%\Git\cmd\git.exe"
    ) else if exist "%LOCALAPPDATA%\Programs\Git\cmd\git.exe" (
        set "GIT_CMD=%LOCALAPPDATA%\Programs\Git\cmd\git.exe"
    ) else (
        echo [LOI] Khong tim thay Git!
        pause
        exit /b 1
    )
)

set "TOKEN=<TOKEN-DA-AN>"

echo =======================================================
echo     DONG BO GITHUB THUAN CMD (ZERO POWERSHELL)
echo =======================================================
echo.

call :sync_one "po-data-main" "po-data.git"
echo.
echo -------------------------------------------------------
echo.
call :sync_one "po-dashboard-main" "po-dashboard.git"

echo.
echo =======================================================
echo          HOAN TAT DONG BO BANG CMD THUAN!
echo =======================================================
echo.
pause
exit /b 0

:sync_one
set "FOLDER=%~1"
set "REPO=%~2"
echo [*] Dang kiem tra va dong bo: %FOLDER%...
if not exist "%~dp0%FOLDER%" (
    echo [BO QUA] Thu muc %FOLDER% khong ton tai.
    goto :eof
)
cd /d "%~dp0%FOLDER%"
"%GIT_CMD%" remote set-url origin https://%TOKEN%@github.com/Donniehieu/%REPO% >nul 2>nul
"%GIT_CMD%" pull --rebase origin main
set "HAS_CHANGE="
for /f "tokens=*" %%a in ('"%GIT_CMD%" status --porcelain') do set "HAS_CHANGE=1"
if defined HAS_CHANGE (
    echo [THAY DOI] Phat hien tap tin moi. Dang commit va push...
    "%GIT_CMD%" add .
    "%GIT_CMD%" commit -m "Auto sync via pure CMD: %date% %time%"
    "%GIT_CMD%" push origin main
    echo [OK] Da day thanh cong len %REPO%!
) else (
    echo [OK] %FOLDER% da dong bo, khong co thay doi.
)
goto :eof
