namespace CppFarmer
{
    /// <summary>
    /// The single page the toolbar button opens.
    ///
    /// Every language keeps its own reference and errors page, but the button has
    /// to lead somewhere language-neutral: pointing it at one language's page made
    /// the other language look unsupported. This index only lists the pages and
    /// explains which one to read, so adding a language means adding a section to
    /// a Markdown file -- not touching the UI.
    ///
    /// The body is an ordinary file next to the plugin, like every other page, so
    /// the wording can be edited without rebuilding.
    /// </summary>
    internal static class ReferenceIndex
    {
        public const string DocId = "farmer_index";

        private const string FileName = "farmer_index.md";

        /// <summary>
        /// Link inserted into the game's docs home page. One entry point rather
        /// than one link per language, so the player lands on the page that
        /// explains the split instead of guessing from two terse labels.
        ///
        /// No backticks here: the docs renderer splits a page on them, and an odd
        /// count would turn the rest of the page into a code block.
        /// </summary>
        public const string HomeLink = "[C++ Farmer 说明](" + DocId + ")";

        public static string Build() => ReferencePages.Load(FileName);
    }
}
