@echo off
REM Double-click or run `dev` to start/restart the three MttTracker dev servers.
REM Pass -StopOnly to just shut them down, e.g. `dev -StopOnly`.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0dev.ps1" %*
