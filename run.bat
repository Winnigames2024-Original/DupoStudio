@echo off
cd /d "%~dp0"
if exist "DupoStudio\bin\Release\net48\DupoStudio.exe" (
    start "" "DupoStudio\bin\Release\net48\DupoStudio.exe"
) else if exist "DupoStudio\bin\Debug\net48\DupoStudio.exe" (
    start "" "DupoStudio\bin\Debug\net48\DupoStudio.exe"
) else (
    echo Сначала соберите проект: build.bat
    pause
)
