#!/usr/bin/env python3
"""C# front-end regression: transpiles tests/csharp and examples/csharp through
cppfarmer_native.dll via ctypes and checks the exit codes.

The C++ CLI (build/cppfarmer.exe) is hard-wired to the C++ front-end, so C# is
exercised directly against the exported cf_transpile_ex entry point -- the same
call the plugin makes. Run after `bash scripts/build.sh`.

Usage:
    python scripts/test_csharp.py
"""
import ctypes
import glob
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DLL = ROOT / "build" / "cppfarmer_native.dll"

if not DLL.is_file():
    print("missing", DLL, "- run bash scripts/build.sh first", file=sys.stderr)
    sys.exit(1)

dll = ctypes.CDLL(str(DLL))
dll.cf_transpile_ex.argtypes = [ctypes.c_char_p, ctypes.c_char_p, ctypes.c_char_p,
                                ctypes.POINTER(ctypes.c_char_p),
                                ctypes.POINTER(ctypes.c_char_p),
                                ctypes.POINTER(ctypes.c_char_p)]
dll.cf_transpile_ex.restype = ctypes.c_int
dll.cf_free.argtypes = [ctypes.c_void_p]


def transpile(lang, src, unlocks=None):
    out = ctypes.c_char_p()
    lmap = ctypes.c_char_p()
    err = ctypes.c_char_p()
    rc = dll.cf_transpile_ex(lang.encode(), src.encode(), (unlocks or "").encode(),
                             ctypes.byref(out), ctypes.byref(lmap), ctypes.byref(err))
    if rc == 0:
        return 0, out.value.decode(), lmap.value.decode()
    return rc, None, err.value.decode()


def main() -> int:
    ok = 0
    bad = 0

    def check(name, ok_condition, detail=""):
        nonlocal ok, bad
        if ok_condition:
            ok += 1
            print(f"[OK ] {name}")
        else:
            bad += 1
            print(f"[FAIL] {name}  {detail}")

    def read(rel):
        return (ROOT / rel).read_text(encoding="utf-8")

    # positive cases
    for rel in ("tests/csharp/entry.cs", "tests/csharp/basic.cs",
                "examples/csharp/farm.cs", "examples/csharp/maze.cs"):
        rc, out, detail = transpile("csharp", read(rel))
        check(rel, rc == 0, detail if rc else "")

    # entry must be hoisted to top-level statements (no `def` in the output)
    entry = read("tests/csharp/entry.cs")
    rc, out, _ = transpile("csharp", entry)
    check("entry contains no 'def'", rc == 0 and "def" not in out, out if rc == 0 else "compile failed")

    # fresh-save unlock set (no 'functions'): the entry still compiles
    rc, out, detail = transpile("csharp", entry, unlocks="variables,operators,loops,lists")
    check("entry compiles with fresh-save unlocks", rc == 0, detail if rc else "")

    # line map is never longer than the generated line count
    rc, out, lmap = transpile("csharp", read("tests/csharp/basic.cs"))
    if rc == 0:
        mapn = len(lmap.split(",")) if lmap else 0
        lines = out.count("\n")
        check("line map length sane", mapn <= lines, f"map={mapn} lines={lines}")

    # negative cases: all must be refused (rc != 0)
    for f in sorted(glob.glob(str(ROOT / "tests" / "csharp" / "err" / "*.cs"))):
        rc, out, detail = transpile("csharp", Path(f).read_text(encoding="utf-8"))
        check(Path(f).as_posix(), rc != 0, detail if rc else "")

    print("=" * 40)
    print(f"ok={ok} bad={bad}")
    return 0 if bad == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
