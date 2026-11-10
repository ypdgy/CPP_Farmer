using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CppFarmer
{
    /// <summary>
    /// Adds an entry button for the reference page.
    ///
    /// The toolbar buttons are wired in the prefab, not in code, so they cannot be
    /// reached by field name. Instead one of them is located through its persistent
    /// UnityEvent target ("CallAddNewDocsWindow"), then cloned: the clone inherits
    /// the same sprite, rounded shape, size and hover colours as the rest of the
    /// bar. Only the glyph is replaced with a "C++" label.
    /// </summary>
    internal class HelpButton : MonoBehaviour
    {
        private const string DocsMethodName = "CallAddNewDocsWindow";

        private GameObject _button;
        private float _nextAttempt;
        private int _attempts;

        private void Update()
        {
            if (Plugin.CfgHelpButton == null || !Plugin.CfgHelpButton.Value) return;
            if (_button != null) return;
            if (Time.unscaledTime < _nextAttempt) return;

            _nextAttempt = Time.unscaledTime + 0.5f;

            Workspace workspace = MainSim.Inst == null ? null : MainSim.Inst.workspace;
            if (workspace == null)
            {
                _attempts = 0;                  // left the game: retry next time
                return;
            }
            if (++_attempts > 60) return;       // 30s of retries, then fall back
            AttachToToolbar(workspace);
        }

        private void AttachToToolbar(Workspace workspace)
        {
            try
            {
                Button template = FindTemplateButton();
                if (template == null) return;

                Transform parent = template.transform.parent;
                GameObject clone = Instantiate(template.gameObject, parent);
                clone.name = "CppFarmer_HelpButton";
                clone.SetActive(true);

                // Drop the cloned persistent listener, otherwise the copy would
                // keep opening the game's own docs window.
                Button button = clone.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick = new Button.ButtonClickedEvent();
                    button.onClick.AddListener(OpenHelp);
                }

                ApplyLabel(clone);
                PlaceLeftOfSiblings(clone, template);

                _button = clone;
                Plugin.Log.LogInfo("C++ reference button added to the toolbar");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("toolbar button not created, using on-screen fallback: " + e.Message);
            }
        }

        /// <summary>Finds a bar button by the method its click event points at.</summary>
        private static Button FindTemplateButton()
        {
            Button fallback = null;
            foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
            {
                if (button == null || button.gameObject == null) continue;
                if (!button.gameObject.scene.IsValid()) continue;   // skip prefab assets

                var ev = button.onClick;
                for (int i = 0; i < ev.GetPersistentEventCount(); i++)
                {
                    string method = ev.GetPersistentMethodName(i);
                    if (string.IsNullOrEmpty(method)) continue;
                    if (method == DocsMethodName) return button;
                    if (method == "AddNewWindow" && fallback == null) fallback = button;
                }
            }
            return fallback;
        }

        /// <summary>
        /// Replaces the cloned glyph with a "C++" label.
        ///
        /// The toolbar buttons are ColoredButton instances, which split their visual
        /// into three serialized parts: buttonPanel (the coloured background), icon
        /// (the glyph) and nameText. Disabling every Image under the clone -- the
        /// first attempt -- also killed the background, so the button rendered as
        /// bare text floating over the screen. Only the icon may be hidden here.
        /// </summary>
        private static void ApplyLabel(GameObject clone)
        {
            ColoredButton colored = clone.GetComponent<ColoredButton>();
            if (colored != null)
            {
                // Keep the background panel alive; it *is* the green rounded plate.
                if (AccessTools.Field(typeof(ColoredButton), "buttonPanel")?.GetValue(colored) is Image panel)
                {
                    panel.enabled = true;
                }

                if (AccessTools.Field(typeof(ColoredButton), "icon")?.GetValue(colored) is Image icon)
                {
                    icon.enabled = false;
                }

                // Reuse the button's own text component when the prefab has one: it
                // already gets themed colours and sits inside the correct layout.
                if (AccessTools.Field(typeof(ColoredButton), "nameText")?.GetValue(colored)
                    is TMPro.TextMeshProUGUI nameText)
                {
                    nameText.gameObject.SetActive(true);
                    nameText.enabled = true;
                    nameText.text = "C++";
                    nameText.alignment = TMPro.TextAlignmentOptions.Center;
                    nameText.enableAutoSizing = true;
                    nameText.fontSizeMin = 6f;
                    nameText.fontSizeMax = 24f;
                    nameText.overflowMode = TMPro.TextOverflowModes.Overflow;
                    Stretch(nameText.rectTransform);
                    ApplyThemeTextColor(nameText);
                    return;
                }
            }

            // No ColoredButton and no text slot: hide only the small glyph images,
            // never anything that fills the button (that would be the background).
            var full = ((RectTransform)clone.transform).rect.size;
            bool measurable = full.x > 1f && full.y > 1f;
            foreach (Image image in clone.GetComponentsInChildren<Image>(true))
            {
                if (!measurable)
                {
                    // Layout not computed yet: keep the root plate, drop the rest.
                    if (image.transform != clone.transform) image.enabled = false;
                    continue;
                }
                Rect rect = image.rectTransform.rect;
                bool coversButton = rect.width >= full.x * 0.9f && rect.height >= full.y * 0.9f;
                if (!coversButton) image.enabled = false;
            }
            foreach (TMPro.TMP_Text existing in clone.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                existing.text = string.Empty;
            }

            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(clone.transform, false);
            Stretch((RectTransform)label.transform);

            var tmp = label.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = "C++";
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 6f;
            tmp.fontSizeMax = 24f;
            tmp.raycastTarget = false;
            if (TMPro.TMP_Settings.defaultFontAsset != null)
            {
                tmp.font = TMPro.TMP_Settings.defaultFontAsset;
            }
            ApplyThemeTextColor(tmp);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ApplyThemeTextColor(TMPro.TMP_Text text)
        {
            try
            {
                if (ThemeManager.Inst != null)
                {
                    text.color = ThemeManager.Inst.Theme.ui.ButtonTextColor;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("theme colour unavailable: " + e.Message);
            }
        }

        /// <summary>
        /// Moves the clone to the left end of the button row.
        ///
        /// Only siblings that are themselves Buttons are measured: the toolbar also
        /// holds plain labels, and including them pushed the new button far to the
        /// left of the row (visible as a gap in the screenshot).
        /// </summary>
        private static void PlaceLeftOfSiblings(GameObject clone, Button template)
        {
            var templateRect = (RectTransform)template.transform;
            var cloneRect = (RectTransform)clone.transform;

            cloneRect.anchorMin = templateRect.anchorMin;
            cloneRect.anchorMax = templateRect.anchorMax;
            cloneRect.pivot = templateRect.pivot;
            cloneRect.sizeDelta = templateRect.sizeDelta;
            cloneRect.localScale = templateRect.localScale;

            Transform parent = clone.transform.parent;
            float minX = templateRect.anchoredPosition.x;
            float maxX = minX;

            foreach (Button other in parent.GetComponentsInChildren<Button>(true))
            {
                var rect = other.transform as RectTransform;
                if (rect == null || rect.parent != parent || rect == cloneRect) continue;
                minX = Mathf.Min(minX, rect.anchoredPosition.x);
                maxX = Mathf.Max(maxX, rect.anchoredPosition.x);
            }

            float width = templateRect.rect.width > 1f ? templateRect.rect.width : templateRect.sizeDelta.x;
            const float gap = 8f;

            // Anchored to the right edge: "further left" means a smaller x.
            bool rightAnchored = templateRect.anchorMin.x > 0.5f || templateRect.anchorMax.x > 0.5f;
            float x = rightAnchored ? minX - (width + gap) : maxX + (width + gap);

            cloneRect.anchoredPosition = new Vector2(x, templateRect.anchoredPosition.y);
        }

        /// <summary>
        /// Fallback entry point, only used if cloning the toolbar button failed.
        /// </summary>
        private void OnGUI()
        {
            if (_button != null) return;
            if (_attempts <= 60) return;                  // still trying to clone
            if (Plugin.CfgHelpButton == null || !Plugin.CfgHelpButton.Value) return;
            if (MainSim.Inst == null || MainSim.Inst.workspace == null) return;

            float rightMargin = Plugin.CfgHelpButtonX == null ? 10f : Plugin.CfgHelpButtonX.Value;
            float topMargin = Plugin.CfgHelpButtonY == null ? 8f : Plugin.CfgHelpButtonY.Value;
            var rect = new Rect(Screen.width - 104f - rightMargin, topMargin + 40f, 104f, 28f);
            if (GUI.Button(rect, "C++ Farmer"))
            {
                OpenHelp();
            }
        }

        /// <summary>
        /// Opens the reference page in a normal docs window.
        ///
        /// DocsWindow derives from MonoBehaviour, not from Window, so it is not
        /// part of workspace.OpenWindows; showing a second copy is the simplest
        /// correct behaviour and matches how the game opens docs pages.
        /// </summary>
        public static void OpenHelp()
        {
            try
            {
                Workspace workspace = MainSim.Inst == null ? null : MainSim.Inst.workspace;
                if (workspace == null) return;

                // Index 0 is the default language; a multi-language build would
                // replace this with a small menu.
                string docId = LanguageRegistry.Default?.ReferenceDocId;
                if (string.IsNullOrEmpty(docId)) return;
                workspace.AddNewDocsWindow(docId, Vector2.zero);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("cannot open the reference page: " + e);
            }
        }
    }
}
