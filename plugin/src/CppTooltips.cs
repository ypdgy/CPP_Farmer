using System;
using System.Collections.Generic;
using HarmonyLib;

namespace CppFarmer
{
    /// <summary>
    /// Hover/completion text for the vocabulary this mod puts in front of the
    /// player.
    ///
    /// The game builds completion tooltips from its own localization table
    /// (code_tooltip_&lt;word&gt;), so everything it knows about has a popup while
    /// everything C++ Farmer adds -- language keywords, the C++ spellings of
    /// container calls, the standard-library names we translate or reject --
    /// shows nothing. That made the list look half finished.
    ///
    /// Every description below is deliberately accurate for this dialect rather
    /// than for real C++: what is translated, and what is rejected outright, is
    /// the thing worth knowing while typing. Text is Markdown read by
    /// CodeUtilities.ApplyCodeTags, which pairs backticks across the whole
    /// tooltip, so the heading holds the only pair and the bodies stay
    /// backtick-free.
    /// </summary>
    internal static class CppTooltips
    {
        private static readonly Dictionary<string, string> Docs =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // ---- types -------------------------------------------------
                { "int", "整数类型。int x = 1;" },
                { "float", "浮点类型。float f = 0.5;  游戏里只有一种浮点。" },
                { "double", "同 float，游戏里只有一种浮点数。" },
                { "bool", "布尔类型，取值 true / false。" },
                { "char", "字符。游戏只有字符串，'a' 与 \"a\" 等价。" },
                { "void", "无返回值。void f() { }" },
                { "auto", "由初始化表达式推断类型。auto x = 1;" },
                { "long", "与 int 等价，游戏只有一种整数。" },
                { "short", "与 int 等价。" },
                { "unsigned", "与 int 等价，没有无符号语义。" },
                { "signed", "与 int 等价。" },
                { "size_t", "整数类型，常用于下标和长度。" },
                { "string", "字符串。std::string s = \"abc\";" },

                // ---- qualifiers --------------------------------------------
                { "const", "只读限定，编译时忽略，可放心写。const int N = 3;" },
                { "constexpr", "同 const。" },
                { "static", "文件内可见；函数内的 static 变量按普通变量处理。" },
                { "inline", "被忽略，可放心写。" },

                // ---- user types --------------------------------------------
                { "struct", "结构体，编译为游戏字典。struct Point { int x; int y; };" },
                { "enum", "枚举，编译为整数常量。enum Dir { North, South };" },
                { "class", "与 struct 相同，没有访问控制。" },
                { "public", "结构体成员区段，字段一律公开。" },
                { "private", "结构体成员区段，实际仍然是公开的。" },
                { "protected", "结构体成员区段，实际仍然是公开的。" },
                { "typedef", "被忽略，只做语法占位，直接用类型名即可。" },
                { "using", "using namespace std; 会被忽略，写不写都行。" },
                { "namespace", "支持 namespace 块，但块内定义被整块跳过。" },
                { "template", "不支持，整块会被跳过。" },
                { "typename", "只出现在 template 里，本方言用不到。" },

                // ---- rejected C++ -----------------------------------------
                { "virtual", "不支持，本方言没有继承。" },
                { "override", "不支持，本方言没有继承。" },
                { "operator", "不支持自定义运算符重载。" },
                { "this", "不支持，直接用变量名或字段名。" },
                { "new", "不支持，直接声明变量或用 std::vector。" },
                { "delete", "不支持，内存由游戏管理。" },
                { "sizeof", "不支持。" },
                { "switch", "不支持，请改用 if / else if。" },
                { "case", "不支持，switch 不可用。" },
                { "default", "不支持，switch 不可用。" },
                { "set", "不支持，请用 std::vector 承载。" },
                { "pair", "不支持，请用 struct 代替。" },

                // ---- statements -------------------------------------------
                { "if", "if (cond) { ... }" },
                { "else", "else { ... }  或  else if (cond) { ... }" },
                { "for", "for (int i = 0; i < n; i++) { ... }" },
                { "while", "while (cond) { ... }" },
                { "do", "do { ... } while (cond);" },
                { "break", "跳出当前循环。" },
                { "continue", "进入下一次循环。" },
                { "return", "return expr;  无返回值写 return;" },
                { "nullptr", "空值，对应游戏里的 None。" },
                { "true", "布尔真。C++ 里小写，游戏词表里是大写 True。" },
                { "false", "布尔假。C++ 里小写，游戏词表里是大写 False。" },

