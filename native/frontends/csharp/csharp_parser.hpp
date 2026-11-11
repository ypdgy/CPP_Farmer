// Recursive-descent parser for the C# subset accepted by the C# front-end.
//
// Produces a cppfarmer::Program, reusing the C++ front-end's AST and lexer. C#
// constructs are lowered here:
//
//   class X { ... }                 -> globals (static fields) + functions
//                                      (static methods); the class is a wrapper
//   static void Main(...)           -> Program.entry (hoisted to top-level code)
//   int[] a = new int[n]            -> "__array__" call (codegen fills a list)
//   Console.WriteLine(a, b)         -> Call("println") (codegen maps to print)
//   Math.Sqrt(x) / Math.PI          -> Call("sqrt") / a float literal
//   a.Length                        -> Member("Length") (codegen maps to len)
//   foreach (var v in xs)           -> RangeFor
//
// Anything C# has that the game cannot express (object instantiation, generics,
// switch, properties, string interpolation, multi-dimensional arrays) is refused
// with a line/column error rather than silently miscompiled.
#pragma once

#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

#include "ast.hpp"
#include "lexer.hpp"

namespace csfarmer {

class ParseError : public std::runtime_error {
public:
    ParseError(const std::string& msg, int line, int col)
        : std::runtime_error(msg), line(line), col(col) {}
    int line;
    int col;
};

// Reuses the C++ front-end's AST node definitions.
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
using cppfarmer::Tok;
using cppfarmer::Token;
using cppfarmer::Type;
using cppfarmer::VarDecl;

class CSharpParser {
public:
    explicit CSharpParser(std::vector<Token> toks) : toks_(std::move(toks)) {}

    Program parseProgram() {
        Program prog;
        while (!atEnd()) {
            // `using System;` has nothing to link against; drop the directive.
            if (matchIdent("using")) {
                skipToSemicolon();
                continue;
            }
            if (checkPunct(";")) {
                advance();
                continue;
            }
            parseClass(prog);
        }
        return prog;
    }

private:
    std::vector<Token> toks_;
    std::size_t i_ = 0;

    // ------------------------------------------------------------------
    // token helpers
    // ------------------------------------------------------------------
    const Token& cur() const { return toks_[i_]; }
    const Token& peek(std::size_t off = 1) const {
        std::size_t idx = i_ + off;
        return idx < toks_.size() ? toks_[idx] : toks_.back();
    }
    bool atEnd() const { return cur().kind == Tok::End; }
    void advance() {
        if (!atEnd()) ++i_;
    }

    bool checkPunct(const std::string& p) const {
        return cur().kind == Tok::Punct && cur().text == p;
    }
    bool checkIdent(const std::string& s) const {
        return cur().kind == Tok::Ident && cur().text == s;
    }

    bool matchPunct(const std::string& p) {
        if (checkPunct(p)) {
            advance();
            return true;
        }
        return false;
    }
    bool matchIdent(const std::string& s) {
        if (checkIdent(s)) {
            advance();
            return true;
        }
        return false;
    }

    void expectPunct(const std::string& p) {
        if (!matchPunct(p)) fail("expected '" + p + "'");
    }
    std::string expectIdent() {
        if (cur().kind != Tok::Ident) fail("expected identifier");
        std::string s = cur().text;
        advance();
        return s;
    }

    [[noreturn]] void fail(const std::string& msg) const {
        throw ParseError(msg + ", got '" + describe(cur()) + "'", cur().line, cur().col);
    }

    static std::string describe(const Token& t) {
        if (t.kind == Tok::End) return "<eof>";
        return t.text;
    }

    void skipToSemicolon() {
        while (!atEnd() && !checkPunct(";")) advance();
        if (checkPunct(";")) advance();
    }

    // ------------------------------------------------------------------
    // class structure
    // ------------------------------------------------------------------
    void parseClass(Program& prog) {
        while (isModifier(cur().text)) advance();

        if (!matchIdent("class")) fail("expected 'class'");
        std::string name = expectIdent();
        (void)name;   // one file, one class; the name is not emitted

        expectPunct("{");
        while (!atEnd() && !checkPunct("}")) {
            if (checkPunct(";")) {
                advance();
                continue;
            }
            parseClassMember(prog);
        }
        expectPunct("}");
    }

