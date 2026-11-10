using System;
using System.IO;

namespace CppFarmer
{
    /// <summary>
    /// The C++ reference pages.
    ///
    /// Localizer.LoadDoc is asked for a virtual document id; only ids claimed by
    /// a language profile are answered, and nothing under StreamingAssets is
    /// touched. The page bodies are ordinary Markdown files shipped next to the
    /// plugin (cppfarmer_reference.md / cppfarmer_errors.md), so wording can be
    /// changed by editing a file and restarting -- no rebuild, no C# edit.
    ///
    /// The renderer supports headings, bold, inline code, links, &lt;columns&gt;
    /// and &lt;unlock=...&gt;. Inline code must stay in *pairs*: the renderer
    /// splits the whole page on backticks and treats every other segment as code.
    /// </summary>
    internal static class CppReference
    {
        /// <summary>Virtual document ids; never real files under StreamingAssets.</summary>
        public const string DocId = "cppfarmer_reference";

        public const string ErrorsDocId = "cppfarmer_errors";

        private const string ReferenceFile = "cppfarmer_reference.md";
        private const string ErrorsFile = "cppfarmer_errors.md";

        /// <summary>Markdown for a virtual document id. Never null.</summary>
        public static string Load(string doc)
        {
            bool errors = string.Equals(doc, ErrorsDocId, StringComparison.Ordinal);
            string fileName = errors ? ErrorsFile : ReferenceFile;
            string path = Path.Combine(
                Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".", fileName);

            try
            {
                if (File.Exists(path)) return File.ReadAllText(path);
                Plugin.Log?.LogWarning("reference page missing: " + path
                    + " -- reinstall the mod package to restore it");
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning("cannot read " + path + ": " + e.Message);
            }

            return MissingPage(fileName);
        }

        /// <summary>
        /// Shown when the Markdown file is absent. Deliberately short and free of
        /// backticks: the renderer splits the page on them, and an odd count would
        /// turn every following segment into a code block.
        /// </summary>
        private static string MissingPage(string fileName)
        {
            return "# 说明页缺失\n"
                + "\n"
                + "没找到 " + fileName + "。\n"
                + "\n"
                + "这个文件随模组包一起发布，应当位于 BepInEx 的 plugins\\CppFarmer 目录。\n"
                + "重新安装一次模组包即可恢复，也可以手动把该文件放回插件目录。\n";
        }
    }
}
