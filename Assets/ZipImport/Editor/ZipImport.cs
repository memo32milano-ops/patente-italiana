using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

// Standalone editor helper (own assembly, so it still runs when the game scripts don't compile):
//  - extracts Builds/update.zip over the project, then refreshes;
//  - writes every compile error/warning of the game scripts to Builds/compile_log.txt.
[InitializeOnLoad]
static class ZipImport
{
    const string Zip = "Builds/update.zip";
    const string Log = "Builds/compile_log.txt";

    static ZipImport()
    {
        CompilationPipeline.compilationStarted += _ => { try { Directory.CreateDirectory("Builds"); File.WriteAllText(Log, "compiling " + DateTime.Now.ToString("HH:mm:ss") + "\n"); } catch { } };
        CompilationPipeline.assemblyCompilationFinished += (asm, msgs) =>
        {
            var sb = new StringBuilder();
            foreach (var m in msgs)
                if (m.type == CompilerMessageType.Error) sb.AppendLine("ERROR " + m.file + ":" + m.line + " " + m.message);
            sb.AppendLine("done " + Path.GetFileName(asm) + " " + DateTime.Now.ToString("HH:mm:ss"));
            try { File.AppendAllText(Log, sb.ToString()); } catch { }
        };
        EditorApplication.delayCall += Extract;
        EditorApplication.update += Poll;
    }

    static double _next;
    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < _next) return;
        _next = EditorApplication.timeSinceStartup + 3;
        if (File.Exists(Zip) && !EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode) Extract();
        if (File.Exists("Builds/export_project.flag")) { File.Delete("Builds/export_project.flag"); ExportProject(); }
    }

    /// <summary>Zips Assets, Packages and ProjectSettings (no Library) to Builds/project_export.zip, for the cloud build repository.</summary>
    [MenuItem("PatenteArabo/6. Export project for cloud build")]
    static void ExportProject()
    {
        const string outZip = "Builds/project_export.zip";
        int n = 0;
        try
        {
            Directory.CreateDirectory("Builds");
            if (File.Exists(outZip)) File.Delete(outZip);
            string root = Path.GetFullPath(".");
            using (var z = ZipFile.Open(outZip, ZipArchiveMode.Create))
                foreach (var dir in new[] { "Assets", "Packages", "ProjectSettings" })
                    foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        var rel = f.Replace('\\', '/');
                        z.CreateEntryFromFile(f, rel, System.IO.Compression.CompressionLevel.Optimal);
                        n++;
                    }
            File.WriteAllText("Builds/export_result.txt", "exported " + n + " files " + DateTime.Now);
        }
        catch (Exception ex) { try { File.WriteAllText("Builds/export_result.txt", "export failed: " + ex); } catch { } }
    }

    [MenuItem("PatenteArabo/0. Import Builds/update.zip")]
    static void Extract()
    {
        if (!File.Exists(Zip)) return;
        int n = 0;
        try
        {
            string root = Path.GetFullPath(".");
            using (var z = ZipFile.OpenRead(Zip))
                foreach (var e in z.Entries)
                {
                    if (string.IsNullOrEmpty(e.Name)) continue;
                    string dst = Path.GetFullPath(Path.Combine(root, e.FullName));
                    if (!dst.StartsWith(root)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    e.ExtractToFile(dst, true);
                    n++;
                }
            File.Delete(Zip);
            File.WriteAllText("Builds/import_result.txt", "imported " + n + " files " + DateTime.Now);
        }
        catch (Exception ex)
        {
            try { File.WriteAllText("Builds/import_result.txt", "import failed: " + ex); } catch { }
            return;
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
    }
}
