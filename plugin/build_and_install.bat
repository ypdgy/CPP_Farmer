@echo off
REM Build the C# plugin and install both the plugin and the native compiler
REM into the game's BepInEx/plugins folder.
REM
REM Prerequisites:
REM   1. BepInEx 5 (x64, Mono) already extracted into the game folder
REM   2. .NET SDK 6+ on PATH  (https://dotnet.microsoft.com/download)
REM
REM usage: plugin\build_and_install.bat ["D:\...\The Farmer Was Replaced"]
setlocal enabledelayedexpansion
cd /d "%~dp0.."

set GAME=%~1
if "%GAME%"=="" if not "%CPPFARMER_GAME%"=="" set GAME=%CPPFARMER_GAME%
if exist "%~dp0GameDir.local.txt" (
    if "%GAME%"=="" set /p GAME=<"%~dp0GameDir.local.txt"
)
if "%GAME%"=="" (
    echo [x] game folder not specified.
    echo     use one of:
    echo       build_and_install.bat "C:\...\The Farmer Was Replaced"
    echo       set CPPFARMER_GAME=C:\...\The Farmer Was Replaced
    echo       echo C:\...\The Farmer Was Replaced ^> plugin\GameDir.local.txt
    echo     GameDir.local.txt is git-ignored and stays on your machine.
    exit /b 1
)

if not exist "%GAME%\TheFarmerWasReplaced.exe" (
    echo [x] game folder looks wrong: %GAME%
    exit /b 1
)
if not exist "%GAME%\BepInEx\core\BepInEx.dll" (
    echo [x] BepInEx is not installed in %GAME%
    echo     download BepInEx 5.4.x x64, extract it into the game folder,
    echo     run the game once, then re-run this script
    exit /b 1
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [x] dotnet SDK not found on PATH
    echo     install .NET SDK 6 or newer, or open the project in Visual Studio
    exit /b 1
)

echo [1/3] building native compiler
if not exist build\cppfarmer_native.dll (
    where g++ >nul 2>nul
    if errorlevel 1 (
        echo [x] build\cppfarmer_native.dll missing and g++ not found
        exit /b 1
    )
    g++ -std=c++20 -O2 -shared -static -I native/include -I native/src -I native/frontends/cpp native/src/registry.cpp native/src/cf_util.cpp native/frontends/cpp/cpp_frontend.cpp -o build\cppfarmer_native.dll || exit /b 1
)

echo [2/3] building plugin
dotnet build plugin\CppFarmer.csproj -c Release /p:GameDir="%GAME%" || exit /b 1

set OUT=%GAME%\BepInEx\plugins\CppFarmer
if not exist "%OUT%" mkdir "%OUT%"

echo [3/3] installing to %OUT%
copy /y "plugin\bin\Release\CppFarmer.dll" "%OUT%\" >nul || exit /b 1
copy /y "build\cppfarmer_native.dll" "%OUT%\" >nul || exit /b 1
copy /y "plugin\pages\*.md" "%OUT%\" >nul 2>nul

echo.
echo done. start the game and check BepInEx\LogOutput.log for "C++ Farmer".
echo create or rename a code file to something.cpp and press F5.
