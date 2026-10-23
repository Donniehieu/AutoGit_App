@echo off
cd /d "%~dp0"
start "" "%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -STA -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0app\server.ps1"
