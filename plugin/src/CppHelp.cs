using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CppFarmer
{
    /// <summary>
    /// A self-contained reference page for the C++ mode, shown in a normal docs
    /// window.
    ///
    /// Localizer.LoadDoc is asked for a virtual document id and only that id is
    /// answered. The one intentional exception is the docs home page, which gets
    /// a single extra link pointing at that virtual id. No file on disk changes.
    /// </summary>
    [HarmonyPatch(typeof(Localizer), nameof(Localizer.LoadDoc))]
    internal static class PatchLoadDoc
    {
        private const string HomeDoc = "docs/home.md";

        [HarmonyPostfix]
        private static void Postfix(string doc, ref string __result)
        {
            if (CppReference.IsVirtual(doc))
            {
                try
                {
                    __result = CppReference.Load(doc);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError("cannot build C++ reference page: " + e);
                }
                return;
            }

            if (Plugin.CfgHomeLink != null && Plugin.CfgHomeLink.Value &&
                string.Equals(doc, HomeDoc, StringComparison.Ordinal))
            {
                __result = CppHomeLink.Inject(__result);
            }
        }
    }

    /// <summary>
    /// Adds one link to the docs home page.
    ///
    /// Inserted right after the first &lt;columns&gt; opener so it sits alongside
    /// the existing "General Info" links instead of being a stray paragraph.
    ///
    /// The link line contains no backticks. CodeUtilities.ApplyCodeTags splits the
    /// whole page on backticks and treats odd segments as code, so a backtick in
    /// inserted text would flip every following segment -- the exact failure from
    /// the earlier docs injection attempt. Inserting backtick-free text at any
    /// position leaves all existing pairing untouched.
    /// </summary>
    internal static class CppHomeLink
    {
        private const string Link = "[C++ Farmer 说明](cppfarmer_reference)";

        public static string Inject(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return markdown;
            if (markdown.Contains("](cppfarmer_reference)", StringComparison.Ordinal)) return markdown;

            int columns = markdown.IndexOf("<columns>", StringComparison.Ordinal);
            if (columns < 0) return markdown;
            int newline = markdown.IndexOf('\n', columns);
            if (newline < 0) return markdown;

            return markdown.Insert(newline + 1, Link + "\n");
        }
    }

    internal static class CppReference
    {
        /// <summary>Virtual document ids; never real files under StreamingAssets.</summary>
        public const string DocId = "cppfarmer_reference";

        public const string ErrorsDocId = "cppfarmer_errors";

        public static bool IsVirtual(string doc)
        {
            return string.Equals(doc, DocId, StringComparison.Ordinal) ||
                   string.Equals(doc, ErrorsDocId, StringComparison.Ordinal);
        }

        /// <summary>Optional override next to the plugin, so a page can be edited without a rebuild.</summary>
        private static string OverridePath(string fileName)
        {
            return System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".",
                fileName);
        }

        public static string Load(string doc)
        {
            bool errors = string.Equals(doc, ErrorsDocId, StringComparison.Ordinal);
            string file = OverridePath(errors ? "cppfarmer_errors.md" : "cppfarmer_reference.md");
            try
            {
                if (System.IO.File.Exists(file))
                {
                    return System.IO.File.ReadAllText(file);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("cannot read " + file + ": " + e.Message);
            }
            return errors ? ErrorsDefault : Default;
        }

        // Markdown supported by the game's renderer: headings, bold, inline code,
        // links, <columns>, <unlock=...>. Kept to those so it cannot misparse.
        // Inline code must stay in *pairs*: the renderer splits the whole page on
        // backticks and treats every other segment as code.
        private const string Default = @"# C++ Farmer v0.1测试版
**警告**: 这个mod是vibe coding做出的，可能存在大量bug，若出现bug请前往github提出issue。

C++ Farmer 让你在游戏内直接用 C++ 写农场脚本。文件名以 `.cpp` 结尾的代码窗口，按下运行键时会先被编译成游戏本身的脚本，然后照常执行。

**重要**：编译产物仍然是游戏语言，所以操作数消耗、成就统计、科技树解锁与手写脚本完全一致。

按运行键报错时，先看 [常见错误与函数调用](cppfarmer_errors)：那里列了所有会被明确拒绝的写法。

## 文件与运行

把代码窗口改名为以 `.cpp` 结尾即可。编译错误显示在同一个错误面板里，行号对应你的 C++ 源码，改完再按运行键，不必重启游戏。

## 函数与入口

函数用返回类型声明，程序入口是 `main`。`main` 会在所有函数定义之后被自动调用。

`int square(int v) { return v * v; }`

`void main() { std::cout << square(3) << std::endl; }`

## 变量与类型

声明时带类型：`int`、`float`、`double`、`bool`、`char`、`std::string`，也支持 `auto`。
数组写成 `int grid[9];`，向量写成 `std::vector<int> v;`。

## 控制流

`if`、`else`、`while`、`for`、`break`、`continue`、`return` 与 C++ 一致。

`for (int i = 0; i < n; i++) { }`

`for (int x : items) { }`

计数循环会被编译成游戏的 `range`，因此操作数消耗与手写 `for i in range(n)` 相同。

## 游戏内置函数

游戏提供的操作在 C++ 里同名直通，调用方式不变。

`move(North);`  `harvest();`  `can_harvest();`

`plant(Entities.Bush);`  `till();`  `use_item(Items.Water);`

`get_water();`  `get_pos_x();`  `get_pos_y();`  `get_world_size();`

枚举沿用点号写法：`Entities.Tree`、`Items.Hay`、`Grounds.Soil`、`Unlocks.Carrots`。
游戏里的 `None`、`True`、`False` 在 C++ 里照常可用。

## 输出

推荐用 `std::cout << ""文本"" << std::endl;`，可以连续拼接多段。

也可以用游戏内置的 `print(""文本"");`，或只含纯字符串的 `printf(""文本"");`。
`printf` 的百分号占位符不支持，会明确报错，请改用 `std::cout`。

## 容器

`std::vector<int> v;`

`v.push_back(1);`  `v.pop_back();`  `int n = v.size();`  `v.empty()`

## 多文件

`#include ""hello.cpp""`

被包含的文件一起参与编译，重复包含会自动忽略，可以省略 include guard。
查找顺序：已打开的代码窗口优先，其次是存档目录下的同名文件。

## 结构体与枚举

`struct Point { int x; int y; };`

`enum Mode { IDLE, RUN };`

结构体编译成字典，字段用 `p.x` 读写；枚举成员会加上枚举名前缀，避免与游戏内置名冲突。

## 断点与单步

在行号栏点击即可下断点，跨文件也能命中。单步调试与原生行为一致。

## 已知限制

游戏内所有数字都是浮点数，所以整型除法按向下取整处理，负数相除的结果与 C++ 不同。
位运算、指针、类与继承、模板都不支持，遇到时会给出明确错误。
不支持 `#include <...>` 形式的头文件，这里没有 C++ 标准库可链接。

## 代码提示

`.cpp` 窗口的补全会给出 C++ 关键字、当前文件里的标识符，以及游戏内置函数。

游戏自带的函数说明仍然适用于运行时行为，本页只说明 C++ 的写法差异。
";

        // Second virtual page. Nothing here duplicates the game's own docs: it
        // only covers what the front-end rejects and how it says so, because
        // those messages are the ones a player meets first.
        private const string ErrorsDefault = @"[<- 返回 C++ Farmer 说明](cppfarmer_reference)
---
# 常见错误与函数调用

## 报错怎么读

按运行键时会先编译整个文件，编译不通过就一步都不会执行。报错长这样：

`main.cpp:12: error: ...`

前面的数字是行号，指的是你的 .cpp 源码，不是游戏语言的行号。

## 只有名字的语句

只写一个名字，既没有括号也没有赋值，编译器会当成笔误：

`printf;` 报 `'printf' 是函数，要写成 printf(...)；只写名字这一行什么也不会做`

`x;` 报 `'x' 这一行什么也不会做（少写了赋值或函数调用？）`

改成 `printf(""hi"");` 或 `use_item(Items.Water);` 就正常了。游戏只对它自己认识的名字提示过这个问题，C++ 独有的名字由本模组来提示。

## printf 的两条限制

- `printf(""hello"");` 只有在纯字符串时才翻译成游戏的 print
- 带百分号占位符会直接报错，请改用 `std::cout << a << b << std::endl;`

## 未知函数

`unknown function 'foo' - add a mapping or a definition`

一般是这三种原因：名字拼错；定义写在了使用之后；函数在另一个文件里却没写 `#include ""foo.cpp""`。

注意：文件里一旦出现 include，未知名字会被放行，留给运行时由游戏报告，因为编译器看不到另一个文件里定义了什么。

## 不支持的方法

`v.foo()` 会报 `unsupported method 'foo()'`。

容器支持的方法只有这些：push_back、emplace_back、append、pop_back、size、length、empty、clear、count、front、back、at、substr。

## 不支持的 C++ 写法

- `switch` / `case` / `default`：请改用 if / else if
- `sizeof`、`new`、`delete`、`this`、`operator`
- `template` / `typename`：不支持模板
- `virtual` / `override`：不支持继承与虚函数
- `namespace x { }`：内容会被整块跳过，请写在文件顶层
- `std::set` / `std::pair`：请改用 std::vector 或 struct
- 指针与引用语义、位运算、`#include <...>`、`#define`

这些词不会出现在补全列表里；手写出来会直接报错并说明原因，不会静默跳过。

## 少写分号

`using namespace std` 后面必须补上 `;`，否则报：

`'using' 后面缺少结尾的 ';'（一直扫到文件末尾都没找到）`

`typedef` 同理。直接写完整的 `using namespace std;` 最省事。

## 全角标点

中文输入法打出的 `；` `（）` `＝` 不会被静默替换，而是提示改成对应的半角字符。静默改写会掩盖输入法问题。

## 没解锁的构造

`this construct needs the 'lists' unlock, which is not unlocked yet`

去科技树解锁对应科技即可。本模组从不替你解锁任何东西，成就与解锁条件仍按游戏原本的规则走。
";
    }

    /// <summary>
    /// Adds an entry button for the reference page.
    ///
    /// The toolbar buttons are wired in the prefab, not in code, so they cannot be
    /// reached by field name. Instead one of them is located through its persistent
    /// UnityEvent target ("CallAddNewDocsWindow"), then cloned: the clone inherits
    /// the same sprite, rounded shape, size and hover colours as the rest of the
    /// bar. Only the glyph is replaced with a "C++" label.
    /// </summary>
    internal class CppHelpButton : MonoBehaviour
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
                workspace.AddNewDocsWindow(CppReference.DocId, Vector2.zero);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("cannot open the C++ reference page: " + e);
            }
        }
    }
}
