@echo off
setlocal
set "REPO_ROOT=%~dp0"
powershell -NoLogo -NoProfile -NoExit -ExecutionPolicy Bypass -Command "Set-Location -LiteralPath '%REPO_ROOT%'; & '%REPO_ROOT%run-web.ps1' -Database demo"
