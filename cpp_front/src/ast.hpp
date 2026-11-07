// AST definitions for the C++ Farmer subset front-end.
#pragma once

#include <memory>
#include <string>
#include <vector>

namespace cppfarmer {

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
// The target language only has dynamic numbers (all doubles).  We keep a coarse
// static type so codegen can emit correct integer division (`//`) and choose
// zero-initialisers, but nothing here is enforced at runtime.
enum class BaseType {
    Unknown,
    Void,
    Int,
    Float,
    Bool,
    String,
    Char,
};

struct Type {
    BaseType base = BaseType::Unknown;
    bool isVector = false;              // std::vector<T>
    bool isMap = false;                 // std::map<K, V> / unordered_map
    std::string userType;               // non-empty for struct types
    std::shared_ptr<Type> element;      // element / value type
    std::shared_ptr<Type> key;          // key type for maps

    static Type of(BaseType b) { return Type{b, false, false, "", nullptr, nullptr}; }
    bool numeric() const {
        return base == BaseType::Int || base == BaseType::Float ||
               base == BaseType::Char;
    }
    bool isStruct() const { return !userType.empty(); }
    std::string name() const {
        if (isVector) return "vector";
        if (isMap) return "map";
        if (!userType.empty()) return userType;
        switch (base) {
            case BaseType::Void: return "void";
            case BaseType::Int: return "int";
            case BaseType::Float: return "float";
            case BaseType::Bool: return "bool";
            case BaseType::String: return "string";
            case BaseType::Char: return "char";
            default: return "auto";
        }
    }
};

// ---------------------------------------------------------------------------
// Expressions
// ---------------------------------------------------------------------------
enum class ExprKind {
    IntLit,
    FloatLit,
    StringLit,
    CharLit,
    BoolLit,
    NullLit,
    Ident,
    Binary,
    Unary,
    Call,
    Index,
    Member,
    Ternary,
    InitList,   // {1, 2, 3}
};

struct Expr;
using ExprPtr = std::unique_ptr<Expr>;

struct Expr {
    ExprKind kind = ExprKind::IntLit;
    int line = 0;

    // literals
    long long ival = 0;
    double fval = 0.0;
    std::string sval;
    bool bval = false;

    // binary / unary / ternary
    std::string op;
    ExprPtr lhs, rhs, third, cond;

    // call
    std::string callee;
    std::vector<ExprPtr> args;

    // member
    std::string member;

    // resolved type, filled in by the semantic pass
    Type type;
};

// ---------------------------------------------------------------------------
// Statements
// ---------------------------------------------------------------------------
enum class StmtKind {
    Block,
    VarDecl,
    ExprStmt,
    If,
    While,
    For,
    RangeFor,
    Return,
    Break,
    Continue,
    Empty,
    Include,        // #include "x.cpp" -> from x import *
};

struct Stmt;
using StmtPtr = std::unique_ptr<Stmt>;

struct VarDecl {
    Type type;
    std::string name;
    ExprPtr init;              // may be null
    ExprPtr arraySize;         // int a[10]  -> arraySize
    bool isConst = false;
};

struct Stmt {
    StmtKind kind = StmtKind::Empty;
    int line = 0;

    std::vector<StmtPtr> body;         // Block
    VarDecl decl;                      // VarDecl

    ExprPtr cond;                      // If / While / For
    StmtPtr thenBranch;                // If
    StmtPtr elseBranch;                // If (may be If for else-if)

    StmtPtr init;                      // For
    ExprPtr step;                      // For
    ExprPtr rangeExpr;                 // RangeFor : collection
    std::string loopVar;               // RangeFor

    ExprPtr value;                     // Return / ExprStmt
    std::string sval;                  // Include: the included file name
};

// ---------------------------------------------------------------------------
// Top-level declarations
// ---------------------------------------------------------------------------
struct Param {
    Type type;
    std::string name;
    ExprPtr defaultValue;      // may be null
};

struct FuncDecl {
    Type returnType;
    std::string name;
    int line = 0;
    std::vector<Param> params;
    StmtPtr body;              // null for prototypes
};

struct FieldDecl {
    Type type;
    std::string name;
};

struct StructDecl {
    std::string name;
    std::vector<FieldDecl> fields;
};

struct EnumDecl {
    std::string name;
    std::vector<std::pair<std::string, ExprPtr>> members;
};

struct GlobalVar {
    VarDecl decl;
};

struct Program {
    std::vector<FuncDecl> functions;
    std::vector<StructDecl> structs;
    std::vector<EnumDecl> enums;
    std::vector<GlobalVar> globals;
    // #include statements, emitted as imports before any definition so that
    // imported names are visible to every function in this file.
    std::vector<StmtPtr> imports;
    // name of the entry point found in the source, empty if none
    std::string entry;
};

}  // namespace cppfarmer
