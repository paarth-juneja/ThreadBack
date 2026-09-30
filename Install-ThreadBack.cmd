@echo off
setlocal
echo ThreadBack setup - keep this window open until setup finishes.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Install-ThreadBack.ps1" %*
set "setupResult=%ERRORLEVEL%"
echo.
if not "%setupResult%"=="0" echo Setup did not finish. Read the error above before trying again.
pause
exit /b %setupResult%
