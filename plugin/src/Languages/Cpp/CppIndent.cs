using System;
using HarmonyLib;
using TMPro;

namespace CppFarmer
{
    /// <summary>
    /// Block auto-indent for .cpp windows.
    ///
    /// CodeWindow.InsertNewLineIndentation copies the current line's indent and
    /// adds exactly one level only when the text before the newline ends with
    /// ':'. In a .cpp window there is never a colon, so "void main() {" leaves
    /// the next line at column 0 and every body line has to be indented by hand.
    ///
    /// This replacement keeps everything the game does (reuse the current line's
    /// indentation, drop trailing blanks, move the caret past the inserted
    /// indent) and adds one level when the line ends with an opening brace. A
    /// trailing // comment after the brace is ignored, so "if (x) { // note"
    /// still opens a block.
    /// </summary>
    [HarmonyPatch(typeof(CodeWindow), "InsertNewLineIndentation")]
    internal static class PatchCppIndent
    {
        private const string SpaceIndent = "    ";
        private const string TabIndent = "\t";

        [HarmonyPrefix]
        private static bool Prefix(CodeWindow __instance, string inserted)
        {
            if (!Plugin.CfgEnabled.Value || !Plugin.CfgSmartIndent.Value) return true;
            if (__instance == null || inserted == null) return true;
            if (!inserted.EndsWith("\n", StringComparison.Ordinal)) return true;
            if (!CppLanguage.Instance.Owns(__instance)) return true;

            CodeInputField input = CodeSupport.GetCodeInput(__instance);
            if (input == null) return true;

            string text = input.text;
            if (string.IsNullOrEmpty(text)) return true;

            int caret = input.stringPosition;
            int newline = caret - 1;                       // the '\n' just typed
            if (newline <= 0 || newline >= text.Length) return true;
            if (text[newline] != '\n') return true;

            int lineStart = newline - 1;
            while (lineStart >= 0 && text[lineStart] != '\n') lineStart--;
            lineStart++;                                   // first char of the current line

            int bodyStart = lineStart;
            while (bodyStart < newline && (text[bodyStart] == ' ' || text[bodyStart] == '\t')) bodyStart++;

            string indent = text.Substring(lineStart, bodyStart - lineStart);
            string body = StripLineComment(text.Substring(bodyStart, newline - bodyStart)).TrimEnd();

            if (body.Length > 0 && body[body.Length - 1] == '{'
                && !CppWords.InsideStringOrComment(text, newline - 1))
            {
                // Stay in whatever style the line already uses: a tab-indented
                // line keeps tabs, everything else gets four spaces.
                indent += indent.StartsWith("\t", StringComparison.Ordinal) ? TabIndent : SpaceIndent;
            }

            if (indent.Length > 0)
            {
                input.text = text.Insert(newline + 1, indent);
                input.stringPosition += indent.Length;
            }
            return false;                                  // handled: original would not add the block level
        }

        /// <summary>"int x; // note" -> "int x; ". Streaming comments included.</summary>
        private static string StripLineComment(string body)
        {
            int slash = body.IndexOf("//", StringComparison.Ordinal);
            return slash < 0 ? body : body.Substring(0, slash);
        }
    }
}
