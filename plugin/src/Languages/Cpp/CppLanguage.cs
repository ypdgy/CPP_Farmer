using System;
using System.Collections.Generic;

namespace CppFarmer
{
    /// <summary>
    /// The C++ subset front-end ("cpp").
    ///
    /// This class is the reference implementation of <see cref="LanguageProfile"/>:
    /// each hook is wired to a shared helper plus this language's own tables
    /// (keywords, colouring rules, page ids). A new language follows the same
    /// shape without touching the helpers.
    /// </summary>
    internal sealed class CppLanguage : LanguageProfile
    {
        public static readonly CppLanguage Instance = new CppLanguage();

        // Extension order is irrelevant: the registry picks the longest match.
        private static readonly string[] ExtensionList =
            { ".cpp", ".cxx", ".cc", ".c++", ".hpp" };

        /// <summary>
        /// Words the front-end actually accepts. Valid C++ that it rejects on
        /// purpose (switch/case/sizeof/new/delete/operator/this/virtual/
        /// template/namespace, and the std::set / std::pair types) stays out:
        /// offering them here would only walk the player into a compile error.
        /// </summary>
        internal static readonly string[] Keywords =
        {
            // types
            "int", "float", "double", "bool", "char", "void", "auto", "long",
            "short", "unsigned", "signed", "size_t", "const", "constexpr",
            "static", "inline", "struct", "enum", "class", "typedef", "using",
            "public", "private", "protected",
            // statements
            "if", "else", "for", "while", "do", "break", "continue", "return",
            "nullptr", "true", "false",
            // containers
            "vector", "map", "array", "string",
            // game values that C++ code uses directly
            "None", "True", "False",
        };

        // Order matters: strings come first so that '//' inside a literal is not
        // taken for a comment, and the preprocessor rule sits before keywords so
        // that `include` is coloured as part of the directive.
        private const string HighlightRules =
            @"(?<string>""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*')" +
            @"|(?<comment>//[^\n]*)" +
            @"|(?<preproc>(?m:^[ \t]*#[^\n]*))" +
            @"|(?<keyword>\b(?:alignas|alignof|auto|bool|break|case|catch|char|class|const|constexpr|continue|default|delete|do|double|else|enum|explicit|extern|false|float|for|friend|goto|if|inline|int|long|mutable|namespace|new|noexcept|nullptr|operator|override|private|protected|public|register|return|short|signed|sizeof|static|struct|switch|template|this|throw|true|try|typedef|typename|union|unsigned|using|virtual|void|volatile|while)\b)" +
            @"|(?<function>\b[a-zA-Z_]\w*(?=\())" +
            @"|(?<builtin>\b(?:North|East|South|West|Entities|Items|Grounds|Unlocks|Leaderboards|Hats)\b)" +
            @"|(?<number>\b(?:0[xX][0-9a-fA-F]+|(?:\d*\.)?\d+)\b)";

        private static readonly Type[] OwnedPatches =
        {
            typeof(PatchCppWordTooltip),   // keyword / std-name descriptions
            typeof(PatchUnlockTooltip),    // tech-tree hints for C++ syntax
        };

        public override string Id => "cpp";
        public override string DisplayName => "C++";
        public override string[] Extensions => ExtensionList;

        // Windows are saved as "main.cpp" instead of the game's "main.cpp.py".
        public override string StorageSuffix => ".cpp";

        // IDE-only header copied into the save folder on load.
        public override string EditorHeaderFileName => "cppfarmer_api.hpp";

        // Virtual document ids and the Markdown files that back them. Ids are
        // what the game's doc links point at; the files live next to the plugin.
        private const string ReferenceId = "cppfarmer_reference";
        private const string ErrorsId = "cppfarmer_errors";
        private const string ReferenceFile = "cppfarmer_reference.md";
        private const string ErrorsFile = "cppfarmer_errors.md";

        public override string ReferenceDocId => ReferenceId;
        public override string ErrorsDocId => ErrorsId;

        public override IEnumerable<Type> PatchTypes => OwnedPatches;

        public override string Prepare(string source, string fileName,
            Func<string, string> resolveInclude,
            Func<string, string, bool> ensureWindow,
            out Dictionary<string, string> moduleToFile)
        {
            // "#include \"x.cpp\"" becomes "from x import *" in the generated
            // script, with the module name aliased to that file's window so the
            // game's own import machinery parses it.
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

        public override string DescribeWord(string word) => CppTooltips.Describe(word);

        public override string DescribeUnlock(UnlockSO unlock) => CppUnlockInfo.Build(unlock);

        public override string BuildDoc(string docId)
        {
            string file = string.Equals(docId, ErrorsId, StringComparison.Ordinal)
                ? ErrorsFile
                : ReferenceFile;
            return ReferencePages.Load(file);
        }
    }
}
