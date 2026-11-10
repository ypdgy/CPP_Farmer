using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace CppFarmer
{
    /// <summary>
    /// Replaces the completer vocabulary for windows whose language defines its
    /// own. The game derives its candidates from Python keywords plus builtins,
    /// which is actively misleading for any other language; each profile decides
    /// what to offer instead (C++ rebuilds it as: identifiers in this file ->
    /// C++ keywords -> the game's builtins).
    /// </summary>
    [HarmonyPatch(typeof(CodeWindow), "GetWordList")]
    internal static class PatchGetWordList
    {
        [HarmonyPostfix]
        private static void Postfix(CodeWindow __instance, ref List<string> __result)
        {
            if (!Plugin.CfgEnabled.Value) return;
            LanguageProfile language = LanguageRegistry.ForWindow(__instance);
            if (language == null) return;

            try
            {
                __result = language.BuildCompletion(__result, CodeSupport.GetSource(__instance));
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("word list rebuild failed: " + e.Message);
            }
        }
    }

    /// <summary>
    /// GetSubWordList feeds the completer when the caret sits inside a longer
    /// word. Same treatment: let the language filter the candidates.
    /// </summary>
    [HarmonyPatch(typeof(CodeWindow), "GetSubWordList")]
    internal static class PatchGetSubWordList
    {
        [HarmonyPostfix]
        private static void Postfix(CodeWindow __instance, object[] __args, ref List<string> __result)
        {
            if (!Plugin.CfgEnabled.Value) return;
            LanguageProfile language = LanguageRegistry.ForWindow(__instance);
            if (language == null) return;

            try
            {
                string fragment = __args != null && __args.Length > 0 ? __args[0] as string : null;
                __result = language.FilterCompletion(__result, fragment);
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("sub word list rebuild failed: " + e.Message);
            }
        }
    }

    /// <summary>
    /// Stops the completer from opening inside strings, character literals and
    /// comments, where it can only get in the way.
    /// OpenCodeCompleterAtCarret() is the single entry point that eventually calls
    /// CodeCompleter.Open, so skipping it leaves no empty popup.
    /// </summary>
    [HarmonyPatch(typeof(CodeWindow), nameof(CodeWindow.OpenCodeCompleterAtCarret))]
    internal static class PatchNoCompleteInStrings
    {
        [HarmonyPrefix]
        private static bool Prefix(CodeWindow __instance)
        {
            if (!Plugin.CfgEnabled.Value) return true;
            if (Plugin.CfgNoCompleteInStrings == null || !Plugin.CfgNoCompleteInStrings.Value) return true;
            LanguageProfile language = LanguageRegistry.ForWindow(__instance);
            if (language == null) return true;

            try
            {
                var input = CodeSupport.GetCodeInput(__instance);
                if (input == null) return true;

                string text = input.text ?? string.Empty;
                int caret = Mathf.Clamp(input.stringPosition, 0, text.Length);
                return !language.InsideLiteralOrComment(text, caret);
            }
            catch
            {
                return true;                         // never break typing
            }
        }
    }
}
