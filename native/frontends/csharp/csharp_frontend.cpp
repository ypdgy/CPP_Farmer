// C# subset front-end: entry point used by the registry.
//
// Shares the C++ front-end's lexer and AST, but uses its own parser and code
// generator. The plugin P/Invokes the language-neutral cf_* API, so this file
// only converts the shared data structures into that ABI.
#include <cstdlib>
#include <exception>
#include <string>
#include <vector>

#include "ast.hpp"
#include "cf_api.h"
#include "cf_util.h"
#include "csharp_codegen.hpp"
#include "csharp_frontend.h"
#include "csharp_parser.hpp"
#include "lexer.hpp"

namespace {

// Parameter order must match cf_compile_fn exactly: (src, unlocks, out,
// line_map, err). Swapping line_map and err compiles fine but returns the line
// table as an error string, which shows up as "every breakpoint is wrong".
int compileCSharp(const char* src, const char* unlocksCsv, char** out,
                  char** lineMapCsv, char** err) {
    if (!src) return 9;
    try {
        cppfarmer::Lexer lexer(src);
        auto tokens = lexer.run();

        csfarmer::CSharpParser parser(std::move(tokens));
        cppfarmer::Program prog = parser.parseProgram();

        csfarmer::Options opt;
        opt.emitLineHints = false;
        csfarmer::CSharpCodeGen gen(opt);
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
    } catch (const csfarmer::ParseError& e) {
        if (err) *err = cf_dup("1:" + std::to_string(e.line) + ":" +
                               std::to_string(e.col) + ": parse error: " + e.what());
        return 2;
    } catch (const csfarmer::CompileError& e) {
        if (err) *err = cf_dup("1:" + std::to_string(e.line) + ":0: " + e.what());
        return 3;
    } catch (const std::exception& e) {
        if (err) *err = cf_dup(std::string("1:0:0: internal error: ") + e.what());
        return 9;
    }
}

}  // namespace

const CfLanguage& cf_csharp_language() {
    static const CfLanguage language = {"csharp", "C#", ".cs", &compileCSharp};
    return language;
}
