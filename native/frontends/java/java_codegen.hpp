// Code generator: lowers the Java subset AST to The Farmer Was Replaced script.
//
// Reuses the C++ front-end's AST and follows its output contract exactly, because
// the plugin consumes the two results identically:
//
//   - every emitted output line carries the source line it came from (0 for
//     compiler-generated lines) in a 1:1 line table, which is what turns editor
//     breakpoints and the execution highlight into offsets inside the generated
//     script;
//   - only strictly necessary parentheses are emitted (a '(' costs interpreter
//     operations and would skew op-count based achievements);
//   - `main` is hoisted to top-level statements so a fresh save runs without the
//     'functions' unlock;
//   - integer division lowers to floor division `//`;
//   - tech-tree constructs are gated against the save's actual unlocks.
//
// Java-specific lowering happens here: System.out.println -> print, string `+`
// concatenation -> str() wrapping, Math.* -> the same helpers the C++ front-end
// uses, arrays -> lists (with a java_list fill helper), `.length` -> len().
#pragma once

#include <algorithm>
#include <cctype>
#include <map>
#include <memory>
#include <optional>
#include <set>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>

#include "ast.hpp"

namespace jfamer {

class CompileError : public std::runtime_error {
public:
    CompileError(const std::string& msg, int line) : std::runtime_error(msg), line(line) {}
    int line;
};

// Reuses the C++ front-end's AST node definitions (see java_parser.hpp).
using cppfarmer::BaseType;
using cppfarmer::Expr;
using cppfarmer::ExprKind;
using cppfarmer::ExprPtr;
using cppfarmer::FuncDecl;
using cppfarmer::GlobalVar;
using cppfarmer::Param;
using cppfarmer::Program;
using cppfarmer::Stmt;
using cppfarmer::StmtKind;
using cppfarmer::StmtPtr;
using cppfarmer::Type;
using cppfarmer::VarDecl;

struct Options {
    bool emitLineHints = false;   // write "# src:Lnn" comments for debugging
    std::string sourceName = "main.java";
};

class JavaCodeGen {
public:
    explicit JavaCodeGen(Options opt) : opt_(std::move(opt)) {}

    std::string generate(const Program& prog) {
        declare(prog);
        scopes_.clear();
        pushScope();  // scope 0 holds globals for the whole program

        for (const auto& gv : prog.globals) genGlobal(gv.decl);

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
                // Compiler-generated entry call has no source line of its own.
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

    const std::vector<int>& lineMap() const { return finalLineMap_; }
    const std::set<std::string>& requiredFeatures() const { return required_; }

    void setUnlocks(const std::vector<std::string>& unlocks) {
        unlocked_.clear();
        for (const auto& u : unlocks) unlocked_.insert(u);
        checkUnlocks_ = true;
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
    std::vector<int> lineMap_;
    int curLine_ = 0;
    bool suppressingReturn_ = false;
    int functionDepth_ = 0;

    std::map<std::string, Type> functionReturns_;
    std::vector<Scope> scopes_;
    std::set<std::string> usedHelpers_;

    std::set<std::string> unlocked_;
    std::set<std::string> required_;
    bool checkUnlocks_ = false;
    std::vector<int> finalLineMap_;

    // Suffix statements that must run before `continue` when a C-style for loop
    // is lowered to a while loop.
    std::vector<std::string> continueSuffix_;

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
    // tech-tree gating (same keys and labels as the C++ front-end)
    // ------------------------------------------------------------------
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

        std::string msg = "这个写法需要先解锁「" + unlockLabel(key) + "」，当前存档还没解锁";
        if (key == "functions") {
            msg += "。main() 会作为顶层代码执行，不需要解锁；"
                   "只有自定义 static 方法才用到这个科技";
        }
        err(msg, line);
    }

    // ------------------------------------------------------------------
    // declarations pass
    // ------------------------------------------------------------------
    void declare(const Program& prog) {
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
    // globals, entry, functions
    // ------------------------------------------------------------------
    static std::string zeroFor(const Type& t) {
        if (t.isVector) return "[]";
        switch (t.base) {
            case BaseType::Int:
            case BaseType::Float:
            case BaseType::Char: return "0";
            case BaseType::Bool: return "False";
            case BaseType::String: return "\"\"";
            default: return "None";
        }
    }

    void genGlobal(const VarDecl& d) {
        Type t = d.type;
        if (d.init && t.base == BaseType::Unknown && !t.isVector) t = infer(*d.init);
        std::string target = declareVar(d.name, t);
        if (d.init) {
            emitAssign(target, t, *d.init);
        } else {
            line(0, target + " = " + zeroFor(t));
        }
    }

    // A return inside an if/loop cannot be hoisted: the target language only
    // allows `return` inside a function, so only statements directly in main's
    // body may carry one.
    static bool hasNestedReturn(const Stmt& s, bool allowReturn) {
        if (s.kind == StmtKind::Return) return !allowReturn;
        for (const auto& child : s.body) {
            if (child && hasNestedReturn(*child, allowReturn)) return true;
        }
        if (s.thenBranch && hasNestedReturn(*s.thenBranch, false)) return true;
        if (s.elseBranch && hasNestedReturn(*s.elseBranch, false)) return true;
        if (s.init && hasNestedReturn(*s.init, false)) return true;
        return false;
    }

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
        if (!fn.params.empty()) return false;
        if (hasNestedReturn(*fn.body, true)) return false;
        if (!trailingReturnsOnly(*fn.body)) return false;
        return true;
    }

    void genEntryBody(const FuncDecl& fn) {
        curLine_ = fn.line;
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
        }
        if (opt_.emitLineHints) line(0, "# fn " + fn.name);
        line(0, "def " + fn.name + "(" + params + "):");
        bool empty = genBody(*fn.body, 1);
        if (empty) line(1, "pass");
        line(0, "");
        popScope();
        --functionDepth_;
    }

