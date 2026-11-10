using System;
using System.Collections.Generic;

namespace CppFarmer
{
    /// <summary>
    /// C subset front-end ("c").
    ///
    /// C shares the parser and code generator with C++: what this mod accepts
    /// from C is a subset of what it accepts from C++, so the only real
    /// difference at compile time is printf (the native side switches dialect on
    /// this id). Everything editor-facing lives here: the keyword table, the
    /// colouring rules, the storage suffix and the doc pages.
    ///
    /// No owned Harmony patches: keyword descriptions and the tech-tree hints are
    /// C++ wordings, so C windows simply do not get them.
    /// </summary>
    internal sealed class CLanguage : LanguageProfile
    {
        public static readonly CLanguage Instance = new CLanguage();

        private static readonly string[] ExtensionList = { ".c" };

        /// <summary>
        /// Words the front-end accepts. C keywords it rejects on purpose
        /// (switch/case/default, union, sizeof, goto) stay out: offering them
        /// would walk the player straight into a compile error.
        /// </summary>
        internal static readonly string[] Keywords =
        {
            // types
            "int", "float", "double", "bool", "char", "void", "long", "short",
            "unsigned", "signed", "size_t", "const", "static", "inline", "struct",
            "enum", "typedef",
            // statements
            "if", "else", "for", "while", "do", "break", "continue", "return",
            // literals and game values C code uses directly
            "true", "false", "NULL", "None", "True", "False",
            // printf is not a game builtin: the front-end rewrites it at compile
            // time, so it never shows up in the game's own candidate list.
            "printf",
        };

        // Same groups and order as the C++ table (strings first so '//' inside a
        // literal is not a comment, preprocessor before keywords so `include` is
        // coloured as part of the directive). The keyword list is C's, including
        // the spellings this front-end rejects -- colouring them is correct even
        // when compiling them is not.
        private const string HighlightRules =
            @"(?<string>""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*')" +
            @"|(?<comment>//[^\n]*)" +
            @"|(?<preproc>(?m:^[ \t]*#[^\n]*))" +
            @"|(?<keyword>\b(?:auto|bool|break|case|char|const|continue|default|do|double|else|enum|extern|float|for|goto|if|inline|int|long|register|restrict|return|short|signed|sizeof|static|struct|switch|typedef|union|unsigned|void|volatile|while|true|false|NULL|size_t)\b)" +
            @"|(?<function>\b[a-zA-Z_]\w*(?=\())" +
            @"|(?<builtin>\b(?:North|East|South|West|Entities|Items|Grounds|Unlocks|Leaderboards|Hats)\b)" +
            @"|(?<number>\b(?:0[xX][0-9a-fA-F]+|(?:\d*\.)?\d+)\b)";

        private const string ReferenceId = "cfarmer_reference";
        private const string ErrorsId = "cfarmer_errors";
        private const string ReferenceFile = "cfarmer_reference.md";
        private const string ErrorsFile = "cfarmer_errors.md";

        public override string Id => "c";
        public override string DisplayName => "C";
        public override string[] Extensions => ExtensionList;

        // Windows are saved as "main.c" instead of the game's "main.c.py".
        public override string StorageSuffix => ".c";

        // IDE-only header copied into the save folder on load.
        public override string EditorHeaderFileName => "cfarmer_api.h";

        public override string ReferenceDocId => ReferenceId;
        public override string ErrorsDocId => ErrorsId;

        public override IEnumerable<Type> PatchTypes => Array.Empty<Type>();

        public override string Prepare(string source, string fileName,
            Func<string, string> resolveInclude,
            Func<string, string, bool> ensureWindow,
            out Dictionary<string, string> moduleToFile)
        {
            // "#include \"farm.c\"" behaves like the C++ case: the module name is
            // aliased to the included window so the game's importer finds it.
            return CppIncludes.Prepare(source, fileName, resolveInclude, ensureWindow,
                out moduleToFile, DisplayName);
        }

        public override List<string> BuildCompletion(List<string> original, string source)
            => Completion.Rebuild(original, source, Keywords);

        public override List<string> FilterCompletion(List<string> original, string fragment)
            => Completion.Filter(original, fragment, Keywords);

        public override bool InsideLiteralOrComment(string text, int caret)
            => Completion.InsideStringOrComment(text, caret);

        public override string Colorize(string code, ColorTheme theme, string searchWord,
            int searchIndex, (int index, int length) imeCompositionRange)
            => SyntaxColoring.Colorize(code, HighlightRules, theme, searchWord,
                searchIndex, imeCompositionRange);

        public override bool OpensBlock(string body)
            => body.Length > 0 && body[body.Length - 1] == '{';

        public override string StripTrailingComment(string body)
        {
            int slash = body.IndexOf("//", StringComparison.Ordinal);
            return slash < 0 ? body : body.Substring(0, slash);
        }

        public override string BuildDoc(string docId)
        {
            string file = string.Equals(docId, ErrorsId, StringComparison.Ordinal)
                ? ErrorsFile
                : ReferenceFile;
            return ReferencePages.Load(file);
        }
    }
}
