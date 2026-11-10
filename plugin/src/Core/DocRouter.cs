using System;
using HarmonyLib;

namespace CppFarmer
{
    /// <summary>
    /// Routes the game's documentation requests to the registered languages.
    ///
    /// Localizer.LoadDoc is asked for a virtual document id and only ids owned by
    /// a <see cref="LanguageProfile"/> are answered. The one intentional
    /// exception is the docs home page, which gets one link per language. No file
    /// on disk changes.
    /// </summary>
    [HarmonyPatch(typeof(Localizer), nameof(Localizer.LoadDoc))]
    internal static class PatchLoadDoc
    {
        private const string HomeDoc = "docs/home.md";

        [HarmonyPostfix]
        private static void Postfix(string doc, ref string __result)
        {
            if (LanguageRegistry.DocOwner(doc) != null)
            {
                try
                {
                    string page = LanguageRegistry.BuildDoc(doc);
                    if (page != null) __result = page;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError("cannot build the reference page for '" + doc + "': " + e);
                }
                return;
            }

            if (Plugin.CfgHomeLink != null && Plugin.CfgHomeLink.Value &&
                string.Equals(doc, HomeDoc, StringComparison.Ordinal))
            {
                __result = HomeLinkInjector.Inject(__result);
            }
        }
    }

    /// <summary>
    /// Adds one link per language to the docs home page.
    ///
    /// Inserted right after the first &lt;columns&gt; opener so the links sit
    /// alongside the existing "General Info" entries instead of being a stray
    /// paragraph.
    ///
    /// The link lines contain no backticks. CodeUtilities.ApplyCodeTags splits the
    /// whole page on backticks and treats odd segments as code, so a backtick in
    /// inserted text would flip every following segment -- the exact failure from
    /// the earlier docs injection attempt. Inserting backtick-free text at any
    /// position leaves all existing pairing untouched.
    /// </summary>
    internal static class HomeLinkInjector
    {
        public static string Inject(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return markdown;

            int columns = markdown.IndexOf("<columns>", StringComparison.Ordinal);
            if (columns < 0) return markdown;
            int newline = markdown.IndexOf('\n', columns);
            if (newline < 0) return markdown;

            string insert = string.Empty;
            foreach (string link in LanguageRegistry.HomeLinks())
            {
                if (string.IsNullOrEmpty(link)) continue;
                if (markdown.Contains(link)) continue;      // already there
                insert += link + "\n";
            }
            if (insert.Length == 0) return markdown;

            return markdown.Insert(newline + 1, insert);
        }
    }
}
