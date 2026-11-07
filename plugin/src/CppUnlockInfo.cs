using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;

namespace CppFarmer
{
    /// <summary>
    /// Adds a "what this unlock gives you in C++" section to research tooltips.
    ///
    /// The authoritative source is UnlockSO.unlocks -- Farm.UnlockAllIn iterates
    /// that list and unlocks each entry, so it already tells us exactly which
    /// builtins and language features a research node grants.
    ///
    /// The tooltip body is Markdown rendered by CodeUtilities.ApplyCodeTags,
    /// which splits the whole page on backticks and treats odd segments as code.
    /// Everything appended here is backtick-free plain text, so the existing
    /// pairing (and therefore the game's own example snippets) stays intact.
    /// This is the exact trap the earlier docs-page injection fell into.
    /// </summary>
    internal static class CppUnlockInfo
    {
        private const int MaxLines = 8;

        /// <summary>Maps a language key granted by an unlock to its C++ form.</summary>
        private static readonly Dictionary<string, string> Map =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // world actions
                { "harvest", "harvest();  收割当前格子" },
                { "can_harvest", "can_harvest();  判断能否收割" },
                { "plant", "plant(Entities.Bush);  种植" },
                { "till", "till();  耕地" },
                { "move", "move(North);  移动" },
                { "can_move", "can_move(North);  判断能否移动" },
                { "use_item", "use_item(Items.Water);  使用物品" },
                { "get_water", "get_water();  获取水量" },
                { "swap", "swap();  交换" },
                { "clear", "clear();  清空输出" },
                { "measure", "measure();  测量" },
                { "do_a_flip", "do_a_flip();" },
                { "pet_the_piggy", "pet_the_piggy();" },
                { "trade", "trade(Items.Carrot);  交易" },
                { "can_trade", "can_trade(Items.Carrot);" },
                { "unlock", "unlock(Unlocks.Carrots);  解锁科技" },
                { "change_hat", "change_hat(Hats.Straw_Hat);" },

                // senses / stats
                { "get_pos_x", "get_pos_x();" },
                { "get_pos_y", "get_pos_y();" },
                { "get_entity_type", "get_entity_type();" },
                { "get_ground_type", "get_ground_type();" },
                { "get_world_size", "get_world_size();" },
                { "num_items", "num_items(Items.Hay);" },
                { "num_unlocked", "num_unlocked(Unlocks.Carrots);" },
                { "get_companion", "get_companion();" },
                { "get_active_power", "get_active_power();" },

                // timing / utilities
                { "get_time", "get_time();" },
                { "get_tick_count", "get_tick_count();" },
                { "min", "min(a, b);" },
                { "max", "max(a, b);" },
                { "abs", "abs(x);" },
                { "random", "random();" },

                // drones
                { "num_drones", "num_drones();" },
                { "max_drones", "max_drones();" },
                { "get_drone_id", "get_drone_id();" },
                { "spawn_drone", "spawn_drone();" },
                { "wait_for", "wait_for();" },
                { "has_finished", "has_finished();" },
                { "send", "send();" },
                { "receive", "receive();" },

                // output / conversion
                { "print", "std::cout << ... << std::endl;  或 print(...)" },
                { "quick_print", "quick_print(...);" },
                { "str", "std::to_string(x);" },

                // language features
                { "variables", "int x = 1;  float f = 0.5;  bool b = true;" },
                { "functions", "int f(int x) { return x; }  自定义函数" },
                { "def", "int f(int x) { return x; }  自定义函数" },
                { "loops", "for (int i = 0; i < n; i++) { }  /  while (cond) { }" },
                { "while", "while (cond) { }" },
                { "for", "for (int i = 0; i < n; i++) { }" },
                { "range", "for (int i = 0; i < n; i++) { }" },
                { "if", "if (cond) { }" },
                { "else", "else { }" },
                { "elif", "else if (cond) { }" },
                { "return", "return x;" },
                { "global", "全局变量（写在文件顶层）" },
                { "break", "break;" },
                { "continue", "continue;" },
                { "pass", "空语句，C++ 写 { }" },
                { "and", "a && b" },
                { "or", "a || b" },
                { "not", "!a" },
                { "true", "true" },
                { "false", "false" },
                { "none", "nullptr" },
                { "operators", "算术与比较运算符（+ - * / % == != < >）" },
                { "comments", "// 单行注释" },

