@echo off
REM Build the C++ Farmer front-end with MSVC or MinGW g++.
setlocal
cd /d "%~dp0.."
if not exist build mkdir build

where g++ >nul 2>nul
if %errorlevel%==0 (
    g++ -std=c++20 -O2 -Wall -Wextra -I cpp_front/src cpp_front/src/main.cpp -o build\cppfarmer.exe || exit /b 1
    g++ -std=c++20 -O2 -Wall -Wextra -I cpp_front/src cpp_front/src/watch.cpp -o build\cppfarmer-watch.exe || exit /b 1
    g++ -std=c++20 -O2 -Wall -Wextra -shared -static -I cpp_front/src cpp_front/src/dll.cpp -o build\cppfarmer_native.dll || exit /b 1
    echo ok: build\cppfarmer.exe build\cppfarmer-watch.exe build\cppfarmer_native.dll
    exit /b 0
)

where cl >nul 2>nul
if %errorlevel%==0 (
    cl /nologo /std:c++20 /O2 /EHsc /I cpp_front\src cpp_front\src\main.cpp /Fo:build\ /Fe:build\cppfarmer.exe || exit /b 1
    cl /nologo /std:c++20 /O2 /EHsc /I cpp_front\src cpp_front\src\watch.cpp /Fo:build\ /Fe:build\cppfarmer-watch.exe || exit /b 1
    echo ok: build\cppfarmer.exe build\cppfarmer-watch.exe
    exit /b 0
)

echo no C++ compiler found (need g++ or cl in PATH)
exit /b 1
