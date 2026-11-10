using System;
using System.Collections.Generic;

namespace CppFarmer
{
    /// <summary>
    /// The C++ subset front-end ("cpp").
    ///
    /// This class is the reference implementation of <see cref="LanguageProfile"/>:
    /// every hook is wired to an existing static helper, and the actual rules
    /// (keywords, regex colouring, include syntax, page text) stay in the files
    /// next to it. A new language follows the same shape without touching any of
    /// them.
    /// </summary>
    internal sealed class CppLanguage : LanguageProfile
    {
        public static readonly CppLanguage Instance = new CppLanguage();

        // Extension order is irrelevant: the registry picks the longest match.
        private static readonly string[] ExtensionList =
            { ".cpp", ".cxx", ".cc", ".c++", ".hpp" };

        private static readonly Type[] OwnedPatches =
        {
            typeof(PatchCppIndent),        // '{' opens a block, "//" ends a comment
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

        public override string ReferenceDocId => CppReference.DocId;
        public override string ErrorsDocId => CppReference.ErrorsDocId;

        public override string HomeLinkText =>
            "[C++ Farmer 说明](" + CppReference.DocId + ")";

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
                out moduleToFile);
        }

        public override List<string> BuildCompletion(List<string> original, string source)
            => CppWords.Rebuild(original, source);

        public override List<string> FilterCompletion(List<string> original, string fragment)
            => CppWords.Filter(original, fragment);

        public override bool InsideLiteralOrComment(string text, int caret)
            => CppWords.InsideStringOrComment(text, caret);

        public override string Colorize(string code, ColorTheme theme, string searchWord,
            int searchIndex, (int index, int length) imeCompositionRange)
            => CppHighlighter.Colorize(code, theme, searchWord, searchIndex,
                imeCompositionRange);

        public override string DescribeWord(string word) => CppTooltips.Describe(word);

        public override string DescribeUnlock(UnlockSO unlock) => CppUnlockInfo.Build(unlock);

        public override string BuildDoc(string docId) => CppReference.Load(docId);
    }
}
