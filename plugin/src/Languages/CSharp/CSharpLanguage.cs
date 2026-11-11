using System;
using System.Collections.Generic;

namespace CppFarmer
{
    /// <summary>
    /// C# subset front-end ("csharp").
    ///
    /// C# has its own parser and code generator on the native side; it differs
    /// from Java in type names (<c>bool</c>/<c>string</c>), the entry point
    /// (<c>Main</c>), output (<c>Console.WriteLine</c>), <c>foreach</c> and the
    /// PascalCase Math members. Everything editor-facing lives here, mirroring
    /// <see cref="JavaLanguage"/>.
    ///
    /// No owned Harmony patches: keyword tooltips and tech-tree hints are C++
    /// wordings, so C# windows simply do not get them.
    /// </summary>
    internal sealed class CSharpLanguage : LanguageProfile
    {
        public static readonly CSharpLanguage Instance = new CSharpLanguage();

        private static readonly string[] ExtensionList = { ".cs" };

        /// <summary>
        /// Words the front-end actually accepts. C# constructs it rejects on
        /// purpose (switch, do/while, properties, object instantiation, generics,
        /// string interpolation, multi-dimensional arrays) stay out: offering them
        /// would walk the player straight into a compile error.
        /// </summary>
        internal static readonly string[] Keywords =
        {
            // primitive types
            "int", "float", "double", "bool", "char", "string", "void", "var",
            // class structure
            "class", "public", "private", "protected", "internal", "static",
            "sealed", "abstract", "partial",
            // statements
            "if", "else", "for", "foreach", "in", "while", "break", "continue", "return",
            // literals and the game values C# code uses directly
            "true", "false", "null", "None", "True", "False",
            // the only library names the front-end maps
            "Console", "Math",
        };

        // Same group order as the C++/Java tables (strings first so '//' inside a
        // literal is not a comment). C# has no preprocessor, so the preproc group
        // is absent. The keyword list is C#'s full set, including the spellings
        // this front-end rejects -- colouring them is correct even when compiling
        // them is not.
        private const string HighlightRules =
            @"(?<string>""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*')" +
            @"|(?<comment>//[^\n]*)" +
            @"|(?<keyword>\b(?:abstract|as|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|goto|if|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|private|protected|public|readonly|ref|return|sbyte|sealed|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|virtual|void|volatile|while|var)\b)" +
            @"|(?<function>\b[a-zA-Z_]\w*(?=\())" +
            @"|(?<builtin>\b(?:North|East|South|West|Entities|Items|Grounds|Unlocks|Leaderboards|Hats)\b)" +
            @"|(?<number>\b(?:0[xX][0-9a-fA-F]+|(?:\d*\.)?\d+)\b)";

        private const string ReferenceId = "csharpfarmer_reference";
        private const string ErrorsId = "csharpfarmer_errors";
        private const string ReferenceFile = "csharpfarmer_reference.md";
        private const string ErrorsFile = "csharpfarmer_errors.md";

        public override string Id => "csharp";
        public override string DisplayName => "C#";
        public override string[] Extensions => ExtensionList;

        // Windows are saved as "main.cs" instead of the game's "main.cs.py".
        public override string StorageSuffix => ".cs";

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
