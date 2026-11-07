using System;
using System.Collections.Generic;

namespace CppFarmer
{
    /// <summary>
    /// Turns  #include "x.cpp"  into something the game can actually follow.
    ///
    /// The first implementation merged the included file into the caller's text.
    /// That worked for execution but made every node belong to the *calling*
    /// window, so the execution highlight and breakpoints could never move into
    /// the other file -- the game moves them by asking each node which CodeWindow
    /// it came from.
    ///
    /// The game already has the right mechanism: `import name` looks the window up
    /// in `workspace.codeWindows[name]` and calls its `Parse()`, so the nodes it
    /// produces carry that window. So the compiler emits `from x import *` and this
    /// class only has to make sure the module name resolves to the right window.
    /// </summary>
    internal static class CppIncludes
    {
        /// <summary>
        /// Scans the source for double-quoted includes, validates them and makes
        /// sure each included file has a live, dirty window. Returns the source
        /// unchanged (the compiler handles the directive) plus a map of
        /// module name -> real window name for RewriteModuleTokens.
        /// </summary>
        public static string Prepare(string source, string originName,
            Func<string, string> resolve, Func<string, string, bool> ensureWindow,
            out Dictionary<string, string> moduleToFile)
        {
            moduleToFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(source)) return source;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = source.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                if (!TryParseInclude(lines[i], out string target, out bool angled)) continue;
                if (angled) continue;                       // no C++ standard library here
                if (!seen.Add(target)) continue;

                string module = ModuleNameOf(target);
                if (module == null)
                {
                    throw new ParseException(
                        "C++ L" + (i + 1) + ": 无法从 #include「" + target
                        + "」得到模块名。文件名必须以字母或下划线开头，只含字母、数字、下划线，"
                        + "例如 hello.cpp 会被导入为 hello", 0, 0);
                }

                // A missing file is reported by the resolver, which knows both the
                // open windows and the save folder.
                if (resolve(target) == null)
                {
                    throw new ParseException(
                        "C++ L" + (i + 1) + ": 找不到 #include「" + target
                        + "」（应位于同一存档目录，或已在该编辑器中打开）", 0, 0);
                }

                if (!ensureWindow(module, target))
                {
                    throw new ParseException(
                        "C++ L" + (i + 1) + ": 无法为 #include「" + target
                        + "」找到可用的代码窗口", 0, 0);
                }
                moduleToFile[module] = target;
            }
            return source;
        }

        /// <summary>"hello.cpp" -> "hello". Null when the stem is not an identifier.</summary>
        public static string ModuleNameOf(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;

            string name = file;
            int slash = name.LastIndexOfAny(new[] { '/', '\\' });
            if (slash >= 0) name = name.Substring(slash + 1);
            int dot = name.LastIndexOf('.');
            if (dot > 0) name = name.Substring(0, dot);
            if (name.Length == 0) return null;

            char first = name[0];
            if (!(char.IsLetter(first) || first == '_')) return null;
            foreach (char c in name)
            {
                if (!(char.IsLetterOrDigit(c) || c == '_')) return null;
            }
            return name;
        }

        /// <summary>
        /// Recognises  [#] include "name"  /  [#] include &lt;name&gt;.
        /// </summary>
        private static bool TryParseInclude(string line, out string target, out bool angled)
        {
            target = null;
            angled = false;
            if (line == null) return false;

            int i = 0;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
            if (i >= line.Length || line[i] != '#') return false;
            i++;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;

            const string keyword = "include";
            if (i + keyword.Length > line.Length) return false;
            if (string.Compare(line, i, keyword, 0, keyword.Length, StringComparison.Ordinal) != 0)
            {
                return false;
            }
            i += keyword.Length;
            if (i >= line.Length || (line[i] != ' ' && line[i] != '\t')) return false;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
            if (i >= line.Length) return false;

            char open = line[i];
            char close = open == '"' ? '"' : open == '<' ? '>' : '\0';
            if (close == '\0') return false;

            int end = line.IndexOf(close, i + 1);
            if (end < 0) return false;

            target = line.Substring(i + 1, end - i - 1).Trim();
            angled = open == '<';
            return target.Length > 0;
        }
    }
}
