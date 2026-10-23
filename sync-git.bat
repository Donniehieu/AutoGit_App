@echo off
rem Dong bo nhanh: uu tien GitSyncHub.exe (khong can PowerShell), neu khong co thi dung CMD thuan.
if exist "%~dp0GitSyncHub.exe" (
    "%~dp0GitSyncHub.exe" --sync-all --config "%~dp0sync-config.json" --log "%~dp0sync.log"
    echo Xong. Xem chi tiet trong sync.log
    pause
) else (
    call "%~dp0sync-pure-cmd.bat"
)
