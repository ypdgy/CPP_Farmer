using System;
using HarmonyLib;

namespace CppFarmer
{
    /// <summary>
    /// Moves the "this line is running" overlay from the generated script onto
    /// the C++ source.
    ///
    /// BlinkManager draws that overlay from node positions, i.e. character
    /// indices into the text the game parsed. For a .cpp window that text is the
    /// generated script, while the editor shows the C++ source -- and comments,
    /// the preprocessor lines and blank lines produce no output at all. Every
    /// offset is therefore shifted backwards, and a small one lands in the
    /// header comment, which is exactly what "the highlight is inside the
    /// comment" looked like.
    ///
    /// Both overlay paths funnel through GetWorldWordRect (per-operation blink)
    /// and GetWorldLineRect (step-by-step full line), and both receive the
    /// offsets as arguments -- so remapping there leaves the node objects alone.
    /// That matters: breakpoint matching compares node wordStart against the
    /// *generated* offsets, so the node must keep its original value.
    /// </summary>
    internal static class CppBlinkMap
    {
        public static void Remap(CodeWindow window, ref int start, ref int end)
        {
            if (window == null || !Plugin.CfgEnabled.Value) return;
            if (!CppSupport.IsCppWindow(window)) return;

            CppSupport.CompiledUnit unit = CppSupport.GetUnit(window);
            if (unit == null) return;
            if (string.IsNullOrEmpty(unit.Generated) || string.IsNullOrEmpty(unit.Source)) return;
            if (unit.LineMap == null || unit.GeneratedLineStarts == null ||
                unit.SourceLineStarts == null)
            {
                return;
            }

            int genLine = LineIndexOf(unit.GeneratedLineStarts, start);
            if (genLine < 0 || genLine >= unit.LineMap.Length) return;

            int sourceLine = unit.LineMap[genLine];
            if (sourceLine <= 0)
            {
                // Output with no source line of its own (helpers such as cpp_sqrt,
                // struct constructors, the trailing main() call). Point at the next
                // real line rather than leaving the overlay at the top of the file.
                for (int i = genLine + 1; i < unit.LineMap.Length && i < genLine + 64; i++)
                {
                    if (unit.LineMap[i] > 0)
                    {
                        sourceLine = unit.LineMap[i];
                        break;
                    }
                }
                if (sourceLine <= 0) return;
            }

            int lineIndex = sourceLine - 1;
            if (lineIndex < 0 || lineIndex >= unit.SourceLineStarts.Length) return;

            string source = unit.Source;
            int lineStart = unit.SourceLineStarts[lineIndex];
            int lineEnd = lineIndex + 1 < unit.SourceLineStarts.Length
                ? unit.SourceLineStarts[lineIndex + 1] - 1
                : source.Length;
            if (lineEnd < lineStart) lineEnd = lineStart;

            // Prefer the exact token: a generated slice usually appears verbatim on
            // the source line (move(North), path.push_back(i)), which reproduces the
            // small rectangle the game draws for its own language. When it does not
            // -- std::cout became print(...), say -- highlight the whole line.
            int tokenStart = Math.Max(0, Math.Min(start, unit.Generated.Length));
            int tokenEnd = Math.Max(tokenStart, Math.Min(end, unit.Generated.Length));
            string token = unit.Generated.Substring(tokenStart, tokenEnd - tokenStart).Trim();
            if (token.Length >= 2 && token.Length <= 80 && token.IndexOf('\n') < 0)
            {
                int at = source.IndexOf(token, lineStart, StringComparison.Ordinal);
                if (at >= 0 && at + token.Length <= lineEnd && IsWholeWord(source, at, token.Length))
                {
                    start = at;
                    end = at + token.Length;
                    return;
                }
            }

            start = lineStart;
            end = lineEnd;
        }

        /// <summary>
        /// True when the match is not part of a longer name. Without this, the
        /// generated `pop(path)` would highlight the "pop" inside `path.pop_back()`
        /// -- same line, wrong token.
        /// </summary>
        private static bool IsWholeWord(string text, int at, int length)
        {
            bool leftOk = at == 0 || !IsWordChar(text[at - 1]);
            int after = at + length;
            bool rightOk = after >= text.Length || !IsWordChar(text[after]);
            return leftOk && rightOk;
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        /// <summary>Index of the line that contains <paramref name="offset"/>.</summary>
        private static int LineIndexOf(int[] starts, int offset)
        {
            if (starts == null || starts.Length == 0) return -1;
            int lo = 0, hi = starts.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (starts[mid] <= offset) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }
    }

    [HarmonyPatch(typeof(BlinkManager), "GetWorldWordRect")]
    internal static class PatchBlinkWordRect
    {
        [HarmonyPrefix]
        private static void Prefix(CodeWindow codeWindow, ref int start, ref int end)
        {
            try
            {
                CppBlinkMap.Remap(codeWindow, ref start, ref end);
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("blink word remap failed: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(BlinkManager), "GetWorldLineRect")]
    internal static class PatchBlinkLineRect
    {
        [HarmonyPrefix]
        private static void Prefix(CodeWindow codeWindow, ref int stringIndex)
        {
            try
            {
                int end = stringIndex;
                CppBlinkMap.Remap(codeWindow, ref stringIndex, ref end);
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("blink line remap failed: " + e.Message);
            }
        }
    }
}
