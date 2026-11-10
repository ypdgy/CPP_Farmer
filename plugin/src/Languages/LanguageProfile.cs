using System;
using System.Collections.Generic;

namespace CppFarmer
{
    /// <summary>
    /// One supported programming language: its file extensions, how its windows
    /// are stored, and every editor behaviour that differs from the game's own
    /// Python-like script language.
    ///
    /// A profile is the *only* thing a new language has to provide on the C#
    /// side. Everything the mod already does generically -- reading window text,
    /// tokenising with the game's lexer, caching compile results, breakpoint and
    /// execution-highlight mapping, saving, file-watching -- lives in
    /// <see cref="CodeSupport"/> / <see cref="SaveStorage"/> and works for any
    /// number of profiles.
    ///
    /// Defaults are deliberately inert: a language that only wants transpiling
    /// (no custom completion, no custom colouring, no help page) overrides
    /// nothing but <see cref="Id"/>, <see cref="DisplayName"/> and
    /// <see cref="Extensions"/>.
    ///
    /// See docs/ADDING_A_LANGUAGE.md for the full checklist.
    /// </summary>
    internal abstract class LanguageProfile
    {
        // ------------------------------------------------------------------
        // Identity
        // ------------------------------------------------------------------

        /// <summary>Stable lowercase id; must match the native front-end id ("cpp").</summary>
        public abstract string Id { get; }

        /// <summary>Shown in logs, error messages and the help page ("C++").</summary>
        public abstract string DisplayName { get; }

        /// <summary>
        /// File extensions claimed by this language, lowest priority last. The
        /// longest matching extension wins, so a future ".cppm" would not be
        /// swallowed by ".cpp".
        /// </summary>
        public abstract string[] Extensions { get; }

        // ------------------------------------------------------------------
        // Storage / packaging
        // ------------------------------------------------------------------

        /// <summary>
        /// Suffix this language's windows are saved with ("\.cpp"), or null to
        /// keep the game's "&lt;window&gt;.py" naming.
        /// </summary>
        public virtual string StorageSuffix => null;

        /// <summary>
        /// Name of the IDE-only header exported into the save folder, or null
        /// when external editors need nothing from us.
        /// </summary>
        public virtual string EditorHeaderFileName => null;

        // ------------------------------------------------------------------
        // In-game documentation
        // ------------------------------------------------------------------

        /// <summary>Virtual document id for the reference page, or null.</summary>
        public virtual string ReferenceDocId => null;

        /// <summary>Virtual document id for the error catalogue, or null.</summary>
        public virtual string ErrorsDocId => null;

        /// <summary>Markdown for a virtual document id, or null if this profile has none.</summary>
        public virtual string BuildDoc(string docId) => null;

        /// <summary>Line added to the game's docs home page, or null.</summary>
        public virtual string HomeLinkText => null;

        // ------------------------------------------------------------------
        // Harmony patches owned by this language
        // ------------------------------------------------------------------

        /// <summary>
        /// Patch classes implementing this language's editor behaviour. They are
        /// applied once at startup; each one is expected to no-op for windows
        /// that belong to another language (see <see cref="Owns"/>).
        /// </summary>
        public virtual IEnumerable<Type> PatchTypes => Array.Empty<Type>();

        // ------------------------------------------------------------------
        // Compile-time hooks
        // ------------------------------------------------------------------

        /// <summary>
        /// Last chance to rewrite the source before it reaches the native
        /// front-end. C++ uses it to turn <c>#include "x.cpp"</c> into module
        /// aliases the game's importer understands.
        /// </summary>
        /// <param name="source">Window text, includes already resolved by the profile.</param>
        /// <param name="fileName">Window name, e.g. "main.cpp".</param>
        /// <param name="resolveInclude">Maps an include target to a window name, or null.</param>
        /// <param name="ensureWindow">Ensures a module window exists for a target.</param>
        /// <param name="moduleToFile">Module name -> window name, for token rewriting.</param>
        public virtual string Prepare(string source, string fileName,
            Func<string, string> resolveInclude,
            Func<string, string, bool> ensureWindow,
            out Dictionary<string, string> moduleToFile)
        {
            moduleToFile = null;
            return source;
        }

        // ------------------------------------------------------------------
        // Editor hooks (all optional)
        // ------------------------------------------------------------------

        /// <summary>Rebuilds the completer's candidate list for this language.</summary>
        public virtual List<string> BuildCompletion(List<string> original, string source) => original;

        /// <summary>Filters the completer's candidates while typing inside a word.</summary>
        public virtual List<string> FilterCompletion(List<string> original, string fragment) => original;

        /// <summary>
        /// True when the caret sits inside a string, char literal or comment,
        /// where the completer should stay closed.
        /// </summary>
        public virtual bool InsideLiteralOrComment(string text, int caret) => false;

        /// <summary>
        /// Colours one window's text. Return null to leave the game's own
        /// colouring in place.
        /// </summary>
        public virtual string Colorize(string code, ColorTheme theme, string searchWord,
            int searchIndex, (int index, int length) imeCompositionRange) => null;

        /// <summary>Description of a keyword or API name, or null when unknown.</summary>
        public virtual string DescribeWord(string word) => null;

        /// <summary>Extra tech-tree tooltip text for an unlock, or null.</summary>
        public virtual string DescribeUnlock(UnlockSO unlock) => null;

        // ------------------------------------------------------------------

        /// <summary>True when <paramref name="window"/> belongs to this language.</summary>
        public bool Owns(CodeWindow window)
        {
            return window != null && LanguageRegistry.ForWindow(window) == this;
        }

        public override string ToString() => Id;
    }
}
