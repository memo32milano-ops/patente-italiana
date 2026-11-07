using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;

namespace PatenteArabo.EditorTools
{
    /// <summary>
    /// iPhone export on the Windows PC, driven by flag files (so it can run unattended):
    ///   Builds/ios_export.flag  -> switch to iOS, export the Xcode project, zip it to Builds/ios_xcode.zip, switch back to Android.
    /// Progress in Builds/ios_status.txt.
    /// </summary>
    [InitializeOnLoad]
    static class IosPcExport
    {
        const string Flag = "Builds/ios_export.flag", Stage = "Builds/ios_stage.txt", Status = "Builds/ios_status.txt";
        static double _next;
        static IosPcExport() { EditorApplication.update += Poll; }

        static void Log(string s) { try { File.AppendAllText(Status, DateTime.Now.ToString("HH:mm:ss") + " " + s + "\n"); } catch { } }

        [MenuItem("PatenteArabo/7. Export iPhone (Xcode project zip)")]
        static void Start() { Directory.CreateDirectory("Builds"); File.WriteAllText(Flag, "1"); }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _next || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            _next = EditorApplication.timeSinceStartup + 3;
            if (File.Exists(Flag))
            {
                File.Delete(Flag);
                File.WriteAllText(Status, "");
                if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS) { File.WriteAllText(Stage, "build"); return; }
                File.WriteAllText(Stage, "build");
                Log("switching to iOS");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS)) { Log("switch FAILED (iOS Build Support installed?)"); File.Delete(Stage); }
                return;
            }
            if (!File.Exists(Stage)) return;
            string st = File.ReadAllText(Stage).Trim();
            if (st == "build" && EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS)
            {
                File.WriteAllText(Stage, "back");
                try
                {
                    var t = Type.GetType("PatenteArabo.EditorTools.IosBuild, Assembly-CSharp-Editor");
                    if (t == null) { Log("IosBuild type missing (scripts not compiled for iOS)"); return; }
                    Log("exporting Xcode project...");
                    var res = (string)t.GetMethod("ExportForPc").Invoke(null, null);
                    Log("export: " + res);
                    if (res.StartsWith("Succeeded"))
                    {
                        const string zip = "Builds/ios_xcode.zip";
                        if (File.Exists(zip)) File.Delete(zip);
                        ZipFile.CreateFromDirectory("Builds/iOS/PatenteItaliana", zip, System.IO.Compression.CompressionLevel.Optimal, false);
                        Log("zip ready " + new FileInfo(zip).Length / (1024 * 1024) + " MB");
                    }
                }
                catch (Exception e) { Log("ERROR " + e); }
            }
            else if (st == "back")
            {
                File.Delete(Stage);
                Log("switching back to Android");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                Log("DONE");
            }
        }
    }
}
