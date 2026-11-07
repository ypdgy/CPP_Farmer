// Native entry points for the BepInEx plugin.
//
// The plugin P/Invokes these instead of re-implementing the compiler in C#,
// so the mod's actual logic stays in C++.
#include <cstdlib>
#include <cstring>
#include <string>
#include <vector>

#include "ast.hpp"
#include "codegen.hpp"
#include "lexer.hpp"
#include "parser.hpp"

#if defined(_WIN32)
#define CPPFARMER_EXPORT extern "C" __declspec(dllexport)
#else
#define CPPFARMER_EXPORT extern "C" __attribute__((visibility("default")))
#endif

namespace {

char* dupString(const std::string& s) {
    char* p = static_cast<char*>(std::malloc(s.size() + 1));
    if (!p) return nullptr;
    std::memcpy(p, s.c_str(), s.size() + 1);
    return p;
}

std::vector<std::string> splitCsv(const char* csv) {
    std::vector<std::string> out;
    if (!csv) return out;
    std::string cur;
    for (const char* p = csv; *p; ++p) {
        char c = *p;
        if (c == ',' || c == ';' || c == ' ' || c == '\t') {
            if (!cur.empty()) {
                out.push_back(cur);
                cur.clear();
            }
            continue;
        }
        cur.push_back(c);
    }
    if (!cur.empty()) out.push_back(cur);
    return out;
}

// Compiles `src`. On success returns 0 and writes the generated script to *out.
// On failure returns a non-zero code and writes "file:line:col: message" to *err.
int compile(const char* src, const char* unlocksCsv, char** out, char** err,
            char** lineMapCsv = nullptr) {
    if (!src) return 9;
    try {
        cppfarmer::Lexer lexer(src);
        auto tokens = lexer.run();

        cppfarmer::Parser parser(std::move(tokens));
        cppfarmer::Program prog = parser.parseProgram();

        cppfarmer::Options opt;
        opt.emitLineHints = false;
        cppfarmer::CodeGen gen(opt);
        std::vector<std::string> unlocks = splitCsv(unlocksCsv);
        if (!unlocks.empty()) gen.setUnlocks(unlocks);

        std::string generated = gen.generate(prog);
        if (out) *out = dupString(generated);

        if (lineMapCsv) {
            // One entry per output line: the source line it came from (0 when
            // the line is compiler-generated, e.g. the runtime prelude).
            std::string csv;
            const std::vector<int>& map = gen.lineMap();
            csv.reserve(map.size() * 3);
            for (std::size_t i = 0; i < map.size(); ++i) {
                if (i) csv.push_back(',');
                csv += std::to_string(map[i]);
            }
            *lineMapCsv = dupString(csv);
        }

        if (err) *err = nullptr;
        return 0;
    } catch (const cppfarmer::LexError& e) {
        if (err) *err = dupString("1:" + std::to_string(e.line) + ":" +
                                  std::to_string(e.col) + ": lex error: " + e.what());
        return 1;
    } catch (const cppfarmer::ParseError& e) {
        if (err) *err = dupString("1:" + std::to_string(e.line) + ":" +
                                  std::to_string(e.col) + ": parse error: " + e.what());
        return 2;
    } catch (const cppfarmer::CompileError& e) {
        if (err) *err = dupString("1:" + std::to_string(e.line) + ":0: " + e.what());
        return 3;
    } catch (const std::exception& e) {
        if (err) *err = dupString(std::string("1:0:0: internal error: ") + e.what());
        return 9;
    }
}

}  // namespace

CPPFARMER_EXPORT int cppfarmer_transpile(const char* src, const char* unlocksCsv, char** out,
                               char** err) {
    return compile(src, unlocksCsv, out, err);
}

CPPFARMER_EXPORT int cppfarmer_transpile_ex(const char* src, const char* unlocksCsv, char** out,
                                  char** lineMapCsv, char** err) {
    return compile(src, unlocksCsv, out, err, lineMapCsv);
}

CPPFARMER_EXPORT void cppfarmer_free(char* p) { std::free(p); }

CPPFARMER_EXPORT const char* cppfarmer_version() { return "cppfarmer 0.2"; }
