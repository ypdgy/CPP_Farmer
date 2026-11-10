using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CppFarmer
{
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
}
