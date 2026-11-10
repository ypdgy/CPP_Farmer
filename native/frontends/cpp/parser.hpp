// Recursive-descent parser for the C++ subset accepted by the C++ Farmer front-end.
#pragma once

#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

#include "ast.hpp"
#include "lexer.hpp"

namespace cppfarmer {

class ParseError : public std::runtime_error {
public:
    ParseError(const std::string& msg, int line, int col)
        : std::runtime_error(msg), line(line), col(col) {}
    int line;
    int col;
};

class Parser {
public:
    explicit Parser(std::vector<Token> toks) : toks_(std::move(toks)) {}

    Program parseProgram() {
        Program prog;
        while (!atEnd()) {
            if (checkPunct(";")) {
                advance();
                continue;
            }
            parseTopLevel(prog);
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
    bool checkKeyword(const std::string& s) const { return checkIdent(s); }

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

    // ------------------------------------------------------------------
    // types
    // ------------------------------------------------------------------
    static bool isTypeWord(const std::string& s) {
        static const char* words[] = {"int",  "float",  "double", "bool", "char",
                                      "void", "string", "auto",   "long", "short",
                                      "unsigned", "signed", "size_t", "int64_t",
                                      "uint64_t", "int32_t", "uint32_t", "byte"};
        for (const char* w : words) {
            if (s == w) return true;
        }
        return false;
    }

    // True when the current token starts a type.
    bool atTypeStart() const {
        if (cur().kind != Tok::Ident) return false;
        const std::string& s = cur().text;
        if (isTypeWord(s)) return true;
        if (s == "const") return true;
        if (s == "struct" || s == "enum" || s == "class") return false;
        // user type: Ident followed by Ident  (Foo x)  or Ident followed by '::'
        if (peek().kind == Tok::Ident && isTypeWord(peek().text)) return false;
        if (peek().kind == Tok::Ident) return true;
        // std::vector<...>  /  vector<...>  /  std::string
        if (s == "std" && peek().kind == Tok::Punct && peek().text == "::") {
            const Token& next = peek(2);
            if (next.kind != Tok::Ident) return false;
            static const char* stdTypes[] = {"vector", "map",  "set",  "string",
                                             "pair",   "array", "deque", "unordered_map",
                                             "unordered_set", "list"};
            for (const char* t : stdTypes) {
                if (next.text == t) return true;
            }
            return isTypeWord(next.text);
        }
        if ((s == "vector" || s == "map" || s == "set" || s == "unordered_map" ||
             s == "unordered_set" || s == "pair" || s == "array") &&
            peek().kind == Tok::Punct && (peek().text == "<" || peek().text == "::")) {
            return true;
        }
        return false;
    }

    Type parseType() {
        Type t;
        bool sawVector = false;

        // leading cv-qualifiers
        while (checkIdent("const") || checkIdent("static") || checkIdent("inline") ||
               checkIdent("constexpr")) {
            advance();
        }

        if (checkIdent("std") && peek().kind == Tok::Punct && peek().text == "::") {
            advance();
            advance();
        }

        if (cur().kind != Tok::Ident) fail("expected a type name");
        std::string name = cur().text;

        if (name == "vector" || name == "array" || name == "deque" || name == "list") {
            sawVector = true;
            advance();
            Type elem;
            if (matchPunct("<")) {
                // skip any leading 'std::'
                elem = parseType();
                // support nested vector<vector<T>>
                while (matchPunct(",")) {  // std::array<T, N>
                    parseExpression();
                }
                expectPunct(">");
            }
            t = Type::of(BaseType::Unknown);
            t.isVector = true;
            t.element = std::make_shared<Type>(elem);
            return t;
        }
        if (name == "map" || name == "unordered_map" || name == "multimap") {
            advance();
            Type key, val;
            if (matchPunct("<")) {
                key = parseType();
                expectPunct(",");
                val = parseType();
                expectPunct(">");
            }
            t = Type::of(BaseType::Unknown);
            t.isMap = true;
            t.key = std::make_shared<Type>(key);
            t.element = std::make_shared<Type>(val);
            return t;
        }
        // Checked here rather than at statement start: a player may legitimately
        // call their own function named set() or pair(), only the type form is
        // unsupported. The grammar would otherwise fail with
        // "expected identifier, got '<'", which hides what went wrong.
        if (name == "set" || name == "unordered_set" || name == "multiset") {
            throw ParseError("不支持 std::set，请用 std::vector 承载", cur().line, cur().col);
        }
        if (name == "pair") {
            throw ParseError("不支持 std::pair，请用 struct 代替", cur().line, cur().col);
        }

        // scalar / named type, possibly several words (unsigned long long)
        BaseType base = BaseType::Unknown;
        bool consumedAny = false;
        while (cur().kind == Tok::Ident && isTypeWord(cur().text)) {
            const std::string& w = cur().text;
            if (w == "int" || w == "size_t" || w == "int64_t" || w == "int32_t" ||
                w == "uint32_t" || w == "uint64_t" || w == "long" || w == "short" ||
                w == "unsigned" || w == "signed") {
                base = BaseType::Int;
            } else if (w == "float" || w == "double") {
                base = BaseType::Float;
            } else if (w == "bool") {
                base = BaseType::Bool;
            } else if (w == "char" || w == "byte") {
                base = BaseType::Char;
            } else if (w == "string") {
                base = BaseType::String;
            } else if (w == "void") {
                base = BaseType::Void;
            } else if (w == "auto") {
                base = BaseType::Unknown;
            }
            advance();
            consumedAny = true;
        }

        if (!consumedAny) {
            // user-defined type (struct) - the name has not been consumed yet
            advance();
            t = Type::of(BaseType::Unknown);
            t.userType = name;
            while (checkPunct("&") || checkPunct("*")) advance();
            return t;
        }

        // trailing references / pointers are meaningless in the target language
        while (checkPunct("&") || checkPunct("*")) advance();

        t = Type::of(base);
        (void)sawVector;
        return t;
    }

    // ------------------------------------------------------------------
    // rejected constructs
    // ------------------------------------------------------------------
    // Valid C++ this dialect does not implement. Letting the grammar fail on
    // them produces "expected ';', got '{'", which says nothing about why the
    // line was refused -- and for `namespace` / `template` the old code even
    // skipped the block silently, so the functions inside vanished.
    static const char* unsupportedReason(const std::string& w) {
        if (w == "switch") return "不支持 switch，请改用 if / else if";
        if (w == "case") return "case 只能配 switch 使用，本模组不支持 switch";
        if (w == "default") return "default 只能配 switch 使用，本模组不支持 switch";
        if (w == "sizeof") return "不支持 sizeof";
        if (w == "new") return "不支持 new，直接声明变量或用 std::vector";
        if (w == "delete") return "不支持 delete，内存由游戏管理";
        if (w == "operator") return "不支持运算符重载";
        if (w == "this") return "不支持 this，直接用变量名或字段名";
        if (w == "virtual" || w == "override") return "不支持继承与虚函数";
        if (w == "template" || w == "typename") return "不支持模板";
        if (w == "namespace") return "不支持 namespace 块（内容会被整块跳过），请写在文件顶层";
        return nullptr;
    }

    [[noreturn]] void rejectUnsupported(const std::string& w) const {
        const char* why = unsupportedReason(w);
        throw ParseError(why ? why : ("不支持 '" + w + "'"),
                         cur().line, cur().col);
    }

    // Called where a statement or top-level declaration starts.
    void rejectUnsupportedKeyword() const {
        if (cur().kind != Tok::Ident) return;
        if (unsupportedReason(cur().text)) rejectUnsupported(cur().text);
    }

    // ------------------------------------------------------------------
    // top level
    // ------------------------------------------------------------------
    // The editor API header only exists for IDE completion; there is nothing to
    // import and no window to open for it. Both  #include "cppfarmer_api.hpp"
    // and  #include <cppfarmer_api.hpp>  are dropped here.
    static bool isEditorHeaderInclude(const std::string& name) {
        const std::string expect = "cppfarmer_api.hpp";
        size_t slash = name.find_last_of("/\\");
        std::string base = (slash == std::string::npos) ? name : name.substr(slash + 1);
        if (base.size() != expect.size()) return false;
        for (size_t i = 0; i < base.size(); ++i) {
            char a = base[i];
            if (a >= 'A' && a <= 'Z') a = static_cast<char>(a - 'A' + 'a');
            if (a != expect[i]) return false;
        }
        return true;
    }

    void parseTopLevel(Program& prog) {
        if (cur().kind == Tok::Include) {
            if (isEditorHeaderInclude(cur().text)) {
                advance();                      // IDE-only stub: emit nothing
                return;
            }
            auto include = std::make_unique<Stmt>();
            include->kind = StmtKind::Include;
            include->line = cur().line;
            include->sval = cur().text;
            advance();
            prog.imports.push_back(std::move(include));
            return;
        }
        rejectUnsupportedKeyword();
        if (checkIdent("struct") || checkIdent("class")) {
            parseStruct(prog);
            return;
        }
        if (checkIdent("enum")) {
            parseEnum(prog);
            return;
        }
        if (checkIdent("using") || checkIdent("typedef")) {
            skipDirective();
            return;
        }

        // function definition or global variable
        std::size_t save = i_;
        const int declLine = cur().line;
        Type type = parseType();
        if (cur().kind != Tok::Ident) {
            i_ = save;
            fail("expected a declaration");
        }
        std::string name = cur().text;
        advance();

        if (checkPunct("(")) {
            FuncDecl fn;
            fn.returnType = type;
            fn.name = name;
            fn.line = declLine;
            advance();
            parseParams(fn);
            expectPunct(")");
            if (matchPunct(";")) return;  // prototype
            fn.body = parseBlock();
            if (name == "main") prog.entry = "main";
            prog.functions.push_back(std::move(fn));
            return;
        }

        // global variable
        i_ = save;
        GlobalVar gv;
        gv.decl = parseVarDecl();
        prog.globals.push_back(std::move(gv));
    }

    // Skips the constructs the front-end accepts but has no semantics for:
    // `using ...;`, `typedef ...;` and `static_assert(...);`.
    //
    // The terminator is mandatory. The loop used to just stop at end of file,
    // so forgetting the ';' after `using namespace std` silently swallowed
    // every statement below it: the script compiled to (almost) nothing,
    // reported no error and appeared to "just stop".
    void skipDirective() {
        const Token start = cur();
        int depth = 0;
        while (!atEnd()) {
            if (checkPunct("{")) {
                ++depth;
                advance();
                continue;
            }
            if (checkPunct("}")) {
                --depth;
                advance();
                continue;
            }
            if (checkPunct(";") && depth == 0) {
                advance();
                return;
            }
            advance();
        }
        throw ParseError("'" + start.text + "' 后面缺少结尾的 ';'（一直扫到文件末尾都没找到）",
                         start.line, start.col);
    }

    void parseStruct(Program& prog) {
        advance();  // struct / class
        StructDecl sd;
        sd.name = expectIdent();
        // optional inheritance
        if (checkPunct(":")) {
            while (!checkPunct("{") && !atEnd()) advance();
        }
        expectPunct("{");
        while (!checkPunct("}") && !atEnd()) {
            if (matchIdent("public") || matchIdent("private") || matchIdent("protected")) {
                matchPunct(":");
                continue;
            }
            if (matchIdent("static")) continue;
            FieldDecl fd;
            fd.type = parseType();
            fd.name = expectIdent();
            if (matchPunct("=")) parseExpression();
            expectPunct(";");
            sd.fields.push_back(std::move(fd));
        }
        expectPunct("}");
        matchPunct(";");
        prog.structs.push_back(std::move(sd));
    }

    void parseEnum(Program& prog) {
        advance();  // enum
        matchIdent("class");
        matchIdent("struct");
        EnumDecl ed;
        ed.name = expectIdent();
        if (checkPunct(":")) {  // enum E : int
            advance();
            parseType();
        }
        expectPunct("{");
        while (!checkPunct("}") && !atEnd()) {
            std::string member = expectIdent();
            ExprPtr value;
            if (matchPunct("=")) {
                value = parseExpression();
            }
            ed.members.emplace_back(member, std::move(value));
            if (!matchPunct(",")) break;
        }
        expectPunct("}");
        matchPunct(";");
        prog.enums.push_back(std::move(ed));
    }

    void parseParams(FuncDecl& fn) {
        if (checkPunct(")")) return;
        // (void)
        if (checkIdent("void") && peek().kind == Tok::Punct && peek().text == ")") {
            advance();
            return;
        }
        do {
            Param p;
            p.type = parseType();
            p.name = expectIdent();
            // int a[10] as parameter - drop the size
            if (matchPunct("[")) {
                if (!checkPunct("]")) parseExpression();
                expectPunct("]");
            }
            if (matchPunct("=")) p.defaultValue = parseExpression();
            fn.params.push_back(std::move(p));
        } while (matchPunct(","));
    }

    // ------------------------------------------------------------------
    // statements
    // ------------------------------------------------------------------
    StmtPtr parseBlock() {
        auto block = std::make_unique<Stmt>();
        block->kind = StmtKind::Block;
        block->line = cur().line;
        expectPunct("{");
        while (!checkPunct("}") && !atEnd()) {
            block->body.push_back(parseStatement());
        }
        expectPunct("}");
        return block;
    }

    StmtPtr parseStatement() {
        int line = cur().line;

        if (cur().kind == Tok::Include) {
            fail("#include 只能写在文件顶层，不能放在函数或代码块里");
        }
        rejectUnsupportedKeyword();
        if (checkPunct("{")) return parseBlock();
        if (checkPunct(";")) {
            advance();
            auto s = std::make_unique<Stmt>();
            s->kind = StmtKind::Empty;
            s->line = line;
            return s;
        }
        if (checkIdent("if")) return parseIf();
        if (checkIdent("while")) return parseWhile();
        if (checkIdent("for")) return parseFor();
        if (checkIdent("do")) return parseDoWhile();

        if (checkIdent("return")) {
            advance();
            auto s = std::make_unique<Stmt>();
            s->kind = StmtKind::Return;
            s->line = line;
            if (!checkPunct(";")) s->value = parseExpression();
            expectPunct(";");
            return s;
        }
        if (checkIdent("break")) {
            advance();
            expectPunct(";");
            auto s = std::make_unique<Stmt>();
            s->kind = StmtKind::Break;
            s->line = line;
            return s;
        }
        if (checkIdent("continue")) {
            advance();
            expectPunct(";");
            auto s = std::make_unique<Stmt>();
            s->kind = StmtKind::Continue;
            s->line = line;
            return s;
        }
        // statements we deliberately ignore but must consume
        if (checkIdent("using") || checkIdent("typedef") || checkIdent("static_assert")) {
            skipDirective();
            auto s = std::make_unique<Stmt>();
            s->kind = StmtKind::Empty;
            s->line = line;
            return s;
        }

        // declaration?
        if (atTypeStart()) {
            auto s = std::make_unique<Stmt>();
            s->kind = StmtKind::VarDecl;
            s->line = line;
            s->decl = parseVarDecl();
            return s;
        }

        // expression statement
        auto s = std::make_unique<Stmt>();
        s->kind = StmtKind::ExprStmt;
        s->line = line;
        s->value = parseExpression();
        expectPunct(";");
        return s;
    }

    VarDecl parseVarDecl() {
        VarDecl d;
        if (checkIdent("const") || checkIdent("constexpr") || checkIdent("static")) {
            d.isConst = true;
            advance();
        }
        d.type = parseType();
        d.name = expectIdent();

        if (matchPunct("[")) {
            if (!checkPunct("]")) d.arraySize = parseExpression();
            expectPunct("]");
        }
        if (matchPunct("=")) {
            d.init = parseExpression();
        }
        expectPunct(";");
        return d;
    }

    StmtPtr parseIf() {
        auto s = std::make_unique<Stmt>();
        s->kind = StmtKind::If;
        s->line = cur().line;
        advance();  // if
        expectPunct("(");
        s->cond = parseExpression();
        expectPunct(")");
        s->thenBranch = parseStatement();
        if (matchIdent("else")) {
            if (checkIdent("if")) {
                s->elseBranch = parseIf();
            } else {
                s->elseBranch = parseStatement();
            }
        }
        return s;
    }

    StmtPtr parseWhile() {
        auto s = std::make_unique<Stmt>();
        s->kind = StmtKind::While;
        s->line = cur().line;
        advance();
        expectPunct("(");
        s->cond = parseExpression();
        expectPunct(")");
        s->thenBranch = parseStatement();
        return s;
    }

    StmtPtr parseDoWhile() {
        advance();  // do
        auto body = parseStatement();
        if (!matchIdent("while")) fail("expected 'while' after do-body");
        expectPunct("(");
        auto cond = parseExpression();
        expectPunct(")");
        expectPunct(";");

        // do { B } while (C)  =>  B ; while C: B
        auto s = std::make_unique<Stmt>();
        s->kind = StmtKind::For;  // re-use For as a while loop with a body prefix
        s->line = cur().line;
        s->cond = std::move(cond);
        s->thenBranch = std::move(body);
        s->init = nullptr;
        s->step = nullptr;
        return s;
    }

    StmtPtr parseFor() {
        advance();  // for
        expectPunct("(");

        // range-for:  for (int x : v)
        {
            std::size_t save = i_;
            bool looksRange = false;
            std::string varName;
            if (atTypeStart()) {
                parseType();
                if (cur().kind == Tok::Ident) {
                    varName = cur().text;
                    advance();
                    if (checkPunct(":")) looksRange = true;
                }
            }
            if (looksRange) {
                advance();  // ':'
                auto s = std::make_unique<Stmt>();
                s->kind = StmtKind::RangeFor;
                s->line = cur().line;
                s->loopVar = varName;
                s->rangeExpr = parseExpression();
                expectPunct(")");
                s->thenBranch = parseStatement();
                return s;
            }
            i_ = save;
        }

        auto s = std::make_unique<Stmt>();
        s->kind = StmtKind::For;
        s->line = cur().line;

        // init
        if (checkPunct(";")) {
            advance();
        } else if (atTypeStart()) {
            auto initStmt = std::make_unique<Stmt>();
            initStmt->kind = StmtKind::VarDecl;
            initStmt->decl = parseVarDecl();
            s->init = std::move(initStmt);
        } else {
            auto initStmt = std::make_unique<Stmt>();
            initStmt->kind = StmtKind::ExprStmt;
            initStmt->value = parseExpression();
            expectPunct(";");
            s->init = std::move(initStmt);
        }

        if (!checkPunct(";")) s->cond = parseExpression();
        expectPunct(";");
        if (!checkPunct(")")) s->step = parseExpression();
        expectPunct(")");
        s->thenBranch = parseStatement();
        return s;
    }

    // ------------------------------------------------------------------
    // expressions
    // ------------------------------------------------------------------
    ExprPtr parseExpression() { return parseAssignment(); }

    ExprPtr makeBinary(std::string op, ExprPtr l, ExprPtr r, int line) {
        auto e = std::make_unique<Expr>();
        e->kind = ExprKind::Binary;
        e->op = std::move(op);
        e->lhs = std::move(l);
        e->rhs = std::move(r);
        e->line = line;
        return e;
    }

    ExprPtr parseAssignment() {
        auto lhs = parseTernary();
        if (cur().kind == Tok::Punct) {
            const std::string& op = cur().text;
            if (op == "=" || op == "+=" || op == "-=" || op == "*=" || op == "/=" ||
                op == "%=" || op == "<<=" || op == ">>=") {
                int line = cur().line;
                advance();
                auto rhs = parseAssignment();
                auto e = std::make_unique<Expr>();
                e->kind = ExprKind::Binary;
                e->op = op;
                e->lhs = std::move(lhs);
                e->rhs = std::move(rhs);
                e->line = line;
                return e;
            }
        }
        return lhs;
    }

    ExprPtr parseTernary() {
        auto cond = parseBinary(0);
        if (checkPunct("?")) {
            int line = cur().line;
            advance();
            auto thenE = parseAssignment();
            expectPunct(":");
            auto elseE = parseAssignment();
            auto e = std::make_unique<Expr>();
            e->kind = ExprKind::Ternary;
            e->cond = std::move(cond);
            e->lhs = std::move(thenE);
            e->rhs = std::move(elseE);
            e->line = line;
            return e;
        }
        return cond;
    }

    struct OpInfo {
        const char* text;
        int prec;
    };

    static int precedenceOf(const std::string& op) {
        if (op == "||") return 1;
        if (op == "&&") return 2;
        if (op == "|") return 3;
        if (op == "^") return 4;
        if (op == "&") return 5;
        if (op == "==" || op == "!=") return 6;
        if (op == "<" || op == "<=" || op == ">" || op == ">=") return 7;
        if (op == "<<" || op == ">>") return 8;
        if (op == "+" || op == "-") return 9;
        if (op == "*" || op == "/" || op == "%") return 10;
        return 0;
    }

    ExprPtr parseBinary(int minPrec) {
        auto lhs = parseUnary();
        for (;;) {
            if (cur().kind != Tok::Punct) break;
            std::string op = cur().text;
            int prec = precedenceOf(op);
            if (prec == 0 || prec < minPrec) break;
            int line = cur().line;
            advance();
            auto rhs = parseBinary(prec + 1);
            lhs = makeBinary(op, std::move(lhs), std::move(rhs), line);
        }
        return lhs;
    }

    ExprPtr parseUnary() {
        int line = cur().line;
        if (cur().kind == Tok::Punct) {
            const std::string& op = cur().text;
            if (op == "!" || op == "-" || op == "+" || op == "~" || op == "&" || op == "*") {
                advance();
                auto operand = parseUnary();
                auto e = std::make_unique<Expr>();
                e->kind = ExprKind::Unary;
                e->op = op;
                e->lhs = std::move(operand);
                e->line = line;
                return e;
            }
            if (op == "++" || op == "--") {
                advance();
                auto operand = parseUnary();
                auto e = std::make_unique<Expr>();
                e->kind = ExprKind::Binary;
                e->op = op;  // handled by codegen as statement-level update
                e->lhs = std::move(operand);
                e->line = line;
                return e;
            }
        }
        return parsePostfix();
    }

    ExprPtr parsePostfix() {
        auto e = parseAtom();
        for (;;) {
            if (checkPunct("(")) {
                int line = cur().line;
                advance();
                auto call = std::make_unique<Expr>();
                call->kind = ExprKind::Call;
                call->line = line;
                if (e->kind == ExprKind::Member) {
                    call->lhs = std::move(e->lhs);
                    call->callee = e->member;
                } else if (e->kind == ExprKind::Ident) {
                    call->callee = e->sval;
                } else {
                    call->lhs = std::move(e);
                    call->callee = "";
                }
                if (!checkPunct(")")) {
                    do {
                        call->args.push_back(parseExpression());
                    } while (matchPunct(","));
                }
                expectPunct(")");
                e = std::move(call);
                continue;
            }
            if (checkPunct("[")) {
                int line = cur().line;
                advance();
                auto idx = std::make_unique<Expr>();
                idx->kind = ExprKind::Index;
                idx->line = line;
                idx->lhs = std::move(e);
                idx->rhs = parseExpression();
                expectPunct("]");
                e = std::move(idx);
                continue;
            }
            if (checkPunct(".") || checkPunct("->")) {
                advance();
                auto m = std::make_unique<Expr>();
                m->kind = ExprKind::Member;
                m->line = cur().line;
                m->lhs = std::move(e);
                m->member = expectIdent();
                e = std::move(m);
                continue;
            }
            if (checkPunct("++") || checkPunct("--")) {
                std::string op = cur().text;
                int line = cur().line;
                advance();
                auto b = std::make_unique<Expr>();
                b->kind = ExprKind::Binary;
                b->op = op;
                b->lhs = std::move(e);
                b->line = line;
                e = std::move(b);
                continue;
            }
            break;
        }
        return e;
    }

    ExprPtr parseAtom() {
        int line = cur().line;

        // Keywords that can only appear inside an expression we cannot model
        // (new/delete/sizeof/this). Reporting them by name here beats the
        // "expected ';', got 'int'" the grammar produces two tokens later.
        rejectUnsupportedKeyword();
        if (cur().kind == Tok::IntLit) {
            auto e = std::make_unique<Expr>();
            e->kind = ExprKind::IntLit;
            e->ival = std::stoll(cur().text);
            e->type = Type::of(BaseType::Int);
            e->line = line;
            advance();
            // integer suffixes like 10L / 10u
            if (cur().kind == Tok::Ident && (cur().text == "L" || cur().text == "u" ||
                                             cur().text == "U" || cur().text == "LL")) {
                advance();
            }
            return e;
        }
        if (cur().kind == Tok::FloatLit) {
            auto e = std::make_unique<Expr>();
            e->kind = ExprKind::FloatLit;
            e->fval = std::stod(cur().text);
            e->type = Type::of(BaseType::Float);
            e->line = line;
            advance();
            return e;
        }
        if (cur().kind == Tok::StringLit) {
            auto e = std::make_unique<Expr>();
            e->kind = ExprKind::StringLit;
            e->sval = cur().text;
            e->type = Type::of(BaseType::String);
            e->line = line;
            advance();
            return e;
        }
        if (cur().kind == Tok::CharLit) {
            auto e = std::make_unique<Expr>();
            e->kind = ExprKind::CharLit;
            e->sval = cur().text;
            e->type = Type::of(BaseType::Char);
            e->line = line;
            advance();
            return e;
        }

        if (checkPunct("(")) {
            advance();
            auto e = parseExpression();
            expectPunct(")");
            return e;
        }
        if (checkPunct("{")) {
            advance();
            auto e = std::make_unique<Expr>();
            e->kind = ExprKind::InitList;
            e->line = line;
            if (!checkPunct("}")) {
                do {
                    e->args.push_back(parseExpression());
                } while (matchPunct(","));
            }
            expectPunct("}");
            return e;
        }

        if (cur().kind != Tok::Ident) fail("expected an expression");

        std::string name = cur().text;
        advance();

        // std:: prefix is transparent; other qualifiers (Enum::Member) are kept
        if (name == "std" && checkPunct("::")) {
            advance();
            name = expectIdent();
        } else {
            while (checkPunct("::")) {
                advance();
                std::string member = expectIdent();
                name = name + "::" + member;
            }
        }

        if (name == "true" || name == "false") {
            auto e = std::make_unique<Expr>();
            e->kind = ExprKind::BoolLit;
            e->bval = (name == "true");
            e->type = Type::of(BaseType::Bool);
            e->line = line;
            return e;
        }
        if (name == "nullptr" || name == "NULL") {
            auto e = std::make_unique<Expr>();
            e->kind = ExprKind::NullLit;
            e->line = line;
            return e;
        }
        if (name == "this") {
            auto e = std::make_unique<Expr>();
            e->kind = ExprKind::Ident;
            e->sval = "self";
            e->line = line;
            return e;
        }
        // type used as a call, e.g. int(x), static_cast<T>(x)
        if (name == "static_cast" || name == "reinterpret_cast" || name == "const_cast") {
            expectPunct("<");
            parseType();
            expectPunct(">");
            expectPunct("(");
            auto inner = parseExpression();
            expectPunct(")");
            return inner;
        }

        auto e = std::make_unique<Expr>();
        e->kind = ExprKind::Ident;
        e->sval = name;
        e->line = line;
        return e;
    }
};

}  // namespace cppfarmer
