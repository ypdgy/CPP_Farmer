@echo off
setlocal

rem Removes the C++ Farmer plugin. The game and BepInEx are left untouched.
rem Usage:  uninstall.bat  ["D:\path\to\The Farmer Was Replaced"]
rem         Without an argument the script asks for the game folder.

if not "%~1"=="" (
    set "GAME=%~1"
    goto :havegame
)

:ask
set "GAME="
echo From which game installation should C++ Farmer be removed?
echo Tip: you can drag the game folder into this window to paste its path.
echo Type q to quit.
set /p "GAME=Game folder: "
if not defined GAME goto :ask
rem Users often paste a quoted path from Explorer's "copy as path".
set "GAME=%GAME:"=%"
if /i "%GAME%"=="q" exit /b 0
if /i "%GAME%"=="quit" exit /b 0
if not exist "%GAME%\TheFarmerWasReplaced.exe" (
    echo.
    echo [!] TheFarmerWasReplaced.exe was not found in "%GAME%".
    echo     Point at the folder that contains the game exe, or type q to quit.
    echo.
    goto :ask
)

:havegame
set "DST=%GAME%\BepInEx\plugins\CppFarmer"

if not exist "%DST%" (
    echo Nothing to remove: %DST% does not exist.
    pause
    exit /b 0
)

echo Removing %DST%
rmdir /S /Q "%DST%"

echo Done. The game's own files were never modified.
echo.
echo BepInEx itself was NOT removed - other mods may depend on it.
echo To remove it manually, quit the game and delete from the game folder:
echo   winhttp.dll
echo   doorstop_config.ini
echo   .doorstop_version
echo   BepInEx\
echo WARNING: deleting BepInEx\ also deletes every other mod and its config.
echo.
echo Note: save files are untouched. If your save looks wrong, quit the game
echo       and restore it from your own backup -^> README.md "Save Safety".
pause
