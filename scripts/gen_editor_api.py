#!/usr/bin/env python3
"""Generate editor/cppfarmer_api.hpp from the game's builtins.py.

The header is for external editors (VS Code / clangd) only: it declares the
game's builtin functions, direction constants and enum namespaces so that
completion and hover work outside the game. The mod's C++ front-end ignores
`#include <...>`, so the header never takes part in the actual transpile.

Usage:
    python scripts/gen_editor_api.py [path to game folder]

If no folder is given, common Steam locations are tried.
"""

from pathlib import Path
import re
import sys

DEFAULT_GAMES = [
    Path(r"D:/SteamLibrary/steamapps/common/The Farmer Was Replaced"),
    Path(r"C:/Program Files (x86)/Steam/steamapps/common/The Farmer Was Replaced"),
]

ENUMS = ["Entities", "Grounds", "Hats", "Items", "Leaderboards", "Unlocks"]

# Front-end support is a subset; these stay out of the header because writing
# them as functions is not valid in the game either.
SKIP_FUNCS = {
    "range", "add", "append", "insert", "len", "pop", "remove", "str",
    "simulate",
}

RETURN_MAP = [
    ("_bool", "bool"),
    ("_int", "int"),
    ("_float", "double"),
    ("string", "std::string"),
    ("dict[", "std::map<int, int>"),
    ("_dict[", "std::map<int, int>"),
    ("list[", "std::vector<int>"),
    ("_list[", "std::vector<int>"),
    ("tuple[", "std::vector<int>"),
    ("_tuple[", "std::vector<int>"),
    ("set[", "std::vector<int>"),
    ("_set[", "std::vector<int>"),
    ("range_class", "std::vector<int>"),
    ("None", "void"),
]


def find_game(argv: list[str]) -> Path:
    if len(argv) > 1:
        p = Path(argv[1])
        if (p / "TheFarmerWasReplaced.exe").is_file():
            return p
        raise SystemExit(f"not a game folder: {p}")
    for p in DEFAULT_GAMES:
        if (p / "TheFarmerWasReplaced.exe").is_file():
            return p
    raise SystemExit("game folder not found; pass it as the first argument")


def builtins_path(game: Path) -> Path:
    base = game / "TheFarmerWasReplaced_Data" / "StreamingAssets" / "Languages"
    for candidate in (base / "EN" / "builtins.py", base / "builtins.py"):
        if candidate.is_file():
            return candidate
    hits = sorted(base.glob("*/builtins.py")) + sorted(base.glob("builtins.py"))
    if not hits:
        raise SystemExit(f"builtins.py not found under {base}")
    return hits[0]


def read_defs(lines: list[str]) -> list[tuple[str, str]]:
    """Return (name, full signature) for every top-level def, joining continuations."""
    out = []
    i = 0
    while i < len(lines):
        m = re.match(r"^def\s+([A-Za-z_]\w*)\s*[\[(]", lines[i])
        if not m:
            i += 1
            continue
        name = m.group(1)
        buf = lines[i]
        depth = buf.count("(") - buf.count(")")
        j = i
        while depth > 0 and j + 1 < len(lines):
            j += 1
            buf += " " + lines[j].strip()
            depth += lines[j].count("(") - lines[j].count(")")
        out.append((name, re.sub(r"\s+", " ", buf)))
        i = j + 1
    return out


def parse_params(signature: str) -> list[tuple[str, str, str | None]]:
    """(name, type text, default) for positional params; skips /, *, ** markers."""
    start = signature.find("(")
    depth = 0
    end = start
    for idx in range(start, len(signature)):
        if signature[idx] == "(":
            depth += 1
        elif signature[idx] == ")":
            depth -= 1
            if depth == 0:
                end = idx
                break
    inner = signature[start + 1:end]
    parts, buf, depth = [], "", 0
    for ch in inner:
        if ch in "([":
            depth += 1
        elif ch in ")]":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append(buf.strip())
            buf = ""
        else:
            buf += ch
    if buf.strip():
        parts.append(buf.strip())

    out = []
    for raw in parts:
        if raw in ("/", "*") or raw.startswith("**"):
            continue
        if raw.startswith("*"):
            continue  # variadic markers: handled by the caller
        default = None
        if "=" in raw:
            raw, default = raw.split("=", 1)
            raw, default = raw.strip(), default.strip()
        if ":" in raw:
            name, typ = raw.split(":", 1)
            name, typ = name.strip(), typ.strip()
        else:
            name, typ = raw.strip(), "int"
        out.append((re.sub(r"\W", "", name) or "arg", typ, default))
    return out