    static bool isModifier(const std::string& s) {
        return s == "public" || s == "private" || s == "protected" ||
               s == "static" || s == "internal" || s == "sealed" ||
               s == "abstract" || s == "partial" || s == "readonly";
    }

    void parseClassMember(Program& prog) {
        bool sawStatic = false;
        while (isModifier(cur().text)) {
            if (cur().text == "static") sawStatic = true;
            advance();
        }

        // The class is modelled as a plain container of globals and functions, so
        // instance members would have no meaning in the generated script.
        if (!sawStatic) {
            fail("类内成员请加 static（本模组把 class 当作静态容器，不支持实例字段/方法）");
        }

        if (cur().kind != Tok::Ident) fail("expected a field or method declaration");

        Type t = parseType();
        std::string name = expectIdent();

        if (checkPunct("(")) {
            parseMethod(prog, t, name);
        } else {
            parseField(prog, t, name);
        }
    }

    void parseField(Program& prog, const Type& t, const std::string& name) {
        VarDecl decl;
        decl.type = t;
        decl.name = name;
        if (matchPunct("=")) decl.init = parseExpression();
        expectPunct(";");

        GlobalVar gv;
        gv.decl = std::move(decl);
        prog.globals.push_back(std::move(gv));
    }

    void parseMethod(Program& prog, const Type& returnType, const std::string& name) {
        expectPunct("(");
        std::vector<Param> params;
        if (!checkPunct(")")) {
            do {
                Param p;
                p.type = parseType();
                p.name = expectIdent();
                params.push_back(std::move(p));
            } while (matchPunct(","));
        }
        expectPunct(")");

        FuncDecl fn;
        fn.returnType = returnType;
        fn.name = name;
        fn.line = cur().line;
        fn.params = std::move(params);

        // static void Main(string[] args) -- or the argument-less overload -- is
        // the entry point. The parameter is a language convention the game has no
        // concept of, so it is dropped and the body is hoisted to top-level code.
        if (name == "Main" && isVoid(returnType) && isEntryParams(fn.params)) {
            fn.params.clear();
            prog.entry = name;
        }

        fn.body = parseBlock();
        prog.functions.push_back(std::move(fn));
    }

    static bool isEntryParams(const std::vector<Param>& params) {
        if (params.empty()) return true;
        if (params.size() != 1) return false;
        const Type& t = params[0].type;
        return t.isVector && t.element && t.element->base == BaseType::String;
    }

    static bool isVoid(const Type& t) {
        return t.base == BaseType::Void && !t.isVector;
    }

    // ------------------------------------------------------------------
    // types
    // ------------------------------------------------------------------
    Type parseBaseType() {
        if (cur().kind != Tok::Ident) fail("expected a type name");
        std::string name = cur().text;
        advance();

        Type t;
        if (name == "int") t = Type::of(BaseType::Int);
        else if (name == "float" || name == "double") t = Type::of(BaseType::Float);
        else if (name == "bool") t = Type::of(BaseType::Bool);
        else if (name == "char") t = Type::of(BaseType::Char);
        else if (name == "string" || name == "String") t = Type::of(BaseType::String);
        else if (name == "void") t = Type::of(BaseType::Void);
        else if (name == "var") t = Type::of(BaseType::Unknown);   // inferred later
        else fail("unsupported type '" + name +
                  "' (supported: int float double bool char string void var)");
        return t;
    }

    Type parseType() {
        Type t = parseBaseType();

        if (matchPunct("[")) {
            expectPunct("]");
            if (checkPunct("[")) fail("only one-dimensional arrays are supported");
            Type inner = t;
            t = Type::of(BaseType::Unknown);
            t.isVector = true;
            t.element = std::make_shared<Type>(inner);
        }
        return t;
    }

    static bool isTypeKeyword(const std::string& s) {
        return s == "int" || s == "float" || s == "double" || s == "bool" ||
               s == "char" || s == "string" || s == "String" || s == "void" ||
               s == "var";
    }

    // ------------------------------------------------------------------
    // statements
    // ------------------------------------------------------------------
    StmtPtr parseBlock() {
        auto s = std::make_unique<Stmt>();
        s->kind = StmtKind::Block;
        s->line = cur().line;
        expectPunct("{");
        while (!atEnd() && !checkPunct("}")) {
            if (checkPunct(";")) {
                advance();
                continue;
            }
            s->body.push_back(parseStatement());
        }
        expectPunct("}");
        return s;
    }

