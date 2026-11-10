using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CppFarmer
{
    /// <summary>
    /// Re-applies the owning language's colouring after the game has done it with
    /// its own rule table.
    ///
    /// Only two places ever assign codeText.text -- UpdateSearch (bound to the
    /// input field's onValueChanged, so it runs on every keystroke) and
    /// OnThemeChanged -- so patching both covers typing and theme switches. A
    /// language that returns null from <see cref="LanguageProfile.Colorize"/>
    /// keeps the game's output untouched.
    /// </summary>
    internal static class EditorHighlight
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
            if (window == null || !LanguageRegistry.IsModWindow(window)) return;

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
                Plugin.Log.LogDebug("highlight tick failed: " + e.Message);
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
            LanguageProfile language = LanguageRegistry.ForWindow(window);
            if (language == null) return;

            try
            {
                if (_codeText == null) _codeText = AccessTools.Field(typeof(CodeWindow), "codeText");

                var text = _codeText?.GetValue(window) as TMPro.TextMeshProUGUI;
                var input = GetInput(window);
                if (text == null || input == null) return;

                ThemeManager manager = ThemeManager.Inst;
                if (manager == null) return;

                string colored = language.Colorize(input.TextWithComposition, manager.Theme,
                    searchWord, searchIndex, input.IMECompositionRange);
                if (colored == null) return;         // this language leaves colouring to the game

                Seen[window] = input.TextWithComposition;

                // Assigning the text makes TMP rebuild the mesh, which can make the
                // window re-run its layout; keep the scroll offset we started with
                // so colouring never moves the view on its own.
                var inputRect = input.transform as RectTransform;
                Vector2 scroll = inputRect != null ? inputRect.anchoredPosition : Vector2.zero;

                text.text = colored;

                if (inputRect != null && inputRect.anchoredPosition != scroll)
                {
                    inputRect.anchoredPosition = scroll;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("highlight failed: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(CodeWindow), nameof(CodeWindow.UpdateSearch))]
    internal static class PatchHighlightSearch
    {
        [HarmonyPostfix]
        private static void Postfix(CodeWindow __instance, string word, int selectedIndex)
        {
            EditorHighlight.Refresh(__instance, word, selectedIndex);
        }
    }

    [HarmonyPatch(typeof(CodeWindow), "OnThemeChanged")]
    internal static class PatchHighlightTheme
    {
        [HarmonyPostfix]
        private static void Postfix(CodeWindow __instance)
        {
            EditorHighlight.Refresh(__instance, string.Empty, -1);
        }
    }

    [HarmonyPatch(typeof(CodeWindow), "LateUpdate")]
    internal static class PatchHighlightTick
    {
        [HarmonyPostfix]
        private static void Postfix(CodeWindow __instance)
        {
            EditorHighlight.Tick(__instance);
        }
    }
}
