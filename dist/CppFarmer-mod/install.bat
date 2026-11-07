@echo off
setlocal

rem Installs C++ Farmer and, if needed, the bundled BepInEx 5.4.23.6.
rem An existing BepInEx installation is ALWAYS left untouched.
rem
rem Usage:  install.bat ["D:\path\to\The Farmer Was Replaced"]
rem         Without an argument the script asks for the game folder.
rem Env:    CPPFARMER_SKIP_BEPINEX=1  never install BepInEx, plugin only

echo.
echo ============================================================
echo   C++ Farmer installer
echo ============================================================
echo.

rem ------------------------------------------------------------------
rem Locate the game folder: argument, else interactive prompt.
rem ------------------------------------------------------------------
if not "%~1"=="" (
    set "GAME=%~1"
    goto :havegame
)

:ask
set "GAME="
echo Where is the game installed?
echo Examples:
echo   C:\Program Files (x86)\Steam\steamapps\common\The Farmer Was Replaced
echo   D:\SteamLibrary\steamapps\common\The Farmer Was Replaced
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
echo.
echo Target: %GAME%
echo.
echo ============================================================
echo  BACK UP YOUR SAVE FOLDER BEFORE CONTINUING
echo  This mod can corrupt or lose save data. Quit the game and back up first.
echo  %USERPROFILE%\AppData\LocalLow\TheFarmerWasReplaced\TheFarmerWasReplaced\Saves\
echo  See README.md -^> "Save Safety" for details.
echo.
echo  Press any key to continue, or close this window to cancel.
echo ============================================================
echo.
pause

rem %~dp0 ends with a backslash.
set "ROOT=%~dp0"
set "PAYLOAD=%ROOT%BepInEx\plugins\CppFarmer"
set "DST=%GAME%\BepInEx\plugins\CppFarmer"

if not exist "%PAYLOAD%\CppFarmer.dll" (
    echo [ERROR] Payload missing next to this script: %PAYLOAD%\CppFarmer.dll
    pause
    exit /b 1
)
if not exist "%PAYLOAD%\cppfarmer_native.dll" (
    echo [ERROR] Payload missing next to this script: %PAYLOAD%\cppfarmer_native.dll
    pause
    exit /b 1
)

rem ------------------------------------------------------------------
rem Step 1: BepInEx. Only when the game has none.
rem ------------------------------------------------------------------
set "BEP_DONE=0"
if exist "%GAME%\BepInEx\core\BepInEx.dll" (
    echo [1/2] Existing BepInEx detected - left untouched.
    echo       If mods do not load, check winhttp.dll and doorstop_config.ini.
) else (
    if "%CPPFARMER_SKIP_BEPINEX%"=="1" (
        echo [1/2] CPPFARMER_SKIP_BEPINEX=1 - BepInEx install skipped.
        echo       The plugin will not load until BepInEx is installed.
    ) else (
        if not exist "%ROOT%BepInEx\core\BepInEx.dll" (
            echo [ERROR] Bundled BepInEx payload is missing: %ROOT%BepInEx\core\
            echo         Re-download the release zip, or install BepInEx 5.4.23.6 manually.
            pause
            exit /b 1
        )
        echo [1/2] Installing bundled BepInEx 5.4.23.6 ...
        if not exist "%GAME%\BepInEx\core" mkdir "%GAME%\BepInEx\core"
        xcopy /Y /Q "%ROOT%BepInEx\core\*.*" "%GAME%\BepInEx\core\" >nul
        if not exist "%GAME%\BepInEx\core\BepInEx.dll" (
            echo [ERROR] Could not copy BepInEx core files.
            echo         Check write permissions on the game folder.
            pause
            exit /b 1
        )
        if not exist "%GAME%\winhttp.dll" (
            copy /Y "%ROOT%winhttp.dll" "%GAME%\winhttp.dll" >nul
        ) else (
            echo       [WARN] Existing winhttp.dll kept, not overwritten.
        )
        if not exist "%GAME%\doorstop_config.ini" (
            copy /Y "%ROOT%doorstop_config.ini" "%GAME%\doorstop_config.ini" >nul
        ) else (
            echo       [WARN] Existing doorstop_config.ini kept, not overwritten.
        )
        if not exist "%GAME%\.doorstop_version" (
            copy /Y "%ROOT%.doorstop_version" "%GAME%\.doorstop_version" >nul
        )
        set "BEP_DONE=1"
    )
)

rem ------------------------------------------------------------------
rem Step 2: the plugin itself.
rem ------------------------------------------------------------------
echo [2/2] Installing C++ Farmer plugin ...
if not exist "%DST%" mkdir "%DST%"
copy /Y "%PAYLOAD%\CppFarmer.dll" "%DST%\CppFarmer.dll" >nul
copy /Y "%PAYLOAD%\cppfarmer_native.dll" "%DST%\cppfarmer_native.dll" >nul
if not exist "%DST%\CppFarmer.dll" (
    echo [ERROR] Could not copy the plugin DLL. Check write permissions.
    pause
    exit /b 1
)

echo.
echo Installed:
echo   %DST%\CppFarmer.dll
echo   %DST%\cppfarmer_native.dll
if "%BEP_DONE%"=="1" (
    echo   BepInEx 5.4.23.6 core + winhttp.dll + doorstop_config.ini
    echo.
    echo   The bundled BepInEx is unmodified. Source and MIT license:
    echo   https://github.com/BepInEx/BepInEx
    echo   See THIRD_PARTY_NOTICES.md for details.
)
echo.
echo Start the game once to let BepInEx generate its config, then look for
echo "C++ Farmer" in BepInEx\LogOutput.log.
pause