                // ---- containers -------------------------------------------
                { "vector", "std::vector<T> 编译为游戏的列表。std::vector<int> v; v.push_back(1);" },
                { "array", "std::array<T, N> 与 std::vector<T> 等价。N 会被忽略。" },
                { "map", "std::map<K, V> 编译为游戏的字典。std::map<string, int> m; m[\"a\"] = 1;" },

                // ---- container methods ------------------------------------
                { "push_back", "v.push_back(x) 追加元素，编译为 append(v, x)。" },
                { "emplace_back", "同 push_back。" },
                { "append", "v.append(x) 追加元素；字符串拼接也可以用它。" },
                { "pop_back", "删除末尾元素。" },
                { "size", "元素个数，编译为 len(v)。" },
                { "length", "同 size()。" },
                { "empty", "是否为空，编译为 len(v) == 0。" },
                { "clear", "清空容器，编译为 cpp_clear(v)。" },
                { "count", "v.count(x) 判断是否包含 x，返回 0 或 1。" },
                { "front", "第一个元素，编译为 v[0]。" },
                { "back", "最后一个元素，编译为 v[len(v) - 1]。" },
                { "at", "v.at(i) 等价于 v[i]。" },
                { "substr", "子串，编译为 cpp_substr。" },

                // ---- free functions ---------------------------------------
                { "abs", "绝对值。" },
                { "min", "较小值。" },
                { "max", "较大值。" },
                { "len", "长度（游戏内建），容器的 size() 也行。" },
                { "pow", "幂运算，编译为 a ** b。" },
                { "sqrt", "平方根，编译为 cpp_sqrt。" },
                { "floor", "向下取整，编译为 a // 1。" },
                { "ceil", "向上取整，编译为 cpp_ceil。" },
                { "round", "四舍五入，编译为 cpp_round。" },
                { "random", "0 到 1 之间的随机浮点数。" },
                { "printf", "只支持没有格式符的字面量，建议改用 std::cout。" },
                { "print", "输出（游戏内建）。" },
                { "str", "转成字符串。" },
                { "to_string", "同 str。" },
                { "time", "游戏时间，编译为 get_time()。" },
                { "clock", "游戏 tick 数，编译为 get_tick_count()。" },

                // ---- iostream ---------------------------------------------
                { "cout", "std::cout << a << b << std::endl;  编译为 print(a, b)。" },
                { "cerr", "同 std::cout。" },
                { "clog", "同 std::cout。" },
                { "endl", "换行并输出，配合 std::cout 使用。" },
                { "std", "std:: 前缀，可以省略。" },

                // ---- leftovers from the game's own word list ---------------
                { "None", "空值。C++ 里写 nullptr。" },
            };

        /// <summary>Markdown body for the word, or null when we have nothing useful.</summary>
        public static string Describe(string word)
        {
            if (string.IsNullOrEmpty(word)) return null;
            if (!Docs.TryGetValue(word, out string body)) return null;

            // "## `word`" is the only backtick pair in the whole tooltip; the
            // bodies above contain none, so ApplyCodeTags keeps its pairing.
            return "## `" + word + "`\n" + body;
        }
    }

    /// <summary>
    /// Fills in the tooltip the game leaves empty for C++ vocabulary.
    ///
    /// TooltipUtils.GetWordTooltip is the single place both consumers go
    /// through: CodeCompleter.GetCompletionTooltip for the completion list and
    /// CodeWindow for hovering a word in the code. Returning a body only when
    /// the game's own chain produced nothing means builtins keep their official
    /// docs and user functions keep their signature.
    /// </summary>
    [HarmonyPatch(typeof(TooltipUtils), nameof(TooltipUtils.GetWordTooltip))]
    internal static class PatchCppWordTooltip
    {
        [HarmonyPostfix]
        private static void Postfix(string word, CodeWindow context, ref TooltipInfo __result)
        {
            if (__result != null) return;
            if (!Plugin.CfgWordTooltips.Value) return;

            try
            {
                if (context == null || !CppSupport.IsCppWindow(context)) return;
                string text = CppTooltips.Describe(word);
                if (text == null) return;
                __result = new TooltipInfo(text);
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("word tooltip failed: " + e.Message);
            }
        }
    }
}
