using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;

namespace CppFarmer
{
    /// <summary>
    /// Stores code windows under their own file extension ("main.cpp") instead of
    /// the game's "main.cpp.py", so external editors treat them as source files.
    ///
    /// Which extensions this applies to comes from
    /// <see cref="LanguageProfile.StorageSuffix"/>; a language without one keeps
    /// the game's naming and is untouched by everything below.
    ///
    /// The game's saver is hard-wired to ".py" in four places, and rewriting
    /// those methods wholesale would be fragile. Instead this class works around
    /// them without changing what the game sees:
    ///
    ///   load  : migrate "x.cpp.py" to "x.cpp", then create a temporary
    ///           "x.cpp.py" mirror so the vanilla loader still finds it,
    ///           and delete the mirror right after the load finishes.
    ///   save  : the vanilla writer produces "x.cpp.py"; this class renames it
    ///           to "x.cpp" and removes storage files whose window is gone.
    ///   watch : the file watcher only accepts ".py"; this class feeds the real
    ///           extension's changes into the game's own pending-change queue.
    ///
    /// All of it is behind Storage/UseCppExtension so a broken game update can
    /// be worked around by setting it to false.
    /// </summary>
    internal static class SaveStorage
    {
        /// <summary>Mirror files created for the duration of one Saver.Load.</summary>
        private static readonly List<string> Mirrors = new List<string>();

        public static bool Enabled =>
            Plugin.CfgCppExtension == null || Plugin.CfgCppExtension.Value;

        /// <summary>Languages that save windows under their own extension.</summary>
        private static IReadOnlyList<LanguageProfile> Languages => LanguageRegistry.WithStorage;

        public static bool IsStorageName(string name) =>
            !string.IsNullOrEmpty(name) && LanguageRegistry.IsStorageName(name);

        private static string SaveDirectory()
        {
            try
            {
                return Saver.GetPathOfSaveDirectory(
                    OptionHolder.GetString("activeSave", "Save0"));
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug("storage: cannot resolve save folder: " + e.Message);
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
                    Plugin.Log.LogWarning("storage: pre-load step failed: " + e);
                }
            }

            ExportEditorFiles(dir);
        }

        /// <summary>
        /// Drops each language's IDE-only header next to the code files, so an
        /// external editor finds it with a plain `#include "cppfarmer_api.hpp"`
        /// (quoted includes search the current file's folder). No editor config
        /// files are written: the quoted include works without them.
        /// The header is not read by the game, and the autosave cleanup only
        /// removes .py / storage suffixes, so it stays put.
        /// </summary>
        private static void ExportEditorFiles(string dir)
        {
            if (Plugin.CfgExportHeader == null || !Plugin.CfgExportHeader.Value) return;

            string pluginDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
            if (string.IsNullOrEmpty(pluginDir)) return;

            foreach (LanguageProfile language in LanguageRegistry.All)
            {
                string headerName = language.EditorHeaderFileName;
                if (string.IsNullOrEmpty(headerName)) continue;

                try
                {
                    string source = Path.Combine(pluginDir, "editor", headerName);
                    if (!File.Exists(source))
                    {
                        Plugin.Log.LogDebug("storage: no editor header for " + language.Id);
                        continue;
                    }

                    string header = Path.Combine(dir, headerName);
                    string content = File.ReadAllText(source);
                    if (!File.Exists(header) ||
                        !string.Equals(File.ReadAllText(header), content, StringComparison.Ordinal))
                    {
                        File.WriteAllText(header, content);
                        Plugin.Log.LogInfo("storage: wrote " + headerName + " to the save folder");
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogDebug("storage: editor export failed: " + e.Message);
                }
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
                    Plugin.Log.LogDebug("storage: cannot remove mirror " + path + ": " + e.Message);
                }
            }
            Mirrors.Clear();
        }

