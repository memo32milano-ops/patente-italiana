using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PatenteArabo.EditorTools
{
    [InitializeOnLoad]
    public static class PatenteSetup
    {
        const string ResDir = "Assets/PatenteArabo/Resources/PatenteArabo";

        static PatenteSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                EnsureMaterials();
                EnsurePlayerSettings();
                EnsureUgsDefine();
            };
        }

        static string Errors(BuildReport r)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var st in r.steps) foreach (var m in st.messages)
                if (m.type == LogType.Error || m.type == LogType.Exception) sb.AppendLine(m.content);
            return sb.ToString();
        }

        /// <summary>Adds the PA_UGS define when the Leaderboards package is installed, so the challenge code compiles.</summary>
        static void EnsureUgsDefine()
        {
            bool has = System.AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Unity.Services.Leaderboards");
            foreach (var t in new[] { NamedBuildTarget.Android, NamedBuildTarget.Standalone })
            {
                var defs = PlayerSettings.GetScriptingDefineSymbols(t).Split(';').Where(d => d.Length > 0).ToList();
                bool on = defs.Contains("PA_UGS");
                if (has && !on) defs.Add("PA_UGS");
                else if (!has && on) defs.Remove("PA_UGS");
                else continue;
                PlayerSettings.SetScriptingDefineSymbols(t, string.Join(";", defs));
            }
        }

        static UnityEditor.PackageManager.Requests.AddAndRemoveRequest _pkgReq;

        [MenuItem("PatenteArabo/5. Install online challenge (Unity Gaming Services)")]
        public static void InstallUgs()
        {
            _pkgReq = UnityEditor.PackageManager.Client.AddAndRemove(new[]
            {
                "com.unity.services.authentication",
                "com.unity.services.leaderboards",
            });
            EditorApplication.update += WaitPkg;
        }

        static void WaitPkg()
        {
            if (_pkgReq == null || !_pkgReq.IsCompleted) return;
            EditorApplication.update -= WaitPkg;
            Debug.Log("[PatenteItaliana] Packages: " + _pkgReq.Status + (_pkgReq.Error != null ? " " + _pkgReq.Error.message : ""));
            Directory.CreateDirectory("Builds");
            File.WriteAllText("Builds/packages_result.txt", _pkgReq.Status + (_pkgReq.Error != null ? " " + _pkgReq.Error.message : ""));
            _pkgReq = null;
        }

        [MenuItem("PatenteArabo/1. Setup project")]
        public static void SetupAll()
        {
            EnsureMaterials();
            EnsurePlayerSettings();
            Debug.Log("[PatenteArabo] Setup done.");
        }

        static void EnsureMaterials()
        {
            Directory.CreateDirectory(ResDir);
            Make("Mat_Lit", "Universal Render Pipeline/Lit", m =>
            {
                m.SetFloat("_Smoothness", 0.2f);
                m.enableInstancing = true;
            });
            Make("Mat_Unlit", "Universal Render Pipeline/Unlit", m => m.enableInstancing = true);
            Make("Mat_Sign", "Universal Render Pipeline/Unlit", m =>
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0f);
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                m.SetOverrideTag("RenderType", "TransparentCutout");
            });
            EnsurePanelSettings();
            AssetDatabase.SaveAssets();
        }

        static void EnsurePanelSettings()
        {
            const string path = ResDir + "/UI/Panel.asset";
            var ps = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(path);
            if (ps != null) return;
            var theme = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.ThemeStyleSheet>(ResDir + "/UI/Theme.tss");
            if (theme == null) { Debug.LogWarning("[PatenteArabo] Theme.tss not imported yet"); return; }
            ps = ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>();
            ps.themeStyleSheet = theme;
            ps.scaleMode = UnityEngine.UIElements.PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = UnityEngine.UIElements.PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.6f;
            AssetDatabase.CreateAsset(ps, path);
            Debug.Log("[PatenteArabo] PanelSettings created");
        }

        static void Make(string name, string shaderName, System.Action<Material> init)
        {
            string path = ResDir + "/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var sh = Shader.Find(shaderName);
            if (sh == null) { Debug.LogWarning("[PatenteArabo] Shader not found: " + shaderName); return; }
            var m = new Material(sh);
            init(m);
            AssetDatabase.CreateAsset(m, path);
        }

        static void EnsurePlayerSettings()
        {
            if (PlayerSettings.productName != "Patente Italiana") PlayerSettings.productName = "Patente Italiana";
            if (PlayerSettings.companyName == "DefaultCompany") PlayerSettings.companyName = "Memo";
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != "com.memo.patentemondo")
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.memo.patentemondo");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            // first public release = 1.0.0; every AAB build raises the version code by one (see BuildAab)
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion.StartsWith("0."))
                PlayerSettings.bundleVersion = "1.0.0";
            if (PlayerSettings.Android.bundleVersionCode < 3) PlayerSettings.Android.bundleVersionCode = 3;
            SetIcons();
        }

        static void PrepareIcon(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            bool dirty = false;
            if (!imp.alphaIsTransparency) { imp.alphaIsTransparency = true; dirty = true; }
            if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (imp.mipmapEnabled) { imp.mipmapEnabled = false; dirty = true; }
            if (dirty) imp.SaveAndReimport();
        }

        static void SetIcons()
        {
            const string dir = "Assets/PatenteArabo/Icon/";
            foreach (var f in new[] { "AppIcon.png", "AppIcon_fg.png", "AppIcon_bg.png" }) PrepareIcon(dir + f);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "AppIcon.png");
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "AppIcon_fg.png");
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "AppIcon_bg.png");
            if (icon == null) return;
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
#if UNITY_ANDROID
            var android = NamedBuildTarget.Android;
            if (fg != null && bg != null)
            {
                var ad = PlayerSettings.GetPlatformIcons(android, UnityEditor.Android.AndroidPlatformIconKind.Adaptive);
                foreach (var pi in ad) { pi.SetTexture(bg, 0); pi.SetTexture(fg, 1); }
                PlayerSettings.SetPlatformIcons(android, UnityEditor.Android.AndroidPlatformIconKind.Adaptive, ad);
            }
