// Lexer for the C++ subset accepted by the C++ Farmer front-end.
#pragma once

#include <cctype>
#include <cstdio>
#include <stdexcept>
#include <string>
#include <vector>

namespace cppfarmer {

enum class Tok {
    End,
    Ident,
    IntLit,
    FloatLit,
    StringLit,
    CharLit,
    Punct,
    Include,        // produced by  #include "x.cpp"
};

struct Token {
    Tok kind = Tok::End;
    std::string text;
    int line = 1;
    int col = 1;
};

class LexError : public std::runtime_error {
public:
    LexError(const std::string& msg, int line, int col)
        : std::runtime_error(msg), line(line), col(col) {}
    int line;
    int col;
};

class Lexer {
public:
    explicit Lexer(std::string src) : src_(std::move(src)) {}

    std::vector<Token> run() {
        std::vector<Token> out;
        while (true) {
            skipTrivia();
            if (hasPending_) {
                hasPending_ = false;
                out.push_back(pending_);
                continue;
            }
            if (pos_ >= src_.size()) {
                out.push_back({Tok::End, "", line_, col_});
                return out;
            }
            out.push_back(next());
        }
    }

private:
    const std::string src_;
    std::size_t pos_ = 0;
    int line_ = 1;
    int col_ = 1;

    // `#include "x"` is recognised inside skipTrivia but must surface as a real
    // token, so it is parked here and handed out by run() before the next lexeme.
    bool hasPending_ = false;
    Token pending_;

    char peek(std::size_t off = 0) const {
        return pos_ + off < src_.size() ? src_[pos_ + off] : '\0';
    }

    void advance() {
        if (pos_ >= src_.size()) return;
        if (src_[pos_] == '\n') {
            ++line_;
            col_ = 1;
        } else {
            ++col_;
        }
        ++pos_;
    }

    // Byte at an offset as unsigned char (0 when past the end).
    unsigned char byteAt(std::size_t off) const {
        return pos_ + off < src_.size()
                   ? static_cast<unsigned char>(src_[pos_ + off])
                   : 0u;
    }

    // The game's editor and copy/paste both can introduce non-ASCII spaces
    // (U+00A0 no-break space above all). Treat them as whitespace instead of
    // failing the whole compile with an unreadable "unexpected character".
    // Returns the byte length of the recognised sequence, 0 if none.
    std::size_t unicodeSpaceLen() const {
        const unsigned char b0 = byteAt(0);
        if (b0 == 0xC2 && byteAt(1) == 0xA0) return 2;                 // U+00A0
        if (b0 == 0xE2 && byteAt(1) == 0x80) {
            const unsigned char b2 = byteAt(2);
            if (b2 >= 0x80 && b2 <= 0x8B) return 3;                    // U+2000..U+200B
            if (b2 == 0xAF) return 3;                                  // U+202F
            if (b2 == 0xA8 || b2 == 0xA9) return 3;                    // U+2028/2029
        }
        if (b0 == 0xE2 && byteAt(1) == 0x81 && byteAt(2) == 0x9F) return 3;  // U+205F
        if (b0 == 0xE3 && byteAt(1) == 0x80 && byteAt(2) == 0x80) return 3;  // U+3000
        if (b0 == 0xEF && byteAt(1) == 0xBB && byteAt(2) == 0xBF) return 3;  // U+FEFF
        return 0;
    }

    // Raw UTF-8 bytes of the character at the cursor (for display).
    std::string bytesAt(unsigned int len) const {
        std::string out;
        for (unsigned int i = 0; i < len && pos_ + i < src_.size(); ++i) {
            out.push_back(src_[pos_ + i]);
        }
        return out;
    }

    // Readable description of the character at the cursor.
    //
    // The target audience types with a Chinese IME, so the most likely mistake is
    // fullwidth punctuation (；（）＝ etc.). Those are reported by name together
    // with the ASCII alternative rather than silently rewritten -- an explicit
    // error teaches the player to switch input modes, a silent fix hides the bug.
    std::string describeChar() const {
        const unsigned char b0 = byteAt(0);
        if (b0 < 0x80) {
            if (b0 == '\t') return "制表符 (tab) 出现在这里不合法";
            if (b0 == '\r') return "回车符 CR 出现在这里不合法";
            if (b0 >= 0x20 && b0 < 0x7F) {
                return std::string("意外字符「") + static_cast<char>(b0) + "」";
            }
            char buf[32];
            std::snprintf(buf, sizeof(buf), "意外字节 0x%02X", b0);
            return buf;
        }

        unsigned int cp = 0;
        unsigned int len = 0;
        if (!decodeUtf8(cp, len)) {
            char buf[32];
            std::snprintf(buf, sizeof(buf), "unexpected byte 0x%02X", b0);
            return buf;
        }

        const std::string ch = bytesAt(len);
        char hex[16];
        std::snprintf(hex, sizeof(hex), "U+%04X", cp);

        char ascii = asciiForPunct(cp);
        if (ascii) {
            std::string how(1, ascii);
            if (ascii == '"') how = "\\\"";
            return "全角字符「" + ch + "」(" + hex + ")，请改用半角「" + how + "」";
        }
        return "无法识别的字符「" + ch + "」(" + hex + ")，标识符请使用英文";
    }

