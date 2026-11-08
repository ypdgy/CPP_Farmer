using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace CppFarmer
{
    [BepInPlugin(Guid, "C++ Farmer", PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "cppfarmer";
        public const string PluginVersion = "0.2.0";

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        internal static ConfigEntry<bool> CfgEnabled;
        internal static ConfigEntry<bool> CfgEnforceUnlocks;
        internal static ConfigEntry<bool> CfgVerbose;
        internal static ConfigEntry<bool> CfgHelpButton;
        internal static ConfigEntry<float> CfgHelpButtonX;
        internal static ConfigEntry<float> CfgHelpButtonY;
        internal static ConfigEntry<bool> CfgUnlockTooltips;
        internal static ConfigEntry<bool> CfgHomeLink;
        internal static ConfigEntry<bool> CfgNoCompleteInStrings;
        internal static ConfigEntry<bool> CfgSmartIndent;
        internal static ConfigEntry<bool> CfgWordTooltips;
        internal static ConfigEntry<bool> CfgCppExtension;
        internal static ConfigEntry<bool> CfgExportHeader;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            CfgEnabled = Config.Bind("General", "Enabled", true,
                "Transpile .cpp windows before the game parses them.");
            CfgEnforceUnlocks = Config.Bind("General", "EnforceUnlocks", true,
                "Refuse to emit constructs whose tech-tree unlock is missing. "
                + "This never unlocks anything; it only reports the missing unlock early.");
            CfgVerbose = Config.Bind("General", "VerboseLog", false,
                "Log generated script sizes and cache hits.");
            CfgHelpButton = Config.Bind("UI", "HelpButton", true,
                "Show a 'C++ 说明' button next to the game's top-right button bar. "
                + "It opens this mod's own reference page; the game's docs are untouched.");
            CfgHelpButtonX = Config.Bind("UI", "HelpButtonRightMargin", 10f,
                "Distance from the right screen edge, in pixels.");
            CfgHelpButtonY = Config.Bind("UI", "HelpButtonTopMargin", 8f,
                "Distance from the top screen edge, in pixels.");
            CfgNoCompleteInStrings = Config.Bind("UI", "NoCompleteInStrings", true,
                "Do not open the code completer inside C++ strings, char literals or comments.");
            CfgSmartIndent = Config.Bind("UI", "SmartIndent", true,
                "In .cpp windows, indent one extra level after a line ending with '{'. "
                + "The game only knows Python's ':'.");
            CfgWordTooltips = Config.Bind("UI", "WordTooltips", true,
                "Show a description when hovering C++ keywords and translated "
                + "standard-library names, in the completion list and in the editor.");
            CfgHomeLink = Config.Bind("UI", "HomeLink", true,
                "Add a C++ reference link to the docs home page.");
            CfgUnlockTooltips = Config.Bind("UI", "UnlockTooltips", true,
                "Show which C++ calls and syntax a research unlock grants when "
                + "hovering over it in the tech tree.");
            CfgCppExtension = Config.Bind("Storage", "UseCppExtension", true,
                "Store .cpp windows as '<name>.cpp' instead of the game's '<name>.py', "
                + "so external editors treat them as C++ files. Existing .cpp.py files "
                + "are migrated on load and written back as .cpp. Set to false to "
                + "fall back to the game's .py naming.");
            CfgExportHeader = Config.Bind("Storage", "ExportEditorHeader", true,
                "Copy cppfarmer_api.hpp (IDE-only declarations of the game's builtins) "
                + "into the active save folder, so an external editor finds it with "
                + "#include \"cppfarmer_api.hpp\" while editing .cpp files.");

            if (!NativeBridge.Available)
            {
                Log.LogError("cppfarmer_native.dll could not be loaded: " + NativeBridge.ProbeError);
                Log.LogError("Place cppfarmer_native.dll next to this plugin in BepInEx/plugins/CppFarmer/.");
                return;
            }
            Log.LogInfo("native compiler: " + NativeBridge.Version);

            var harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(PatchIsValidCodeFileName));
            harmony.PatchAll(typeof(PatchCppStorageLoad));
            harmony.PatchAll(typeof(PatchCppStorageWrite));
            harmony.PatchAll(typeof(PatchCppStorageWatcher));
            harmony.PatchAll(typeof(PatchParserParse));
            harmony.PatchAll(typeof(PatchGetWordList));
            harmony.PatchAll(typeof(PatchGetSubWordList));
            harmony.PatchAll(typeof(PatchCodeWindowLoad));
            harmony.PatchAll(typeof(PatchLoadDoc));
            harmony.PatchAll(typeof(PatchUpdateBreakpoints));
            harmony.PatchAll(typeof(PatchCppHighlightSearch));
            harmony.PatchAll(typeof(PatchCppHighlightTheme));
            harmony.PatchAll(typeof(PatchCppHighlightTick));
            harmony.PatchAll(typeof(PatchKeepScrollOnBlur));
            harmony.PatchAll(typeof(PatchSaveDiagnostics));
            harmony.PatchAll(typeof(PatchUnlockTooltip));
            harmony.PatchAll(typeof(PatchCppWordTooltip));
            // Private helpers of the overlay renderer; a game update that renames
            // them should cost the highlight, not the whole plugin.
            try
            {
                harmony.PatchAll(typeof(PatchBlinkWordRect));
                harmony.PatchAll(typeof(PatchBlinkLineRect));
            }
            catch (Exception e)
            {
                Log.LogWarning("execution highlight remap skipped: " + e.Message);
            }
            harmony.PatchAll(typeof(PatchNoCompleteInStrings));
            // InsertNewLineIndentation is a private helper; if a game update moves
            // it, the editor should simply fall back to vanilla indentation
            // instead of taking the whole plugin down with it.
            try
            {
                harmony.PatchAll(typeof(PatchCppIndent));
            }
            catch (Exception e)
            {
                Log.LogWarning("smart indent patch skipped: " + e.Message);
            }
            Log.LogInfo("harmony patches applied");

            var host = new GameObject("CppFarmer_HelpButton");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<CppHelpButton>();
            CppSupport.RefreshUnlocks();
        }

        private float _nextUnlockRefresh;

        /// <summary>Keeps the tech-tree vocabulary fresh on the main thread.</summary>
        private void Update()
        {
            if (Time.unscaledTime < _nextUnlockRefresh) return;
            _nextUnlockRefresh = Time.unscaledTime + 2f;
            CppSupport.RefreshUnlocks();
        }
    }

    /// <summary>
    /// Lets .cpp files live in the save folder as ordinary code files so the
    /// editor, save/backup and file watcher treat them like any other script.
    /// </summary>
    [HarmonyPatch(typeof(Saver), nameof(Saver.IsValidCodeFileName))]
    internal static class PatchIsValidCodeFileName
    {
        [HarmonyPostfix]
        private static void Postfix(object[] __args, ref bool __result)
        {
            if (__result || __args == null || __args.Length == 0) return;
            string name = __args[0] as string;
            if (CppSupport.IsCppFileName(name)) __result = true;
        }
    }

    /// <summary>
    /// The single choke point of the whole mod.
    ///
    /// Parser.Parse(TokenStream, CodeWindow) is static and receives the already
    /// tokenized source. Swapping only the token stream means everything the
    /// game normally does afterwards is untouched:
    ///   - keyword/unlock gating still runs on the generated script,
    ///   - the executor still charges the same per-node operation costs,
    ///   - achievements, stats, leaderboards and save data see one ordinary run.
    /// We deliberately never call Farm.Unlock / UnlockOrUpgrade / ResetUnlocks,
    /// and never touch MainSim.RecordsAchievements.
    /// </summary>
    [HarmonyPatch(typeof(Parser), "Parse", new[] { typeof(TokenStream), typeof(CodeWindow) })]
    internal static class PatchParserParse
    {
        private static bool _busy;
        private static int _depth;
        private const int MaxDepth = 3;

        [HarmonyPrefix]
        private static bool Prefix(object[] __args, ref Program __result)
        {
            if (_busy) return true;                       // re-entrant call: run original
            if (__args == null || __args.Length < 2) return true;
            if (!Plugin.CfgEnabled.Value) return true;
            if (_depth >= MaxDepth)
            {
                Plugin.Log.LogWarning("cpp parse recursion guard hit; running original parser");
                return true;
            }

            _depth++;
            try
            {
                CodeWindow window = __args[1] as CodeWindow;
                if (window == null || !CppSupport.IsCppWindow(window)) return true;

                string fileName = CppSupport.GetFileName(window);
                Plugin.Log.LogInfo("cpp parse begin: " + fileName);

                string source = CppSupport.GetSource(window);
                if (source == null)
                {
                    // A window that exists but whose input text is not readable yet
                    // (save loading) still has its file on disk -- use that instead
                    // of falling through to the vanilla parser. Falling through
                    // would read the C++ text as the game's own language, where
                    // "//" is the floor-division operator, and the player gets a
                    // bogus "not a valid expression" on the first comment line.
                    source = CppSupport.ReadCodeFileFromDisk(fileName);
                    if (source != null)
                    {
                        Plugin.Log.LogWarning("cpp parse: '" + fileName
                            + "' window text unavailable, read from disk ("
                            + source.Length + " chars)");
                    }
                    else
                    {
                        Plugin.Log.LogWarning("cpp parse: '" + fileName
                            + "' text unavailable everywhere, compiling as empty");
                        source = string.Empty;
                    }
                }
                Plugin.Log.LogInfo("cpp parse: source " + source.Length + " chars");

                string prepared;
                string generated;
                int[] lineMap;
                Dictionary<string, string> moduleToFile;
                try
                {
                    // `#include "x.cpp"` becomes `from x import *` in the generated
                    // script, and the module name is aliased to that file's window
                    // so the game's own import machinery parses it. Nodes then carry
                    // the included window, which is what makes the execution
                    // highlight and breakpoints able to move across files.
                    prepared = CppIncludes.Prepare(
                        source, fileName, CppSupport.ResolveInclude,
                        CppSupport.EnsureModuleWindow, out moduleToFile);
                    Plugin.Log.LogInfo("cpp parse: includes prepared");

                    generated = CppSupport.TranspileCached(prepared, out lineMap);
                    Plugin.Log.LogInfo("cpp parse: transpiled " + generated.Length + " chars");

                    // Remember the result *for this window*: two .cpp windows compiled
                    // in turn would otherwise overwrite each other's line table.
                    CppSupport.Remember(window, prepared, generated, lineMap);
                }
                catch (ParseException e)
                {
                    // The game's error panel shows this, but the log has to carry it
                    // too: a rethrown ParseException otherwise leaves no trace of
                    // which stage refused the file.
                    Plugin.Log.LogWarning("cpp parse rejected: " + e.Message);
                    throw;
                }

                Plugin.Log.LogInfo("cpp parse: tokenizing");
                TokenStream stream = CppSupport.Tokenize(generated, out bool hadUnknown);
                if (stream != null) CppSupport.RewriteModuleTokens(stream, moduleToFile);
                if (stream == null)
                {
                    // The generated script contains something the game's lexer
                    // cannot classify. Report it instead of silently parsing the
                    // raw C++, which would surface a confusing unrelated error.
                    string why = hadUnknown
                        ? "生成的脚本包含游戏词法器无法识别的字符"
                        : "游戏词法器未返回 token 流";
                    Plugin.Log.LogError(why + "（src=" + source.Length
                        + " chars, gen=" + generated.Length + " chars）");
                    CppSupport.DumpGenerated(generated);
                    throw new ParseException("C++ 编译成功，但" + why + "，已写入 last_generated.py 供排查", 0, 0);
                }

                Plugin.Log.LogInfo("cpp parse: calling game parser");
                _busy = true;
                try
                {
                    __result = Parser.Parse(stream, window);   // re-enters, hits the guard
                }
                finally
                {
                    _busy = false;
                }
                return false;
            }
            catch (ParseException)
            {
                throw;                                     // shown by the game's error panel
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("C++ front-end crashed, falling back: " + e);
                return true;
            }
            finally
            {
                _depth--;
            }
        }
    }

    /// <summary>
    /// Replaces the completer vocabulary for .cpp windows. The game derives its
    /// candidate list from Python keywords plus builtins; for C++ that produces
    /// exactly the wrong suggestions, so the list is rebuilt as:
    /// identifiers in this file -> C++ keywords -> the game's builtins.
    /// </summary>
    [HarmonyPatch(typeof(CodeWindow), "GetWordList")]
    internal static class PatchGetWordList
    {
        [HarmonyPostfix]
        private static void Postfix(CodeWindow __instance, ref List<string> __result)
        {
            if (!Plugin.CfgEnabled.Value) return;
            if (__instance == null || !CppSupport.IsCppWindow(__instance)) return;

            try
            {
                __result = CppWords.Rebuild(__result, CppSupport.GetSource(__instance));
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("word list rebuild failed: " + e.Message);
            }
        }
    }

    /// <summary>
    /// GetSubWordList feeds the completer when the caret sits inside a longer
    /// word. Same treatment: drop the game's own keywords, add C++ ones.
    /// </summary>
    [HarmonyPatch(typeof(CodeWindow), "GetSubWordList")]
    internal static class PatchGetSubWordList
    {
        [HarmonyPostfix]
        private static void Postfix(CodeWindow __instance, object[] __args, ref List<string> __result)
        {
            if (!Plugin.CfgEnabled.Value) return;
            if (__instance == null || !CppSupport.IsCppWindow(__instance)) return;

            try
            {
                string fragment = __args != null && __args.Length > 0 ? __args[0] as string : null;
                __result = CppWords.Filter(__result, fragment);
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("sub word list rebuild failed: " + e.Message);
            }
        }
    }

    /// <summary>
    /// Remembers which CodeWindow holds which file name. Include resolution needs
    /// the live editor text (it may be newer than disk), and the workspace API is
    /// not reachable from the parse path.
    /// </summary>
    [HarmonyPatch(typeof(CodeWindow), "Load")]
    internal static class PatchCodeWindowLoad
    {
        [HarmonyPostfix]
        private static void Postfix(CodeWindow __instance)
        {
            // Load(string text, bool ...) -- the string is the file *content*, not
            // its name (IL: ldarg.1 -> CodeInputField.set_text). The name lives in
            // the fileName field, so read it from there.
            if (__instance == null) return;
            CppSupport.RegisterWindow(__instance, CppSupport.GetFileName(__instance));
        }
    }

    /// <summary>
    /// Re-marks breakpoints after the game does it with the wrong coordinate
    /// space, and merges breakpoints set inside included files.
    ///
    /// Each CodeWindow keeps its own breakpoint list in its own line numbers.
    /// Because includes are appended to the main file, line N of hello.cpp lands
    /// at (segment start + N) in the merged text -- that is the offset applied
    /// here before converting to a position inside the generated script.
    /// </summary>
    [HarmonyPatch(typeof(BreakPointPanel), "UpdateBreakpoints")]
    internal static class PatchUpdateBreakpoints
    {
        [HarmonyPostfix]
        private static void Postfix(BreakPointPanel __instance, object[] __args)
        {
            if (!Plugin.CfgEnabled.Value) return;
            if (__instance == null || __args == null || __args.Length == 0) return;
            if (!(__args[0] is Node tree)) return;

            // Local to this call: breakpoint updates also arrive from the
            // simulation thread, and a shared StringBuilder is not thread-safe.
            var trace = new System.Text.StringBuilder();

            try
            {
                CodeWindow owner = CppSupport.GetPanelWindow(__instance);
                if (owner == null) return;
                if (!CppSupport.IsCppWindow(owner)) return;

                string ownerFile = CppSupport.GetFileName(owner);
                CppSupport.CompiledUnit unit = CppSupport.GetUnit(owner);

                trace.AppendLine("owner=" + ownerFile);
                if (unit == null)
                {
                    trace.AppendLine("unit=none -> skipped");
                    return;
                }
                trace.AppendLine("generated=" + (unit.Generated == null ? 0 : unit.Generated.Length)
                    + " chars, map=" + (unit.LineMap == null ? 0 : unit.LineMap.Length) + " entries");

                var lines = new List<int>();
                CppSupport.AddBreakpointLines(owner, 1, lines, trace, ownerFile);
                trace.AppendLine("lines(1-based merged)=" + Join(lines));

                HashSet<int> offsets = CppSupport.BuildBreakpointOffsets(
                    lines, unit.Generated, unit.LineMap, trace);
                trace.AppendLine("offsets=" + Join(offsets));

                tree.InsertBreakpointsRec(offsets);
            }
            catch (Exception e)
            {
                trace.AppendLine("EXCEPTION " + e);
                Plugin.Log.LogWarning("breakpoint remap failed: " + e.Message);
            }
            finally
            {
                Dump(trace);
            }
        }

        private static string Join(IEnumerable<int> values)
        {
            var sb = new System.Text.StringBuilder("[");
            bool first = true;
            foreach (int v in values)
            {
                if (!first) sb.Append(',');
                sb.Append(v);
                first = false;
            }
            return sb.Append(']').ToString();
        }

        /// <summary>
        /// Overwrites bp_debug.txt every time. BepInEx logs at Info by default and
        /// LogDebug is invisible, which is why an earlier round of breakpoint
        /// problems could not be diagnosed from the log at all.
        /// </summary>
        private static void Dump(System.Text.StringBuilder trace)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".";
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "bp_debug.txt"),
                    System.DateTime.Now.ToString("HH:mm:ss.fff") + "\n" + trace.ToString());
            }
            catch
            {
                // diagnostics must never break the patch
            }
        }
    }

    /// <summary>
    /// Records why a save failed.
    ///
    /// `Saver.Save` swallows the exception and shows a generic "close other
    /// programs" dialog, so the actual cause never reaches the log. A finalizer
    /// sees the exception without changing behaviour.
    /// </summary>
    [HarmonyPatch(typeof(Saver), "WriteTextAtomically")]
    internal static class PatchSaveDiagnostics
    {
        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, System.IO.FileInfo destinationFile)
        {
            if (__exception != null)
            {
                Plugin.Log.LogError("save write failed for "
                    + (destinationFile == null ? "<null>" : destinationFile.FullName)
                    + " :: " + __exception.GetType().Name + ": " + __exception.Message);
            }
            return __exception;
        }
    }

    internal static class CppSupport
    {
        private static readonly string[] Extensions = { ".cpp", ".cxx", ".cc", ".c++", ".hpp" };

        // Keys the game uses in Farm.unlockedKeyWords / the save's unlock list.
        private static readonly string[] KnownUnlockKeys =
        {
            "variables", "functions", "loops", "operators", "lists", "dictionaries",
            "sets", "tuples", "import", "pass", "none", "true", "false", "comments"
        };

        private static FieldInfo _fileNameField;
        private static FieldInfo _codeInputField;
        private static PropertyInfo _codeInputProperty;
        private static FieldInfo _codeTextField;
        private static int _sourceWarnings;
        private static MethodInfo _tokenizeMethod;
        private static PropertyInfo _simInstProperty;
        private static FieldInfo _workspaceField;
        private static PropertyInfo _openWindowsProperty;
        private static MethodInfo _getUnlocks;
        private static MethodInfo _isUnlocked;

        /// <summary>
        /// One transpile result, kept per window.
        ///
        /// Breakpoint remapping needs the generated text and its line table to
        /// belong to the same compilation as the tree being marked. With a single
        /// global cache, compiling window B would silently retarget window A's
        /// breakpoints at B's script.
        /// </summary>
        internal sealed class CompiledUnit
        {
            public string Source;
            public string Generated;
            public int[] LineMap;

            /// <summary>
            /// Char offset of every line start in Generated / Source. The execution
            /// overlay asks for a position on every drawn frame, so the blink remap
            /// must not rescan the text each time.
            /// </summary>
            public int[] GeneratedLineStarts;
            public int[] SourceLineStarts;
        }

        /// <summary>Char offset of the first character of each line.</summary>
        public static int[] LineStarts(string text)
        {
            if (string.IsNullOrEmpty(text)) return new[] { 0 };
            var starts = new List<int>(text.Length / 24 + 2) { 0 };
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') starts.Add(i + 1);
            }
            return starts.ToArray();
        }

        // The game parses on the main thread AND re-parses imported modules from
        // the simulation thread. A plain Dictionary written from two threads can
        // corrupt its bucket chain; a later TryGetValue then loops forever with
        // no exception -- exactly the "F5 hangs" symptom. Use concurrent maps and
        // serialise the actual native compile call as well.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<CodeWindow, CompiledUnit> Units =
            new System.Collections.Concurrent.ConcurrentDictionary<CodeWindow, CompiledUnit>();

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CompiledUnit> UnitsBySource =
            new System.Collections.Concurrent.ConcurrentDictionary<string, CompiledUnit>(StringComparer.Ordinal);

        private static readonly object NativeLock = new object();

        public static void Remember(CodeWindow window, string source, string generated,
            int[] lineMap)
        {
            var unit = new CompiledUnit
            {
                Source = source,
                Generated = generated,
                LineMap = lineMap,
                GeneratedLineStarts = LineStarts(generated),
                SourceLineStarts = LineStarts(source),
            };
            if (window != null)
            {
                PruneUnits();
                Units[window] = unit;
            }
            if (source != null)
            {
                if (UnitsBySource.Count > 8) UnitsBySource.Clear();
                UnitsBySource[source] = unit;
            }
        }

        public static CompiledUnit GetUnit(CodeWindow window)
        {
            if (window == null) return null;
            return Units.TryGetValue(window, out CompiledUnit unit) ? unit : null;
        }

        private static void PruneUnits()
        {
            if (Units.Count < 16) return;
            var dead = new List<CodeWindow>();
            foreach (KeyValuePair<CodeWindow, CompiledUnit> pair in Units)
            {
                if (pair.Key == null) dead.Add(pair.Key);
            }
            foreach (CodeWindow key in dead)
            {
                Units.TryRemove(key, out _);
            }
        }

        private static FieldInfo _bpPanelField;
        private static FieldInfo _bpLinesField;

        public static CodeWindow GetPanelWindow(BreakPointPanel panel)
        {
            if (panel == null) return null;
            if (_bpPanelField == null)
            {
                _bpPanelField = AccessTools.Field(typeof(BreakPointPanel), "function");
            }
            return _bpPanelField?.GetValue(panel) as CodeWindow;
        }

        /// <summary>Breakpoint lines (0-based) currently set in a window.</summary>
        public static List<int> GetBreakpointLines(CodeWindow window)
        {
            if (window == null) return null;
            try
            {
                if (_bpPanelField == null)
                {
                    _bpPanelField = AccessTools.Field(typeof(BreakPointPanel), "function");
                }
                FieldInfo panelField = AccessTools.Field(typeof(CodeWindow), "breakpointPanel");
                object panel = panelField?.GetValue(window);
                if (panel == null) return null;

                if (_bpLinesField == null)
                {
                    _bpLinesField = AccessTools.Field(typeof(BreakPointPanel), "breakpointLines");
                }
                return _bpLinesField?.GetValue(panel) as List<int>;
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("cannot read breakpoint lines: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Appends a window's breakpoints as 1-based lines of the merged source.
        /// <paramref name="lineBase"/> is the merged line that this window's line 0
        /// corresponds to (1 for the main file, segment start for includes).
        /// </summary>
        public static void AddBreakpointLines(CodeWindow window, int lineBase, List<int> into,
            System.Text.StringBuilder trace, string label)
        {
            List<int> lines = GetBreakpointLines(window);
            if (lines == null)
            {
                trace?.AppendLine("  " + label + ": panel unavailable");
                return;
            }
            trace?.AppendLine("  " + label + " bpLines=" + lines.Count
                + " (0-based) -> base " + lineBase);
            foreach (int line in lines)
            {
                into.Add(lineBase + line);
            }
        }

        /// <summary>
        /// Translates merged-source line numbers into character offsets inside the
        /// generated script, using the compiler's output-line -> source-line table.
        /// </summary>
        /// <summary>
        /// Translates merged-source line numbers into character offsets inside the
        /// generated script.
        ///
        /// An exact match is tried first. This matters because the generator does
        /// not emit statements in source order: all function definitions are
        /// declared before the top-level body, so the output-line -> source-line
        /// table is *not* monotonic. The previous "first entry &gt;= line" scan
        /// therefore returned the wrong statement whenever a body line sat after a
        /// later-defined function.
        /// </summary>
        public static HashSet<int> BuildBreakpointOffsets(List<int> mergedLines,
            string generated, int[] map, System.Text.StringBuilder trace = null)
        {
            var set = new HashSet<int>();
            if (mergedLines == null || mergedLines.Count == 0) return set;
            if (string.IsNullOrEmpty(generated) || map == null || map.Length == 0) return set;

            var starts = new List<int>(map.Length + 1) { 0 };
            for (int i = 0; i < generated.Length; i++)
            {
                if (generated[i] == '\n') starts.Add(i + 1);
            }

            foreach (int sourceLine in mergedLines)
            {
                int scriptLine = -1;

                // 1) exact: one output line carries this source line
                for (int s = 0; s < map.Length; s++)
                {
                    if (map[s] == sourceLine)
                    {
                        scriptLine = s;
                        break;
                    }
                }

                // 2) no statement on that line (blank, comment, lone brace):
                //    take the closest following source line.
                if (scriptLine < 0)
                {
                    int best = int.MaxValue;
                    for (int s = 0; s < map.Length; s++)
                    {
                        if (map[s] <= sourceLine || map[s] == 0) continue;
                        if (map[s] < best)
                        {
                            best = map[s];
                            scriptLine = s;
                        }
                    }
                }

                if (scriptLine < 0 || scriptLine >= starts.Count)
                {
                    trace?.AppendLine("  line " + sourceLine + " -> no output line");
                    continue;
                }
                trace?.AppendLine("  line " + sourceLine + " -> out#" + (scriptLine + 1)
                    + " offset " + starts[scriptLine]);
                set.Add(starts[scriptLine]);
            }
            return set;
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, WeakReference>
            WindowRegistry =
                new System.Collections.Concurrent.ConcurrentDictionary<string, WeakReference>(
                    StringComparer.OrdinalIgnoreCase);

        public static void RegisterWindow(CodeWindow window, string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            WindowRegistry[fileName] = new WeakReference(window);
        }

        public static string GetFileName(CodeWindow window)
        {
            try
            {
                if (_fileNameField == null)
                {
                    _fileNameField = AccessTools.Field(typeof(CodeWindow), "fileName");
                }
                return _fileNameField?.GetValue(window) as string;
            }
            catch
            {
                return null;
            }
        }

        private static FieldInfo _dirtyField;

        /// <summary>
        /// Makes sure an included file has a live, dirty window.
        ///
        /// No alias is added to workspace.codeWindows. CaptureCodeFiles enumerates
        /// that dictionary's Values and does dictionary.Add(item.fileName, ...);
        /// an alias pointing at the same window makes it appear twice and throws
        /// a duplicate-key exception -- which the game reports as "failed to write
        /// save". Instead RewriteModuleTokens changes the import token's value to
        /// the real window name before Parser sees it, so the native key matches.
        /// </summary>
        public static bool EnsureModuleWindow(string module, string fileName)
        {
            if (string.IsNullOrEmpty(module) || string.IsNullOrEmpty(fileName)) return false;
            try
            {
                Workspace workspace = GetWorkspace();
                if (workspace == null) return false;

                // Do NOT call workspace.OpenCodeWindow here. We are already
                // inside a Parser.Parse prefix; OpenCodeWindow synchronously
                // calls Parse() on the new window, which re-enters this patch and
                // can spiral into a hang. Requires the file to be open instead --
                // the player normally has it open anyway.
                CodeWindow window = FindOpenWindow(fileName);
                if (window == null)
                {
                    Plugin.Log.LogWarning("module " + module + ": window " + fileName
                        + " is not open; refusing to create one during parse");
                    return false;
                }

                // Parse() short-circuits to cachedProgram when dirty is false; a
                // restored window may not be dirty yet, which would make import
                // fail with "syntax error in import".
                if (_dirtyField == null)
                {
                    _dirtyField = AccessTools.Field(typeof(CodeWindow), "dirty");
                }
                _dirtyField?.SetValue(window, true);
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("cannot prepare module " + module + ": " + e.Message);
                return false;
            }
        }

        private static FieldInfo _tokenListField;

        /// <summary>
        /// Rewrites `from hello import *` into `from hello.cpp import *` at the
        /// token level, before Parser.Parse consumes the stream.
        ///
        /// Token.value is a plain public string; Parser uses it verbatim as the
        /// module name, and does not require it to be a bare identifier once the
        /// token has already been classified as IDENTIFIER. That lets us match the
        /// real window key ("hello.cpp") without touching codeWindows at all.
        /// </summary>
        public static void RewriteModuleTokens(TokenStream stream,
            System.Collections.Generic.Dictionary<string, string> moduleToFile)
        {
            if (stream == null || moduleToFile == null || moduleToFile.Count == 0) return;
            try
            {
                if (_tokenListField == null)
                {
                    _tokenListField = AccessTools.Field(typeof(TokenStream), "tokens");
                }
                if (!(_tokenListField?.GetValue(stream) is System.Collections.IEnumerable list)) return;

                // Only the identifier immediately after FROM may be rewritten.
                // A blanket rename would also corrupt variables that happen to
                // share the module name (int hello = 1;).
                bool afterFrom = false;
                foreach (object item in list)
                {
                    if (!(item is Token token)) continue;
                    if (afterFrom)
                    {
                        if (token.type == TokenType.NEW_LINE || token.type == TokenType.IGNORE)
                        {
                            continue;
                        }
                        if (token.type == TokenType.IDENTIFIER &&
                            moduleToFile.TryGetValue(token.value, out string real))
                        {
                            token.value = real;
                        }
                        afterFrom = false;
                    }
                    if (token.type == TokenType.FROM) afterFrom = true;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("module token rewrite failed: " + e.Message);
            }
        }

        public static Workspace GetWorkspace()
        {
            try
            {
                object sim = GetSim();
                if (sim == null) return null;
                if (_workspaceField == null)
                {
                    _workspaceField = AccessTools.Field(typeof(MainSim), "workspace");
                }
                return _workspaceField?.GetValue(sim) as Workspace;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Finds the editor window that holds a given file name.
        ///
        /// Queries the live workspace rather than trusting the registry alone:
        /// restored windows and renames do not all pass through CodeWindow.Load.
        /// </summary>
        public static CodeWindow FindOpenWindow(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            try
            {
                if (WindowRegistry.TryGetValue(name, out WeakReference weak) &&
                    weak != null && weak.IsAlive)
                {
                    CodeWindow cached = weak.Target as CodeWindow;
                    if (cached != null &&
                        string.Equals(GetFileName(cached), name, StringComparison.OrdinalIgnoreCase))
                    {
                        return cached;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("window registry lookup failed: " + e.Message);
            }

            try
            {
                object sim = GetSim();
                if (sim == null) return null;

                if (_workspaceField == null)
                {
                    _workspaceField = AccessTools.Field(typeof(MainSim), "workspace");
                }
                object workspace = _workspaceField?.GetValue(sim);
                if (workspace == null) return null;

                if (_openWindowsProperty == null)
                {
                    _openWindowsProperty = AccessTools.Property(typeof(WorkspaceBase), "OpenWindows")
                                           ?? AccessTools.Property(workspace.GetType(), "OpenWindows");
                }
                if (_openWindowsProperty == null) return null;

                if (!(_openWindowsProperty.GetValue(workspace) is System.Collections.IEnumerable windows))
                {
                    return null;
                }

                foreach (object item in windows)
                {
                    CodeWindow window = item as CodeWindow;
                    if (window == null) continue;
                    if (string.Equals(GetFileName(window), name, StringComparison.OrdinalIgnoreCase))
                    {
                        return window;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("workspace scan failed: " + e.Message);
            }
            return null;
        }

        /// <summary>
        /// Resolves an included file. Open editor windows win over disk so that
        /// unsaved edits are what actually gets compiled.
        /// </summary>
        public static string ResolveInclude(string name)
        {
            try
            {
                CodeWindow window = FindOpenWindow(name);
                if (window != null)
                {
                    string live = GetSource(window);
                    if (!string.IsNullOrEmpty(live)) return live;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("include via window failed: " + e.Message);
            }

            foreach (string dir in SaveDirectories())
            {
                // The game stores code files as "<window name>.py", so a window
                // called hello.cpp lives on disk as hello.cpp.py. Looking for the
                // bare name only would fail whenever that window is closed.
                foreach (string candidate in new[] { name, name + ".py" })
                {
                    try
                    {
                        string path = System.IO.Path.Combine(dir, candidate);
                        if (System.IO.File.Exists(path)) return System.IO.File.ReadAllText(path);
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogDebug("include via disk failed: " + e.Message);
                    }
                }
            }

            LogIncludeFailure(name);
            return null;
        }

        /// <summary>Records what was actually visible, so a failure is diagnosable.</summary>
        private static void LogIncludeFailure(string name)
        {
            try
            {
                var names = new List<string>();
                object sim = GetSim();
                if (sim != null)
                {
                    object workspace = _workspaceField?.GetValue(sim);
                    if (workspace != null && _openWindowsProperty != null &&
                        _openWindowsProperty.GetValue(workspace) is System.Collections.IEnumerable windows)
                    {
                        foreach (object item in windows)
                        {
                            CodeWindow window = item as CodeWindow;
                            if (window == null) continue;
                            names.Add(GetFileName(window) ?? "<null>");
                        }
                    }
                }
                Plugin.Log.LogWarning("找不到 include \"" + name + "\"；当前打开的代码窗口: "
                    + (names.Count == 0 ? "<none>" : string.Join(", ", names)));
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("include failure diagnostics failed: " + e.Message);
            }
        }

        private static object GetSim()
        {
            if (_simInstProperty == null)
            {
                _simInstProperty = AccessTools.Property(typeof(MainSim), "Inst");
            }
            return _simInstProperty?.GetValue(null);
        }

        /// <summary>Candidate save folders, newest layout first.</summary>
        private static System.Collections.Generic.IEnumerable<string> SaveDirectories()
        {
            string root = null;
            try
            {
                root = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "Saves");
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("persistentDataPath unavailable: " + e.Message);
            }
            if (root == null || !System.IO.Directory.Exists(root)) yield break;

            string[] dirs;
            try
            {
                dirs = System.IO.Directory.GetDirectories(root);
            }
            catch
            {
                yield break;
            }
            foreach (string dir in dirs) yield return dir;
        }

        public static bool IsCppFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (string ext in Extensions)
            {
                if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static bool IsCppWindow(CodeWindow window)
        {
            try
            {
                if (_fileNameField == null)
                {
                    _fileNameField = AccessTools.Field(typeof(CodeWindow), "fileName");
                    if (_fileNameField == null)
                    {
                        Plugin.Log.LogError("CodeWindow.fileName not found");
                        return false;
                    }
                }
                return IsCppFileName(_fileNameField.GetValue(window) as string);
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("IsCppWindow failed: " + e.Message);
                return false;
            }
        }

        public static TMPro.CodeInputField GetCodeInput(CodeWindow window)
        {
            try
            {
                if (_codeInputField == null)
                {
                    _codeInputField = AccessTools.Field(typeof(CodeWindow), "codeInput");
                }
                TMPro.CodeInputField input = _codeInputField?.GetValue(window) as TMPro.CodeInputField;
                if (input != null) return input;

                // The field is private; the game also exposes CodeInput publicly.
                // Reading it keeps the mod alive if the field name ever changes.
                if (_codeInputProperty == null)
                {
                    _codeInputProperty = AccessTools.Property(typeof(CodeWindow), "CodeInput");
                }
                return _codeInputProperty?.GetValue(window) as TMPro.CodeInputField;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Last-resort source for a window whose input text cannot be read yet
        /// (the save loader creates windows and parses them in one go). The file
        /// on disk holds exactly what the window was created from.
        /// </summary>
        public static string ReadCodeFileFromDisk(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            try
            {
                foreach (string dir in SaveDirectories())
                {
                    // .cpp windows are stored under their own name now; older
                    // saves used "<name>.py". Try both, newest layout first.
                    foreach (string candidate in new[] { fileName, fileName + ".py" })
                    {
                        string path = System.IO.Path.Combine(dir, candidate);
                        if (System.IO.File.Exists(path)) return System.IO.File.ReadAllText(path);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("disk fallback failed: " + e.Message);
            }
            return null;
        }

        public static string GetSource(CodeWindow window)
        {
            try
            {
                // Read through the concrete type instead of reflecting the "text"
                // property. The reflection path silently returned null when the
                // property lookup did not resolve, which made every .cpp window
                // compile as an empty script.
                TMPro.CodeInputField input = GetCodeInput(window);
                if (input != null)
                {
                    string text = input.text;
                    if (text != null) return text;
                }
                else
                {
                    // Visible on purpose: this is the difference between "my code
                    // does nothing" and a diagnosable failure. Limited so a broken
                    // window cannot flood the log every frame.
                    if (_sourceWarnings++ < 3)
                    {
                        Plugin.Log.LogWarning("cpp source: codeInput field is null; "
                            + "cannot read the editor text (falling back to disk)");
                    }
                }

                if (_codeTextField == null)
                {
                    _codeTextField = AccessTools.Field(typeof(CodeWindow), "codeText");
                }
                if (_codeTextField != null && _codeTextField.FieldType == typeof(string))
                {
                    return _codeTextField.GetValue(window) as string;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("cannot read the code window text: " + e);
            }
            return null;
        }

        /// <summary>
        /// Tokenises text with the game's own lexer.
        ///
        /// Tokenizer.Tokenize returns TRUE when at least one token came out as
        /// UNKNOWN (TokenType 39), i.e. true means "there is something the lexer
        /// could not classify". Getting this polarity backwards silently sends the
        /// raw C++ to the parser instead of the generated script.
        /// Returns null on failure.
        /// </summary>
        public static TokenStream Tokenize(string text, out bool hadUnknownToken)
        {
            hadUnknownToken = false;
            if (_tokenizeMethod == null)
            {
                _tokenizeMethod = AccessTools.Method(typeof(Tokenizer), "Tokenize");
                if (_tokenizeMethod == null)
                {
                    Plugin.Log.LogError("Tokenizer.Tokenize not found");
                    return null;
                }
            }
            object[] args = { text, null };
            object result = _tokenizeMethod.Invoke(null, args);
            hadUnknownToken = result is bool bad && bad;
            if (hadUnknownToken) return null;
            return args[1] as TokenStream;
        }

        /// <summary>
        /// Compiles and returns the generated script. Throws ParseException with a
        /// readable message on failure so the game's own error panel shows it.
        /// </summary>
        private static string _unlocksCsv;
        private static DateTime _unlocksAt;

        /// <summary>
        /// BuildUnlocksCsv reads through MainSim, which takes the simulation lock.
        /// Imported modules are parsed *during* simulation, so touching that lock
        /// on every compile risks a deadlock. The vocabulary changes only when the
        /// player buys research, so a short cache is safe and keeps the parse path
        /// off that lock entirely.
        /// </summary>
        /// <summary>
        /// Called from Plugin.Update on the main thread. BuildUnlocksCsv goes
        /// through MainSim (simulation lock); moving it off the compile path
        /// means an imported module's Parse never has to take that lock from
        /// the simulation thread.
        /// </summary>
        public static void RefreshUnlocks()
        {
            try
            {
                _unlocksCsv = BuildUnlocksCsv();
                _unlocksAt = DateTime.UtcNow;
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("unlock refresh failed: " + e.Message);
            }
        }

        private static string CachedUnlocks()
        {
            return _unlocksCsv;   // null => fail open, never block a compile
        }

        public static string TranspileCached(string source, out int[] lineMap)
        {
            if (source != null && UnitsBySource.TryGetValue(source, out CompiledUnit cached))
            {
                if (Plugin.CfgVerbose.Value) Plugin.Log.LogDebug("transpile cache hit");
                lineMap = cached.LineMap;
                return cached.Generated;
            }

            string unlocks;
            string error;
            string generated;
            int[] map;
            lock (NativeLock)
            {
                unlocks = Plugin.CfgEnforceUnlocks.Value ? CachedUnlocks() : null;
                error = NativeBridge.TranspileEx(source, unlocks,
                    out generated, out map);
            }
            if (error != null)
            {
                lineMap = null;
                UnitsBySource.Clear();
                Plugin.Log.LogWarning("C++ error: " + error);

                // Native errors look like "1:<line>:<col>: message". Re-shape them
                // into something the game's error panel can point at, using the
                // character offset of the offending line inside the C++ source.
                int line = 0;
                string message = error;
                string[] parts = error.Split(new[] { ':' }, 4);
                if (parts.Length >= 4 && int.TryParse(parts[1], out int parsedLine))
                {
                    line = parsedLine;
                    message = parts[3].Trim();
                }
                int offset = Math.Min(OffsetOfLine(source, line), Math.Max(0, source.Length - 1));
                throw new ParseException($"C++ L{line}: {message}", offset, offset + 1);
            }

            lineMap = map;
            if (source != null)
            {
                if (UnitsBySource.Count > 8) UnitsBySource.Clear();
                UnitsBySource[source] = new CompiledUnit
                {
                    Source = source,
                    Generated = generated,
                    LineMap = map,
                };
            }
            if (Plugin.CfgVerbose.Value)
            {
                Plugin.Log.LogDebug($"transpiled {source.Length} -> {generated.Length} chars");
            }
            return generated;
        }

        /// <summary>Character index of the first character of <paramref name="line"/>.</summary>
        private static int OffsetOfLine(string text, int line)
        {
            if (text == null || line <= 1) return 0;
            int seen = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '\n') continue;
                if (++seen == line) return i + 1;
            }
            return text.Length;
        }

        /// <summary>
        /// Unlock names that are actually available in this save.
        ///
        /// GetUnlockedKeywords() returns *language keywords* (def, =, for ...),
        /// which is a different vocabulary from the unlock names in save.json
        /// (functions, variables, loops ...). Gating must use the latter, so we
        /// read GetUnlocks() -- the same name->level map the save serialises.
        /// Returns null when nothing usable is found, which disables gating
        /// rather than blocking a player whose unlocks we misread.
        /// </summary>
        private static string BuildUnlocksCsv()
        {
            try
            {
                if (_simInstProperty == null)
                {
                    _simInstProperty = AccessTools.Property(typeof(MainSim), "Inst");
                }
                object sim = _simInstProperty?.GetValue(null);
                if (sim == null) return null;

                var names = new HashSet<string>(StringComparer.Ordinal);

                if (_getUnlocks == null)
                {
                    _getUnlocks = AccessTools.Method(typeof(MainSim), "GetUnlocks");
                }
                if (_getUnlocks != null &&
                    _getUnlocks.Invoke(sim, null) is Dictionary<string, int> levels)
                {
                    foreach (var pair in levels)
                    {
                        if (pair.Value > 0) names.Add(pair.Key);
                    }
                }

                // Cross-check the keys we care about one by one. This also covers
                // saves where GetUnlocks omits level-0 entries entirely.
                if (_isUnlocked == null)
                {
                    _isUnlocked = AccessTools.Method(typeof(MainSim), "IsUnlocked");
                }
                if (_isUnlocked != null)
                {
                    foreach (string key in KnownUnlockKeys)
                    {
                        if (_isUnlocked.Invoke(sim, new object[] { key }) is bool ok && ok)
                        {
                            names.Add(key);
                        }
                    }
                }

                bool recognised = names.Contains("functions") || names.Contains("variables");
                if (!recognised || names.Count == 0)
                {
                    Plugin.Log.LogDebug("unlock vocabulary not recognised; gating disabled");
                    DumpUnlockDiagnostics(names);
                    return null;
                }

                // The game's own keyword set is still useful for later features,
                // but it must never be used as the gate vocabulary.
                DumpUnlockDiagnostics(names);
                return string.Join(",", names);
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("unlock lookup failed: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Writes the observed unlock vocabulary next to the plugin once per run.
        /// Cheap insurance: if the game renames unlocks, this file shows exactly
        /// what to map instead of guessing.
        /// </summary>
        private static void DumpUnlockDiagnostics(HashSet<string> names)
        {
            if (_diagnosticsWritten) return;
            _diagnosticsWritten = true;
            try
            {
                string dir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".",
                    "..");
                string path = System.IO.Path.Combine(dir, "CppFarmer", "cpp_unlocks.txt");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));

                var lines = new List<string>
                {
                    "# C++ Farmer unlock diagnostics",
                    "# presence=true means our gate will treat the feature as available",
                    ""
                };
                foreach (string key in KnownUnlockKeys)
                {
                    lines.Add($"{key,-14} presence={names.Contains(key)}");
                }
                lines.Add("");
                lines.Add("all observed unlock names:");
                foreach (string n in names) lines.Add("  " + n);

                System.IO.File.WriteAllLines(path, lines);
                Plugin.Log.LogInfo("unlock diagnostics written to " + path);
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("cannot write unlock diagnostics: " + e.Message);
            }
        }

        private static bool _diagnosticsWritten;

        /// <summary>
        /// Writes the generated script next to the plugin. Used when the game's
        /// lexer rejects it, so the failure can be inspected without guessing.
        /// </summary>
        public static void DumpGenerated(string generated)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".";
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "last_generated.py"),
                    generated ?? string.Empty);
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("cannot dump generated script: " + e.Message);
            }
        }
    }
}
