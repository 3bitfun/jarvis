@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo .NET SDK was not found on PATH.
    echo Install the .NET SDK, then run this manager again.
    pause
    exit /b 1
)

:menu
cls
echo ======================================
echo           J.A.R.V.I.S. Manager
echo ======================================
echo  [1] Build Windows EXE ^(Release publish^)
echo  [2] Run in Debug mode
echo  [3] Build Windows installer ^(optional GGUF import during setup^)
echo  [4] Build installer with a bundled GGUF
echo  [5] Exit
echo.
set "choice="
set /p "choice=Choose an option: "
if errorlevel 1 exit /b 0
if "%choice%"=="1" goto publish
if "%choice%"=="2" goto debug
if "%choice%"=="3" goto installer
if "%choice%"=="4" goto bundledinstaller
if "%choice%"=="5" exit /b 0
echo Invalid choice.
pause
goto menu

:publish
echo.
echo Publishing Windows x64 executable...
dotnet publish "JARVIS.csproj" -c Release -r win-x64 --self-contained true -o "dist\win-x64"
if errorlevel 1 goto failed
if not exist "dist\win-x64\models" mkdir "dist\win-x64\models"
echo.
echo Build complete: "%~dp0dist\win-x64\JARVIS.exe"
echo Put .gguf models in "%~dp0dist\win-x64\models".
pause
goto menu

:installer
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0installer\Build-Installer.ps1"
if errorlevel 1 goto failed
pause
goto menu

:bundledinstaller
echo Choose a GGUF you have permission to redistribute.
set "ggufpath="
set /p "ggufpath=Full GGUF path (without quotes): "
if not defined ggufpath goto menu
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0installer\Build-Installer.ps1" -GgufPath "%ggufpath%"
if errorlevel 1 goto failed
pause
goto menu

:debug
echo.
echo Starting J.A.R.V.I.S. in Debug mode...
dotnet run --project "JARVIS.csproj" -c Debug
if errorlevel 1 goto failed
pause
goto menu

:failed
echo.
echo Command failed. Review the output above.
pause
goto menu