def cpp_return(sig: str) -> str:
    if "->" not in sig:
        return "void"
    ret = sig.split("->", 1)[1].strip()
    ret = ret.split(":")[0].strip()  # drop trailing comment
    for key, value in RETURN_MAP:
        if ret.startswith(key) or key in ret:
            return value
    if ret.startswith(("Entity", "Direction", "Ground", "Item", "Unlock", "Measure",
                       "WorldSizes", "Drone")):
        return "int"
    return "int"


def cpp_param(pname: str, typ: str, default: str | None) -> str:
    t = "int"
    if "_float" in typ or typ == "float":
        t = "double"
    elif "string" in typ:
        t = "std::string"
    elif "dict" in typ:
        t = "std::map<int, int>"
    if default is not None:
        default = default.replace("_float", "0").replace("None", "0").strip()
        if default in ("None", ""):
            default = "0"
        return f"{t} {pname} = {default}"
    return f"{t} {pname}"


def enum_members(lines: list[str]) -> dict[str, list[str]]:
    out: dict[str, list[str]] = {}
    for idx, line in enumerate(lines):
        m = re.match(r"^class\s+(\w+)\(_Enum\):", line)
        if not m or m.group(1) not in ENUMS:
            continue
        name = m.group(1)
        members = []
        j = idx + 1
        while j < len(lines):
            row = lines[j]
            if row and not row.startswith(("\t", " ")):
                break
            mm = re.match(r"^[ \t]+([A-Za-z_]\w*)\s*:", row)
            if mm:
                members.append(mm.group(1))
            j += 1
        # builtins.py repeats names such as Unlocks.Unlock across groups;
        # C++ does not allow duplicate struct members, so keep the first.
        out[name] = list(dict.fromkeys(members))
    return out


def main() -> int:
    game = find_game(sys.argv)
    src = builtins_path(game).read_text(encoding="utf-8")
    lines = src.splitlines()

    enums = enum_members(lines)
    defs = read_defs(lines)

    out: list[str] = []
    out.append("// cppfarmer_api.hpp - IDE-only declarations for C++ Farmer.")
    out.append("//")
    out.append("// This header exists so VS Code / clangd can complete and describe the")
    out.append("// game's builtin functions and enum constants while you edit .cpp files")
    out.append("// outside the game. The mod's front-end ignores `#include <...>` entirely,")
    out.append("// so including it has no effect on the script that actually runs.")
    out.append("//")
    out.append("// Generated from the game's builtins.py by scripts/gen_editor_api.py.")
    out.append("// Do not edit by hand; re-run the script after a game update.")
    out.append("#pragma once")
    out.append("")
    out.append("#include <string>")
    out.append("#include <vector>")
    out.append("#include <map>")
    # std::cout is the idiomatic output in this dialect, so the editor must know
    # it without the player adding <iostream> themselves.
    out.append("#include <iostream>")
    out.append("")
    out.append("// ---- directions ----------------------------------------------------")
    for i, direction in enumerate(("North", "East", "South", "West")):
        out.append(f"const int {direction} = {i};")
    out.append("")

    for name in ENUMS:
        members = enums.get(name, [])
        if not members:
            continue
        out.append(f"// ---- {name} " + "-" * max(0, 58 - len(name)))
        # The game writes `Entities.Bush`; standard C++ only allows that on an
        # object, so emit a struct plus one inline instance of it.
        out.append(f"struct {name}_t {{")
        for i, member in enumerate(members):
            out.append(f"    static const int {member} = {i};")
        out.append("};")
        out.append(f"inline const {name}_t {name}{{}};")
        out.append("")

    out.append("// ---- world & movement ----------------------------------------------")
    order = [
        "move", "can_move", "get_pos_x", "get_pos_y", "get_pos_z", "get_world_size",
        "get_entity_type", "get_ground_type", "harvest", "can_harvest", "plant",
        "till", "dig", "water", "get_water", "clear", "swap", "push", "can_push",
        "place", "hover", "get_hover", "measure", "get_hardness", "get_stability",
        "prospect_iron", "prospect_quartz", "use_item", "num_items", "change_hat",
        "get_companion", "get_time", "get_tick_count", "set_execution_speed", "set_world_size",
        "get_cost", "unlock", "num_unlocked", "jump", "random", "max_drones",
        "num_drones", "spawn_drone", "wait_for", "has_finished", "do_a_flip",
        "pet_the_piggy", "leaderboard_run", "print", "quick_print", "abs", "min", "max",
    ]

    by_name: dict[str, list[str]] = {}
    for name, sig in defs:
        by_name.setdefault(name, []).append(sig)

    emitted: set[str] = set()
    for name in order:
        if name in emitted or name in SKIP_FUNCS or name not in by_name:
            continue
        sig = by_name[name][0]
        if name in ("print", "quick_print"):
            out.append(f"template <typename... Args> void {name}(Args... args);")
            emitted.add(name)
            continue
        if name == "spawn_drone":
            out.append("int spawn_drone(int task);   // returns a drone handle")
            emitted.add(name)
            continue
        if name in ("min", "max"):
            out.append(f"double {name}(double a, double b);")
            emitted.add(name)
            continue
        params = parse_params(sig)
        rendered = []
        for pname, ptype, pdefault in params:
            rendered.append(cpp_param(pname, ptype, pdefault))
        out.append(f"{cpp_return(sig)} {name}({', '.join(rendered)});")
        emitted.add(name)

    out.append("")
    out.append("// Game-specific notes for this front-end:")
    out.append("// - `get_companion()` returns (entity, (x, y)); tuple types are not")
    out.append("//   supported here, so the stub returns int. Ignore the value or store")
    out.append("//   it in an int if you only need the call for its side effect.")
    out.append("// - `measure()` / `get_cost()` return containers; see docs/LANGUAGE.md")
    out.append("//   for what the transpiler can actually do with them.")
    out.append("")

    editor = Path(__file__).resolve().parent.parent / "editor"
    target = editor / "cppfarmer_api.hpp"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text("\n".join(out), encoding="utf-8", newline="\n")
    print(f"wrote {target} ({len(out)} lines)")

    # C twin of the header; same source, C syntax.
    write_c_header(editor / "cfarmer_api.h", enums, defs)
    for name in ENUMS:
        if enums.get(name):
            print(f"  {name}: {len(enums[name])} members")
    print(f"  functions: {len(emitted)}")
    return 0



