// Java subset front-end: entry point used by the registry.
//
// Shares the C++ front-end's lexer (the token set Java needs is a subset of what
// it produces) and AST, but uses its own parser and code generator. The plugin
// P/Invokes the language-neutral cf_* API, so this file only has to convert the
// shared data structures into that ABI -- the same shape as cpp_frontend.cpp.
#include <cstdlib>
#include <exception>
#include <string>
#include <vector>

#include "ast.hpp"
#include "cf_api.h"
#include "cf_util.h"
#include "java_codegen.hpp"
#include "java_frontend.h"
#include "java_parser.hpp"
#include "lexer.hpp"

namespace {

// Compiles `src`. On success returns 0 and writes the generated script to *out.
// On failure returns a non-zero code and writes "file:line:col: message" to *err.
// Parameter order must match cf_compile_fn exactly: (src, unlocks, out, line_map,
// err). Swapping line_map and err would compile fine but return the line table as
// an error string, which shows up as "every breakpoint is wrong".
int compileJava(const char* src, const char* unlocksCsv, char** out,
                char** lineMapCsv, char** err) {
    if (!src) return 9;
    try {
        cppfarmer::Lexer lexer(src);
        auto tokens = lexer.run();

        jfamer::JavaParser parser(std::move(tokens));
        cppfarmer::Program prog = parser.parseProgram();

        jfamer::Options opt;
        opt.emitLineHints = false;
        jfamer::JavaCodeGen gen(opt);
        std::vector<std::string> unlocks = cf_split_csv(unlocksCsv);
        if (!unlocks.empty()) gen.setUnlocks(unlocks);

        std::string generated = gen.generate(prog);
        if (out) *out = cf_dup(generated);

        if (lineMapCsv) {
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
    } catch (const jfamer::ParseError& e) {
        if (err) *err = cf_dup("1:" + std::to_string(e.line) + ":" +
                               std::to_string(e.col) + ": parse error: " + e.what());
        return 2;
    } catch (const jfamer::CompileError& e) {
        if (err) *err = cf_dup("1:" + std::to_string(e.line) + ":0: " + e.what());
        return 3;
    } catch (const std::exception& e) {
        if (err) *err = cf_dup(std::string("1:0:0: internal error: ") + e.what());
        return 9;
    }
}

}  // namespace

const CfLanguage& cf_java_language() {
    static const CfLanguage language = {"java", "Java", ".java", &compileJava};
    return language;
}