    StmtPtr parseStatement() {
        const int line = cur().line;

        if (checkPunct("{")) return parseBlock();

        if (checkIdent("if")) return parseIf();
        if (checkIdent("while")) return parseWhile();
        if (checkIdent("for")) return parseFor();
        if (checkIdent("foreach")) return parseForeach();
        if (checkIdent("return")) return parseReturn();
        if (checkIdent("break") || checkIdent("continue")) return parseBreakContinue();
        if (cur().kind == Tok::Ident && isTypeKeyword(cur().text)) {
            return parseLocalDecl();
        }

        auto s = std::make_unique<Stmt>();
        s->kind = StmtKind::ExprStmt;
        s->line = line;
        s->value = parseExpression();
        expectPunct(";");
        return s;
    }

    StmtPtr parseLocalDecl() {
        auto s = std::make_unique<Stmt>();
        s->kind = StmtKind::VarDecl;
        s->line = cur().line;
        s->decl.type = parseType();
        s->decl.name = expectIdent();
        if (matchPunct("=")) s->decl.init = parseExpression();
        expectPunct(";");
        return s;
    }

    StmtPtr parseIf() {
        auto s = std::make_unique<Stmt>();
        s->kind = StmtKind::If;
        s->line = cur().line;
        advance();                                    // if
        expectPunct("(");
        s->cond = parseExpression();
        expectPunct(")");
        s->thenBranch = parseStatement();
        if (matchIdent("else")) s->elseBranch = parseStatement();
        return s;
    }

    StmtPtr parseWhile() {
        auto s = std::make_unique<Stmt>();
        s->kind = StmtKind::While;
        s->line = cur().line;
        advance();                                    // while
        expectPunct("(");
        s->cond = parseExpression();
        expectPunct(")");
        s->thenBranch = parseStatement();
        return s;
    }

    // C# foreach (Type v in collection) { ... }
    StmtPtr parseForeach() {
        auto s = std::make_unique<Stmt>();
        s->kind = StmtKind::RangeFor;
        s->line = cur().line;
        advance();                                    // foreach
        expectPunct("(");
        Type varType = parseType();
        std::string varName = expectIdent();
        if (!matchIdent("in")) fail("foreach 需要写成 foreach (类型 变量 in 集合)");
        ExprPtr range = parseExpression();
        expectPunct(")");
        s->loopVar = varName;
        s->rangeExpr = std::move(range);
        (void)varType;
        s->thenBranch = parseStatement();
        return s;
    }

    StmtPtr parseFor() {
        advance();                                    // for
        expectPunct("(");

        // Tolerate the Java-style for-each spelling in a for header.
        if (cur().kind == Tok::Ident && !checkPunct(";")) {
            std::size_t ahead = i_;
            int depth = 0;
            bool foundColon = false;
            while (ahead < toks_.size()) {
                const Token& t = toks_[ahead];
                if (t.kind == Tok::Punct && t.text == "(") depth++;
                if (t.kind == Tok::Punct && t.text == ")") depth--;
                if (t.kind == Tok::Punct && t.text == ";" && depth == 0) break;
                if (t.kind == Tok::Punct && t.text == ":" && depth == 0) {
                    foundColon = true;
                    break;
                }
                if (t.kind == Tok::End) break;
                ahead++;
            }
            if (foundColon) {
                Type varType = parseType();
                std::string varName = expectIdent();
                expectPunct(":");
                ExprPtr range = parseExpression();
                expectPunct(")");

                auto s = std::make_unique<Stmt>();
                s->kind = StmtKind::RangeFor;
                s->line = cur().line;
                s->loopVar = varName;
                s->rangeExpr = std::move(range);
                (void)varType;
                s->thenBranch = parseStatement();
                return s;
            }
        }

        auto s = std::make_unique<Stmt>();
        s->kind = StmtKind::For;
        s->line = cur().line;

        if (!checkPunct(";")) {
            if (cur().kind == Tok::Ident && isTypeKeyword(cur().text)) {
                s->init = parseLocalDecl();
            } else {
                auto init = std::make_unique<Stmt>();
                init->kind = StmtKind::ExprStmt;
                init->line = cur().line;
                init->value = parseExpression();
                expectPunct(";");
                s->init = std::move(init);
            }
        } else {
            expectPunct(";");
        }

        if (!checkPunct(";")) s->cond = parseExpression();
        expectPunct(";");

        if (!checkPunct(")")) s->step = parseExpression();
        expectPunct(")");

        s->thenBranch = parseStatement();
        return s;
    }

