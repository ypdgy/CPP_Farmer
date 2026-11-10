// cppfarmer - compile a C++ subset into The Farmer Was Replaced script.
#include <cstdio>
#include <cstring>
#include <fstream>
#include <iostream>
#include <sstream>
#include <string>
#include <vector>

#include "ast.hpp"
#include "codegen.hpp"
#include "lexer.hpp"
#include "parser.hpp"

namespace {

std::vector<std::string> splitCsv(const std::string& s) {
    std::vector<std::string> out;
    std::string cur;
    for (char c : s) {
        if (c == ',' || c == ';' || c == ' ') {
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

std::string readFile(const std::string& path, bool& ok) {
    std::ifstream in(path, std::ios::binary);
    if (!in) {
        ok = false;
        return {};
    }
    std::ostringstream ss;
    ss << in.rdbuf();
    ok = true;
    return ss.str();
}

bool writeFile(const std::string& path, const std::string& data) {
    std::ofstream out(path, std::ios::binary | std::ios::trunc);
    if (!out) return false;
    out << data;
    return static_cast<bool>(out);
}

void usage() {
    std::cout <<
        "cppfarmer - C/C++ subset -> The Farmer Was Replaced script compiler\n"
        "\n"
        "usage: cppfarmer <input> [--lang cpp|c] [-o <out.py>] [--check] [--stdout]\n"
        "\n"
        "  --lang <id>    front-end dialect: cpp (default) or c\n"
        "  -o <file>      write generated script to <file> (default: <input>.py)\n"
        "  --line-hints   emit '# src:NN' comments before each function\n"
        "  --check        parse and type-check only, do not write output\n"
        "  --stdout       print the generated script instead of writing a file\n"
        "  --unlocks <csv>  comma separated unlock keys from the save; constructs\n"
        "                   that need a missing unlock are rejected up front\n"
        "  --print-features  list the tech-tree features the program requires\n"
        "  --line-map     print 'output line -> source line' pairs\n";
}

}  // namespace

int main(int argc, char** argv) {
    std::string input;
    std::string output;
    bool checkOnly = false;
    bool toStdout = false;
    bool printFeatures = false;
    bool printLineMap = false;
    std::vector<std::string> unlocks;
    bool hasUnlocks = false;
    bool langSet = false;
    cppfarmer::Options opt;

    for (int i = 1; i < argc; ++i) {
        std::string a = argv[i];
        if (a == "-h" || a == "--help") {
            usage();
            return 0;
        }
        if (a == "-o" && i + 1 < argc) {
            output = argv[++i];
            continue;
        }
        if (a == "--check") {
            checkOnly = true;
            continue;
        }
        if (a == "--stdout") {
            toStdout = true;
            continue;
        }
        if (a == "--line-hints") {
            opt.emitLineHints = true;
            continue;
        }
        if (a == "--print-features") {
            printFeatures = true;
            continue;
        }
        if (a == "--line-map") {
            printLineMap = true;
            continue;
        }
        if (a == "--lang" && i + 1 < argc) {
            std::string id = argv[++i];
            langSet = true;
            if (id == "c" || id == "C") {
                opt.dialect = cppfarmer::Dialect::C;
            } else if (id != "cpp" && id != "c++" && id != "C++") {
                std::cerr << "unknown language: " << id << " (cpp, c)" << std::endl;
                return 2;
            }
            continue;
        }
        if (a == "--unlocks" && i + 1 < argc) {
            unlocks = splitCsv(argv[++i]);
            hasUnlocks = true;
            continue;
        }
        if (!a.empty() && a[0] == '-') {
            std::cerr << "unknown option: " << a << "\n";
            return 2;
        }
        input = a;
    }

    if (input.empty()) {
        usage();
        return 2;
    }

    // Pick the dialect from the extension unless --lang said otherwise: the
    // regression scripts rely on this, and it matches what the plugin does.
    if (!langSet) {
        const std::string ext = ".c";
        if (input.size() > ext.size() &&
            input.compare(input.size() - ext.size(), ext.size(), ext) == 0) {
            opt.dialect = cppfarmer::Dialect::C;
        }
    }

    bool ok = false;
    std::string src = readFile(input, ok);
    if (!ok) {
        std::cerr << "error: cannot read " << input << "\n";
        return 1;
    }
    opt.sourceName = input;

    if (output.empty()) {
        std::string base = input;
        auto dot = base.find_last_of('.');
        auto sep = base.find_last_of("/\\");
        if (dot != std::string::npos && (sep == std::string::npos || dot > sep)) {
            base = base.substr(0, dot);
        }
        output = base + ".py";
    }

    try {
        cppfarmer::Lexer lexer(src);
        auto tokens = lexer.run();

        cppfarmer::Parser parser(std::move(tokens));
        cppfarmer::Program prog = parser.parseProgram();

        cppfarmer::CodeGen gen(opt);
        if (hasUnlocks) gen.setUnlocks(unlocks);
        std::string generated = gen.generate(prog);

        if (printLineMap) {
            const std::vector<int>& map = gen.lineMap();
            std::size_t line = 0;
            std::size_t at = 0;
            for (std::size_t i = 0; i < map.size(); ++i) {
                std::cout << (i + 1) << " -> " << map[i] << "\n";
            }
            (void)line;
            (void)at;
            if (checkOnly || toStdout) return 0;
        }

        if (printFeatures) {
            for (const auto& f : gen.requiredFeatures()) std::cout << f << "\n";
            if (checkOnly || toStdout) return 0;
        }

        if (checkOnly) {
            std::cout << "ok: " << prog.functions.size() << " function(s), "
                      << prog.structs.size() << " struct(s), " << prog.globals.size()
                      << " global(s), " << generated.size() << " bytes generated\n";
            return 0;
        }
        if (toStdout) {
            std::cout << generated;
            return 0;
        }
        if (!writeFile(output, generated)) {
            std::cerr << "error: cannot write " << output << "\n";
            return 1;
        }
        std::cout << "wrote " << output << " (" << generated.size() << " bytes)\n";
        return 0;
    } catch (const cppfarmer::LexError& e) {
        std::cerr << input << ":" << e.line << ":" << e.col << ": lex error: " << e.what()
                  << "\n";
        return 1;
    } catch (const cppfarmer::ParseError& e) {
        std::cerr << input << ":" << e.line << ":" << e.col << ": parse error: " << e.what()
                  << "\n";
        return 1;
    } catch (const cppfarmer::CompileError& e) {
        std::cerr << input << ":" << e.line << ": error: " << e.what() << "\n";
        return 1;
    } catch (const std::exception& e) {
        std::cerr << "error: " << e.what() << "\n";
        return 1;
    }
}