# Declared by <stdlib.h> with a different signature; redeclaring it here makes
# gcc warn, and the C library prototype is good enough for the editor.
C_SKIP = {"abs"}

C_TYPE_MAP = {
    "std::string": "const char*",
    "std::map<int, int>": "int",
    "std::vector<int>": "int",
}


def c_type(t: str) -> str:
    return C_TYPE_MAP.get(t, t)


def c_return(sig: str) -> str:
    return c_type(cpp_return(sig))


def c_param(pname: str, ptype: str, default: str | None) -> str:
    """C has no default arguments: keep the type and the name only."""
    rendered = cpp_param(pname, ptype, default)
    if " = " in rendered:
        rendered = rendered.split(" = ", 1)[0]
    # The type is embedded in "TYPE name", so replace text, not the whole string.
    for cpp_type, c_equivalent in C_TYPE_MAP.items():
        rendered = rendered.replace(cpp_type, c_equivalent)
    return rendered


ORDER = [
    "move", "can_move", "get_pos_x", "get_pos_y", "get_pos_z", "get_world_size",
    "get_entity_type", "get_ground_type", "harvest", "can_harvest", "plant",
    "till", "dig", "water", "get_water", "clear", "swap", "push", "can_push",
    "place", "hover", "get_hover", "measure", "get_hardness", "get_stability",
    "prospect_iron", "prospect_quartz", "use_item", "num_items", "change_hat",
    "get_companion", "get_time", "get_tick_count", "set_execution_speed",
    "set_world_size", "get_cost", "unlock", "num_unlocked", "jump", "random",
    "max_drones", "num_drones", "spawn_drone", "wait_for", "has_finished",
    "do_a_flip", "pet_the_piggy", "leaderboard_run", "print", "quick_print",
    "abs", "min", "max",
]


