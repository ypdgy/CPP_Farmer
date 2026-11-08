using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;

namespace CppFarmer
{
    /// <summary>
    /// Stores .cpp windows on disk as "&lt;name&gt;.cpp" instead of the game's
    /// "&lt;name&gt;.py", so external editors treat them as C++ files.
    ///
    /// The game's saver is hard-wired to ".py" in four places, and rewriting
    /// those methods wholesale would be fragile. Instead this class works
    /// around them without changing what the game sees:
    ///
    ///   load  : migrate "x.cpp.py" to "x.cpp", then create a temporary
    ///           "x.cpp.py" mirror so the vanilla loader still finds it,
    ///           and delete the mirror right after the load finishes.
    ///   save  : the vanilla writer produces "x.cpp.py"; this class renames it
    ///           to "x.cpp" and removes .cpp files whose window is gone.
    ///   watch : the file watcher only accepts ".py"; this class feeds ".cpp"
    ///           changes into the game's own pending-change queue.
    ///
    /// All of it is behind Storage/UseCppExtension so a broken game update can
    /// be worked around by setting it to false.
    /// </summary>
    internal static class CppStorage
    {
        private const string CppSuffix = ".cpp";

        /// <summary>Mirror files created for the duration of one Saver.Load.</summary>
        private static readonly List<string> Mirrors = new List<string>();

        public static bool Enabled =>
            Plugin.CfgCppExtension == null || Plugin.CfgCppExtension.Value;

        public static bool IsCppName(string name) =>
            !string.IsNullOrEmpty(name) &&
            name.EndsWith(CppSuffix, StringComparison.OrdinalIgnoreCase);

        /// <summary>Window name -> file name the game should read/write.</summary>
        public static string DiskNameFor(string windowName) =>
            IsCppName(windowName) ? windowName : windowName + ".py";

        private static string SaveDirectory()
        {
            try
            {
                return Saver.GetPathOfSaveDirectory(
                    OptionHolder.GetString("activeSave", "Save0"));
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("cpp storage: cannot resolve save folder: " + e.Message);
                return null;
            }
        }

        // ------------------------------------------------------------------
        // Load
        // ------------------------------------------------------------------
        public static void BeforeLoad()
        {
            string dir = SaveDirectory();
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

            if (Enabled)
            {
                try
                {
                    MigrateLegacyFiles(dir);
                    CreateMirrors(dir);
                }
                catch (Exception e)
                {
                    // Never block the game's own load: the vanilla loader still
                    // reads any .cpp.py that is already on disk.
                    Plugin.Log.LogWarning("cpp storage: pre-load step failed: " + e);
                }
            }

            ExportEditorFiles(dir);
        }

        /// <summary>
        /// Drops the IDE-only API header next to the code files, so an external
        /// editor finds it with a plain `#include "cppfarmer_api.hpp"` (quoted
        /// includes search the current file's folder). No .vscode folder is
        /// created: the quoted include works without any editor configuration.
        /// The header is not read by the game, and the autosave cleanup only
        /// removes .py / .cpp, so it stays put.
        /// </summary>
        private static void ExportEditorFiles(string dir)
        {
            if (Plugin.CfgExportHeader == null || !Plugin.CfgExportHeader.Value) return;
            try
            {
                string pluginDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
                if (string.IsNullOrEmpty(pluginDir)) return;
                string source = Path.Combine(pluginDir, "editor", "cppfarmer_api.hpp");
                if (!File.Exists(source))
                {
                    Plugin.Log.LogDebug("cpp storage: no editor header next to the plugin");
                    return;
                }

                string header = Path.Combine(dir, "cppfarmer_api.hpp");
                string content = File.ReadAllText(source);
                if (!File.Exists(header) ||
                    !string.Equals(File.ReadAllText(header), content, StringComparison.Ordinal))
                {
                    File.WriteAllText(header, content);
                    Plugin.Log.LogInfo("cpp storage: wrote cppfarmer_api.hpp to the save folder");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("cpp storage: editor export failed: " + e.Message);
            }
        }

        public static void AfterLoad()
        {
            if (Mirrors.Count == 0) return;
            foreach (string path in Mirrors)
            {
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogDebug("cpp storage: cannot remove mirror " + path + ": " + e.Message);
                }
            }
            Mirrors.Clear();
        }

        /// <summary>
        /// "x.cpp.py" (old layout) becomes "x.cpp". If both exist the .cpp wins,
        /// because the new layout is authoritative once it has been written.
        /// </summary>
        private static void MigrateLegacyFiles(string dir)
        {
            foreach (string legacy in Directory.GetFiles(dir, "*" + CppSuffix + ".py"))
            {
                string target = legacy.Substring(0, legacy.Length - ".py".Length);
                try
                {
                    if (File.Exists(target))
                    {
                        File.Delete(legacy);
                        Plugin.Log.LogInfo("cpp storage: dropped stale " + Path.GetFileName(legacy));
                    }
                    else
                    {
                        File.Move(legacy, target);
                        Plugin.Log.LogInfo("cpp storage: migrated "
                            + Path.GetFileName(legacy) + " -> " + Path.GetFileName(target));
                    }
                }
                catch (Exception e)
                {
                    // Locked by an external editor: keep the legacy file so the
                    // vanilla loader still finds the code.
                    Plugin.Log.LogWarning("cpp storage: cannot migrate "
                        + Path.GetFileName(legacy) + ": " + e.Message);
                }
            }
        }

