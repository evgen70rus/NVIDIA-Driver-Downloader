@echo off
setlocal
title NVIDIA Driver Downloader Update
set "SCRIPT=%~dp0REPAIR.ps1"
if not exist "%SCRIPT%" (
  echo ERROR: REPAIR.ps1 was not found.
  pause
  exit /b 1
)
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%"
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" (
  echo.
  echo Update failed. The existing program was not intentionally replaced.
  pause
  exit /b %RC%
)
echo.
echo Update completed.
pause
exit /b 0
