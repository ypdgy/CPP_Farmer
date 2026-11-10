using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CppFarmer
{
    /// <summary>
    /// Thin P/Invoke layer over cppfarmer_native.dll. All compile logic lives in
    /// the native front-ends; the managed side only marshals strings and forwards
    /// the language id, so adding a language never changes this file.
    ///
    /// The DLL exports a language-neutral cf_* surface. Pre-0.3 builds only
    /// exported cppfarmer_*; those names still exist as aliases, which is why an
    /// outdated plugin keeps working against a new DLL rather than failing on a
    /// missing entry point.
    /// </summary>
    internal static class NativeBridge
    {
        private const string DllName = "cppfarmer_native.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl,
            CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern int cf_transpile(IntPtr language, IntPtr src, IntPtr unlocksCsv,
            out IntPtr output, out IntPtr error);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl,
            CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern int cf_transpile_ex(IntPtr language, IntPtr src, IntPtr unlocksCsv,
            out IntPtr output, out IntPtr lineMapCsv, out IntPtr error);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern void cf_free(IntPtr p);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern IntPtr cf_version();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern int cf_language_count();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern IntPtr cf_language_id(int index);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern IntPtr cf_language_display(int index);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern IntPtr cf_language_extensions(int index);

        private static int _probe;  // 0 = unknown, 1 = ok, -1 = failed
        private static string _probeError;

        public static bool Available
        {
            get
            {
                if (_probe == 0)
                {
                    try
                    {
                        PtrToUtf8(cf_version());
                        _probe = 1;
                    }
                    catch (Exception e)
                    {
                        _probe = -1;
                        _probeError = e.Message;
                    }
                }
                return _probe == 1;
            }
        }

        public static string ProbeError => _probeError;

        public static string Version
        {
            get
            {
                try
                {
                    IntPtr v = cf_version();
                    return v == IntPtr.Zero ? "?" : PtrToUtf8(v);
                }
                catch
                {
                    return "unavailable";
                }
            }
        }

        /// <summary>Language ids the DLL can compile, for mismatch diagnostics.</summary>
        public static List<string> NativeLanguageIds()
        {
            var ids = new List<string>();
            try
            {
                int count = cf_language_count();
                for (int i = 0; i < count; i++)
                {
                    string id = PtrToUtf8(cf_language_id(i));
                    if (!string.IsNullOrEmpty(id)) ids.Add(id);
                }
            }
            catch (Exception e)
            {
                Plugin.Log?.LogDebug("cannot list native languages: " + e.Message);
            }
            return ids;
        }

        /// <summary>
        /// Transpiles source. Returns null on success, otherwise an error string
        /// formatted as "file:line:col: message". A null/empty language id makes
        /// the DLL use its default front-end.
        /// </summary>
        public static string Transpile(string language, string source, string unlocksCsv,
            out string generated)
        {
            generated = null;
            IntPtr langPtr = IntPtr.Zero, srcPtr = IntPtr.Zero, unlocksPtr = IntPtr.Zero;
            IntPtr outPtr = IntPtr.Zero, errPtr = IntPtr.Zero;
            try
            {
                langPtr = Utf8OrZero(language);
                srcPtr = Utf8(source);
                unlocksPtr = unlocksCsv == null ? IntPtr.Zero : Utf8(unlocksCsv);

                int rc = cf_transpile(langPtr, srcPtr, unlocksPtr, out outPtr, out errPtr);
                if (rc == 0)
                {
                    generated = outPtr == IntPtr.Zero ? string.Empty : PtrToUtf8(outPtr);
                    return null;
                }
                string err = errPtr == IntPtr.Zero ? "unknown error" : PtrToUtf8(errPtr);
                return string.IsNullOrEmpty(err) ? "unknown error" : err;
            }
            catch (Exception e)
            {
                return "native bridge failure: " + e.Message;
            }
            finally
            {
                if (langPtr != IntPtr.Zero) Marshal.FreeHGlobal(langPtr);
                if (srcPtr != IntPtr.Zero) Marshal.FreeHGlobal(srcPtr);
                if (unlocksPtr != IntPtr.Zero) Marshal.FreeHGlobal(unlocksPtr);
                if (outPtr != IntPtr.Zero) cf_free(outPtr);
                if (errPtr != IntPtr.Zero) cf_free(errPtr);
            }
        }

        /// <summary>
        /// Transpiles and also returns the output-line -> source-line table used
        /// to translate editor breakpoints into positions inside the generated
        /// script. Returns null on success, else an error string.
        /// </summary>
        public static string TranspileEx(string language, string source, string unlocksCsv,
            out string generated, out int[] lineMap)
        {
            generated = null;
            lineMap = null;
            IntPtr langPtr = IntPtr.Zero, srcPtr = IntPtr.Zero, unlocksPtr = IntPtr.Zero;
            IntPtr outPtr = IntPtr.Zero, mapPtr = IntPtr.Zero, errPtr = IntPtr.Zero;
            try
            {
                langPtr = Utf8OrZero(language);
                srcPtr = Utf8(source);
                unlocksPtr = unlocksCsv == null ? IntPtr.Zero : Utf8(unlocksCsv);

                int rc = cf_transpile_ex(langPtr, srcPtr, unlocksPtr, out outPtr, out mapPtr, out errPtr);
                if (rc != 0)
                {
                    string err = errPtr == IntPtr.Zero ? "unknown error" : PtrToUtf8(errPtr);
                    return string.IsNullOrEmpty(err) ? "unknown error" : err;
                }

                generated = outPtr == IntPtr.Zero ? string.Empty : PtrToUtf8(outPtr);
                lineMap = ParseLineMap(mapPtr == IntPtr.Zero ? null : PtrToUtf8(mapPtr));
                return null;
            }
            catch (Exception e)
            {
                return "native bridge failure: " + e.Message;
            }
            finally
            {
                if (langPtr != IntPtr.Zero) Marshal.FreeHGlobal(langPtr);
                if (srcPtr != IntPtr.Zero) Marshal.FreeHGlobal(srcPtr);
                if (unlocksPtr != IntPtr.Zero) Marshal.FreeHGlobal(unlocksPtr);
                if (outPtr != IntPtr.Zero) cf_free(outPtr);
                if (mapPtr != IntPtr.Zero) cf_free(mapPtr);
                if (errPtr != IntPtr.Zero) cf_free(errPtr);
            }
        }

        private static int[] ParseLineMap(string csv)
        {
            if (string.IsNullOrEmpty(csv)) return null;
            string[] parts = csv.Split(',');
            var map = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                int.TryParse(parts[i], out map[i]);
            }
            return map;
        }

        /// <summary>
        /// Reads a NUL-terminated UTF-8 string. Marshal.PtrToStringUTF8 is not in
        /// netstandard2.0, which is what BepInEx plugins target.
        /// </summary>
        private static string PtrToUtf8(IntPtr p)
        {
            if (p == IntPtr.Zero) return null;
            int len = 0;
            while (Marshal.ReadByte(p, len) != 0) len++;
            if (len == 0) return string.Empty;
            byte[] buf = new byte[len];
            Marshal.Copy(p, buf, 0, len);
            return System.Text.Encoding.UTF8.GetString(buf);
        }

        /// <summary>Copies a string into unmanaged UTF-8 memory (no BOM).</summary>
        private static IntPtr Utf8(string s)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(s);
            IntPtr p = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, p, bytes.Length);
            Marshal.WriteByte(p, bytes.Length, 0);
            return p;
        }

        /// <summary>Null for a missing language id, which selects the DLL default.</summary>
        private static IntPtr Utf8OrZero(string s)
        {
            return string.IsNullOrEmpty(s) ? IntPtr.Zero : Utf8(s);
        }
    }
}
