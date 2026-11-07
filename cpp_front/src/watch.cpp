// cppfarmer-watch - watch a folder of .cpp files and transpile them into a target
// folder (typically the game's save directory) whenever they change.
//
// The output file keeps the source's base name with a .py extension, so the
// game's File Watcher picks it up and the script appears in the code window.
#include <cctype>
#include <chrono>
#include <cstdio>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <map>
#include <sstream>
#include <string>
#include <thread>
#include <vector>

#include "ast.hpp"
#include "codegen.hpp"
#include "lexer.hpp"
#include "parser.hpp"

namespace fs = std::filesystem;

namespace {

struct SourceState {
    fs::file_time_type mtime{};
    bool ok = false;
    bool seen = false;
};

std::string readFile(const fs::path& p, bool& ok) {
    std::ifstream in(p, std::ios::binary);
    if (!in) {
        ok = false;
        return {};
    }
    std::ostringstream ss;
    ss << in.rdbuf();
    ok = true;
    return ss.str();
}

// Write via a temporary file and rename, mirroring how the game itself saves,
// so the watcher never observes a half-written script.
bool writeAtomic(const fs::path& target, const std::string& data) {
    fs::path tmp = target;
    tmp += ".tmp";
    {
        std::ofstream out(tmp, std::ios::binary | std::ios::trunc);
        if (!out) return false;
        out << data;
        if (!out) return false;
    }
    std::error_code ec;
    fs::rename(tmp, target, ec);
    if (ec) {
        fs::remove(tmp, ec);
        return false;
    }
    return true;
}

bool isSourceFile(const fs::path& p) {
    std::string ext = p.extension().string();
    for (auto& c : ext) c = static_cast<char>(std::tolower(static_cast<unsigned char>(c)));
    return ext == ".cpp" || ext == ".cc" || ext == ".cxx" || ext == ".c++";
}

std::string stemOf(const fs::path& p) { return p.stem().string(); }

bool compileOne(const fs::path& src, const fs::path& outDir, bool lineHints) {
    bool ok = false;
    std::string text = readFile(src, ok);
    if (!ok) {
        std::cerr << "  ! cannot read " << src.string() << "\n";
        return false;
    }

    cppfarmer::Options opt;
    opt.sourceName = src.filename().string();
    opt.emitLineHints = lineHints;

    try {
        cppfarmer::Lexer lexer(text);
        auto tokens = lexer.run();
        cppfarmer::Parser parser(std::move(tokens));
        cppfarmer::Program prog = parser.parseProgram();
        cppfarmer::CodeGen gen(opt);
        std::string generated = gen.generate(prog);

        fs::path out = outDir / (stemOf(src) + ".py");
        if (!writeAtomic(out, generated)) {
            std::cerr << "  ! cannot write " << out.string() << "\n";
            return false;
        }
        std::cout << "  + " << src.filename().string() << " -> " << out.filename().string()
                  << " (" << generated.size() << " bytes)\n";
        return true;
    } catch (const cppfarmer::LexError& e) {
        std::cerr << "  ! " << src.filename().string() << ":" << e.line << ":" << e.col
                  << ": " << e.what() << "\n";
    } catch (const cppfarmer::ParseError& e) {
        std::cerr << "  ! " << src.filename().string() << ":" << e.line << ":" << e.col
                  << ": " << e.what() << "\n";
    } catch (const cppfarmer::CompileError& e) {
        std::cerr << "  ! " << src.filename().string() << ":" << e.line << ": " << e.what()
                  << "\n";
    } catch (const std::exception& e) {
        std::cerr << "  ! " << src.filename().string() << ": " << e.what() << "\n";
    }
    return false;
}

void usage() {
    std::cout
        << "cppfarmer-watch - keep .cpp sources transpiled into a target folder\n"
        << "\n"
        << "usage: cppfarmer-watch <srcDir> <outDir> [options]\n"
        << "\n"
        << "  --interval <ms>   poll interval, default 400\n"
        << "  --line-hints      emit '# fn name' comments in the output\n"
        << "  --once            compile everything once and exit\n"
        << "\n"
        << "Enable 'file watcher' in the game options so the generated scripts\n"
        << "reload without restarting the save.\n";
}

}  // namespace

int main(int argc, char** argv) {
    std::vector<std::string> positional;
    int intervalMs = 400;
    bool lineHints = false;
    bool once = false;

    for (int i = 1; i < argc; ++i) {
        std::string a = argv[i];
        if (a == "-h" || a == "--help") {
            usage();
            return 0;
        }
        if (a == "--interval" && i + 1 < argc) {
            intervalMs = std::stoi(argv[++i]);
            continue;
        }
        if (a == "--line-hints") {
            lineHints = true;
            continue;
        }
        if (a == "--once") {
            once = true;
            continue;
        }
        positional.push_back(a);
    }

    if (positional.size() < 2) {
        usage();
        return 2;
    }

    fs::path srcDir = positional[0];
    fs::path outDir = positional[1];

    std::error_code ec;
    if (!fs::exists(srcDir, ec)) {
        std::cerr << "error: source directory does not exist: " << srcDir.string() << "\n";
        return 1;
    }
    fs::create_directories(outDir, ec);

    std::cout << "cppfarmer-watch: " << srcDir.string() << " -> " << outDir.string() << "\n";

    std::map<std::string, SourceState> state;

    auto scan = [&](bool force) {
        std::error_code err;
        for (const auto& entry : fs::directory_iterator(srcDir, err)) {
            if (!entry.is_regular_file()) continue;
            if (!isSourceFile(entry.path())) continue;

            const std::string key = entry.path().filename().string();
            auto mt = fs::last_write_time(entry.path(), err);
            if (err) continue;

            auto it = state.find(key);
            if (!force && it != state.end() && it->second.seen && it->second.mtime == mt) {
                continue;
            }
            SourceState st;
            st.mtime = mt;
            st.seen = true;
            st.ok = compileOne(entry.path(), outDir, lineHints);
            state[key] = st;
        }
    };

    scan(true);
    if (once) return 0;

    std::cout << "watching for changes (Ctrl+C to stop)\n";
    for (;;) {
        std::this_thread::sleep_for(std::chrono::milliseconds(intervalMs));
        scan(false);
    }
}
