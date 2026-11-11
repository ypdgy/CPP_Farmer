// Language registry and the exported cf_* surface.
//
// This file is the only place that knows the full list of front-ends. Adding a
// language = include its header, add one entry to kLanguages, and update the
// build script. Nothing else in the native tree changes.
#include <cstdlib>
#include <cstring>
#include <string>

#include "cf_api.h"
#include "cf_util.h"
#include "cpp_frontend.h"

namespace {

// Index 0 is the default language: used when the plugin passes no id.
const CfLanguage* const kLanguages[] = {
    &cf_cpp_language(),
    &cf_c_language(),
    // &cf_rust_language(),
};

const int kLanguageCount =
    static_cast<int>(sizeof(kLanguages) / sizeof(kLanguages[0]));

const CfLanguage* Find(const char* id) {
    if (!id || !*id) return kLanguages[0];
    for (int i = 0; i < kLanguageCount; ++i) {
        if (std::strcmp(kLanguages[i]->id, id) == 0) return kLanguages[i];
    }
    return nullptr;
}

void SetError(char** err, const char* language, int rc) {
    if (!err) return;
    *err = cf_dup(std::string("1:0:0: unknown language '")
                  + (language && *language ? language : "") + "' (code "
                  + std::to_string(rc) + ")");
}

}  // namespace

CF_EXPORT const char* cf_version(void) { return "cppfarmer 0.4.1"; }

CF_EXPORT int cf_language_count(void) { return kLanguageCount; }

CF_EXPORT const char* cf_language_id(int index) {
    if (index < 0 || index >= kLanguageCount) return nullptr;
    return kLanguages[index]->id;
}

CF_EXPORT const char* cf_language_display(int index) {
    if (index < 0 || index >= kLanguageCount) return nullptr;
    return kLanguages[index]->display_name;
}

CF_EXPORT const char* cf_language_extensions(int index) {
    if (index < 0 || index >= kLanguageCount) return nullptr;
    return kLanguages[index]->extensions;
}

CF_EXPORT int cf_transpile_ex(const char* language, const char* src,
                              const char* unlocks_csv, char** out,
                              char** line_map_csv, char** err) {
    if (out) *out = nullptr;
    if (line_map_csv) *line_map_csv = nullptr;
    if (err) *err = nullptr;

    const CfLanguage* frontend = Find(language);
    if (!frontend) {
        SetError(err, language, 10);
        return 10;
    }
    return frontend->compile(src, unlocks_csv, out, line_map_csv, err);
}

CF_EXPORT int cf_transpile(const char* language, const char* src,
                           const char* unlocks_csv, char** out, char** err) {
    return cf_transpile_ex(language, src, unlocks_csv, out, nullptr, err);
}

CF_EXPORT void cf_free(char* p) { std::free(p); }

// ---------------------------------------------------------------------------
// Compatibility aliases. Pre-0.3 plugins import these names directly.
// ---------------------------------------------------------------------------
CF_EXPORT int cppfarmer_transpile(const char* src, const char* unlocks_csv,
                                  char** out, char** err) {
    return cf_transpile(nullptr, src, unlocks_csv, out, err);
}

CF_EXPORT int cppfarmer_transpile_ex(const char* src, const char* unlocks_csv,
                                     char** out, char** line_map_csv, char** err) {
    return cf_transpile_ex(nullptr, src, unlocks_csv, out, line_map_csv, err);
}

CF_EXPORT const char* cppfarmer_version(void) { return cf_version(); }

// 0.2.0 exported cppfarmer_free. Renaming everything to cf_* dropped it, so an
// older plugin would load fine, compile fine, and then throw
// EntryPointNotFoundException on its first release of a result buffer -- the
// aliases above only cover the compile calls.
CF_EXPORT void cppfarmer_free(char* p) { cf_free(p); }
