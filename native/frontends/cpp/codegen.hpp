// Code generator: lowers the C++ subset AST to The Farmer Was Replaced script.
//
// The same generator serves both registered languages. `Dialect::C` only
// changes the few places where C and C++ genuinely differ in this subset
// (currently: printf with format specifiers), so the parser and the AST stay
// shared. Anything C accepts but C++ does not is a superset, which is harmless:
// a .c file may still use `std::vector` if the player wants to.
#pragma once

#include <algorithm>
#include <cctype>
#include <cstdio>
#include <cstring>
#include <map>
#include <memory>
#include <optional>
#include <set>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>

#include "ast.hpp"

namespace cppfarmer {

enum class Dialect { Cpp, C };

class CompileError : public std::runtime_error {
public:
    CompileError(const std::string& msg, int line)
        : std::runtime_error(msg), line(line) {}
    int line;
};

struct Options {
    bool emitLineHints = false;   // write "# src:Lnn" comments for debugging
    std::string sourceName = "main.cpp";
    Dialect dialect = Dialect::Cpp;
};

class CodeGen {
public:
    explicit CodeGen(Options opt) : opt_(std::move(opt)) {}

    std::string generate(const Program& prog) {
        declare(prog);
        scopes_.clear();
        pushScope();  // scope 0 holds globals for the whole program

        hasImports_ = !prog.imports.empty();

        // Imports come first so that names from included files are in scope for
        // every definition below.
        for (const auto& imp : prog.imports) {
            if (imp) genStmt(*imp, 0);
        }

        // Structs become constructor functions returning dicts. They must be
        // emitted before any code that calls them.
        for (const auto& sd : prog.structs) genStruct(sd);
        for (const auto& ed : prog.enums) genEnum(ed);
        for (const auto& gv : prog.globals) genGlobal(gv.decl);
        // The entry point becomes top-level statements whenever that cannot change
        // behaviour. A `def` -- any function definition -- needs the game's
        // 'functions' unlock, which a fresh save has not researched yet, and
        // wrapping main in one would make the language unusable at the start of a
        // run. Every other function still needs that unlock, because there is no
        // other way to reuse code in the target language.
        const FuncDecl* entry = nullptr;
        for (const auto& fn : prog.functions) {
            if (!prog.entry.empty() && fn.name == prog.entry) {
                entry = &fn;
                continue;
            }
            genFunction(fn);
        }

        if (entry != nullptr && canHoistEntry(*entry)) {
            genEntryBody(*entry);
        } else {
            if (entry != nullptr) genFunction(*entry);
            if (!prog.entry.empty()) {
                // Compiler-generated entry call: it has no source line of its own,
                // and leaving curLine_ pointing at the last function body would
                // make the line table claim it came from there -- which misleads
                // breakpoint mapping into snapping a breakpoint onto that
                // function's last line.
                curLine_ = 0;
                line(0, prog.entry + "()");
            }
        }

        std::string body = join(lines_);
        if (!body.empty() && body.back() != '\n') body += '\n';

        std::string prelude = buildPrelude();
        if (!prelude.empty()) prelude += "\n";

        int preludeLines = 0;
        for (char c : prelude) {
            if (c == '\n') ++preludeLines;
        }

        finalLineMap_.assign(static_cast<std::size_t>(preludeLines), 0);
        finalLineMap_.insert(finalLineMap_.end(), lineMap_.begin(), lineMap_.end());

        return prelude + body;
    }

private:
    struct VarInfo {
        std::string asmName;
        Type type;
    };
    struct Scope {
        std::map<std::string, VarInfo> vars;
    };

    Options opt_;
    std::vector<std::string> lines_;
    // Source line for each emitted output line. The game keys breakpoints by
    // character offset into the editor text, so the plugin needs this table to
    // translate a C++ line into an offset inside the generated script.
    std::vector<int> lineMap_;
    bool hasImports_ = false;
    int curLine_ = 0;
    // True while main's body is being emitted at the top level, where a `return`
    // statement would be a syntax error in the generated script.
    bool suppressingReturn_ = false;
    // 1 while emitting a function body, including a hoisted main. 0 means "global
    // scope", where no statement-level construct may be emitted.
    int functionDepth_ = 0;

    std::map<std::string, const StructDecl*> structs_;
    std::map<std::string, std::string> enumMemberToQualified_;
    std::map<std::string, Type> functionReturns_;

    std::vector<Scope> scopes_;
    std::set<std::string> usedHelpers_;

    // tech-tree gating state
    std::set<std::string> unlocked_;
    std::set<std::string> required_;
    bool checkUnlocks_ = false;
    std::vector<int> finalLineMap_;

public:
    // Features this program needs from the tech tree, filled during generation.
    const std::set<std::string>& requiredFeatures() const { return required_; }

    // Output line -> source line (0 when the line is compiler-generated).
    const std::vector<int>& lineMap() const { return finalLineMap_; }

    void setUnlocks(const std::vector<std::string>& unlocks) {
        unlocked_.clear();
        for (const auto& u : unlocks) unlocked_.insert(u);
        checkUnlocks_ = true;
    }

private:

    // ------------------------------------------------------------------
    // output helpers
    // ------------------------------------------------------------------
    void line(int indent, const std::string& text) {
        std::string s;
        for (int i = 0; i < indent; ++i) s += '\t';
        lines_.push_back(s + text);
        lineMap_.push_back(curLine_);
    }

    static std::string join(const std::vector<std::string>& v) {
        std::string s;
        for (std::size_t i = 0; i < v.size(); ++i) {
            s += v[i];
            if (i + 1 < v.size()) s += '\n';
        }
        return s;
    }

    [[noreturn]] void err(const std::string& msg, int ln) { throw CompileError(msg, ln); }

    // ------------------------------------------------------------------
    // tech tree gating
    // ------------------------------------------------------------------
    // The game gates language constructs behind unlocks; `Farm.unlockedKeyWords`
    // is exposed through MainSim.GetUnlockedKeywords(). When the caller supplies
    // that set we refuse to emit code the player cannot run yet, and say which
    // unlock is missing instead of letting the game throw a bare parse error.
    // The game shows this text inside its own editor, so name the unlock the way
    // the tech tree does instead of echoing the internal key.
    static std::string unlockLabel(const std::string& key) {
        if (key == "functions") return "函数定义";
        if (key == "dictionaries") return "字典";
        if (key == "lists") return "列表";
        if (key == "import") return "导入其它文件";
        if (key == "loops") return "循环";
        if (key == "variables") return "变量";
        if (key == "operators") return "运算符";
        return key;
    }