    // Recognises  #include "name"  and parks it as a Tok::Include token.
    // Returns false (restoring the cursor) for any other directive, including
    // #include <...>, so the caller keeps its "drop the line" behaviour.
    bool tryIncludeDirective() {
        const std::size_t savePos = pos_;
        const int saveLine = line_;
        const int saveCol = col_;

        advance();                                     // '#'
        while (pos_ < src_.size() && (peek() == ' ' || peek() == '\t')) advance();

        const char* keyword = "include";
        for (const char* k = keyword; *k; ++k) {
            if (peek() != *k) {
                pos_ = savePos; line_ = saveLine; col_ = saveCol;
                return false;
            }
            advance();
        }
        while (pos_ < src_.size() && (peek() == ' ' || peek() == '\t')) advance();
        if (peek() != '"') {                           // #include <...>
            pos_ = savePos; line_ = saveLine; col_ = saveCol;
            return false;
        }
        advance();                                     // opening quote

        std::string name;
        while (pos_ < src_.size() && peek() != '"' && peek() != '\n') {
            name.push_back(peek());
            advance();
        }
        if (peek() == '"') advance();
        while (pos_ < src_.size() && peek() != '\n') advance();   // rest of line

        pending_ = Token{Tok::Include, name, saveLine, saveCol};
        hasPending_ = true;
        return true;
    }

    void skipTrivia() {
        for (;;) {
            if (pos_ >= src_.size()) return;

            // whitespace
            if (std::isspace(static_cast<unsigned char>(peek()))) {
                advance();
                continue;
            }
            if (std::size_t n = unicodeSpaceLen()) {
                for (std::size_t i = 0; i < n; ++i) advance();
                continue;
            }
            // line comment
            if (peek() == '/' && peek(1) == '/') {
                while (pos_ < src_.size() && peek() != '\n') advance();
                continue;
            }
            // block comment
            if (peek() == '/' && peek(1) == '*') {
                advance();
                advance();
                while (pos_ < src_.size() && !(peek() == '*' && peek(1) == '/')) advance();
                advance();
                advance();
                continue;
            }
            // preprocessor directive
            if (peek() == '#') {
                if (tryIncludeDirective()) return;   // parked in pending_
                while (pos_ < src_.size()) {
                    if (peek() == '\\' && peek(1) == '\n') {
                        advance();
                        advance();
                        continue;
                    }
                    if (peek() == '\n') break;
                    advance();
                }
                continue;
            }
            return;
        }
    }

