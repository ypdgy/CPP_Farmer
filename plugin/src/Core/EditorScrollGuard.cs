using System;
using HarmonyLib;
using UnityEngine;

namespace CppFarmer
{
    /// <summary>
    /// Keeps a code window scrolled where it was when it loses focus.
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
                if (__instance == null || !LanguageRegistry.IsModWindow(__instance)) return true;

                TMPro.CodeInputField input = CodeSupport.GetCodeInput(__instance);
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
