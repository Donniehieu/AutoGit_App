@echo off
chcp 65001 >nul
title Tu dong tai va cai dat PowerShell 7

echo =======================================================
echo        TU DONG CAI DAT POWERSHELL MOI NHAT
echo =======================================================
echo.

where winget >nul 2>nul
if %errorlevel% equ 0 (
    echo Dang cai dat PowerShell 7 qua winget...
    winget install --id Microsoft.PowerShell -e --source winget --accept-package-agreements --accept-source-agreements
    goto finished
)

echo Winget khong kha dung. Dang su dung curl de tai truc tiep tu Microsoft...
set "PS_URL=https://github.com/PowerShell/PowerShell/releases/download/v7.4.5/PowerShell-7.4.5-win-x64.msi"
set "DEST=%TEMP%\PowerShell-Setup.msi"

curl -L -o "%DEST%" "%PS_URL%"
if exist "%DEST%" (
    echo Dang tien hanh cai dat PowerShell...
    msiexec /i "%DEST%" /quiet /norestart
    del "%DEST%" >nul 2>nul
    echo Da cai dat thanh cong!
) else (
    echo [LOI] Khong the tai bo cai dat. Vui long kiem tra ket noi mang.
)

:finished
echo.
echo =======================================================
echo      HOAN TAT! POWERSHELL DA SAN SANG SU DUNG.
echo =======================================================
pause