    void requireUnlock(const std::string& key, int line) {
        required_.insert(key);
        if (!checkUnlocks_) return;
        if (unlocked_.count(key)) return;

        std::string msg = "这个写法需要先解锁「" + unlockLabel(key)
            + "」，当前存档还没解锁";
        if (key == "functions") {
            msg += "。main() 会作为顶层代码执行，不需要解锁；"
                   "只有自定义函数、struct / class 才用到这个科技";
        }
        err(msg, line);
    }

    // ------------------------------------------------------------------
    // declarations pass
    // ------------------------------------------------------------------
    void declare(const Program& prog) {
        for (const auto& sd : prog.structs) structs_[sd.name] = &sd;
        for (const auto& ed : prog.enums) {
            for (const auto& m : ed.members) {
                enumMemberToQualified_[m.first] = ed.name + "_" + m.first;
            }
        }
        for (const auto& fn : prog.functions) {
            functionReturns_[fn.name] = fn.returnType;
        }
    }

    // ------------------------------------------------------------------
    // scopes
    // ------------------------------------------------------------------
    void pushScope() { scopes_.emplace_back(); }
    void popScope() { scopes_.pop_back(); }

    const VarInfo* lookup(const std::string& name) const {
        for (auto it = scopes_.rbegin(); it != scopes_.rend(); ++it) {
            auto f = it->vars.find(name);
            if (f != it->vars.end()) return &f->second;
        }
        return nullptr;
    }

    std::string asmOf(const std::string& name) const {
        const VarInfo* v = lookup(name);
        return v ? v->asmName : name;
    }

    Type typeOf(const std::string& name) const {
        const VarInfo* v = lookup(name);
        if (v) return v->type;
        return Type::of(BaseType::Unknown);
    }

    // Declares a variable, renaming on shadowing so C++ block scope semantics
    // survive in a function-scoped target language.
    std::string declareVar(const std::string& name, const Type& t) {
        std::string asmName = name;
        if (lookup(name) != nullptr) {
            int n = 1;
            for (;;) {
                std::string candidate = name + "_" + std::to_string(n);
                if (lookup(candidate) == nullptr && !nameTaken(candidate)) {
                    asmName = candidate;
                    break;
                }
                ++n;
            }
            // Shadowing: hide the outer binding in this scope.
            scopes_.back().vars[name] = VarInfo{asmName, t};
            scopes_.back().vars[asmName] = VarInfo{asmName, t};
        } else {
            scopes_.back().vars[name] = VarInfo{asmName, t};
        }
        return asmName;
    }

