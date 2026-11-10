using System;
using System.Collections.Generic;

namespace CppFarmer
{
    /// <summary>
    /// The single lookup point for "which language owns this window?".
    ///
    /// Profiles register themselves in a fixed order; index 0 is the default and
    /// is the one the help button and the fallback paths refer to. Nothing else
    /// in the mod hard-codes a language id or extension.
    /// </summary>
    internal static class LanguageRegistry
    {
        private static readonly List<LanguageProfile> Ordered = new List<LanguageProfile>();
        private static readonly List<LanguageProfile> StorageOrdered = new List<LanguageProfile>();

        static LanguageRegistry()
        {
            Register(new CppLanguage());
            // Register(new RustLanguage());   // add front-ends here
        }

        public static void Register(LanguageProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            foreach (LanguageProfile existing in Ordered)
            {
                if (existing.Id == profile.Id)
                {
                    Plugin.Log?.LogWarning("language '" + profile.Id + "' registered twice; keeping the first");
                    return;
                }
                foreach (string ext in profile.Extensions)
                {
                    if (Array.Exists(existing.Extensions,
                            e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase)))
                    {
                        Plugin.Log?.LogWarning("extension '" + ext + "' claimed by '"
                            + existing.Id + "' and '" + profile.Id + "'; '" + profile.Id + "' wins");
                    }
                }
            }

            Ordered.Add(profile);
            if (!string.IsNullOrEmpty(profile.StorageSuffix)) StorageOrdered.Add(profile);
        }

        /// <summary>All registered languages, default first.</summary>
        public static IReadOnlyList<LanguageProfile> All => Ordered;

        /// <summary>The language used for the help button and legacy fallbacks.</summary>
        public static LanguageProfile Default => Ordered.Count > 0 ? Ordered[0] : null;

        /// <summary>
        /// Resolves a window/file name to its language. The longest matching
        /// extension wins, so a future ".cppm" cannot be swallowed by ".cpp".
        /// </summary>
        public static LanguageProfile ForName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            LanguageProfile best = null;
            int bestLength = 0;
            foreach (LanguageProfile profile in Ordered)
            {
                foreach (string ext in profile.Extensions)
                {
                    if (ext.Length <= bestLength) continue;
                    if (!fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) continue;
                    best = profile;
                    bestLength = ext.Length;
                }
            }
            return best;
        }

        /// <summary>Resolves a live editor window to its language, or null.</summary>
        public static LanguageProfile ForWindow(CodeWindow window)
        {
            if (window == null) return null;
            return ForName(CodeSupport.GetFileName(window));
        }

        public static bool IsSupportedName(string fileName) => ForName(fileName) != null;

        public static bool IsModWindow(CodeWindow window) => ForWindow(window) != null;

        // ------------------------------------------------------------------
        // Storage: languages that save windows under their own extension
        // ------------------------------------------------------------------

        public static IReadOnlyList<LanguageProfile> WithStorage => StorageOrdered;

        /// <summary>The language that stores <paramref name="fileName"/> verbatim, or null.</summary>
        public static LanguageProfile StorageForName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            foreach (LanguageProfile profile in StorageOrdered)
            {
                if (fileName.EndsWith(profile.StorageSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }
            return null;
        }

        public static bool IsStorageName(string fileName) => StorageForName(fileName) != null;

        /// <summary>Window name -> file name the game should read/write.</summary>
        public static string DiskNameFor(string windowName)
        {
            if (string.IsNullOrEmpty(windowName)) return windowName;
            return IsStorageName(windowName) ? windowName : windowName + ".py";
        }

        // ------------------------------------------------------------------
        // In-game documentation
        // ------------------------------------------------------------------

        /// <summary>The profile that owns a virtual document id, or null.</summary>
        public static LanguageProfile DocOwner(string docId)
        {
            if (string.IsNullOrEmpty(docId)) return null;
            foreach (LanguageProfile profile in Ordered)
            {
                if (string.Equals(profile.ReferenceDocId, docId, StringComparison.Ordinal)) return profile;
                if (string.Equals(profile.ErrorsDocId, docId, StringComparison.Ordinal)) return profile;
            }
            return null;
        }

        /// <summary>Markdown for a virtual document id, or null when it is not ours.</summary>
        public static string BuildDoc(string docId)
        {
            return DocOwner(docId)?.BuildDoc(docId);
        }

        /// <summary>Links to add to the game's docs home page, default language first.</summary>
        public static IEnumerable<string> HomeLinks()
        {
            foreach (LanguageProfile profile in Ordered)
            {
                if (!string.IsNullOrEmpty(profile.HomeLinkText)) yield return profile.HomeLinkText;
            }
        }
    }
}
