// C++ subset front-end: entry point used by the registry.
//
// The plugin P/Invokes the language-neutral cf_* API instead of re-implementing
// the compiler in C#, so the mod's actual logic stays in C++.
#include <cstdlib>
#include <exception>
#include <string>
#include <vector>

#include "ast.hpp"
#include "cf_api.h"
#include "cf_util.h"
#include "codegen.hpp"
#include "cpp_frontend.h"
#include "lexer.hpp"
#include "parser.hpp"

namespace {

using cppfarmer::Dialect;

// Compiles `src`. On success returns 0 and writes the generated script to *out.
// On failure returns a non-zero code and writes "file:line:col: message" to *err.
// Parameter order must match cf_compile_fn exactly.
int compileWith(Dialect dialect, const char* src, const char* unlocksCsv, char** out,
                char** lineMapCsv, char** err) {
    if (!src) return 9;
    try {
        cppfarmer::Lexer lexer(src);
        auto tokens = lexer.run();

        cppfarmer::Parser parser(std::move(tokens));
        cppfarmer::Program prog = parser.parseProgram();

        cppfarmer::Options opt;
        opt.emitLineHints = false;
        opt.dialect = dialect;
        cppfarmer::CodeGen gen(opt);
        std::vector<std::string> unlocks = cf_split_csv(unlocksCsv);
        if (!unlocks.empty()) gen.setUnlocks(unlocks);

        std::string generated = gen.generate(prog);
        if (out) *out = cf_dup(generated);

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
            *lineMapCsv = cf_dup(csv);
        }

        if (err) *err = nullptr;
        return 0;
    } catch (const cppfarmer::LexError& e) {
        if (err) *err = cf_dup("1:" + std::to_string(e.line) + ":" +
                               std::to_string(e.col) + ": lex error: " + e.what());
        return 1;
    } catch (const cppfarmer::ParseError& e) {
        if (err) *err = cf_dup("1:" + std::to_string(e.line) + ":" +
                               std::to_string(e.col) + ": parse error: " + e.what());
        return 2;
    } catch (const cppfarmer::CompileError& e) {
        if (err) *err = cf_dup("1:" + std::to_string(e.line) + ":0: " + e.what());
        return 3;
    } catch (const std::exception& e) {
        if (err) *err = cf_dup(std::string("1:0:0: internal error: ") + e.what());
        return 9;
    }
}

int compileCpp(const char* src, const char* unlocksCsv, char** out,
               char** lineMapCsv, char** err) {
    return compileWith(Dialect::Cpp, src, unlocksCsv, out, lineMapCsv, err);
}

int compileC(const char* src, const char* unlocksCsv, char** out,
             char** lineMapCsv, char** err) {
    return compileWith(Dialect::C, src, unlocksCsv, out, lineMapCsv, err);
}

}  // namespace

const CfLanguage& cf_cpp_language() {
    // Extensions are advertised to the plugin for logging and conflict checks;
    // the plugin's own LanguageProfile decides which windows it claims.
    static const CfLanguage language = {"cpp", "C++", ".cpp;.cxx;.cc;.c++;.hpp", &compileCpp};
    return language;
}

const CfLanguage& cf_c_language() {
    // C shares the parser with C++: the subset it needs is a subset of what the
    // C++ front-end already accepts. The dialect flag only changes printf.
    static const CfLanguage language = {"c", "C", ".c", &compileC};
    return language;
}