def write_c_header(target: Path, enums: dict[str, list[str]],
                   defs: list[tuple[str, str]]) -> None:
    """C twin of the C++ header: same names, C syntax."""
    out: list[str] = []
    out.append("/* cfarmer_api.h - IDE-only declarations for C++ Farmer (C language).")
    out.append(" *")
    out.append(" * This header exists so an editor can complete and describe the game")
    out.append(" * builtins and enum constants while you edit .c files outside the game.")
    out.append(" * The mod's front-end ignores any #include of a *_api.h file, so")
    out.append(" * including it has no effect on the script that actually runs.")
    out.append(" *")
    out.append(" * Generated from the game's builtins.py by scripts/gen_editor_api.py.")
    out.append(" * Do not edit by hand; re-run the script after a game update.")
    out.append(" */")
    out.append("#ifndef CFARMER_API_H")
    out.append("#define CFARMER_API_H")
    out.append("")
    out.append("#include <stdbool.h>")
    out.append("")
    out.append("/* ---- directions --------------------------------------------------- */")
    out.append("enum { North = 0, East = 1, South = 2, West = 3 };")
    out.append("")

    for name in ENUMS:
        members = enums.get(name, [])
        if not members:
            continue
        out.append("/* ---- " + name + " " + "-" * max(0, 58 - len(name)) + " */")
        # The game writes Entities.Bush, which in C is a struct member, so
        # mirror that shape: one struct plus a const instance of it.
        out.append("typedef struct {")
        for member in members:
            out.append("    int " + member + ";")
        out.append("} cf_" + name + "_t;")
        values = ", ".join(str(i) for i in range(len(members)))
        out.append("static const cf_" + name + "_t " + name + " = { " + values + " };")
        out.append("")

    out.append("/* ---- world & movement --------------------------------------------- */")
    by_name: dict[str, list[str]] = {}
    for name, sig in defs:
        by_name.setdefault(name, []).append(sig)

    emitted: set[str] = set()
    for name in ORDER:
        if name in emitted or name in SKIP_FUNCS or name in C_SKIP or name not in by_name:
            continue
        sig = by_name[name][0]
        if name in ("print", "quick_print"):
            # Emitted as one block below: the spelling differs between the two
            # languages, and a header editor may compile this file as either.
            emitted.add(name)
            continue
        if name == "spawn_drone":
            out.append("int spawn_drone(int task);   /* returns a drone handle */")
            emitted.add(name)
            continue
        if name in ("min", "max"):
            out.append("double " + name + "(double a, double b);")
            emitted.add(name)
            continue
        params = parse_params(sig)
        rendered = [c_param(pname, ptype, pdefault) for pname, ptype, pdefault in params]
        args = ", ".join(rendered) if rendered else "void"
        out.append(c_return(sig) + " " + name + "(" + args + ");")
        emitted.add(name)

    out.append("")
    out.append("/* ---- output ------------------------------------------------------- */")
    out.append("/* print and quick_print accept any argument list and return nothing.")
    out.append(" *")
    out.append(" * No prototype can say that portably: `void print();` is \"unspecified\"")
    out.append(" * in C89 but \"no arguments\" to C++ and to MSVC's IntelliSense (it shows")
    out.append(" * `void print(void)`), while `const char* first, ...` rejects a call")
    out.append(" * whose first argument is not a string. So C++ gets a variadic")
    out.append(" * template and C gets a variadic macro -- both accept any call without")
    out.append(" * a diagnostic, which is exactly the game's rule. */")
    out.append("#ifdef __cplusplus")
    out.append("template <typename... Args> void print(Args... args);")
    out.append("template <typename... Args> void quick_print(Args... args);")
    out.append("#else")
    out.append("#define print(...) ((void)0)")
    out.append("#define quick_print(...) ((void)0)")
    out.append("#endif")
    out.append("")
    out.append("/* ---- C-only ------------------------------------------------------- */")
    out.append("/* printf is not a game builtin: the front-end lowers printf(\"x=%d\", n)")
    out.append(" * into string concatenation at compile time. The prototype matches")
    out.append(" * <stdio.h> so including both headers is harmless. */")
    out.append("int printf(const char* format, ...);")
    out.append("")
    out.append("/* get_companion() returns a tuple; store it in an int if you only need")
    out.append(" * the call for its side effect. */")
    out.append("")
    out.append("#endif /* CFARMER_API_H */")
    out.append("")

    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(chr(10).join(out), encoding="utf-8", newline=chr(10))
    print(f"wrote {target} ({len(out)} lines)")


if __name__ == "__main__":
    raise SystemExit(main())
