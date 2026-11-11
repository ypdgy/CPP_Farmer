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
        public const string PluginVersion = "0.5.0";

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
                "Transpile windows of a registered language (e.g. .cpp) before the game parses them.");
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
                "Add a link to the C++ Farmer reference index on the game's docs home page.");
            CfgUnlockTooltips = Config.Bind("UI", "UnlockTooltips", true,
                "Show which C++ calls and syntax a research unlock grants when "
                + "hovering over it in the tech tree.");
            CfgCppExtension = Config.Bind("Storage", "UseCppExtension", true,
                "Store windows of languages that define their own suffix as "
                + "'<name>.<ext>' instead of the game's '<name>.py', so external editors "
                + "treat them as source files. Existing <name>.<ext>.py files are migrated "
                + "on load and written back without the .py suffix. Set to false to fall "
                + "back to the game's .py naming.");
            CfgExportHeader = Config.Bind("Storage", "ExportEditorHeader", true,
                "Copy each language's IDE header (e.g. cppfarmer_api.hpp) into the "
                + "active save folder, so an external editor finds it with a plain "
                + "quoted include while editing that language's files.");

            if (!NativeBridge.Available)
            {
                Log.LogError("cppfarmer_native.dll could not be loaded: " + NativeBridge.ProbeError);
                Log.LogError("Place cppfarmer_native.dll next to this plugin in BepInEx/plugins/CppFarmer/.");
                return;
            }
            Log.LogInfo("native compiler: " + NativeBridge.Version);
            ReportLanguageSupport();

            var harmony = new Harmony(Guid);

            // Language-neutral plumbing. These dispatch to whichever profile owns
            // the window, so they are registered once for every language.
            harmony.PatchAll(typeof(PatchIsValidCodeFileName));
            harmony.PatchAll(typeof(PatchStorageLoad));
            harmony.PatchAll(typeof(PatchStorageWrite));
            harmony.PatchAll(typeof(PatchStorageWatcher));
            harmony.PatchAll(typeof(PatchParserParse));
            harmony.PatchAll(typeof(PatchGetWordList));
            harmony.PatchAll(typeof(PatchGetSubWordList));
            harmony.PatchAll(typeof(PatchCodeWindowLoad));
            harmony.PatchAll(typeof(PatchLoadDoc));
            harmony.PatchAll(typeof(PatchUpdateBreakpoints));
            harmony.PatchAll(typeof(PatchHighlightSearch));
            harmony.PatchAll(typeof(PatchHighlightTheme));
            harmony.PatchAll(typeof(PatchHighlightTick));
            harmony.PatchAll(typeof(PatchKeepScrollOnBlur));
            harmony.PatchAll(typeof(PatchIndent));
            harmony.PatchAll(typeof(PatchSaveDiagnostics));
            harmony.PatchAll(typeof(PatchNoCompleteInStrings));

            // Private helpers of the overlay renderer; a game update that renames
            // them should cost the highlight, not the whole plugin.
            TryPatch(harmony, "execution highlight remap",
                typeof(PatchBlinkWordRect), typeof(PatchBlinkLineRect));

            // Language-owned editor behaviour. Each class ignores windows that
            // belong to another language, so registering all of them is safe.
            foreach (LanguageProfile language in LanguageRegistry.All)
            {
                foreach (Type patch in language.PatchTypes)
                {
                    TryPatch(harmony, language.DisplayName + " editor features", patch);
                }
            }
            Log.LogInfo("harmony patches applied");

            var host = new GameObject("CppFarmer_HelpButton");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<HelpButton>();
            CodeSupport.RefreshUnlocks();
        }

        /// <summary>
        /// Logs which languages are usable. A profile whose id the DLL does not
        /// know would silently fall back to the game parser, so say it out loud.
        /// </summary>
        private static void ReportLanguageSupport()
        {
            List<string> native = NativeBridge.NativeLanguageIds();
            foreach (LanguageProfile language in LanguageRegistry.All)
            {
                if (native.Contains(language.Id))
                {
                    Log.LogInfo("language '" + language.Id + "' (" + language.DisplayName + ") ready");
                }
                else
                {
                    Log.LogWarning("language '" + language.Id + "' has no native front-end;"
                        + " its windows will fall back to the game's own parser");
                }
            }
        }

        /// <summary>
        /// Applies patches that target private game methods. A game update that
        /// renames one should cost that single feature, not the whole plugin.
        /// </summary>
        private static void TryPatch(Harmony harmony, string what, params Type[] types)
        {
            try
            {
                foreach (Type type in types) harmony.PatchAll(type);
            }
            catch (Exception e)
            {
                Log.LogWarning(what + " skipped: " + e.Message);
            }
        }

        private float _nextUnlockRefresh;

        /// <summary>Keeps the tech-tree vocabulary fresh on the main thread.</summary>
        private void Update()
        {
            if (Time.unscaledTime < _nextUnlockRefresh) return;
            _nextUnlockRefresh = Time.unscaledTime + 2f;
            CodeSupport.RefreshUnlocks();
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
            if (LanguageRegistry.IsSupportedName(name)) __result = true;
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
                Plugin.Log.LogWarning("parse recursion guard hit; running original parser");
                return true;
            }

            _depth++;
            try
            {
                CodeWindow window = __args[1] as CodeWindow;
                LanguageProfile language = LanguageRegistry.ForWindow(window);
                if (language == null) return true;

                string fileName = CodeSupport.GetFileName(window);
                Plugin.Log.LogInfo(language.Id + " parse begin: " + fileName);

                string source = CodeSupport.GetSource(window);
                if (source == null)
                {
                    // A window that exists but whose input text is not readable yet
                    // (save loading) still has its file on disk -- use that instead
                    // of falling through to the vanilla parser. Falling through
                    // would read the C++ text as the game's own language, where
                    // "//" is the floor-division operator, and the player gets a
                    // bogus "not a valid expression" on the first comment line.
                    source = CodeSupport.ReadCodeFileFromDisk(fileName);
                    if (source != null)
                    {
                        Plugin.Log.LogWarning(language.Id + " parse: '" + fileName
                            + "' window text unavailable, read from disk ("
                            + source.Length + " chars)");
                    }
                    else
                    {
                        Plugin.Log.LogWarning(language.Id + " parse: '" + fileName
                            + "' text unavailable everywhere, compiling as empty");
                        source = string.Empty;
                    }
                }
                Plugin.Log.LogInfo(language.Id + " parse: source " + source.Length + " chars");

                string prepared;
                string generated;
                int[] lineMap;
                Dictionary<string, string> moduleToFile;
                try
                {
                    // The profile gets the last word on the source before it
                    // reaches the native front-end. C++ turns `#include "x.cpp"`
                    // into module aliases the game's importer understands.
                    prepared = language.Prepare(
                        source, fileName, CodeSupport.ResolveInclude,
                        CodeSupport.EnsureModuleWindow, out moduleToFile);
                    Plugin.Log.LogInfo(language.Id + " parse: includes prepared");

                    generated = CodeSupport.TranspileCached(language, prepared, out lineMap);
                    Plugin.Log.LogInfo(language.Id + " parse: transpiled "
                        + generated.Length + " chars");

                    // Remember the result *for this window*: two .cpp windows compiled
                    // in turn would otherwise overwrite each other's line table.
                    CodeSupport.Remember(window, prepared, generated, lineMap);
                }
                catch (ParseException e)
                {
                    // The game's error panel shows this, but the log has to carry it
                    // too: a rethrown ParseException otherwise leaves no trace of
                    // which stage refused the file.
                    Plugin.Log.LogWarning(language.Id + " parse rejected: " + e.Message);
                    throw;
                }

                Plugin.Log.LogInfo(language.Id + " parse: tokenizing");
                TokenStream stream = CodeSupport.Tokenize(generated, out bool hadUnknown);
                if (stream != null) CodeSupport.RewriteModuleTokens(stream, moduleToFile);
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
                    CodeSupport.DumpGenerated(generated);
                    throw new ParseException(language.DisplayName + " 编译成功，但" + why
                        + "，已写入 last_generated.py 供排查", 0, 0);
                }

                Plugin.Log.LogInfo(language.Id + " parse: calling game parser");
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
                Plugin.Log.LogError("front-end crashed, falling back: " + e);
                return true;
            }
            finally
            {
                _depth--;
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
            CodeSupport.RegisterWindow(__instance, CodeSupport.GetFileName(__instance));
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
                CodeWindow owner = CodeSupport.GetPanelWindow(__instance);
                if (owner == null) return;
                if (!LanguageRegistry.IsModWindow(owner)) return;

                string ownerFile = CodeSupport.GetFileName(owner);
                CodeSupport.CompiledUnit unit = CodeSupport.GetUnit(owner);

                trace.AppendLine("owner=" + ownerFile);
                if (unit == null)
                {
                    trace.AppendLine("unit=none -> skipped");
                    return;
                }
                trace.AppendLine("generated=" + (unit.Generated == null ? 0 : unit.Generated.Length)
                    + " chars, map=" + (unit.LineMap == null ? 0 : unit.LineMap.Length) + " entries");

                var lines = new List<int>();
                CodeSupport.AddBreakpointLines(owner, 1, lines, trace, ownerFile);
                trace.AppendLine("lines(1-based merged)=" + Join(lines));

                HashSet<int> offsets = CodeSupport.BuildBreakpointOffsets(
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
}
