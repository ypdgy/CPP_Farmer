#!/usr/bin/env bash
# Build the C++ Farmer front-end.
# Requires a C++20 compiler (g++ 10+ / clang 12+ / MSVC 19.30+).
set -euo pipefail

cd "$(dirname "$0")/.."
CXX="${CXX:-g++}"
OUT=build
mkdir -p "$OUT"

echo "compiling with $CXX"
"$CXX" -std=c++20 -O2 -Wall -Wextra -I cpp_front/src \
    cpp_front/src/main.cpp -o "$OUT/cppfarmer.exe"

"$CXX" -std=c++20 -O2 -Wall -Wextra -I cpp_front/src \
    cpp_front/src/watch.cpp -o "$OUT/cppfarmer-watch.exe"

# Native core for the BepInEx plugin. Linked fully static so the plugin folder
# never needs libstdc++/libwinpthread from the toolchain.
"$CXX" -std=c++20 -O2 -Wall -Wextra -shared -static -I cpp_front/src \
    cpp_front/src/dll.cpp -o "$OUT/cppfarmer_native.dll"

echo "ok: $OUT/cppfarmer.exe  $OUT/cppfarmer-watch.exe  $OUT/cppfarmer_native.dll"