        /// <summary>
        /// The vanilla loader only enumerates "*.py" and strips the extension to
        /// get the window name, so "x.cpp.py" yields the window "x.cpp".
        /// </summary>
        private static void CreateMirrors(string dir)
        {
            foreach (string cpp in Directory.GetFiles(dir, "*" + CppSuffix))
            {
                if (!IsCppName(Path.GetFileName(cpp))) continue;

                string mirror = cpp + ".py";
                string content = File.ReadAllText(cpp);
                try
                {
                    if (File.Exists(mirror) &&
                        string.Equals(File.ReadAllText(mirror), content, StringComparison.Ordinal))
                    {
                        Mirrors.Add(mirror);       // stale leftovers: delete after load
                        continue;
                    }
                    if (File.Exists(mirror))
                    {
                        Plugin.Log.LogWarning("cpp storage: "
                            + Path.GetFileName(mirror) + " differs from "
                            + Path.GetFileName(cpp) + "; using the .cpp content");
                    }
                    File.WriteAllText(mirror, content);
                    Mirrors.Add(mirror);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("cpp storage: cannot mirror "
                        + Path.GetFileName(cpp) + ": " + e.Message);
                }
            }
        }

        // ------------------------------------------------------------------
        // Save
        // ------------------------------------------------------------------
        public static void AfterWriteCodeFiles(object[] args)
        {
            if (!Enabled || args == null || args.Length < 3) return;
            if (!(args[0] is Dictionary<string, string> code)) return;
            if (!(args[2] is string savePath) || string.IsNullOrEmpty(savePath)) return;

            foreach (KeyValuePair<string, string> pair in code)
            {
                if (!IsCppName(pair.Key)) continue;

                string written = Path.Combine(savePath, pair.Key + ".py");
                string target = Path.Combine(savePath, pair.Key);
                if (!File.Exists(written)) continue;
                try
                {
                    if (File.Exists(target)) File.Delete(target);
                    File.Move(written, target);
                    UpdateLastWritten(written, target, pair.Value);
                }
                catch (Exception e)
                {
                    // Leaving "x.cpp.py" is not fatal: the next load migrates it.
                    Plugin.Log.LogError("cpp storage: cannot rename "
                        + Path.GetFileName(written) + ": " + e.Message);
                }
            }

            // The vanilla cleanup only looks at ".py", so closed .cpp windows
            // would otherwise stay on disk forever.
            foreach (string orphan in Directory.GetFiles(savePath, "*" + CppSuffix))
            {
                string name = Path.GetFileName(orphan);
                if (!IsCppName(name) || code.ContainsKey(name)) continue;
                try
                {
                    File.Delete(orphan);
                    Plugin.Log.LogInfo("cpp storage: removed closed window " + name);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogDebug("cpp storage: cannot remove " + name + ": " + e.Message);
                }
            }
        }

        /// <summary>
        /// Keeps the game's "we wrote this ourselves" cache in sync so the file
        /// watcher does not treat our own write as an external edit.
        /// </summary>
        private static void UpdateLastWritten(string oldPath, string newPath, string content)
        {
            try
            {
                FieldInfo field = AccessTools.Field(typeof(Saver), "lastWrittenCode");
                if (field?.GetValue(null) is System.Collections.Concurrent.ConcurrentDictionary<string, string> map)
                {
                    string ignored;
                    map.TryRemove(oldPath, out ignored);
                    map[newPath] = content;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("cpp storage: lastWrittenCode update failed: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // File watcher
        // ------------------------------------------------------------------
        /// <summary>
        /// Returns false when a .cpp change was queued (the vanilla handler only
        /// understands .py), true to let the original run.
        /// </summary>
        public static bool QueueCppChange(FileSystemEventArgs e)
        {
            if (!Enabled || e == null) return true;
            string name = e.Name;
            if (!IsCppName(name)) return true;

            try
            {
                FieldInfo dictField = AccessTools.Field(typeof(Saver), "pendingCodeChanges");
                FieldInfo lockField = AccessTools.Field(typeof(Saver), "pendingCodeChangesLock");
                object dict = dictField?.GetValue(null);
                object gate = lockField?.GetValue(null);
                if (dict == null || gate == null) return true;

                Type changeType = AccessTools.Inner(typeof(Saver), "PendingCodeChange");
                if (changeType == null) return true;

                ConstructorInfo ctor = changeType.GetConstructors(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)[0];
                object change = ctor.Invoke(new object[] { name, e.FullPath, DateTime.UtcNow });

                lock (gate)
                {
                    ((System.Collections.IDictionary)dict)[name] = change;
                }
                Plugin.Log.LogDebug("cpp storage: queued external change " + name);
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("cpp storage: file watcher hook failed: " + ex);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(Saver), nameof(Saver.Load))]
    internal static class PatchCppStorageLoad
    {
        [HarmonyPrefix]
        private static void Prefix() => CppStorage.BeforeLoad();

        [HarmonyPostfix]
        private static void Postfix() => CppStorage.AfterLoad();
    }

    [HarmonyPatch(typeof(Saver), "WriteCodeFiles")]
    internal static class PatchCppStorageWrite
    {
        [HarmonyPostfix]
        private static void Postfix(object[] __args) => CppStorage.AfterWriteCodeFiles(__args);
    }

    [HarmonyPatch(typeof(Saver), "OnFileChanged")]
    internal static class PatchCppStorageWatcher
    {
        [HarmonyPrefix]
        private static bool Prefix(object[] __args)
        {
            if (__args == null || __args.Length < 2) return true;
            return CppStorage.QueueCppChange(__args[1] as FileSystemEventArgs);
        }
    }
}