                // containers
                { "lists", "std::vector<int> v;  v.push_back(1);  v.size();" },
                { "list", "std::vector<int> v;" },
                { "append", "v.push_back(x);" },
                { "pop", "v.pop_back();  （游戏内 pop()）" },
                { "insert", "v.insert(i, x);" },
                { "remove", "v.remove(x);" },
                { "len", "v.size();  （游戏内 len(v)）" },
                { "dictionaries", "struct Point { int x; int y; };  编译为字典" },
                { "dict", "struct Point { int x; int y; };" },
                { "dicts", "struct Point { int x; int y; };" },
                { "sets", "游戏内 set，C++ 用 std::vector 承载" },
                { "set", "游戏内 set" },
                { "add", "s.add(x);" },
                { "tuples", "元组，编译为列表" },
                { "scopes", "C++ 作用域规则" },
                { "import", "#include \"file.cpp\"" },
                { "from", "#include \"file.cpp\"  （对应 from x import *）" },
                { "senses", "get_pos_x();  get_pos_y();  get_entity_type();" },
                { "utilities", "min();  max();  abs();  random();" },
                { "timing", "get_time();  get_tick_count();" },
                { "debug", "std::cout << ... << std::endl;" },
                { "stats", "num_items();  num_unlocked();" },
                { "expand", "get_world_size();" },
                { "megafarm", "spawn_drone();  num_drones();  wait_for();" },
            };

        /// <summary>Returns the block to append, or null when nothing applies.</summary>
        public static string Build(UnlockSO unlock)
        {
            if (unlock == null || unlock.unlocks == null) return null;

            var lines = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string raw in unlock.unlocks)
            {
                if (string.IsNullOrEmpty(raw)) continue;

                // UnlockAllIn supports digit-prefixed entries ("2expand"),
                // meaning "only above level N". Strip the prefix for lookup.
                string key = raw;
                if (key.Length > 1 && char.IsDigit(key[0])) key = key.Substring(1);
                if (!seen.Add(key)) continue;
                if (!Map.TryGetValue(key, out string cpp)) continue;

                lines.Add(cpp);
                if (lines.Count >= MaxLines) break;
            }

            if (lines.Count == 0) return null;

            var sb = new StringBuilder("\n\n### C++ 解锁内容（解锁后可用）\n");
            foreach (string line in lines)
            {
                sb.Append('\n').Append(line);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Appends the C++ section to research tooltips. UnlockBox.GetTooltipInfo
    /// delegates to TooltipUtils.UnlockTooltip(name), whose result is a
    /// TooltipInfo with a public `text` field, so no MarkdownText patch and no
    /// docs-file changes are involved.
    /// </summary>
    [HarmonyPatch(typeof(TooltipUtils), nameof(TooltipUtils.UnlockTooltip))]
    internal static class PatchUnlockTooltip
    {
        [HarmonyPostfix]
        private static void Postfix(string unlockName, ref TooltipInfo __result)
        {
            if (!Plugin.CfgUnlockTooltips.Value) return;
            if (__result == null || string.IsNullOrEmpty(__result.text)) return;

            try
            {
                UnlockSO unlock = ResourceManager.GetUnlock(unlockName);
                if (unlock == null) return;

                string extra = CppUnlockInfo.Build(unlock);
                if (extra == null) return;

                // Guard against a double append if the same TooltipInfo is reused.
                if (__result.text.Contains("### C++ 解锁内容")) return;
                __result.text += extra;
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("unlock tooltip failed: " + e.Message);
            }
        }
    }
}
