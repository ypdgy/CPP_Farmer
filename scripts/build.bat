@echo off
REM Build the native front-ends with MSVC or MinGW g++.
REM Layout: native/include (ABI), native/src (registry), native/frontends/<lang>.
setlocal
cd /d "%~dp0.."
if not exist build mkdir build

set INC=-I native/include -I native/src -I native/frontends/cpp
set CORE=native/src/registry.cpp native/src/cf_util.cpp native/frontends/cpp/cpp_frontend.cpp

where g++ >nul 2>nul
if %errorlevel%==0 (
    g++ -std=c++20 -O2 -Wall -Wextra %INC% native/frontends/cpp/cli.cpp -o build\cppfarmer.exe || exit /b 1
    g++ -std=c++20 -O2 -Wall -Wextra %INC% native/frontends/cpp/watch.cpp -o build\cppfarmer-watch.exe || exit /b 1
    g++ -std=c++20 -O2 -Wall -Wextra -shared -static %INC% %CORE% -o build\cppfarmer_native.dll || exit /b 1
    echo ok: build\cppfarmer.exe build\cppfarmer-watch.exe build\cppfarmer_native.dll
    exit /b 0
)

where cl >nul 2>nul
if %errorlevel%==0 (
    cl /nologo /std:c++20 /O2 /EHsc /I native\include /I native\src /I native\frontends\cpp native\frontends\cpp\cli.cpp /Fo:build\ /Fe:build\cppfarmer.exe || exit /b 1
    cl /nologo /std:c++20 /O2 /EHsc /I native\include /I native\src /I native\frontends\cpp native\frontends\cpp\watch.cpp /Fo:build\ /Fe:build\cppfarmer-watch.exe || exit /b 1
    echo ok: build\cppfarmer.exe build\cppfarmer-watch.exe
    exit /b 0
)

echo no C++ compiler found (need g++ or cl in PATH)
exit /b 1