    Token next() {
        const int startLine = line_;
        const int startCol = col_;
        const char c = peek();

        // identifier / keyword
        if (std::isalpha(static_cast<unsigned char>(c)) || c == '_') {
            std::string text;
            while (std::isalnum(static_cast<unsigned char>(peek())) || peek() == '_') {
                text.push_back(peek());
                advance();
            }
            return {Tok::Ident, text, startLine, startCol};
        }

        // number
        if (std::isdigit(static_cast<unsigned char>(c)) ||
            (c == '.' && std::isdigit(static_cast<unsigned char>(peek(1))))) {
            // 0x / 0b literals are folded to decimal here so later stages only
            // ever see plain integers.
            if (c == '0' && (peek(1) == 'x' || peek(1) == 'X' || peek(1) == 'b' ||
                             peek(1) == 'B')) {
                const bool hex = (peek(1) == 'x' || peek(1) == 'X');
                advance();  // 0
                advance();  // x / b
                long long value = 0;
                int digits = 0;
                while (true) {
                    char ch = peek();
                    int digit;
                    if (ch >= '0' && ch <= '9') {
                        digit = ch - '0';
                    } else if (hex && ch >= 'a' && ch <= 'f') {
                        digit = ch - 'a' + 10;
                    } else if (hex && ch >= 'A' && ch <= 'F') {
                        digit = ch - 'A' + 10;
                    } else {
                        break;
                    }
                    value = value * (hex ? 16 : 2) + digit;
                    ++digits;
                    advance();
                }
                if (digits == 0) throw LexError("malformed numeric literal", startLine, startCol);
                // integer suffix
                while (peek() == 'u' || peek() == 'U' || peek() == 'l' || peek() == 'L') advance();
                return {Tok::IntLit, std::to_string(value), startLine, startCol};
            }

            std::string text;
            bool isFloat = false;
            while (std::isdigit(static_cast<unsigned char>(peek()))) {
                text.push_back(peek());
                advance();
            }
            if (peek() == '.') {
                isFloat = true;
                text.push_back(peek());
                advance();
                while (std::isdigit(static_cast<unsigned char>(peek()))) {
                    text.push_back(peek());
                    advance();
                }
            }
            if (peek() == 'e' || peek() == 'E') {
                isFloat = true;
                text.push_back(peek());
                advance();
                if (peek() == '+' || peek() == '-') {
                    text.push_back(peek());
                    advance();
                }
                while (std::isdigit(static_cast<unsigned char>(peek()))) {
                    text.push_back(peek());
                    advance();
                }
            }
            return {isFloat ? Tok::FloatLit : Tok::IntLit, text, startLine, startCol};
        }

        // string literal
        if (c == '"') {
            advance();
            std::string text;
            while (pos_ < src_.size() && peek() != '"') {
                if (peek() == '\\') {
                    advance();
                    char e = peek();
                    switch (e) {
                        case 'n': text.push_back('\n'); break;
                        case 't': text.push_back('\t'); break;
                        case '\\': text.push_back('\\'); break;
                        case '"': text.push_back('"'); break;
                        case '0': text.push_back('\0'); break;
                        default: text.push_back(e); break;
                    }
                    advance();
                    continue;
                }
                text.push_back(peek());
                advance();
            }
            if (pos_ >= src_.size()) throw LexError("unterminated string literal", startLine, startCol);
            advance();  // closing quote
            return {Tok::StringLit, text, startLine, startCol};
        }

        // char literal
        if (c == '\'') {
            advance();
            char value = peek();
            if (value == '\\') {
                advance();
                switch (peek()) {
                    case 'n': value = '\n'; break;
                    case 't': value = '\t'; break;
                    case '\\': value = '\\'; break;
                    case '\'': value = '\''; break;
                    default: value = peek(); break;
                }
            }
            advance();
            if (peek() != '\'') throw LexError("unterminated char literal", startLine, startCol);
            advance();
            return {Tok::CharLit, std::string(1, value), startLine, startCol};
        }

        // multi-character operators, longest match first
        static const char* threeOps[] = {"<<=", ">>=", "...", "<=>"};
        static const char* twoOps[] = {"==", "!=", "<=", ">=", "&&", "||", "++", "--",
                                       "+=", "-=", "*=", "/=", "%=", "<<", ">>", "->",
                                       "::", "&=", "|=", "^="};
        for (const char* op3 : threeOps) {
            if (src_.compare(pos_, 3, op3) == 0) {
                for (int i = 0; i < 3; ++i) advance();
                return {Tok::Punct, op3, startLine, startCol};
            }
        }
        for (const char* op2 : twoOps) {
            if (src_.compare(pos_, 2, op2) == 0) {
                advance();
                advance();
                return {Tok::Punct, op2, startLine, startCol};
            }
        }

        static const std::string singles = "+-*/%(){}[]<>=!&|^~?:;,.@$";
        if (singles.find(c) != std::string::npos) {
            advance();
            return {Tok::Punct, std::string(1, c), startLine, startCol};
        }

        throw LexError(describeChar(), startLine, startCol);
    }

    // Decodes one UTF-8 code point at the cursor. Returns its byte length, or 0.
    unsigned int decodeUtf8(unsigned int& cp, unsigned int& len) const {
        const unsigned char b0 = byteAt(0);
        int extra;
        if ((b0 & 0xE0) == 0xC0) {
            cp = b0 & 0x1Fu;
            extra = 1;
        } else if ((b0 & 0xF0) == 0xE0) {
            cp = b0 & 0x0Fu;
            extra = 2;
        } else if ((b0 & 0xF8) == 0xF0) {
            cp = b0 & 0x07u;
            extra = 3;
        } else {
            return 0;
        }
        for (int i = 1; i <= extra; ++i) {
            unsigned char b = byteAt(static_cast<std::size_t>(i));
            if ((b & 0xC0) != 0x80) return 0;
            cp = (cp << 6) | (b & 0x3Fu);
        }
        len = static_cast<unsigned int>(extra + 1);
        return len;
    }

    // Maps CJK input-method punctuation to the ASCII character it stands for.
    // Only used to build the error message; the character is never rewritten.
    static char asciiForPunct(unsigned int cp) {
        // Fullwidth forms U+FF01..U+FF5E are ASCII + 0xFEE0, but only the
        // punctuation half is folded. Fullwidth digits (U+FF10..U+FF19) and
        // letters are excluded on purpose: returning them as punctuation tokens
        // would corrupt the token stream, and an unmapped character reports a
        // clear code point instead.
        if (cp >= 0xFF01 && cp <= 0xFF20 && !(cp >= 0xFF10 && cp <= 0xFF19)) {
            return static_cast<char>(cp - 0xFEE0);
        }
        if (cp >= 0xFF3B && cp <= 0xFF40) return static_cast<char>(cp - 0xFEE0);
        if (cp >= 0xFF5B && cp <= 0xFF5E) return static_cast<char>(cp - 0xFEE0);
        switch (cp) {
            case 0x3002: return '.';   // 。
            case 0x3001: return ',';   // 、
            case 0x201C:               // “
            case 0x201D: return '"';   // ”
            case 0x2018:               // ‘
            case 0x2019: return '\'';  // ’
            default: return 0;
        }
    }
};

}  // namespace cppfarmer