    bool nameTaken(const std::string& n) const {
        for (const auto& s : scopes_) {
            if (s.vars.count(n)) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    // struct / enum / global / function emission
    // ------------------------------------------------------------------
    static std::string zeroFor(const Type& t) {
        if (t.isVector) return "[]";
        if (t.isMap) return "{}";
        if (t.isStruct()) return t.userType + "()";
        switch (t.base) {
            case BaseType::Int:
            case BaseType::Float:
            case BaseType::Char: return "0";
            case BaseType::Bool: return "False";
            case BaseType::String: return "\"\"";
            default: return "None";
        }
    }

    void genStruct(const StructDecl& sd) {
        requireUnlock("dictionaries", 0);
        curLine_ = 0;   // synthesised constructor: no single source line
        std::string params, dict;
        for (std::size_t i = 0; i < sd.fields.size(); ++i) {
            const auto& f = sd.fields[i];
            if (i) {
                params += ", ";
                dict += ", ";
            }
            params += f.name + " = " + zeroFor(f.type);
            dict += "\"" + f.name + "\": " + f.name;
        }
        if (opt_.emitLineHints) line(0, "# struct " + sd.name);
        line(0, "def " + sd.name + "(" + params + "):");
        line(1, "return {" + dict + "}");
        line(0, "");
    }

    void genEnum(const EnumDecl& ed) {
        curLine_ = 0;   // synthesised constants: no single source line
        if (opt_.emitLineHints) line(0, "# enum " + ed.name);
        long long next = 0;
        for (const auto& m : ed.members) {
            std::string value;
            if (m.second) {
                value = expr(*m.second);
            } else {
                value = std::to_string(next);
            }
            line(0, ed.name + "_" + m.first + " = " + value);
            ++next;
        }
        line(0, "");
    }

    void genGlobal(const VarDecl& d) {
        // Globals live in scope 0 and stay visible to every function body.
        Type t = d.type;
        if (d.init && t.base == BaseType::Unknown && !t.isVector && !t.isMap &&
            !t.isStruct()) {
            t = infer(*d.init);
        }
        std::string target = declareVar(d.name, t);
        if (d.arraySize) {
            requireUnlock("lists", 0);
            usedHelpers_.insert("list");
            std::string fill = zeroFor(Type::of(d.type.base));
            line(0, target + " = cpp_list(" + expr(*d.arraySize) + ", " + fill + ")");
        } else if (d.init) {
            emitAssign(target, t, *d.init);
        } else {
            line(0, target + " = " + zeroFor(t));
        }
    }

    // ------------------------------------------------------------------
    // entry point
    // ------------------------------------------------------------------
    // A return inside an if/loop cannot be hoisted: the target language only
    // allows `return` inside a function, so only statements directly in main's
    // body may carry one.
    static bool hasNestedReturn(const Stmt& s, bool allowReturn) {
        if (s.kind == StmtKind::Return) return !allowReturn;
        // A block passes its own flag down: main's body is a Block, and its
        // direct statements are exactly the ones that may carry a return.
        for (const auto& child : s.body) {
            if (child && hasNestedReturn(*child, allowReturn)) return true;
        }
        if (s.thenBranch && hasNestedReturn(*s.thenBranch, false)) return true;
        if (s.elseBranch && hasNestedReturn(*s.elseBranch, false)) return true;
        if (s.init && hasNestedReturn(*s.init, false)) return true;
        return false;
    }

    // Dropping a return is only safe when nothing follows it, otherwise the
    // statements after it would suddenly run.
    static bool trailingReturnsOnly(const Stmt& block) {
        bool sawReturn = false;
        for (const auto& child : block.body) {
            if (!child) continue;
            if (child->kind == StmtKind::Return) {
                sawReturn = true;
                continue;
            }
            if (sawReturn) return false;
        }
        return true;
    }

    static bool canHoistEntry(const FuncDecl& fn) {
        if (!fn.body) return false;
        if (!fn.params.empty()) return false;   // int main(int argc, char** argv)
        if (hasNestedReturn(*fn.body, true)) return false;
        if (!trailingReturnsOnly(*fn.body)) return false;
        return true;
    }

    void genEntryBody(const FuncDecl& fn) {
        curLine_ = fn.line;
        // The body is emitted at column 0, but it is still "inside main": anything
        // that needs to know whether statements are allowed (ternary lowering,
        // for instance) must not look at the indentation.
        ++functionDepth_;
        pushScope();
        suppressingReturn_ = true;
        bool empty = genBody(*fn.body, 0);
        suppressingReturn_ = false;
        popScope();
        --functionDepth_;
        if (empty) line(0, "# empty entry point");
    }

    void genFunction(const FuncDecl& fn) {
        requireUnlock("functions", fn.line);
        curLine_ = fn.line;
        ++functionDepth_;
        pushScope();
        std::string params;
        for (std::size_t i = 0; i < fn.params.size(); ++i) {
            const Param& p = fn.params[i];
            std::string asmName = declareVar(p.name, p.type);
            if (i) params += ", ";
            params += asmName;
            if (p.defaultValue) params += " = " + expr(*p.defaultValue);
        }
        if (opt_.emitLineHints) line(0, "# fn " + fn.name);
        line(0, "def " + fn.name + "(" + params + "):");
        scopeDepthGuard_ = 0;
        bool empty = genBody(*fn.body, 1);
        if (empty) line(1, "pass");
        line(0, "");
        popScope();
        --functionDepth_;
    }

    int scopeDepthGuard_ = 0;

    // ------------------------------------------------------------------
    // statements
    // ------------------------------------------------------------------
    // Returns true if nothing was emitted (caller must emit `pass`).
    bool genBody(const Stmt& body, int indent) {
        std::size_t before = lines_.size();
        if (body.kind == StmtKind::Block) {
            pushScope();
            for (const auto& s : body.body) {
                if (s) genStmt(*s, indent);
            }
            popScope();
        } else {
            genStmt(body, indent);
        }
        return lines_.size() == before;
    }

    void genStmt(const Stmt& s, int indent) {
        if (s.line > 0) curLine_ = s.line;
        switch (s.kind) {
            case StmtKind::Empty:
                return;
            case StmtKind::Include: {
                // The target language is imported, not textually included. Each
                // .cpp file is compiled on its own and gets its own CodeWindow, so
                // every node inside it carries that window -- which is what lets
                // the execution highlight and breakpoints jump across files.
                std::string module = moduleNameOf(s.sval);
                if (module.empty()) {
                    err("不能从 #include \"" + s.sval + "\" 推导模块名："
                        "文件名必须是合法标识符（字母或下划线开头，只含字母、数字、下划线）",
                        s.line);
                }
                requireUnlock("import", s.line);
                line(indent, "from " + module + " import *");
                return;
            }
            case StmtKind::Block: {
                pushScope();
                for (const auto& c : s.body) {
                    if (c) genStmt(*c, indent);
                }
                popScope();
                return;
            }
            case StmtKind::VarDecl:
                genVarDeclStmt(s.decl, indent);
                return;
            case StmtKind::ExprStmt: {
                if (!s.value) return;
                genExprStmt(*s.value, indent);
                return;
            }
            case StmtKind::If:
                genIf(s, indent);
                return;
            case StmtKind::While:
                requireUnlock("loops", s.line);
                line(indent, "while " + cond(*s.cond) + ":");
                emitLoopBody(*s.thenBranch, indent + 1);
                return;
            case StmtKind::For:
                genFor(s, indent);
                return;
            case StmtKind::RangeFor: {
                requireUnlock("loops", s.line);
                pushScope();
                declareVar(s.loopVar, Type::of(BaseType::Unknown));
                line(indent, "for " + asmOf(s.loopVar) + " in " + expr(*s.rangeExpr) + ":");
                emitLoopBody(*s.thenBranch, indent + 1);
                popScope();
                return;
            }
            case StmtKind::Return:
                // The entry point is hoisted to the top level so the script does
                // not need a `def` (function definitions require the game's
                // 'functions' unlock, which a fresh save does not have). A
                // top-level `return` has no meaning there and is dropped.
                if (suppressingReturn_) return;
                if (s.value) {
                    line(indent, "return " + expr(*s.value));
                } else {
                    line(indent, "return");
                }
                return;
            case StmtKind::Break:
                line(indent, "break");
                return;
            case StmtKind::Continue:
                if (!continueSuffix_.empty()) line(indent, continueSuffix_.back());
                line(indent, "continue");
                return;
        }
    }

    // Suffix statements that must run before `continue` when a C-style for loop
    // is lowered to a while loop.
    std::vector<std::string> continueSuffix_;

    void emitLoopBody(const Stmt& body, int indent) {
        bool empty = genBody(body, indent);
        if (empty) line(indent, "pass");
    }

    void genVarDeclStmt(const VarDecl& d, int indent) {
        Type t = d.type;
        if (d.init && t.base == BaseType::Unknown && !t.isVector && !t.isMap &&
            !t.isStruct()) {
            t = infer(*d.init);
        }
        std::string target = declareVar(d.name, t);

        if (d.arraySize) {
            // The target language has no documented `[x] * n` repetition, so
            // build fixed arrays through a helper loop instead.
            requireUnlock("lists", 0);
            usedHelpers_.insert("list");
            line(indent, target + " = cpp_list(" + expr(*d.arraySize) + ", " +
                                    zeroFor(Type::of(t.base)) + ")");
            return;
        }
        if (!d.init) {
            line(indent, target + " = " + zeroFor(t));
            return;
        }
        emitAssign(target, t, *d.init, indent);
    }

    // Handles initialisation, lowering ternary / initialiser lists.
    void emitAssign(const std::string& target, const Type& t, const Expr& init,
                    int indent = 0) {
        if (init.kind == ExprKind::Ternary) {
            // A ternary lowers to if/else, which is legal at the top level of the
            // generated script too -- but only once we are emitting statements.
            // A global initialiser runs before any statement, so it stays refused.
            if (functionDepth_ == 0) {
                err("conditional expression outside a function is not supported", init.line);
            }
            // Every branch assigns, so no zero-initialisation is needed first.
            emitTernaryAssign(target, init, indent);
            return;
        }
        if (init.kind == ExprKind::InitList && t.isStruct()) {
            std::string args;
            for (std::size_t i = 0; i < init.args.size(); ++i) {
                if (i) args += ", ";
                args += expr(*init.args[i]);
            }
            line(indent, target + " = " + t.userType + "(" + args + ")");
            return;
        }
        if (init.kind == ExprKind::InitList) {
            std::string args;
            for (std::size_t i = 0; i < init.args.size(); ++i) {
                if (i) args += ", ";
                args += expr(*init.args[i]);
            }
            line(indent, target + " = [" + args + "]");
            return;
        }
        line(indent, target + " = " + expr(init));
    }

    void emitTernaryAssign(const std::string& target, const Expr& t, int indent) {
        line(indent, "if " + cond(*t.cond) + ":");
        emitTernaryBranch(target, *t.lhs, indent + 1);
        if (t.rhs) {
            if (t.rhs->kind == ExprKind::Ternary) {
                line(indent, "else:");
                emitTernaryNested(target, *t.rhs, indent + 1);
            } else {
                line(indent, "else:");
                line(indent + 1, target + " = " + expr(*t.rhs));
            }
        }
    }

    void emitTernaryBranch(const std::string& target, const Expr& v, int indent) {
        if (v.kind == ExprKind::Ternary) {
            emitTernaryNested(target, v, indent);
            return;
        }
        line(indent, target + " = " + expr(v));
    }

    void emitTernaryNested(const std::string& target, const Expr& t, int indent) {
        line(indent, "if " + cond(*t.cond) + ":");
        emitTernaryBranch(target, *t.lhs, indent + 1);
        if (t.rhs) {
            line(indent, "else:");
            emitTernaryBranch(target, *t.rhs, indent + 1);
        }
    }

    void genIf(const Stmt& s, int indent) {
        line(indent, "if " + cond(*s.cond) + ":");
        emitLoopBody(*s.thenBranch, indent + 1);
        const Stmt* e = s.elseBranch.get();
        while (e) {
            if (e->kind == StmtKind::If) {
                line(indent, "elif " + cond(*e->cond) + ":");
                emitLoopBody(*e->thenBranch, indent + 1);
                e = e->elseBranch.get();
                continue;
            }
            line(indent, "else:");
            emitLoopBody(*e, indent + 1);
            break;
        }
    }

    void genExprStmt(const Expr& e, int indent) {
        // std::cout << a << b;  ->  print(a, b)
        if (isOutputStream(e)) {
            if (opt_.dialect == Dialect::C) {
                err("C 里没有 cout / 输出流；请用 printf(\"...\", ...) 或 print(...)",
                    e.line);
            }
            std::vector<const Expr*> parts;
            flattenStream(e, parts);
            std::string args;
            bool first = true;
            for (const Expr* p : parts) {
                // stream objects and manipulators carry no value
                if (p->kind == ExprKind::Ident) {
                    const std::string& n = p->sval;
                    if (n == "endl" || n == "std::endl" || n == "cout" || n == "cerr" ||
                        n == "std::cout" || n == "std::cerr" || n == "clog") {
                        continue;
                    }
                }
                if (!first) args += ", ";
                args += expr(*p);
                first = false;
            }
            if (first) return;  // nothing printable
            line(indent, "print(" + args + ")");
            return;
        }
        // x++;  x--;  ++x;  --x;
        if (e.kind == ExprKind::Binary && (e.op == "++" || e.op == "--")) {
            std::string target = expr(*e.lhs);
            std::string step = (e.op == "++") ? " += 1" : " -= 1";
            line(indent, target + step);
            return;
        }
        // assignment with ternary RHS
        if (e.kind == ExprKind::Binary && isAssignOp(e.op) && e.rhs &&
            e.rhs->kind == ExprKind::Ternary) {
            std::string target = expr(*e.lhs);
            Type t = typeOfExpr(*e.lhs);
            emitTernaryAssign(target, *e.rhs, indent);
            (void)t;
            return;
        }

        // Anything left is an expression statement that produces a value nobody
        // uses: `printf;`, `x;`, `p.x;`, `a + b;`. Emitting it verbatim makes the
        // generated script evaluate the value and throw it away, so the game
        // reports nothing and the typo survives as a silent no-op. The game does
        // hint "did you mean print()?" for its own builtins; this is the same
        // idea for the names only this mod knows about.
        bool hasEffect = e.kind == ExprKind::Call ||
                         (e.kind == ExprKind::Binary && isAssignOp(e.op));
        if (!hasEffect) {
            if (e.kind == ExprKind::Ident) {
                if (isCallableName(e.sval)) {
                    err("'" + e.sval + "' 是函数，要写成 " + e.sval +
                            "(...)；只写名字这一行什么也不会做",
                        e.line);
                }
                err("'" + e.sval + "' 这一行什么也不会做（少写了赋值或函数调用？）", e.line);
            }
            err("这个表达式既不是赋值也不是调用，这一行什么也不会做", e.line);
        }
        line(indent, expr(e));
    }

    // Names that only exist as calls in this dialect: the standard-library
    // spellings call() maps, plus whatever the game provides or the file defines.
    bool isCallableName(const std::string& n) const {
        static const std::set<std::string> mapped = {
            "abs",   "min",   "max",  "len",  "pow",  "sqrt", "floor", "ceil",
            "round", "random", "printf", "print", "str", "to_string", "time", "clock"};
        return mapped.count(n) > 0 || functionReturns_.count(n) > 0 ||
               structs_.count(n) > 0 || isGameBuiltin(n);
    }

    static bool isAssignOp(const std::string& op) {
        return op == "=" || op == "+=" || op == "-=" || op == "*=" || op == "/=" ||
               op == "%=";
    }

    static bool isOutputStream(const Expr& e) {
        const Expr* cur = &e;
        bool sawCout = false;
        while (cur->kind == ExprKind::Binary && cur->op == "<<") {
            if (cur->lhs && cur->lhs->kind == ExprKind::Ident &&
                (cur->lhs->sval == "cout" || cur->lhs->sval == "cerr" ||
                 cur->lhs->sval == "std::cout" || cur->lhs->sval == "std::cerr")) {
                sawCout = true;
            }
            cur = cur->lhs.get();
        }
        return sawCout;
    }

    static void flattenStream(const Expr& e, std::vector<const Expr*>& out) {
        if (e.kind == ExprKind::Binary && e.op == "<<") {
            flattenStream(*e.lhs, out);
            out.push_back(e.rhs.get());
            return;
        }
        out.push_back(&e);
    }

    // Lowers a C-style for loop. Simple counted loops become `for i in range()`;
    // anything else becomes a while loop with the step appended on `continue`.
    void genFor(const Stmt& s, int indent) {
        requireUnlock("loops", s.line);
        pushScope();

        // do { } while (c) is represented as For without init/step
        if (!s.init && !s.step) {
            emitLoopBody(*s.thenBranch, indent);       // run body once
            line(indent, "while " + cond(*s.cond) + ":");
            emitLoopBody(*s.thenBranch, indent + 1);
            popScope();
            return;
        }

        // Detect the canonical  for (i = 0; i < n; i++)  shape -> range().
        // Done before the initialiser is emitted so no redundant `i = 0` is
        // written to the output.
        if (s.init && s.init->kind == StmtKind::VarDecl && !s.init->decl.arraySize &&
            s.init->decl.init) {
            const VarDecl& d = s.init->decl;
            if (auto rangeCall = tryRange(s, d)) {
                std::string asmName = declareVar(d.name, d.type);
                line(indent, "for " + asmName + " in " + *rangeCall + ":");
                emitLoopBody(*s.thenBranch, indent + 1);
                popScope();
                return;
            }
        }

        if (s.init && s.init->kind == StmtKind::VarDecl) {
            genVarDeclStmt(s.init->decl, indent);
        } else if (s.init && s.init->kind == StmtKind::ExprStmt && s.init->value) {
            genExprStmt(*s.init->value, indent);
        }

        std::string condText = s.cond ? cond(*s.cond) : "True";

        // Normalise "i++" into "i += 1" for the continue suffix.
        std::string suffix;
        if (s.step) {
            if (s.step->kind == ExprKind::Binary &&
                (s.step->op == "++" || s.step->op == "--")) {
                suffix = expr(*s.step->lhs) + (s.step->op == "++" ? " += 1" : " -= 1");
            } else {
                suffix = expr(*s.step);
            }
        }
        if (!suffix.empty()) continueSuffix_.push_back(suffix);

        line(indent, "while " + condText + ":");
        emitLoopBody(*s.thenBranch, indent + 1);
        if (!suffix.empty()) {
            line(indent + 1, suffix);
            continueSuffix_.pop_back();
        }
        popScope();
    }

    // Recognises  for (int i = <start>; i <cmp> <stop>; <step>)  and returns the
    // equivalent range() call. Returns nullopt when the loop is not canonical.
    std::optional<std::string> tryRange(const Stmt& s, const VarDecl& d) {
        if (!s.cond || !s.step) return std::nullopt;
        if (!d.init || s.cond->kind != ExprKind::Binary) return std::nullopt;
        if (s.cond->op != "<" && s.cond->op != "<=" && s.cond->op != ">" &&
            s.cond->op != ">=") {
            return std::nullopt;
        }
        if (s.cond->lhs->kind != ExprKind::Ident || s.cond->lhs->sval != d.name) {
            return std::nullopt;
        }

        int direction = 0;
        if (s.step->kind == ExprKind::Binary) {
            const std::string& op = s.step->op;
            bool sameVar = s.step->lhs && s.step->lhs->kind == ExprKind::Ident &&
                           s.step->lhs->sval == d.name;
            if (sameVar && (op == "++" || op == "+=")) direction = 1;
            if (sameVar && (op == "--" || op == "-=")) direction = -1;
            // var += 1 only (a different increment cannot map to range())
            if (op == "+=" || op == "-=") {
                if (!s.step->rhs || s.step->rhs->kind != ExprKind::IntLit ||
                    s.step->rhs->ival != 1) {
                    return std::nullopt;
                }
            }
        }
        if (direction == 0) return std::nullopt;

        const bool up = direction > 0;
        if (up && !(s.cond->op == "<" || s.cond->op == "<=")) return std::nullopt;
        if (!up && !(s.cond->op == ">" || s.cond->op == ">=")) return std::nullopt;

        std::string start = expr(*d.init);
        std::string stop = expr(*s.cond->rhs);

        bool startIsZero = d.init->kind == ExprKind::IntLit && d.init->ival == 0;

        if (up) {
            if (s.cond->op == "<=") stop = "(" + stop + " + 1)";
            if (startIsZero) return "range(" + stop + ")";
            return "range(" + start + ", " + stop + ")";
        }
        if (s.cond->op == ">=") stop = "(" + stop + " - 1)";
        return "range(" + start + ", " + stop + ", -1)";
    }

    // ------------------------------------------------------------------
    // expressions
    // ------------------------------------------------------------------
    // Binding powers. The target language follows Python precedence; emitting
    // with the matching power keeps only *required* parentheses in the output.
    // This matters beyond cosmetics: '(' produces a BracketNode, which costs
    // interpreter operations and would skew op-count based progress and any
    // time/op-sensitive achievement or leaderboard run.
    static int precOf(const Expr& e) {
        switch (e.kind) {
            case ExprKind::Binary: {
                const std::string& op = e.op;
                if (isAssignOp(op) || op == "<<=" || op == ">>=") return 10;
                if (op == "||") return 35;
                if (op == "&&") return 40;
                if (op == "==" || op == "!=" || op == "<" || op == "<=" || op == ">" ||
                    op == ">=") {
                    return 50;
                }
                if (op == "+" || op == "-") return 60;
                if (op == "*" || op == "/" || op == "%" || op == "//") return 70;
                if (op == "**") return 80;
                return 50;
            }
            case ExprKind::Unary:
                return 90;
            case ExprKind::Ternary:
                return 5;
            default:
                return 100;
        }
    }

    std::string expr(const Expr& e) { return emitExpr(e, 0); }

    std::string emitExpr(const Expr& e, int minPrec) {
        std::string s = emitCore(e);
        if (precOf(e) < minPrec) return "(" + s + ")";
        return s;
    }

    std::string emitCore(const Expr& e) {
        switch (e.kind) {
            case ExprKind::IntLit: return std::to_string(e.ival);
            case ExprKind::FloatLit: return formatFloat(e.fval);
            case ExprKind::StringLit: return quote(e.sval);
            case ExprKind::CharLit: return quote(e.sval);
            case ExprKind::BoolLit: return e.bval ? "True" : "False";
            case ExprKind::NullLit: return "None";
            case ExprKind::Ident: return ident(e);
            case ExprKind::Binary: return binary(e);
            case ExprKind::Unary: return unary(e);
            case ExprKind::Call: return call(e);
            case ExprKind::Index: return emitExpr(*e.lhs, 100) + "[" + expr(*e.rhs) + "]";
            case ExprKind::Member: return member(e);
            case ExprKind::Ternary:
                err("conditional expressions are lowered at statement level only", e.line);
            case ExprKind::InitList: {
                requireUnlock("lists", e.line);
                std::string s = "[";
                for (std::size_t i = 0; i < e.args.size(); ++i) {
                    if (i) s += ", ";
                    s += expr(*e.args[i]);
                }
                return s + "]";
            }
        }
        err("unsupported expression", e.line);
    }

    static std::string formatFloat(double v) {
        std::ostringstream os;
        os.precision(15);
        os << v;
        std::string s = os.str();
        return s;
    }

    static std::string quote(const std::string& s) {
        std::string out = "\"";
        for (char c : s) {
            switch (c) {
                case '"': out += "\\\""; break;
                case '\\': out += "\\\\"; break;
                case '\n': out += "\\n"; break;
                case '\t': out += "\\t"; break;
                default: out += c; break;
            }
        }
        return out + "\"";
    }

    std::string ident(const Expr& e) {
        const std::string& raw = e.sval;
        // Enum member, possibly qualified (Dir::North -> Dir_North)
        if (raw.find("::") != std::string::npos) {
            std::string flat = raw;
            std::replace(flat.begin(), flat.end(), ':', '_');
            // "Dir::North" -> "Dir__North" -> collapse
            std::string collapsed;
            for (std::size_t i = 0; i < flat.size(); ++i) {
                if (flat[i] == '_' && i + 1 < flat.size() && flat[i + 1] == '_') continue;
                collapsed += flat[i];
            }
            return collapsed;
        }
        if (auto it = enumMemberToQualified_.find(raw);
            it != enumMemberToQualified_.end() && lookup(raw) == nullptr) {
            return it->second;
        }
        return asmOf(raw);
    }

    // "hello.cpp" -> "hello";  "../lib/util.hpp" -> "util".
    // Returns empty when the stem is not usable as a module identifier.
    static std::string moduleNameOf(const std::string& file) {
        std::string name = file;
        std::size_t slash = name.find_last_of("/\\");
        if (slash != std::string::npos) name = name.substr(slash + 1);
        std::size_t dot = name.rfind('.');
        if (dot != std::string::npos) name = name.substr(0, dot);
        if (name.empty()) return {};

        auto ok = [](char c) {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                   (c >= '0' && c <= '9') || c == '_';
        };
        if (!( (name[0] >= 'a' && name[0] <= 'z') || (name[0] >= 'A' && name[0] <= 'Z') || name[0] == '_')) {
            return {};
        }
        for (char c : name) {
            if (!ok(c)) return {};
        }
        return name;
    }

    std::string binary(const Expr& e) {
        const std::string& op = e.op;

        if (op == "++" || op == "--") {
            err("increment/decrement is only supported as a standalone statement or a "
                "for-loop step", e.line);
        }
        if (!e.lhs || !e.rhs) err("malformed binary expression", e.line);

        if (isAssignOp(op)) {
            std::string lhs = expr(*e.lhs);
            requireUnlock("variables", e.line);
            if (op == "/=") {
                // integer division assignment
                Type lt = typeOfExpr(*e.lhs);
                Type rt = infer(*e.rhs);
                if (lt.base == BaseType::Int && rt.base == BaseType::Int) {
                    return lhs + " = " + lhs + " // " + expr(*e.rhs);
                }
            }
            return lhs + " " + op + " " + expr(*e.rhs);
        }

        if (op == "&" || op == "|" || op == "^" || op == "<<" || op == ">>" ||
            op == "<<=" || op == ">>=") {
            err("bitwise operators are not available in the target language "
                "(consider arithmetic instead)", e.line);
        }

        std::string target = op;
        if (op == "&&") target = "and";
        if (op == "||") target = "or";

        // Integer division maps to floor division, which is what the target
        // language's `//` does.
        if (op == "/") {
            Type lt = infer(*e.lhs);
            Type rt = infer(*e.rhs);
            if (lt.base == BaseType::Int && rt.base == BaseType::Int) target = "//";
        }

        if (target == "+" || target == "-" || target == "*" || target == "/" ||
            target == "//" || target == "%" || target == "**") {
            requireUnlock("operators", e.line);
        }

        const int p = precOf(e);
        const bool rightAssoc = (target == "**");
        std::string l = rightAssoc ? emitExpr(*e.lhs, p + 1) : emitExpr(*e.lhs, p);
        std::string r = rightAssoc ? emitExpr(*e.rhs, p) : emitExpr(*e.rhs, p + 1);
        return l + " " + target + " " + r;
    }

    std::string unary(const Expr& e) {
        const std::string& op = e.op;
        if (op == "!") {
            // `not` binds looser than comparisons in this language, so keep the
            // operand parenthesised unless it is atomic.
            return "not " + emitExpr(*e.lhs, 100);
        }
        if (op == "-") {
            requireUnlock("operators", e.line);
            return "-" + emitExpr(*e.lhs, 90);
        }
        if (op == "+") return expr(*e.lhs);
        if (op == "++" || op == "--") {
            err("increment/decrement is only supported as a standalone statement", e.line);
        }
        err("unsupported unary operator '" + op + "'", e.line);
    }

    // printf with a format string is lowered into string concatenation:
    //   printf("x=%d items", n)  ->  print("x=" + str(n) + " items")
    //
    // The target language has str() and string + string (it concatenates when
    // both operands are strings), so a format string lowers exactly. Width and
    // precision flags are consumed by the scanner but not applied: the game
    // prints floats its own way, and silently emitting wrong digits would be
    // worse than dropping the padding.
    //
    // Only reached for Dialect::C. Any mismatch between placeholders and
    // arguments is a compile error, never a silently mangled line.
    std::string printfCall(const Expr& e) {
        if (e.args.empty()) err("printf 至少需要一个格式字符串", e.line);

        const Expr& fmt = *e.args[0];
        if (fmt.kind != ExprKind::StringLit) {
            err("printf 的格式串必须是字符串字面量，否则无法在编译期校验占位符", e.line);
        }

        auto isIn = [](const char* set, char c) {
            return c != '\0' && std::strchr(set, c) != nullptr;
        };

        const std::string& s = fmt.sval;
        std::vector<std::string> parts;
        std::string literal;
        std::size_t arg = 1;

        for (std::size_t i = 0; i < s.size(); ++i) {
            if (s[i] != '%') {
                literal.push_back(s[i]);
                continue;
            }

            if (i + 1 >= s.size()) err("printf 的格式串以 % 结尾", e.line);
            if (s[i + 1] == '%') {          // literal percent sign
                literal.push_back('%');
                ++i;
                continue;
            }

            std::size_t j = i + 1;
            while (j < s.size() && isIn("-+ #0", s[j])) ++j;                 // flags
            while (j < s.size() && std::isdigit(static_cast<unsigned char>(s[j]))) ++j;
            if (j < s.size() && s[j] == '.') {                               // precision
                ++j;
                while (j < s.size() && std::isdigit(static_cast<unsigned char>(s[j]))) ++j;
            }
            while (j < s.size() && isIn("hljztL", s[j])) ++j;                // length
            if (j >= s.size()) err("printf 的格式串不完整", e.line);

            // Only conversions the target language can reproduce exactly.
            // %x/%o would silently print decimal, and %e/%g stay plain decimals,
            // so they are refused instead of producing wrong digits.
            const char conv = s[j];
            if (!isIn("diufsc", conv)) {
                err(std::string("printf 暂不支持 %") + conv
                    + " 这个转换符；可用 %d %i %u %f %s %c 或 %%", e.line);
            }
            if (arg >= e.args.size()) err("printf 的占位符比参数多", e.line);

            if (!literal.empty()) {
                parts.push_back(quote(literal));
                literal.clear();
            }
            // A string argument needs no str() round trip; keep it literal so
            // `printf("%s", "x")` lowers to a plain concatenation.
            const Expr& value = *e.args[arg];
            if (conv == 'c' && value.kind != ExprKind::CharLit) {
                err("printf 的 %c 需要字符字面量（如 'A'）；整数不会被自动转成字符", e.line);
            }
            const bool literal = value.kind == ExprKind::StringLit ||
                                 value.kind == ExprKind::CharLit;
            parts.push_back(literal ? quote(value.sval)
                                    : "str(" + expr(value) + ")");
            ++arg;
            i = j;
        }

        if (!literal.empty()) parts.push_back(quote(literal));
        if (arg != e.args.size()) err("printf 的参数比占位符多", e.line);

        std::string out = parts.empty() ? "\"\"" : parts[0];
        for (std::size_t k = 1; k < parts.size(); ++k) out += " + " + parts[k];
        return "print(" + out + ")";
    }

    std::string call(const Expr& e) {
        std::string args;
        for (std::size_t i = 0; i < e.args.size(); ++i) {
            if (i) args += ", ";
            args += expr(*e.args[i]);
        }

        // method call: obj.method(args)
        if (e.lhs) {
            std::string obj = expr(*e.lhs);
            const std::string& m = e.callee;
            if (m == "push_back" || m == "emplace_back" || m == "append") {
                return "append(" + obj + ", " + args + ")";
            }
            if (m == "pop_back") return "pop(" + obj + ")";
            if (m == "size" || m == "length") return "len(" + obj + ")";
            if (m == "empty" && e.args.empty()) return "(len(" + obj + ") == 0)";
            if (m == "clear" && e.args.empty()) {
                usedHelpers_.insert("clear");
                return "cpp_clear(" + obj + ")";
            }
            if (m == "count" && e.args.size() == 1) {
                usedHelpers_.insert("has");
                return "cpp_has(" + obj + ", " + args + ")";
            }
            if (m == "front") return obj + "[0]";
            if (m == "back") return obj + "[len(" + obj + ") - 1]";
            if (m == "at" && e.args.size() == 1) return obj + "[" + args + "]";
            if (m == "substr") {
                usedHelpers_.insert("substr");
                return "cpp_substr(" + obj + ", " + args + ")";
            }
            err("unsupported method '" + m + "()'", e.line);
        }

        const std::string& f = e.callee;
        // user-defined function
        if (functionReturns_.count(f) || structs_.count(f)) return f + "(" + args + ")";

        if (f == "abs" || f == "min" || f == "max") return f + "(" + args + ")";
        if (f == "len") return "len(" + args + ")";
        if (f == "pow" && e.args.size() == 2) {
            return "(" + expr(*e.args[0]) + " ** " + expr(*e.args[1]) + ")";
        }
        if (f == "sqrt" && e.args.size() == 1) {
            usedHelpers_.insert("sqrt");
            return "cpp_sqrt(" + args + ")";
        }
        if (f == "floor" && e.args.size() == 1) return "(" + args + " // 1)";
        if (f == "ceil" && e.args.size() == 1) {
            usedHelpers_.insert("ceil");
            return "cpp_ceil(" + args + ")";
        }
        if (f == "round" && e.args.size() == 1) {
            usedHelpers_.insert("round");
            return "cpp_round(" + args + ")";
        }
        if (f == "random" && e.args.empty()) return "random()";
        if (f == "printf") {
            // In a .c window printf is the normal way to print, so the format
            // string is decoded at compile time into string concatenation.
            if (opt_.dialect == Dialect::C) return printfCall(e);

            // In C++ mode only the literal-only form is accepted: the message
            // below points at std::cout, which is the idiomatic spelling there.
            if (e.args.size() == 1) {
                const Expr& a = *e.args[0];
                bool hasSpecifier = false;
                if (a.kind == ExprKind::StringLit) {
                    hasSpecifier = a.sval.find('%') != std::string::npos;
                }
                if (a.kind == ExprKind::StringLit && !hasSpecifier) {
                    return "print(" + quote(a.sval) + ")";
                }
            }
            err("printf 的格式化占位符不受支持：请改用 std::cout << ... << std::endl; "
                "或把内容拼成一个字符串后 print(...)",
                e.line);
        }
        if (f == "print") return "print(" + args + ")";
        if (f == "str" || f == "to_string") return "str(" + args + ")";
        if (f == "time" && e.args.empty()) return "get_time()";
        if (f == "clock" && e.args.empty()) return "get_tick_count()";

        // Pass through anything that matches a game builtin; otherwise report it.
        if (isGameBuiltin(f)) return f + "(" + args + ")";

        // With #include present the compiler only sees one file, so an unknown
        // name may legitimately come from an imported module. Checking it here
        // would reject valid programs; the game reports a missing name at run
        // time anyway.
        if (hasImports_) return f + "(" + args + ")";

        err("unknown function '" + f + "' - add a mapping or a definition", e.line);
    }

    static bool isGameBuiltin(const std::string& f) {
        static const std::set<std::string> names = {
            "move", "can_move", "harvest", "can_harvest", "plant", "till", "use_item",
            "get_water", "get_entity_type", "get_ground_type", "get_pos_x", "get_pos_y",
            "get_pos_z", "get_world_size", "get_time", "get_tick_count", "num_items",
            "num_unlocked", "unlock", "do_a_flip", "pet_the_piggy", "measure",
            "get_companion", "spawn_drone", "wait_for", "num_drones", "max_drones",
            "send", "receive", "get_cost", "place", "swap", "quick_print", "clear",
            "set_world_size", "set_execution_speed", "dig", "get_hardness",
            "get_stability", "prospect_iron", "prospect_quartz", "push", "can_push",
            "jump", "hover", "get_hover", "append", "add", "remove", "pop", "insert",
            "range", "len", "str", "random", "get_globals", "change_hat",
            "leaderboard_run", "simulate", "simulate_async", "await", "has_finished",
            "get_drone_id",
        };
        return names.count(f) > 0;
    }

    std::string member(const Expr& e) {
        Type t = infer(*e.lhs);
        if (t.isStruct()) {
            return expr(*e.lhs) + "[" + quote(e.member) + "]";
        }
        // enum-like constant, e.g. Items.Water
        if (e.lhs->kind == ExprKind::Ident) {
            std::string base = e.lhs->sval;
            if (base == "Items" || base == "Entities" || base == "Grounds" ||
                base == "Unlocks" || base == "Hats" || base == "Leaderboards") {
                return base + "." + e.member;
            }
        }
        err("unsupported member access '." + e.member + "'", e.line);
    }

    std::string cond(const Expr& e) {
        // `not` needs no wrapping; everything else is parenthesised already.
        return expr(e);
    }

    // ------------------------------------------------------------------
    // type inference
    // ------------------------------------------------------------------
    Type typeOfExpr(const Expr& e) { return infer(e); }

    Type infer(const Expr& e) {
        switch (e.kind) {
            case ExprKind::IntLit: return Type::of(BaseType::Int);
            case ExprKind::FloatLit: return Type::of(BaseType::Float);
            case ExprKind::StringLit: return Type::of(BaseType::String);
            case ExprKind::CharLit: return Type::of(BaseType::Char);
            case ExprKind::BoolLit: return Type::of(BaseType::Bool);
            case ExprKind::NullLit: return Type::of(BaseType::Unknown);
            case ExprKind::Ident: {
                if (e.sval.find("::") != std::string::npos ||
                    enumMemberToQualified_.count(e.sval)) {
                    return Type::of(BaseType::Int);
                }
                Type t = typeOf(e.sval);
                return t;
            }
            case ExprKind::Unary:
                if (e.op == "!") return Type::of(BaseType::Bool);
                return infer(*e.lhs);
            case ExprKind::Binary: {
                const std::string& op = e.op;
                if (op == "&&" || op == "||" || op == "==" || op == "!=" || op == "<" ||
                    op == "<=" || op == ">" || op == ">=") {
                    return Type::of(BaseType::Bool);
                }
                if (isAssignOp(op)) return infer(*e.lhs);
                Type lt = infer(*e.lhs);
                Type rt = e.rhs ? infer(*e.rhs) : Type::of(BaseType::Unknown);
                if (op == "/" && lt.base == BaseType::Int && rt.base == BaseType::Int) {
                    return Type::of(BaseType::Int);
                }
                if (lt.base == BaseType::Float || rt.base == BaseType::Float) {
                    return Type::of(BaseType::Float);
                }
                if (lt.base == BaseType::String || rt.base == BaseType::String) {
                    return Type::of(BaseType::String);
                }
                if (lt.base == BaseType::Int && rt.base == BaseType::Int) {
                    return Type::of(BaseType::Int);
                }
                return Type::of(BaseType::Unknown);
            }
            case ExprKind::Index: {
                Type base = infer(*e.lhs);
                if (base.element) return *base.element;
                return Type::of(BaseType::Unknown);
            }
            case ExprKind::Member: {
                Type base = infer(*e.lhs);
                if (base.isStruct()) {
                    auto it = structs_.find(base.userType);
                    if (it != structs_.end()) {
                        for (const auto& f : it->second->fields) {
                            if (f.name == e.member) return f.type;
                        }
                    }
                }
                return Type::of(BaseType::Unknown);
            }
            case ExprKind::Call: {
                if (e.lhs) {
                    const std::string& m = e.callee;
                    if (m == "size" || m == "length" || m == "count") {
                        return Type::of(BaseType::Int);
                    }
                    if (m == "empty") return Type::of(BaseType::Bool);
                    if (m == "pop_back" || m == "front" || m == "back" || m == "at") {
                        Type base = infer(*e.lhs);
                        if (base.element) return *base.element;
                    }
                    return Type::of(BaseType::Unknown);
                }
                auto fr = functionReturns_.find(e.callee);
                if (fr != functionReturns_.end()) return fr->second;
                auto st = structs_.find(e.callee);
                if (st != structs_.end()) {
                    Type t = Type::of(BaseType::Unknown);
                    t.userType = st->first;
                    return t;
                }
                if (e.callee == "abs" && !e.args.empty()) return infer(*e.args[0]);
                if ((e.callee == "min" || e.callee == "max") && !e.args.empty()) {
                    return infer(*e.args[0]);
                }
                if (e.callee == "str" || e.callee == "to_string") {
                    return Type::of(BaseType::String);
                }
                if (e.callee == "sqrt" || e.callee == "pow" || e.callee == "random") {
                    return Type::of(BaseType::Float);
                }
                if (e.callee == "floor" || e.callee == "ceil" || e.callee == "round") {
                    return Type::of(BaseType::Int);
                }
                if (e.callee == "len") return Type::of(BaseType::Int);
                return Type::of(BaseType::Unknown);
            }
            case ExprKind::Ternary: return infer(*e.lhs);
            case ExprKind::InitList:
                if (!e.args.empty()) {
                    Type t = Type::of(BaseType::Unknown);
                    t.isVector = true;
                    t.element = std::make_shared<Type>(infer(*e.args[0]));
                    return t;
                }
                return Type::of(BaseType::Unknown);
        }
        return Type::of(BaseType::Unknown);
    }

    // ------------------------------------------------------------------
    // runtime prelude
    // ------------------------------------------------------------------
    std::string buildPrelude() {
        std::string out;
        if (usedHelpers_.count("list")) {
            out += "def cpp_list(n, v):\n";
            out += "\tr = []\n";
            out += "\tfor i in range(n):\n";
            out += "\t\tappend(r, v)\n";
            out += "\treturn r\n\n";
        }
        if (usedHelpers_.count("clear")) {
            out += "def cpp_clear(c):\n";
            out += "\twhile len(c) > 0:\n";
            out += "\t\tpop(c)\n\n";
        }
        if (usedHelpers_.count("sqrt")) {
            out += "def cpp_sqrt(x):\n";
            out += "\tif x <= 0:\n";
            out += "\t\treturn 0\n";
            out += "\tg = x\n";
            out += "\tfor i in range(40):\n";
            out += "\t\tg = (g + x / g) / 2\n";
            out += "\treturn g\n\n";
        }
        if (usedHelpers_.count("ceil")) {
            out += "def cpp_ceil(x):\n";
            out += "\treturn -((-x) // 1)\n\n";
        }
        if (usedHelpers_.count("round")) {
            out += "def cpp_round(x):\n";
            out += "\tif x >= 0:\n";
            out += "\t\treturn (x + 0.5) // 1\n";
            out += "\treturn -((-x + 0.5) // 1)\n\n";
        }
        if (usedHelpers_.count("has")) {
            out += "def cpp_has(m, k):\n";
            out += "\treturn k in m\n\n";
        }
        if (usedHelpers_.count("substr")) {
            out += "def cpp_substr(s, start, n):\n";
            out += "\treturn s[start:start + n]\n\n";
        }
        return out;
    }
};

}  // namespace cppfarmer
