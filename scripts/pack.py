#!/usr/bin/env python3
"""Assemble dist/CppFarmer-mod.zip from the versioned payload sources.

The release zip is a *build artifact*. Everything in dist/CppFarmer-mod/ is
version-controlled, including the unmodified BepInEx 5.4.23.6 payload
(winhttp.dll, doorstop_config.ini, BepInEx/core/*) and the third-party notices.
Only C++ Farmer's own two DLLs are injected from local build outputs, so a
stale DLL can never leak into a release from the source tree.

Usage:
    python scripts/pack.py

Requires:
    build/cppfarmer_native.dll       (scripts/build.sh / build.bat)
    plugin/bin/Release/CppFarmer.dll (dotnet build -c Release ...)

Windows note: do NOT replace this with Compress-Archive -- it writes backslash
entry names, and some unzip tools then create literal backslashes in filenames.
Always use python's zipfile (forward slashes) as below.
"""

from pathlib import Path
import zipfile
import sys

ROOT = Path(__file__).resolve().parent.parent
PAYLOAD = ROOT / "dist" / "CppFarmer-mod"
OUT = ROOT / "dist" / "CppFarmer-mod.zip"
PREFIX = "CppFarmer-mod"
PLUGIN_DIR = "BepInEx/plugins/CppFarmer/"

BINARIES = {
    ROOT / "build" / "cppfarmer_native.dll":
        f"{PREFIX}/{PLUGIN_DIR}cppfarmer_native.dll",
    ROOT / "plugin" / "bin" / "Release" / "CppFarmer.dll":
        f"{PREFIX}/{PLUGIN_DIR}CppFarmer.dll",
}

# Copied into the package from the repo, not from the payload:
#   (source directory, archive directory, file that must exist)
# editor/      IDE headers, so players can point their editor at the game API
# plugin/pages in-game reference pages (plain Markdown, editable without a rebuild)
EXTRA_DIRS = (
    (ROOT / "editor", f"{PREFIX}/editor", "cppfarmer_api.hpp"),
    (ROOT / "plugin" / "pages", f"{PREFIX}/{PLUGIN_DIR}", "cppfarmer_reference.md"),
)

# Sanity checks: a zip without these would install a broken BepInEx.
REQUIRED = (
    "install.bat",
    "uninstall.bat",
    "README.md",
    "THIRD_PARTY_NOTICES.md",
    "LICENSES/BepInEx-LGPL-2.1.txt",
    "winhttp.dll",
    "doorstop_config.ini",
    ".doorstop_version",
    "BepInEx/core/BepInEx.dll",
    "BepInEx/core/BepInEx.Preloader.dll",
)


def main() -> int:
    if not PAYLOAD.is_dir():
        print(f"missing payload directory: {PAYLOAD}", file=sys.stderr)
        return 1

    absent = [f for f in REQUIRED if not (PAYLOAD / f).is_file()]
    if absent:
        print("payload incomplete, missing:", file=sys.stderr)
        for name in absent:
            print(f"  {name}", file=sys.stderr)
        return 1

    missing = [str(p) for p in BINARIES if not p.is_file()]
    if missing:
        print("build the project first; missing binaries:", file=sys.stderr)
        for path in missing:
            print(f"  {path}", file=sys.stderr)
        return 1

    entries: list[tuple[Path, str]] = []
    for path in sorted(PAYLOAD.rglob("*")):
        if not path.is_file():
            continue
        rel = path.relative_to(PAYLOAD).as_posix()
        # Our own plugin DLLs are injected below from the build outputs. The
        # BepInEx DLLs in BepInEx/core/ are part of the payload and must stay.
        if rel.startswith(PLUGIN_DIR):
            continue
        # The editor assets are owned by EXTRA_DIRS below (they live in the
        # repo, not in the payload), so a stale copy in the payload would
        # produce duplicate zip entries.
        if rel.startswith("editor/"):
            continue
        entries.append((path, f"{PREFIX}/{rel}"))

    for src_dir, arc_dir, marker in EXTRA_DIRS:
        if not (src_dir / marker).is_file():
            print(f"missing {marker} in {src_dir}", file=sys.stderr)
            return 1
        for path in sorted(src_dir.rglob("*")):
            if not path.is_file():
                continue
            rel = path.relative_to(src_dir).as_posix()
            entries.append((path, arc_dir.rstrip("/") + "/" + rel))

    for src, arc in BINARIES.items():
        entries.append((src, arc))

    # Pages must land next to the DLL: the plugin reads them from its own folder.
    required_arcs = {
        f"{PREFIX}/{PLUGIN_DIR}cppfarmer_reference.md",
        f"{PREFIX}/{PLUGIN_DIR}cppfarmer_errors.md",
        f"{PREFIX}/{PLUGIN_DIR}cfarmer_reference.md",
        f"{PREFIX}/{PLUGIN_DIR}cfarmer_errors.md",
        f"{PREFIX}/{PLUGIN_DIR}javafarmer_reference.md",
        f"{PREFIX}/{PLUGIN_DIR}javafarmer_errors.md",
        f"{PREFIX}/{PLUGIN_DIR}farmer_index.md",
        f"{PREFIX}/editor/cfarmer_api.h",
    }
    have = {arc for _, arc in entries}
    absent_arcs = sorted(required_arcs - have)
    if absent_arcs:
        print("package incomplete, missing:", file=sys.stderr)
        for arc in absent_arcs:
            print(f"  {arc}", file=sys.stderr)
        return 1

    seen = set()
    duplicates = [arc for _, arc in entries if arc in seen or seen.add(arc)]
    if duplicates:
        print("duplicate zip entries:", file=sys.stderr)
        for arc in sorted(set(duplicates)):
            print(f"  {arc}", file=sys.stderr)
        return 1

    if OUT.exists():
        OUT.unlink()
    with zipfile.ZipFile(OUT, "w", zipfile.ZIP_DEFLATED) as zf:
        for src, arc in entries:
            zf.write(src, arc)

    print(f"wrote {OUT.relative_to(ROOT)} ({OUT.stat().st_size} bytes, {len(entries)} files)")
    for _, arc in entries:
        print(f"  {arc}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