    StmtPtr parseReturn() {
        auto s = std::make_unique<Stmt>();
        s->kind = StmtKind::Return;
        s->line = cur().line;
        advance();                                    // return
        if (!checkPunct(";")) s->value = parseExpression();
        expectPunct(";");
        return s;
    }

    StmtPtr parseBreakContinue() {
        auto s = std::make_unique<Stmt>();
        s->line = cur().line;
        if (matchIdent("break")) s->kind = StmtKind::Break;
        else {
            advance();                                 // continue
            s->kind = StmtKind::Continue;
        }
        expectPunct(";");
        return s;
    }

    // ------------------------------------------------------------------
    // expressions
    // ------------------------------------------------------------------
    ExprPtr parseExpression() { return parseAssignment(); }

    ExprPtr parseAssignment() {
        ExprPtr lhs = parseOr();
        if (checkPunct("?")) {
            fail("三元表达式 a ? b : c 不受支持，请改写为 if/else");
        }
        if (checkPunct("&") || checkPunct("|") || checkPunct("^") ||
            checkPunct("<<") || checkPunct(">>")) {
            fail("位运算不受支持，请改用算术或布尔运算");
        }
        if (cur().kind == Tok::Punct && isAssignOp(cur().text)) {
            std::string op = cur().text;
            advance();
            ExprPtr rhs = parseAssignment();
            auto e = std::make_unique<Expr>();
            e->kind = ExprKind::Binary;
            e->op = op;
            e->line = lhs->line;
            e->lhs = std::move(lhs);
            e->rhs = std::move(rhs);
            return e;
        }
        return lhs;
    }

    static bool isAssignOp(const std::string& s) {
        return s == "=" || s == "+=" || s == "-=" || s == "*=" || s == "/=" || s == "%=";
    }

    ExprPtr parseOr() {
        ExprPtr lhs = parseAnd();
        while (matchPunct("||")) lhs = binary(std::move(lhs), "||", parseAnd());
        return lhs;
    }

    ExprPtr parseAnd() {
        ExprPtr lhs = parseEquality();
        while (matchPunct("&&")) lhs = binary(std::move(lhs), "&&", parseEquality());
        return lhs;
    }

    ExprPtr parseEquality() {
        ExprPtr lhs = parseComparison();
        while (checkPunct("==") || checkPunct("!=")) {
            std::string op = cur().text;
            advance();
            lhs = binary(std::move(lhs), op, parseComparison());
        }
        return lhs;
    }

    ExprPtr parseComparison() {
        ExprPtr lhs = parseAdditive();
        while (checkPunct("<") || checkPunct("<=") || checkPunct(">") || checkPunct(">=")) {
            std::string op = cur().text;
            advance();
            lhs = binary(std::move(lhs), op, parseAdditive());
        }
        return lhs;
    }

    ExprPtr parseAdditive() {
        ExprPtr lhs = parseMultiplicative();
        while (checkPunct("+") || checkPunct("-")) {
            std::string op = cur().text;
            advance();
            lhs = binary(std::move(lhs), op, parseMultiplicative());
        }
        return lhs;
    }

    ExprPtr parseMultiplicative() {
        ExprPtr lhs = parseUnary();
        while (checkPunct("*") || checkPunct("/") || checkPunct("%")) {
            std::string op = cur().text;
            advance();
            lhs = binary(std::move(lhs), op, parseUnary());
        }
        return lhs;
    }

    ExprPtr parseUnary() {
        if (checkPunct("!") || checkPunct("-") || checkPunct("+")) {
            std::string op = cur().text;
            const int line = cur().line;
            advance();
            auto e = std::make_unique<Expr>();
            e->kind = ExprKind::Unary;
            e->op = op;
            e->line = line;
            e->lhs = parseUnary();
            return e;
        }
        if (checkPunct("++") || checkPunct("--")) {
            std::string op = cur().text;
            const int line = cur().line;
            advance();
            auto e = std::make_unique<Expr>();
            e->kind = ExprKind::Binary;
            e->op = op;
            e->line = line;
            e->lhs = parseUnary();
            return e;
        }
        return parsePostfix();
    }

