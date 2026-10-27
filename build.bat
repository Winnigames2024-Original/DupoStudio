@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo === Сборка Dupo Studio ===

where dotnet >nul 2>nul
if %errorlevel%==0 (
    dotnet build DupoApp.sln -c Release
    goto done
)

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" set "VSWHERE=%ProgramFiles%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" goto nomsbuild

for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSB=%%i"
if not defined MSB goto nomsbuild

"%MSB%" DupoApp.sln /p:Configuration=Release /m
goto done

:nomsbuild
echo Не нашёл ни dotnet, ни MSBuild. Откройте DupoApp.sln в Visual Studio и нажмите Build.

:done
echo.
echo Готово. Студия: DupoStudio\bin\Release\net48\DupoStudio.exe
pause
