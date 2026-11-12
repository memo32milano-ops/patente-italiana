using System.IO;
using UnityEngine;

namespace PatenteArabo
{
    /// <summary>
    /// Saves that survive a crash or a full disk in the middle of writing: write a temporary file, then swap it in.
    /// A file that cannot be read is kept aside (".bad") instead of being overwritten by an empty one.
    /// </summary>
    public static class SafeFile
    {
        public static void Write(string path, string text)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            if (File.Exists(path))
            {
                try { File.Replace(tmp, path, null); return; }
                catch { File.Delete(path); }
            }
            File.Move(tmp, path);
        }

        /// <summary>Text of the file, or null when there is none. A leftover ".tmp" (crash after writing it) is used if the main file is missing.</summary>
        public static string Read(string path)
        {
            if (File.Exists(path)) return File.ReadAllText(path);
            string tmp = path + ".tmp";
            return File.Exists(tmp) ? File.ReadAllText(tmp) : null;
        }

        public static void KeepBad(string path)
        {
            try { if (File.Exists(path)) File.Copy(path, path + ".bad", true); }
            catch (System.Exception e) { Debug.LogWarning("keep bad file: " + e.Message); }
        }
    }
}
