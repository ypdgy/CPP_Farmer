@echo off
REM Transpile src\*.cpp into a game save folder and keep watching.
REM   usage: scripts\watch_to_save.bat [SaveName]     (default: Save0)
setlocal
cd /d "%~dp0.."

set SAVE=%~1
if "%SAVE%"=="" set SAVE=Save0

set DEST=%USERPROFILE%\AppData\LocalLow\TheFarmerWasReplaced\TheFarmerWasReplaced\Saves\%SAVE%

if not exist "%DEST%" (
    echo save folder not found: %DEST%
    exit /b 1
)
if not exist build\cppfarmer-watch.exe (
    echo build\cppfarmer-watch.exe missing - run scripts\build.bat first
    exit /b 1
)

echo generating into %DEST%
echo remember to enable "file watcher" in the game options
build\cppfarmer-watch.exe src "%DEST%" --line-hints