    ExprPtr parsePostfix() {
        ExprPtr e = parsePrimary();
        for (;;) {
            if (checkPunct("[")) {                     // a[i]
                advance();
                ExprPtr index = parseExpression();
                expectPunct("]");
                auto idx = std::make_unique<Expr>();
                idx->kind = ExprKind::Index;
                idx->line = e->line;
                idx->lhs = std::move(e);
                idx->rhs = std::move(index);
                e = std::move(idx);
                continue;
            }
            if (checkPunct(".")) {                     // .Length / Entities.Tree
                advance();
                std::string member = expectIdent();
                if (checkPunct("(")) {
                    fail("不支持对象方法调用 '." + member +
                         "()'；C# 只支持数组 .Length 和游戏枚举（如 Entities.Tree）");
                }
                auto m = std::make_unique<Expr>();
                m->kind = ExprKind::Member;
                m->line = e->line;
                m->lhs = std::move(e);
                m->member = member;
                e = std::move(m);
                continue;
            }
            if (checkPunct("++") || checkPunct("--")) {  // i++ / i--
                std::string op = cur().text;
                const int line = cur().line;
                advance();
                auto b = std::make_unique<Expr>();
                b->kind = ExprKind::Binary;
                b->op = op;
                b->line = line;
                b->lhs = std::move(e);
                e = std::move(b);
                continue;
            }
            break;
        }
        return e;
    }