        /// <summary>
        /// "x.cpp.py" (old layout) becomes "x.cpp". If both exist the storage file
        /// wins, because the new layout is authoritative once it has been written.
        /// </summary>
        private static void MigrateLegacyFiles(string dir)
        {
            foreach (LanguageProfile language in Languages)
            {
                string suffix = language.StorageSuffix;
                foreach (string legacy in Directory.GetFiles(dir, "*" + suffix + ".py"))
                {
                    string target = legacy.Substring(0, legacy.Length - ".py".Length);
                    try
                    {
                        if (File.Exists(target))
                        {
                            File.Delete(legacy);
                            Plugin.Log.LogInfo("storage: dropped stale "
                                + Path.GetFileName(legacy));
                        }
                        else
                        {
                            File.Move(legacy, target);
                            Plugin.Log.LogInfo("storage: migrated "
                                + Path.GetFileName(legacy) + " -> " + Path.GetFileName(target));
                        }
                    }
                    catch (Exception e)
                    {
                        // Locked by an external editor: keep the legacy file so the
                        // vanilla loader still finds the code.
                        Plugin.Log.LogWarning("storage: cannot migrate "
                            + Path.GetFileName(legacy) + ": " + e.Message);
                    }
                }
            }
        }

        /// <summary>
        /// The vanilla loader only enumerates "*.py" and strips the extension to
        /// get the window name, so "x.cpp.py" yields the window "x.cpp".
        /// </summary>
        private static void CreateMirrors(string dir)
        {
            foreach (LanguageProfile language in Languages)
            {
                string suffix = language.StorageSuffix;
                foreach (string stored in Directory.GetFiles(dir, "*" + suffix))
                {
                    if (!stored.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;

                    string mirror = stored + ".py";
                    string content = File.ReadAllText(stored);
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
                            Plugin.Log.LogWarning("storage: "
                                + Path.GetFileName(mirror) + " differs from "
                                + Path.GetFileName(stored) + "; using the stored content");
                        }
                        File.WriteAllText(mirror, content);
                        Mirrors.Add(mirror);
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning("storage: cannot mirror "
                            + Path.GetFileName(stored) + ": " + e.Message);
                    }
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
                if (!IsStorageName(pair.Key)) continue;

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
                    Plugin.Log.LogError("storage: cannot rename "
                        + Path.GetFileName(written) + ": " + e.Message);
                }
            }

            // The vanilla cleanup only looks at ".py", so closed windows stored
            // under their own extension would otherwise stay on disk forever.
            foreach (LanguageProfile language in Languages)
            {
                foreach (string orphan in Directory.GetFiles(savePath, "*" + language.StorageSuffix))
                {
                    string name = Path.GetFileName(orphan);
                    // GetFiles("*" + suffix) can be loose on Windows (8.3 short
                    // names); never delete a file that belongs to another language.
                    if (!name.EndsWith(language.StorageSuffix, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!IsStorageName(name) || code.ContainsKey(name)) continue;
                    try
                    {
                        File.Delete(orphan);
                        Plugin.Log.LogInfo("storage: removed closed window " + name);
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogDebug("storage: cannot remove " + name + ": " + e.Message);
                    }
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
                Plugin.Log.LogDebug("storage: lastWrittenCode update failed: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // File watcher
        // ------------------------------------------------------------------
        /// <summary>
        /// Returns false when an external change was queued (the vanilla handler
        /// only understands .py), true to let the original run.
        /// </summary>
        public static bool QueueExternalChange(FileSystemEventArgs e)
        {
            if (!Enabled || e == null) return true;
            string name = e.Name;
            if (!IsStorageName(name)) return true;

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
                Plugin.Log.LogDebug("storage: queued external change " + name);
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("storage: file watcher hook failed: " + ex);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(Saver), nameof(Saver.Load))]
    internal static class PatchStorageLoad
    {
        [HarmonyPrefix]
        private static void Prefix() => SaveStorage.BeforeLoad();

        [HarmonyPostfix]
        private static void Postfix() => SaveStorage.AfterLoad();
    }

    [HarmonyPatch(typeof(Saver), "WriteCodeFiles")]
    internal static class PatchStorageWrite
    {
        [HarmonyPostfix]
        private static void Postfix(object[] __args) => SaveStorage.AfterWriteCodeFiles(__args);
    }

    [HarmonyPatch(typeof(Saver), "OnFileChanged")]
    internal static class PatchStorageWatcher
    {
        [HarmonyPrefix]
        private static bool Prefix(object[] __args)
        {
            if (__args == null || __args.Length < 2) return true;
            return SaveStorage.QueueExternalChange(__args[1] as FileSystemEventArgs);
        }
    }
}
