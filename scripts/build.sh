#!/usr/bin/env bash
# Build the native front-ends.
# Requires a C++20 compiler (g++ 10+ / clang 12+ / MSVC 19.30+).
#
# Layout:
#   native/include/       public ABI shared with the plugin (cf_api.h)
#   native/src/           registry + shared helpers
#   native/frontends/*/   one directory per language
set -euo pipefail

cd "$(dirname "$0")/.."
CXX="${CXX:-g++}"
OUT=build
mkdir -p "$OUT"

INCLUDES=(-I native/include -I native/src -I native/frontends/cpp -I native/frontends/java -I native/frontends/csharp)

echo "compiling with $CXX"
"$CXX" -std=c++20 -O2 -Wall -Wextra "${INCLUDES[@]}" \
    native/frontends/cpp/cli.cpp -o "$OUT/cppfarmer.exe"

"$CXX" -std=c++20 -O2 -Wall -Wextra "${INCLUDES[@]}" \
    native/frontends/cpp/watch.cpp -o "$OUT/cppfarmer-watch.exe"

# Native core for the BepInEx plugin. Linked fully static so the plugin folder
# never needs libstdc++/libwinpthread from the toolchain.
"$CXX" -std=c++20 -O2 -Wall -Wextra -shared -static "${INCLUDES[@]}" \
    native/src/registry.cpp native/src/cf_util.cpp native/frontends/cpp/cpp_frontend.cpp \
    native/frontends/java/java_frontend.cpp native/frontends/csharp/csharp_frontend.cpp \
    -o "$OUT/cppfarmer_native.dll"

echo "ok: $OUT/cppfarmer.exe  $OUT/cppfarmer-watch.exe  $OUT/cppfarmer_native.dll"
