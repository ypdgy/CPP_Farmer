using System;
using System.Collections.Generic;

namespace CppFarmer
{
    /// <summary>
    /// Java subset front-end ("java").
    ///
    /// Java has its own parser and code generator on the native side (it is not
    /// a dialect of the C++ front-end: classes, static members, System.out and
    /// string concatenation are different enough). What lives here is everything
    /// editor-facing, mirroring <see cref="CLanguage"/>: keyword table, colouring
    /// rules, storage suffix and doc pages.
    ///
    /// No owned Harmony patches: keyword tooltips and tech-tree hints are C++
    /// wordings, so Java windows simply do not get them.
    /// </summary>
    internal sealed class JavaLanguage : LanguageProfile
    {
        public static readonly JavaLanguage Instance = new JavaLanguage();

        private static readonly string[] ExtensionList = { ".java" };

        /// <summary>
        /// Words the front-end actually accepts. Java it rejects on purpose
        /// (switch/case/default, do/while, object instantiation, generics,
        /// multi-dimensional arrays) stays out: offering them here would walk the
        /// player straight into a compile error.
        /// </summary>
        internal static readonly string[] Keywords =
        {
            // primitive types
            "int", "float", "double", "boolean", "char", "String", "void",
            // class structure
            "class", "public", "private", "protected", "static", "final", "new",
            // statements
            "if", "else", "for", "while", "break", "continue", "return",
            // literals and the game values Java code uses directly
            "true", "false", "null", "None", "True", "False",
            // the only standard-library names the front-end maps
            "System", "Math",
        };

        // Same group order as the C++ table (strings first so '//' inside a
        // literal is not a comment). Java has no preprocessor, so the preproc
        // group is absent. The keyword list is Java's full set, including the
        // spellings this front-end rejects -- colouring them is correct even when
        // compiling them is not.
        private const string HighlightRules =
            @"(?<string>""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*')" +
            @"|(?<comment>//[^\n]*)" +
            @"|(?<keyword>\b(?:abstract|boolean|break|byte|case|catch|char|class|const|continue|default|do|double|else|enum|extends|final|finally|float|for|goto|if|implements|import|instanceof|int|interface|long|native|new|package|private|protected|public|return|short|static|strictfp|super|switch|synchronized|this|throw|throws|transient|try|void|volatile|while|true|false|null|String)\b)" +
            @"|(?<function>\b[a-zA-Z_]\w*(?=\())" +
            @"|(?<builtin>\b(?:North|East|South|West|Entities|Items|Grounds|Unlocks|Leaderboards|Hats)\b)" +
            @"|(?<number>\b(?:0[xX][0-9a-fA-F]+|(?:\d*\.)?\d+)\b)";

        private const string ReferenceId = "javafarmer_reference";
        private const string ErrorsId = "javafarmer_errors";
        private const string ReferenceFile = "javafarmer_reference.md";
        private const string ErrorsFile = "javafarmer_errors.md";

        public override string Id => "java";
        public override string DisplayName => "Java";
        public override string[] Extensions => ExtensionList;

        // Windows are saved as "main.java" instead of the game's "main.java.py".
        public override string StorageSuffix => ".java";

        public override string ReferenceDocId => ReferenceId;
        public override string ErrorsDocId => ErrorsId;

        public override IEnumerable<Type> PatchTypes => Array.Empty<Type>();

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