    // ------------------------------------------------------------------
    // statements
    // ------------------------------------------------------------------
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
            case StmtKind::Include:
                err("Java 不支持 #include；跨文件导入没有对应的目标语言机制", s.line);
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

    void emitLoopBody(const Stmt& body, int indent) {
        bool empty = genBody(body, indent);
        if (empty) line(indent, "pass");
    }

    void genVarDeclStmt(const VarDecl& d, int indent) {
        Type t = d.type;
        if (d.init && t.base == BaseType::Unknown && !t.isVector) t = infer(*d.init);
        std::string target = declareVar(d.name, t);

        if (!d.init) {
            line(indent, target + " = " + zeroFor(t));
            return;
        }
        emitAssign(target, t, *d.init, indent);
    }

    void emitAssign(const std::string& target, const Type& t, const Expr& init,
                    int indent = 0) {
        (void)t;   // element type is not needed for a plain list assignment
        if (init.kind == ExprKind::InitList) {
            // Java array literal: {1, 2, 3} -> [1, 2, 3]. The target language has
            // no list literal other than [ ... ].
            requireUnlock("lists", init.line);
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

    void genExprStmt(const Expr& e, int indent) {
        // i++ / i--  ->  i += 1 / i -= 1
        if (e.kind == ExprKind::Binary && (e.op == "++" || e.op == "--")) {
            std::string target = expr(*e.lhs);
            std::string step = (e.op == "++") ? " += 1" : " -= 1";
            line(indent, target + step);
            return;
        }

        bool hasEffect = e.kind == ExprKind::Call ||
                         (e.kind == ExprKind::Binary && isAssignOp(e.op));
        if (!hasEffect) {
            if (e.kind == ExprKind::Ident) {
                if (isCallableName(e.sval)) {
                    err("'" + e.sval + "' 是方法，要写成 " + e.sval +
                            "(...)；只写名字这一行什么也不会做", e.line);
                }
                err("'" + e.sval + "' 这一行什么也不会做（少写了赋值或方法调用？）", e.line);
            }
            err("这个表达式既不是赋值也不是调用，这一行什么也不会做", e.line);
        }
        line(indent, expr(e));
    }

    bool isCallableName(const std::string& n) const {
        static const std::set<std::string> mapped = {
            "abs",   "min",  "max", "sqrt", "floor", "ceil", "round",
            "random", "print", "str", "to_string"};
        return mapped.count(n) > 0 || functionReturns_.count(n) > 0 || isGameBuiltin(n);
    }

    static bool isAssignOp(const std::string& op) {
        return op == "=" || op == "+=" || op == "-=" || op == "*=" || op == "/=" || op == "%=";
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

    void genFor(const Stmt& s, int indent) {
        requireUnlock("loops", s.line);
        pushScope();

        if (!s.init && !s.step) {
            emitLoopBody(*s.thenBranch, indent);       // run body once
            line(indent, "while " + cond(*s.cond) + ":");
            emitLoopBody(*s.thenBranch, indent + 1);
            popScope();
            return;
        }

        // Canonical  for (int i = 0; i < n; i++)  ->  range().
        if (s.init && s.init->kind == StmtKind::VarDecl && s.init->decl.init) {
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

        std::string suffix;
        if (s.step) {
            if (s.step->kind == ExprKind::Binary && (s.step->op == "++" || s.step->op == "--")) {
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
    static int precOf(const Expr& e) {
        switch (e.kind) {
            case ExprKind::Binary: {
                const std::string& op = e.op;
                if (isAssignOp(op)) return 10;
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
            case ExprKind::Ident: return asmOf(e.sval);
            case ExprKind::Binary: return binary(e);
            case ExprKind::Unary: return unary(e);
            case ExprKind::Call: return call(e);
            case ExprKind::Index: return emitExpr(*e.lhs, 100) + "[" + expr(*e.rhs) + "]";
            case ExprKind::Member: return member(e);
            case ExprKind::InitList: {
                requireUnlock("lists", e.line);
                std::string s = "[";
                for (std::size_t i = 0; i < e.args.size(); ++i) {
                    if (i) s += ", ";
                    s += expr(*e.args[i]);
                }
                return s + "]";
            }
            case ExprKind::Ternary:
                err("Java 前端不支持三元表达式，请改写为 if/else", e.line);
        }
        err("unsupported expression", e.line);
    }

    static std::string formatFloat(double v) {
        std::ostringstream os;
        os.precision(15);
        os << v;
        return os.str();
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
                Type lt = infer(*e.lhs);
                Type rt = infer(*e.rhs);
                if (lt.base == BaseType::Int && rt.base == BaseType::Int) {
                    return lhs + " = " + lhs + " // " + expr(*e.rhs);
                }
            }
            return lhs + " " + op + " " + expr(*e.rhs);
        }

        if (op == "&" || op == "|" || op == "^" || op == "<<" || op == ">>") {
            err("位运算在目标语言里不可用（请改用算术或布尔运算）", e.line);
        }

        std::string target = op;
        if (op == "&&") target = "and";
        if (op == "||") target = "or";

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

        // Java `+` concatenates strings; the target language needs str() around
        // the non-string operand to keep `"x=" + n` working.
        if (target == "+") {
            Type lt = infer(*e.lhs);
            Type rt = infer(*e.rhs);
            if (lt.base == BaseType::String || rt.base == BaseType::String) {
                std::string l = lt.base == BaseType::String
                    ? emitExpr(*e.lhs, p)
                    : "str(" + emitExpr(*e.lhs, p) + ")";
                std::string r = rt.base == BaseType::String
                    ? emitExpr(*e.rhs, p + 1)
                    : "str(" + emitExpr(*e.rhs, p + 1) + ")";
                return l + " + " + r;
            }
        }

        std::string l = emitExpr(*e.lhs, p);
        std::string r = emitExpr(*e.rhs, p + 1);
        return l + " " + target + " " + r;
    }

    std::string unary(const Expr& e) {
        const std::string& op = e.op;
        if (op == "!") {
            return "not " + emitExpr(*e.lhs, 100);
        }
        if (op == "-") {
            requireUnlock("operators", e.line);
            return "-" + emitExpr(*e.lhs, 90);
        }
        if (op == "+") return expr(*e.lhs);
        err("unsupported unary operator '" + op + "'", e.line);
    }

    std::string call(const Expr& e) {
        std::string args;
        for (std::size_t i = 0; i < e.args.size(); ++i) {
            if (i) args += ", ";
            args += expr(*e.args[i]);
        }

        const std::string& f = e.callee;

        // System.out.println(...) / System.out.print(...) -> print(...)
        if (f == "println" || f == "print") return "print(" + args + ")";

        // new int[n] -> a list of n zero elements
        if (f == "__array__" && e.args.size() == 1) {
            requireUnlock("lists", e.line);
            usedHelpers_.insert("list");
            Type elem = e.type.element ? *e.type.element : Type::of(BaseType::Unknown);
            return "java_list(" + args + ", " + zeroFor(elem) + ")";
        }

        // user-defined static method
        if (functionReturns_.count(f)) return f + "(" + args + ")";

        if (f == "abs" || f == "min" || f == "max") return f + "(" + args + ")";
        if (f == "sqrt" && e.args.size() == 1) {
            usedHelpers_.insert("sqrt");
            return "cpp_sqrt(" + args + ")";
        }
        if (f == "pow" && e.args.size() == 2) {
            // `**` binds tighter than `+`/`-` in the target language, so both
            // operands are emitted above its precedence to force the same
            // grouping Java's Math.pow(a, b) implies.
            return emitExpr(*e.args[0], 81) + " ** " + emitExpr(*e.args[1], 81);
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
        if (f == "str" || f == "to_string") return "str(" + args + ")";

        // Pass through anything that matches a game builtin; otherwise report it.
        if (isGameBuiltin(f)) return f + "(" + args + ")";

        err("unknown method '" + f + "' - add a mapping or a definition", e.line);
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
        if (e.member == "length") return "len(" + expr(*e.lhs) + ")";
        // enum-like constant, e.g. Items.Water / Entities.Tree / Grounds.Grassland
        if (e.lhs->kind == ExprKind::Ident) {
            std::string base = e.lhs->sval;
            if (base == "Items" || base == "Entities" || base == "Grounds" ||
                base == "Unlocks" || base == "Hats" || base == "Leaderboards") {
                return base + "." + e.member;
            }
        }
        err("unsupported member access '." + e.member + "'", e.line);
    }

    std::string cond(const Expr& e) { return expr(e); }

    // ------------------------------------------------------------------
    // type inference
    // ------------------------------------------------------------------
    Type infer(const Expr& e) {
        switch (e.kind) {
            case ExprKind::IntLit: return Type::of(BaseType::Int);
            case ExprKind::FloatLit: return Type::of(BaseType::Float);
            case ExprKind::StringLit: return Type::of(BaseType::String);
            case ExprKind::CharLit: return Type::of(BaseType::Char);
            case ExprKind::BoolLit: return Type::of(BaseType::Bool);
            case ExprKind::NullLit: return Type::of(BaseType::Unknown);
            case ExprKind::Ident: return typeOf(e.sval);
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
            case ExprKind::Member:
                if (e.member == "length") return Type::of(BaseType::Int);
                return Type::of(BaseType::Unknown);
            case ExprKind::Call: {
                if (e.callee == "println" || e.callee == "print") {
                    return Type::of(BaseType::Void);
                }
                if (e.callee == "__array__") {
                    Type t = Type::of(BaseType::Unknown);
                    t.isVector = true;
                    if (e.type.element) t.element = e.type.element;
                    return t;
                }
                auto fr = functionReturns_.find(e.callee);
                if (fr != functionReturns_.end()) return fr->second;
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
                return Type::of(BaseType::Unknown);
            }
            case ExprKind::InitList: {
                Type t = Type::of(BaseType::Unknown);
                t.isVector = true;
                if (!e.args.empty()) {
                    t.element = std::make_shared<Type>(infer(*e.args[0]));
                }
                return t;
            }
            case ExprKind::Ternary:
                return Type::of(BaseType::Unknown);
        }
        return Type::of(BaseType::Unknown);
    }

    // ------------------------------------------------------------------
    // runtime prelude (helpers shared with the C++ front-end's shape)
    // ------------------------------------------------------------------
    std::string buildPrelude() {
        std::string out;
        if (usedHelpers_.count("list")) {
            out += "def java_list(n, v):\n";
            out += "\tr = []\n";
            out += "\tfor i in range(n):\n";
            out += "\t\tappend(r, v)\n";
            out += "\treturn r\n\n";
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
        return out;
    }
};

}  // namespace jfamer
