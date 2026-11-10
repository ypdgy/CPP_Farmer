// Language-neutral ABI between the BepInEx plugin and the native front-ends.
//
// One DLL hosts every front-end. Each front-end registers a CfLanguage
// descriptor (id, display name, file extensions, compile function); the plugin
// picks one by file extension and passes its id back on every call. Adding a
// language therefore means adding a front-end and one entry in
// native/src/registry.cpp -- no change to this header, the plugin, or the
// existing front-ends.
//
// Strings crossing this boundary are UTF-8, NUL-terminated and owned by the
// callee until the caller releases them with cf_free.
#ifndef CF_API_H
#define CF_API_H

#if defined(_WIN32)
#  define CF_EXPORT extern "C" __declspec(dllexport)
#else
#  define CF_EXPORT extern "C" __attribute__((visibility("default")))
#endif

// Compiles `src` into the game's script language.
//
//   src            source text of one code window (never null)
//   unlocks_csv    available tech-tree unlocks, ',' / ';' / whitespace separated,
//                  or null to skip unlock gating
//   out            receives the generated script, malloc'd (may be null)
//   line_map_csv   receives one integer per output line: the source line it came
//                  from, 0 for compiler-generated lines (may be null)
//   err            receives "file:line:col: message", malloc'd (may be null)
//
// Returns 0 on success. Non-zero codes are front-end specific; the plugin only
// distinguishes 0 from "failed" and shows `err` to the player.
typedef int (*cf_compile_fn)(const char* src, const char* unlocks_csv,
                             char** out, char** line_map_csv, char** err);

typedef struct CfLanguage {
    const char* id;            // stable lowercase id, e.g. "cpp"
    const char* display_name;  // shown in logs and the help page, e.g. "C++"
    const char* extensions;    // ';' separated, leading dot: ".cpp;.cxx"
    cf_compile_fn compile;
} CfLanguage;

// Version string of the native library, e.g. "cppfarmer 0.3.0".
CF_EXPORT const char* cf_version(void);

// Introspection. Index 0 is the default language: it is used for windows whose
// extension matched nothing (which only happens for legacy hand-edited saves).
CF_EXPORT int cf_language_count(void);
CF_EXPORT const char* cf_language_id(int index);
CF_EXPORT const char* cf_language_display(int index);
CF_EXPORT const char* cf_language_extensions(int index);

// Compile with an explicit language id. A null or empty id selects the default.
CF_EXPORT int cf_transpile(const char* language, const char* src,
                           const char* unlocks_csv, char** out, char** err);

CF_EXPORT int cf_transpile_ex(const char* language, const char* src,
                              const char* unlocks_csv, char** out,
                              char** line_map_csv, char** err);

// Releases any string returned through this API.
CF_EXPORT void cf_free(char* p);

// ---------------------------------------------------------------------------
// Compatibility aliases (pre-0.3 plugins call these). They always compile as
// the default language. Kept so an old plugin build keeps working with a new
// DLL instead of failing on a missing entry point.
// ---------------------------------------------------------------------------
CF_EXPORT int cppfarmer_transpile(const char* src, const char* unlocks_csv,
                                  char** out, char** err);
CF_EXPORT int cppfarmer_transpile_ex(const char* src, const char* unlocks_csv,
                                     char** out, char** line_map_csv, char** err);
CF_EXPORT const char* cppfarmer_version(void);

#endif  // CF_API_H
