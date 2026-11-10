using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace CppFarmer
{
    /// <summary>
    /// Everything the mod needs from the host game, plus the compile pipeline:
    /// reading a window's text, finding the file on disk, tokenising with the
    /// game's lexer, caching one transpile result per window, and translating
    /// C++ line numbers into breakpoints and execution-highlight ranges.
    ///
    /// Language-specific decisions are NOT made here: the caller resolves the
    /// window's <see cref="LanguageProfile"/> through
    /// <see cref="LanguageRegistry"/> and passes it in, so this class works
    /// unchanged for any number of front-ends.
    /// </summary>
    internal static class CodeSupport
    {
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

        public static string TranspileCached(LanguageProfile language, string source,
            out int[] lineMap)
        {
            // The key includes the language id: identical text compiled by two
            // front-ends must not share one cache slot.
            string cacheKey = source == null ? null : language.Id + "#|" + source;
            if (cacheKey != null && UnitsBySource.TryGetValue(cacheKey, out CompiledUnit cached))
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
                error = NativeBridge.TranspileEx(language.Id, source, unlocks,
                    out generated, out map);
            }
            if (error != null)
            {
                lineMap = null;
                UnitsBySource.Clear();
                Plugin.Log.LogWarning(language.DisplayName + " error: " + error);

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
                throw new ParseException($"{language.DisplayName} L{line}: {message}",
                    offset, offset + 1);
            }

            lineMap = map;
            if (cacheKey != null)
            {
                if (UnitsBySource.Count > 8) UnitsBySource.Clear();
                UnitsBySource[cacheKey] = new CompiledUnit
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
