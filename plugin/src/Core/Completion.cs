using System;
using System.Collections.Generic;
using System.Text;

namespace CppFarmer
{
    /// <summary>
    /// Completion vocabulary for windows written in a compiled language.
    ///
    /// The game builds its candidate list from Python keywords plus every builtin
    /// it knows about. For a C or C++ file that list is actively misleading, so
    /// it is rebuilt as: identifiers in this file first, then the language's own
    /// keywords, then the game's builtins and enum names (world access still goes
    /// through those).
    ///
    /// The keyword table belongs to the language profile; everything else here is
    /// shared, including the rule that a preprocessor line contributes no
    /// identifiers.
    /// </summary>
    internal static class Completion
    {
        /// <summary>
        /// Names the game offers that only make sense in its own language.
        /// They stay out of every compiled language's list so the completer stops
        /// suggesting them.
        /// </summary>
        private static readonly HashSet<string> NotInSourceLanguage =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "def", "elif", "import", "from", "as", "global", "lambda", "yield",
                "with", "try", "except", "finally", "raise", "assert", "del", "is",
                "async", "await", "nonlocal", "pass", "and", "or", "not", "in",
            };

        /// <summary>
        /// The only names from the game's own list worth offering in a compiled
        /// language. GetWordList mixes builtins with GetUnlockedKeywords() (every
        /// Python keyword), tokenizedIdentifiers, cachedProgram.allVars --
        /// variables of the *generated* script -- and a hard-coded "__name__".
        /// A blacklist can never cover all of that, so Rebuild uses a whitelist.
        /// </summary>
        private static readonly HashSet<string> GameValues =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "True", "False", "None", "North", "South", "East", "West",
                "Entities", "Items", "Grounds", "Unlocks", "Hats", "Leaderboards",
            };

        private static bool AllowedFromGame(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            if (word.StartsWith("__", StringComparison.Ordinal)) return false;  // __name__, __builtins__ ...
            if (NotInSourceLanguage.Contains(word)) return false;
            if (GameValues.Contains(word)) return true;

            try
            {
                if (BuiltinFunctions.Functions.ContainsKey(word)) return true;
                if (BuiltinFunctions.Methods.ContainsKey(word)) return true;
            }
            catch
            {
                // dictionaries not initialised yet; fall through to reject
            }
            return false;
        }

        /// <summary>
        /// Rebuilds the whole candidate list for a window.
        /// <paramref name="keywords"/> comes from the owning language profile and
        /// must only contain spellings that front-end actually accepts: offering
        /// a rejected construct would walk the player straight into an error.
        /// </summary>
        public static List<string> Rebuild(List<string> original, string source,
            string[] keywords)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>(512);

            // 1. identifiers already present in the file, so the player's own
            //    names sort naturally next to the keywords
            foreach (string ident in Identifiers(source))
            {
                if (seen.Add(ident)) result.Add(ident);
            }

            // 2. the language's keywords
            if (keywords != null)
            {
                foreach (string kw in keywords)
                {
                    if (seen.Add(kw)) result.Add(kw);
                }
            }

            // 3. only the game names that are genuinely callable from this
            //    language. Everything else (Python keywords, __name__,
            //    generated-script variables) is dropped rather than blacklisted
            //    one by one.
            if (original != null)
            {
                foreach (string word in original)
                {
                    if (!AllowedFromGame(word)) continue;
                    if (seen.Add(word)) result.Add(word);
                }
            }

            return result;
        }

        /// <summary>
        /// Used by GetSubWordList: keeps the game's candidates (builtins, enum
        /// members) but strips language-specific names and adds keywords that
        /// match the fragment being completed.
        /// </summary>
        public static List<string> Filter(List<string> original, string fragment,
            string[] keywords)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>(256);

            if (original != null)
            {
                foreach (string word in original)
                {
                    if (string.IsNullOrEmpty(word)) continue;
                    if (word.StartsWith("__", StringComparison.Ordinal)) continue;
                    if (NotInSourceLanguage.Contains(word)) continue;
                    if (seen.Add(word)) result.Add(word);
                }
            }

            if (keywords != null)
            {
                foreach (string kw in keywords)
                {
                    if (fragment != null && fragment.Length > 0 &&
                        !kw.StartsWith(fragment, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (seen.Add(kw)) result.Add(kw);
                }
            }

            return result;
        }

        /// <summary>
        /// True when the caret sits inside a string, character literal or comment.
        /// The game's completer only looks at the word immediately before the
        /// caret, so it happily offers suggestions while you are typing text
        /// inside quotes. Scanning from the start of the file is cheap enough for
        /// an editor-sized buffer and needs no parser.
        ///
        /// This is the C-family rule (`//`, `/* */`, both quote styles). A
        /// language with different comment syntax must not use it.
        /// </summary>
        public static bool InsideStringOrComment(string text, int caret)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (caret < 0) caret = 0;
            if (caret > text.Length) caret = text.Length;

            bool inString = false, inChar = false, inBlock = false, inLine = false;
            for (int i = 0; i < caret; i++)
            {
                char c = text[i];
                char next = (i + 1 < caret) ? text[i + 1] : '\0';

                if (inLine)
                {
                    if (c == '\n') inLine = false;
                    continue;
                }
                if (inBlock)
                {
                    if (c == '*' && next == '/') { inBlock = false; i++; }
                    continue;
                }
                if (inString || inChar)
                {
                    char quote = inString ? '"' : '\'';
                    if (c == '\\') { i++; continue; }        // escaped quote
                    if (c == quote)
                    {
                        if (inString) inString = false; else inChar = false;
                    }
                    continue;
                }
                if (c == '/' && next == '/') { inLine = true; i++; continue; }
                if (c == '/' && next == '*') { inBlock = true; i++; continue; }
                if (c == '"') { inString = true; continue; }
                if (c == '\'') { inChar = true; continue; }
            }
            return inString || inChar || inBlock || inLine;
        }

        /// <summary>Extracts identifiers from source text.</summary>
        private static IEnumerable<string> Identifiers(string source)
        {
            if (string.IsNullOrEmpty(source)) yield break;

            // Preprocessor lines are not identifiers: without this, the IDE-only
            // `#include "cppfarmer_api.hpp"` would put `cppfarmer_api` and `hpp`
            // into the completion list.
            foreach (string line in source.Split('\n'))
            {
                if (line.TrimStart().StartsWith("#", StringComparison.Ordinal)) continue;

                var sb = new StringBuilder(32);
                foreach (char c in line)
                {
                    if (char.IsLetterOrDigit(c) || c == '_')
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (sb.Length > 0)
                    {
                        string word = sb.ToString();
                        sb.Clear();
                        if (word.Length > 1 && !char.IsDigit(word[0])) yield return word;
                    }
                }
                if (sb.Length > 1 && !char.IsDigit(sb[0])) yield return sb.ToString();
            }
        }
    }
}