#endif
        }

        [MenuItem("PatenteArabo/2. Build Android APK")]
        public static void BuildApk()
        {
            SetupAll();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                var any = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
                if (any != null) scenes = new[] { any };
            }
            Directory.CreateDirectory("Builds");
            EditorUserBuildSettings.buildAppBundle = false;
            SetIcons();
            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "Builds/PatenteItaliana.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var msg = "[PatenteArabo] Build result: " + report.summary.result + "  errors=" + report.summary.totalErrors +
                      "  size=" + report.summary.totalSize + "  path=" + report.summary.outputPath;
            Debug.Log(msg);
            File.WriteAllText("Builds/build_result.txt", msg + "\n" + Errors(report));
        }

        [MenuItem("PatenteArabo/4. Build AAB for Google Play")]
        public static void BuildAab()
        {
            SetupAll();
            if (!PlayerSettings.Android.useCustomKeystore || string.IsNullOrEmpty(PlayerSettings.Android.keystoreName))
            {
                File.WriteAllText("Builds/build_result.txt", "AAB: no keystore");
                EditorUtility.DisplayDialog("Patente Italiana", "Create or select your upload keystore first:\nEdit > Project Settings > Player > Android > Publishing Settings > Keystore Manager.", "OK");
                return;
            }
            if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
            {
                File.WriteAllText("Builds/build_result.txt", "AAB: passwords missing");
                EditorUtility.DisplayDialog("Patente Italiana", "Type the keystore and key passwords in Publishing Settings (Unity forgets them when it restarts), then build again.", "OK");
                return;
            }
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                var any = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
                if (any != null) scenes = new[] { any };
            }
            Directory.CreateDirectory("Builds");
            EditorUserBuildSettings.buildAppBundle = true;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "Builds/PatenteItaliana-" + PlayerSettings.bundleVersion + "-" + PlayerSettings.Android.bundleVersionCode + ".aab",
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            EditorUserBuildSettings.buildAppBundle = false;
            var msg = "[PatenteItaliana] AAB: " + report.summary.result + "  errors=" + report.summary.totalErrors + "  path=" + report.summary.outputPath;
            Debug.Log(msg);
            File.WriteAllText("Builds/build_result.txt", msg + "\n" + Errors(report));
            if (report.summary.result == BuildResult.Succeeded)
            {
                PlayerSettings.Android.bundleVersionCode++;   // next upload needs a higher version code
                EditorUtility.RevealInFinder(report.summary.outputPath);
            }
        }

        [MenuItem("PatenteArabo/3. Switch platform to Android")]
        public static void SwitchAndroid()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }
    }
}
