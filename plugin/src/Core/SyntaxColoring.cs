using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CppFarmer
{
    /// <summary>
    /// Applies a language's syntax colouring to one window.
    ///
    /// The game colours editor text with `CodeUtilities.SyntaxColor2`, whose rule
    /// table is written for its own language:
    ///
    ///     (?&lt;comment&gt;#.*)|(?&lt;string&gt;...)|(?&lt;keyword&gt;(?:in|for|def|...))|...
    ///
    /// `#.*` treats every line starting with '#' as a comment, so `#include` shows
    /// up grey while `//` -- the actual comment in C-family languages -- gets no
    /// colour at all.
    ///
    /// This mirrors that implementation exactly (same &lt;noparse&gt; wrapping, same
    /// search highlighting, same IME underline) but takes the rule table from the
    /// owning profile. Group names are the contract: `string`, `comment`,
    /// `preproc`, `keyword`, `function`, `builtin`, `number`.
    /// </summary>
    internal static class SyntaxColoring
    {
        public static string Colorize(string code, string rules, ColorTheme theme,
            string searchWord, int searchIndex, (int index, int length) imeCompositionRange)
        {
            if (code == null) return string.Empty;
            if (theme == null || string.IsNullOrEmpty(rules)) return code;

            if (imeCompositionRange.length > 0 && searchIndex >= imeCompositionRange.index)
            {
                searchIndex += imeCompositionRange.length;
            }

            string pattern = string.IsNullOrEmpty(searchWord)
                ? rules
                : "(?<search>(?i:" + Regex.Escape(searchWord) + ")(?-i))|" + rules;

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
}
