using System;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace CppFarmer
{
    /// <summary>
    /// Syntax colouring for .cpp windows.
    ///
    /// The game colours editor text with `CodeUtilities.SyntaxColor2`, whose rule
    /// table is written for its own language:
    ///
    ///     (?&lt;comment&gt;#.*)|(?&lt;string&gt;...)|(?&lt;keyword&gt;\b(?:in|for|def|...)\b)|...
    ///
    /// `#.*` treats every line starting with '#' as a comment, so `#include` shows
    /// up grey while `//` -- the actual C++ comment -- gets no colour at all.
    ///
    /// This mirrors that implementation exactly (same &lt;noparse&gt; wrapping, same
    /// search highlighting, same IME underline) with a C++ rule table.
    /// </summary>
    internal static class CppHighlighter
    {
        // Order matters: strings come first so that '//' inside a literal is not
        // taken for a comment, and the preprocessor rule sits before keywords so
        // that `include` is coloured as part of the directive.
        private const string Rules =
            @"(?<string>""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*')" +
            @"|(?<comment>//[^\n]*)" +
            @"|(?<preproc>(?m:^[ \t]*#[^\n]*))" +
            @"|(?<keyword>\b(?:alignas|alignof|auto|bool|break|case|catch|char|class|const|constexpr|continue|default|delete|do|double|else|enum|explicit|extern|false|float|for|friend|goto|if|inline|int|long|mutable|namespace|new|noexcept|nullptr|operator|override|private|protected|public|register|return|short|signed|sizeof|static|struct|switch|template|this|throw|true|try|typedef|typename|union|unsigned|using|virtual|void|volatile|while)\b)" +
            @"|(?<function>\b[a-zA-Z_]\w*(?=\())" +
            @"|(?<builtin>\b(?:North|East|South|West|Entities|Items|Grounds|Unlocks|Leaderboards|Hats)\b)" +
            @"|(?<number>\b(?:0[xX][0-9a-fA-F]+|(?:\d*\.)?\d+)\b)";

        public static string Colorize(string code, ColorTheme theme, string searchWord,
            int searchIndex, (int index, int length) imeCompositionRange)
        {
            if (code == null) return string.Empty;
            if (theme == null) return code;

            if (imeCompositionRange.length > 0 && searchIndex >= imeCompositionRange.index)
            {
                searchIndex += imeCompositionRange.length;
            }

            string pattern = string.IsNullOrEmpty(searchWord)
                ? Rules
                : "(?<search>(?i:" + Regex.Escape(searchWord) + ")(?-i))|" + Rules;

            var palette = theme.code;
            var sb = new StringBuilder("<noparse>");
            int consumed = 0;

            foreach (Match match in Regex.Matches(code, pattern))
            {
                sb.Append(Underline(code.Substring(consumed, match.Index - consumed), consumed,
                    imeCompositionRange));

                string body = Format(match.Value, match.Index, searchWord, searchIndex, theme,
                    imeCompositionRange);
                string color = null;

                if (match.Groups["string"].Success) color = palette.@string;
                else if (match.Groups["comment"].Success) color = palette.comment;
                else if (match.Groups["preproc"].Success) color = palette.keyword;
                else if (match.Groups["keyword"].Success) color = palette.keyword;
                else if (match.Groups["function"].Success) color = palette.function;
                else if (match.Groups["builtin"].Success) color = palette.builtin;
                else if (match.Groups["number"].Success) color = palette.number;

                sb.Append(color == null
                    ? body
                    : "</noparse><color=" + color + "><noparse>" + body + "</noparse></color><noparse>");

                consumed = match.Index + match.Length;
            }

            sb.Append(Underline(code.Substring(consumed), consumed, imeCompositionRange));
            sb.Append("</noparse>");
            return sb.ToString();
        }

        private static string Format(string value, int start, string searchWord, int searchIndex,
            ColorTheme theme, (int index, int length) ime)
        {
            if (string.IsNullOrEmpty(searchWord)) return Underline(value, start, ime);

            var sb = new StringBuilder();
            int consumed = 0;
            foreach (Match match in Regex.Matches(value, Regex.Escape(searchWord),
                         RegexOptions.IgnoreCase))
            {
                sb.Append(Underline(value.Substring(consumed, match.Index - consumed),
                    start + consumed, ime));
                string mark = start + match.Index == searchIndex
                    ? theme.ui.search_match_current
                    : theme.ui.search_match;
                sb.Append("</noparse><mark=" + mark + " from_search><noparse>");
                sb.Append(Underline(match.Value, start + match.Index, ime));
                sb.Append("</noparse></mark><noparse>");
                consumed = match.Index + match.Length;
            }
            sb.Append(Underline(value.Substring(consumed), start + consumed, ime));
            return sb.ToString();
        }

        private static string Underline(string value, int start, (int index, int length) ime)
        {
            int from = Math.Max(start, ime.index) - start;
            int to = Math.Min(start + value.Length, ime.index + ime.length) - start;
            if (ime.length <= 0 || from >= to) return value;
            return value.Substring(0, from) + "</noparse><u><noparse>"
                + value.Substring(from, to - from) + "</noparse></u><noparse>"
                + value.Substring(to);
        }
    }

    /// <summary>
    /// Re-applies colouring after the game has done it with its own rule table.
    ///
    /// Only two places ever assign codeText.text -- UpdateSearch (bound to the
    /// input field's onValueChanged, so it runs on every keystroke) and
    /// OnThemeChanged -- so patching both covers typing and theme switches.
    /// </summary>
    internal static class CppHighlight
    {
        private static FieldInfo _codeText;
        private static FieldInfo _codeInput;

        // Last text coloured per window. Refresh is driven by UpdateSearch, which
        // *should* be bound to onValueChanged -- but that binding lives in the
        // prefab and cannot be verified statically. Comparing text in LateUpdate
        // makes typing recolour even if the binding is absent; the comparison is
        // one string reference check per frame once the values match.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<CodeWindow, string> Seen =
            new System.Collections.Concurrent.ConcurrentDictionary<CodeWindow, string>();

        public static void Tick(CodeWindow window)
        {
            if (!Plugin.CfgEnabled.Value) return;
            if (window == null || !CppSupport.IsCppWindow(window)) return;

            try
            {
                var input = GetInput(window);
                if (input == null) return;

                string current = input.TextWithComposition;
                if (Seen.TryGetValue(window, out string previous) &&
                    string.Equals(previous, current, StringComparison.Ordinal))
                {
                    return;
                }
                Refresh(window, string.Empty, -1);
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("cpp highlight tick failed: " + e.Message);
            }
        }

        private static TMPro.CodeInputField GetInput(CodeWindow window)
        {
            if (_codeInput == null) _codeInput = AccessTools.Field(typeof(CodeWindow), "codeInput");
            return _codeInput?.GetValue(window) as TMPro.CodeInputField;
        }

        public static void Refresh(CodeWindow window, string searchWord, int searchIndex)
        {
            if (!Plugin.CfgEnabled.Value) return;
            if (window == null || !CppSupport.IsCppWindow(window)) return;

            try
            {
                if (_codeText == null) _codeText = AccessTools.Field(typeof(CodeWindow), "codeText");

                var text = _codeText?.GetValue(window) as TMPro.TextMeshProUGUI;
                var input = GetInput(window);
                if (text == null || input == null) return;

                Seen[window] = input.TextWithComposition;

                ThemeManager manager = ThemeManager.Inst;
                if (manager == null) return;

                // Assigning the text makes TMP rebuild the mesh, which can make the
                // window re-run its layout; keep the scroll offset we started with
                // so colouring never moves the view on its own.
                var inputRect = input.transform as RectTransform;
                Vector2 scroll = inputRect != null ? inputRect.anchoredPosition : Vector2.zero;

                text.text = CppHighlighter.Colorize(input.TextWithComposition, manager.Theme,
                    searchWord, searchIndex, input.IMECompositionRange);

                if (inputRect != null && inputRect.anchoredPosition != scroll)
                {
                    inputRect.anchoredPosition = scroll;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("cpp highlight failed: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(CodeWindow), nameof(CodeWindow.UpdateSearch))]
    internal static class PatchCppHighlightSearch
    {
        [HarmonyPostfix]
        private static void Postfix(CodeWindow __instance, string word, int selectedIndex)
        {
            CppHighlight.Refresh(__instance, word, selectedIndex);
        }
    }

    [HarmonyPatch(typeof(CodeWindow), "OnThemeChanged")]
    internal static class PatchCppHighlightTheme
    {
        [HarmonyPostfix]
        private static void Postfix(CodeWindow __instance)
        {
            CppHighlight.Refresh(__instance, string.Empty, -1);
        }
    }

    [HarmonyPatch(typeof(CodeWindow), "LateUpdate")]
    internal static class PatchCppHighlightTick
    {
        [HarmonyPostfix]
        private static void Postfix(CodeWindow __instance)
        {
            CppHighlight.Tick(__instance);
        }
    }

    /// <summary>
    /// Keeps a .cpp window scrolled where it was when it loses focus.
    ///
    /// Clicking another window deactivates the input, and TMP then rebuilds the
    /// text mesh, which the game answers with CodeWindow.OnCodeChanged ->
    /// ScrollToStringPosition(caret - 1). When the caret was never placed -- the
    /// player scrolled with the wheel and only read the file -- that index is 0,
    /// and "scroll to position 0" means jumping back to the first line.
    ///
    /// Only that one case is suppressed: an unfocused window, a target at the very
    /// start, and a view that is currently scrolled down. Real caret moves and
    /// "reveal error" (which targets a concrete token) are untouched.
    /// </summary>
    [HarmonyPatch(typeof(CodeWindow), nameof(CodeWindow.ScrollToStringPosition))]
    internal static class PatchKeepScrollOnBlur
    {
        [HarmonyPrefix]
        private static bool Prefix(CodeWindow __instance, int stringPosition)
        {
            try
            {
                if (!Plugin.CfgEnabled.Value) return true;
                if (__instance == null || !CppSupport.IsCppWindow(__instance)) return true;

                TMPro.CodeInputField input = CppSupport.GetCodeInput(__instance);
                if (input == null || input.isFocused) return true;
                if (stringPosition > 1) return true;                     // a real target

                var rect = input.transform as RectTransform;
                if (rect == null || rect.anchoredPosition.y <= 1f) return true;   // already at top

                return false;                                            // spurious jump to line 1
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("scroll guard failed: " + e.Message);
                return true;
            }
        }
    }
}
