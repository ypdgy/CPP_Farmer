using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace CppFarmer
{
    /// <summary>
    /// Word list used by the in-game code completer for .cpp windows.
    ///
    /// The game builds its candidate list from Python keywords plus every builtin
    /// it knows about. For a C++ file that list is actively misleading, so we
    /// rebuild it: identifiers from the current file first, then C++ keywords,
    /// then the game's builtins and enum names (which C++ code still needs,
    /// because world access goes through them).
    /// </summary>
    internal static class CppWords
    {
        /// <summary>
        /// Words the front-end actually accepts. Valid C++ that it rejects on
        /// purpose (switch/case/sizeof/new/delete/operator/this/virtual/
        /// template/namespace, and the std::set / std::pair types) stays out:
        /// offering them here would only walk the player into a compile error.
        /// Writing one by hand is still reported by name, with the reason.
        /// </summary>
        private static readonly string[] Keywords =
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

        /// <summary>
        /// Names the game offers that only make sense in its own language.
        /// They stay out of the C++ list so the completer stops suggesting them.
        /// </summary>
        private static readonly HashSet<string> NotInCpp = new HashSet<string>(StringComparer.Ordinal)
        {
            "def", "elif", "import", "from", "as", "global", "lambda", "yield",
            "with", "try", "except", "finally", "raise", "assert", "del", "is",
            "async", "await", "nonlocal", "pass", "and", "or", "not", "in",
        };

        /// <summary>
        /// The only names from the game's own list worth offering in C++.
        /// GetWordList mixes builtins with GetUnlockedKeywords() (every Python
        /// keyword), tokenizedIdentifiers, cachedProgram.allVars -- variables of
        /// the *generated* script -- and a hard-coded "__name__". A blacklist can
        /// never cover all of that, so Rebuild uses a whitelist instead.
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
            if (NotInCpp.Contains(word)) return false;
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

        public static List<string> Rebuild(List<string> original, string source)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>(512);

            // 1. identifiers already present in the file, so the player's own
            //    names sort naturally next to the keywords
            foreach (string ident in Identifiers(source))
            {
                if (seen.Add(ident)) result.Add(ident);
            }

            // 2. C++ keywords
            foreach (string kw in Keywords)
            {
                if (seen.Add(kw)) result.Add(kw);
            }

            // 3. only the game names that are genuinely callable from C++.
            //    Everything else (Python keywords, __name__, generated-script
            //    variables) is dropped rather than blacklisted one by one.
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
        /// members) but strips language-specific names and adds C++ keywords
        /// that match the fragment being completed.
        /// </summary>
        public static List<string> Filter(List<string> original, string fragment)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>(256);

            if (original != null)
            {
                foreach (string word in original)
                {
                    if (string.IsNullOrEmpty(word)) continue;
                    if (word.StartsWith("__", StringComparison.Ordinal)) continue;
                    if (NotInCpp.Contains(word)) continue;
                    if (seen.Add(word)) result.Add(word);
                }
            }

            foreach (string kw in Keywords)
            {
                if (fragment != null && fragment.Length > 0 &&
                    !kw.StartsWith(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (seen.Add(kw)) result.Add(kw);
            }

            return result;
        }

        /// <summary>
        /// True when the caret sits inside a string, character literal or comment.
        /// The game's completer only looks at the word immediately before the
        /// caret, so it happily offers suggestions while you are typing text
        /// inside quotes. Scanning from the start of the file is cheap enough for
        /// an editor-sized buffer and needs no parser.
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

        /// <summary>Extracts C++ identifiers from source text.</summary>
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

    /// <summary>
    /// Stops the completer from opening inside C++ strings, character literals
    /// and comments. OpenCodeCompleterAtCarret() is the single entry point that
    /// eventually calls CodeCompleter.Open, so skipping it leaves no empty popup.
    /// </summary>
    [HarmonyPatch(typeof(CodeWindow), nameof(CodeWindow.OpenCodeCompleterAtCarret))]
    internal static class PatchNoCompleteInStrings
    {
        [HarmonyPrefix]
        private static bool Prefix(CodeWindow __instance)
        {
            if (!Plugin.CfgEnabled.Value) return true;
            if (Plugin.CfgNoCompleteInStrings == null || !Plugin.CfgNoCompleteInStrings.Value) return true;
            if (__instance == null || !CppSupport.IsCppWindow(__instance)) return true;

            try
            {
                var input = CppSupport.GetCodeInput(__instance);
                if (input == null) return true;

                string text = input.text ?? string.Empty;
                int caret = Mathf.Clamp(input.stringPosition, 0, text.Length);
                return !CppWords.InsideStringOrComment(text, caret);
            }
            catch
            {
                return true;                         // never break typing
            }
        }
    }
}