    ExprPtr parsePrimary() {
        const int line = cur().line;

        // C# keywords / operators this subset refuses, reported by name instead
        // of being misparsed.
        if (cur().kind == Tok::Ident) {
            static const char* unsupported[] = {
                "switch", "case", "default", "do", "this", "base", "throw", "try",
                "catch", "finally", "interface", "enum", "namespace", "struct",
                "const", "ref", "out", "params", "goto", "yield", "lock", "checked",
                "unchecked", "delegate", "event", "operator", "typeof", "sizeof",
                "virtual", "override", "explicit", "implicit", "is", "as", "in",
                "async", "await",
            };
            for (const char* w : unsupported) {
                if (cur().text == w) {
                    fail("C# 关键字 '" + cur().text + "' 不受支持");
                }
            }
        }

        if (cur().kind == Tok::IntLit) {
            auto e = std::make_unique<Expr>();
            e->kind = ExprKind::IntLit;
            e->line = line;
            e->ival = std::stoll(cur().text);
            advance();
            return e;
        }
        if (cur().kind == Tok::FloatLit) {
            auto e = std::make_unique<Expr>();
            e->kind = ExprKind::FloatLit;
            e->line = line;
            e->fval = std::stod(cur().text);
            advance();
            return e;
        }
        if (cur().kind == Tok::StringLit) {
            auto e = std::make_unique<Expr>();
            e->kind = ExprKind::StringLit;
            e->line = line;
            e->sval = cur().text;
            advance();
            return e;
        }
        if (cur().kind == Tok::CharLit) {
            auto e = std::make_unique<Expr>();
            e->kind = ExprKind::CharLit;
            e->line = line;
            e->sval = cur().text;
            advance();
            return e;
        }
        if (checkPunct("(")) {
            advance();
            ExprPtr inner = parseExpression();
            expectPunct(")");
            return inner;
        }

        if (cur().kind == Tok::Ident) {
            std::string name = cur().text;

            if (name == "true" || name == "false") {
                advance();
                auto e = std::make_unique<Expr>();
                e->kind = ExprKind::BoolLit;
                e->line = line;
                e->bval = (name == "true");
                return e;
            }
            if (name == "null") {
                advance();
                auto e = std::make_unique<Expr>();
                e->kind = ExprKind::NullLit;
                e->line = line;
                return e;
            }

            // Console.WriteLine(...) / Console.Write(...) -> print
            if (name == "Console" && peek().kind == Tok::Punct && peek().text == "." &&
                peek(2).kind == Tok::Ident &&
                (peek(2).text == "WriteLine" || peek(2).text == "Write")) {
                std::string callee = (peek(2).text == "WriteLine") ? "println" : "print";
                for (int k = 0; k < 3; ++k) advance();  // Console . WriteLine
                return parseCall(callee, line);
            }
            // System.Console.WriteLine(...) -- tolerate the fully qualified form.
            if (name == "System" && peek().kind == Tok::Punct && peek().text == "." &&
                peek(2).kind == Tok::Ident && peek(2).text == "Console" &&
                peek(3).kind == Tok::Punct && peek(3).text == "." &&
                peek(4).kind == Tok::Ident &&
                (peek(4).text == "WriteLine" || peek(4).text == "Write")) {
                std::string callee = (peek(4).text == "WriteLine") ? "println" : "print";
                for (int k = 0; k < 5; ++k) advance();
                return parseCall(callee, line);
            }

            // Math.<Member>(...) / Math.PI  (PascalCase in C#)
            if (name == "Math" && peek().kind == Tok::Punct && peek().text == "." &&
                peek(2).kind == Tok::Ident) {
                std::string what = peek(2).text;
                if (what == "PI" && peek(3).kind != Tok::Punct) {
                    for (int k = 0; k < 3; ++k) advance();
                    auto e = std::make_unique<Expr>();
                    e->kind = ExprKind::FloatLit;
                    e->line = line;
                    e->fval = 3.14159265358979323846;
                    return e;
                }
                if (peek(3).kind == Tok::Punct && peek(3).text == "(") {
                    std::string mapped;
                    if (what == "Sqrt") mapped = "sqrt";
                    else if (what == "Pow") mapped = "pow";
                    else if (what == "Abs") mapped = "abs";
                    else if (what == "Min") mapped = "min";
                    else if (what == "Max") mapped = "max";
                    else if (what == "Floor") mapped = "floor";
                    else if (what == "Ceiling") mapped = "ceil";
                    else if (what == "Round") mapped = "round";
                    else fail("unsupported Math member 'Math." + what + "'");
                    for (int k = 0; k < 3; ++k) advance();  // Math . Fn
                    return parseCall(mapped, line);
                }
                fail("unsupported Math member 'Math." + what + "'");
            }

            // new int[n]  /  new int[] { ... }
            if (name == "new") {
                advance();
                Type elemType = parseBaseType();
                if (matchPunct("[")) {
                    ExprPtr size = parseExpression();
                    expectPunct("]");
                    if (matchPunct("{")) {
                        return parseArrayLiteral(line);
                    }
                    auto e = std::make_unique<Expr>();
                    e->kind = ExprKind::Call;
                    e->line = line;
                    e->callee = "__array__";
                    e->type.isVector = true;
                    e->type.element = std::make_shared<Type>(elemType);
                    e->args.push_back(std::move(size));
                    return e;
                }
                fail("'new' only supports arrays (new int[n]); object instantiation "
                     "is not available in the target language");
            }

            advance();
            if (checkPunct("(")) return parseCall(name, line);

            auto e = std::make_unique<Expr>();
            e->kind = ExprKind::Ident;
            e->line = line;
            e->sval = name;
            return e;
        }

        if (checkPunct("{")) {                         // {1, 2, 3}
            return parseArrayLiteral(line);
        }

        fail("unexpected token in expression");
    }

    ExprPtr parseCall(const std::string& callee, int line) {
        auto e = std::make_unique<Expr>();
        e->kind = ExprKind::Call;
        e->line = line;
        e->callee = callee;
        expectPunct("(");
        if (!checkPunct(")")) {
            do {
                e->args.push_back(parseExpression());
            } while (matchPunct(","));
        }
        expectPunct(")");
        return e;
    }

    ExprPtr parseArrayLiteral(int line) {
        auto e = std::make_unique<Expr>();
        e->kind = ExprKind::InitList;
        e->line = line;
        expectPunct("{");
        if (!checkPunct("}")) {
            do {
                e->args.push_back(parseExpression());
            } while (matchPunct(","));
        }
        expectPunct("}");
        return e;
    }

    ExprPtr binary(ExprPtr lhs, const std::string& op, ExprPtr rhs) {
        auto e = std::make_unique<Expr>();
        e->kind = ExprKind::Binary;
        e->op = op;
        e->line = lhs->line;
        e->lhs = std::move(lhs);
        e->rhs = std::move(rhs);
        return e;
    }
};

}  // namespace csfarmer
