@echo off
setlocal EnableDelayedExpansion
chcp 65001 >nul
title Dong bo Git - CMD thuan (khong can PowerShell)
rem Dong bo moi thu muc con co .git nam canh file nay. Khong luu token trong file:
rem dang nhap GitHub 1 lan qua Git Credential Manager (cua so dang nhap hien ra o lan dau).
rem Dung rieng cho 1 repo:  sync-pure-cmd.bat "D:\duong-dan\repo" [nhanh]

set "GIT_CMD=git"
where git >nul 2>nul
if errorlevel 1 (
    if exist "%ProgramFiles%\Git\cmd\git.exe" (set "GIT_CMD=%ProgramFiles%\Git\cmd\git.exe") else if exist "%LOCALAPPDATA%\Programs\Git\cmd\git.exe" (set "GIT_CMD=%LOCALAPPDATA%\Programs\Git\cmd\git.exe") else (
        echo [LOI] Khong tim thay Git. Cai bang: winget install --id Git.Git -e
        pause & exit /b 1
    )
)

echo =======================================================
echo     DONG BO GITHUB BANG CMD THUAN
echo =======================================================
if not "%~1"=="" (
    call :sync_one "%~1" "%~2"
) else (
    for /d %%D in ("%~dp0*") do (
        if exist "%%~fD\.git" call :sync_one "%%~fD" ""
    )
)
echo.
echo HOAN TAT.
pause
exit /b 0

:sync_one
set "DIR=%~1"
set "BR=%~2"
echo.
echo [*] %DIR%
pushd "%DIR%" || goto :eof
if "%BR%"=="" for /f "delims=" %%b in ('"%GIT_CMD%" rev-parse --abbrev-ref HEAD 2^>nul') do set "BR=%%b"
if "%BR%"=="" set "BR=main"
set "HAS="
for /f "delims=" %%a in ('"%GIT_CMD%" status --porcelain') do set "HAS=1"
if defined HAS (
    "%GIT_CMD%" add -A
    "%GIT_CMD%" commit -m "Auto sync: %date% %time%"
)
"%GIT_CMD%" pull --rebase --autostash origin %BR%
if errorlevel 1 (
    echo [LOI] Pull bi loi hoac xung dot, huy rebase. Hay xu ly thu cong.
    "%GIT_CMD%" rebase --abort >nul 2>nul
    popd & goto :eof
)
"%GIT_CMD%" push -u origin HEAD:%BR%
if errorlevel 1 (echo [LOI] Push that bai.) else (echo [OK] Da dong bo %BR%.)
popd
goto :eof
