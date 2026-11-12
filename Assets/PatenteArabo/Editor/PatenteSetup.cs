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

        internal static string Errors(BuildReport r)
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
            foreach (var t in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.Standalone })
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
            // no "Made with Unity" screen at start (allowed for every licence from Unity 6): the app opens straight away
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
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

        // ------------------------------------------------------------------ cloud build (GitHub Actions / GameCI)
        internal static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        /// <summary>Creates a URP asset when the project has none (fresh project in the cloud), by reflection
        /// so this file also compiles where URP is missing.</summary>
        internal static void EnsureUrp()
        {
            if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null) return;
            var assetT = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime");
            var dataT = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime");
            if (assetT == null || dataT == null) { Debug.LogWarning("[Patente] URP not installed"); return; }
            Directory.CreateDirectory("Assets/Settings");
            var data = ScriptableObject.CreateInstance(dataT);
            AssetDatabase.CreateAsset(data, "Assets/Settings/Cloud_Renderer.asset");
            var create = assetT.GetMethod("Create", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var asset = (UnityEngine.Rendering.RenderPipelineAsset)create.Invoke(null, new object[] { data });
            AssetDatabase.CreateAsset(asset, "Assets/Settings/Cloud_URP.asset");
            AssetDatabase.SaveAssets();
            UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = asset;
            for (int q = 0; q < QualitySettings.names.Length; q++) { QualitySettings.SetQualityLevel(q, false); QualitySettings.renderPipeline = asset; }
            Debug.Log("[Patente] URP asset created");
        }

        internal static string[] EnsureScene()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray();
            if (scenes.Length > 0) return scenes;
            var any = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
            if (any == null)
            {
                Directory.CreateDirectory("Assets/Scenes");
                var sc = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects, UnityEditor.SceneManagement.NewSceneMode.Single);
                any = "Assets/Scenes/Main.unity";
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc, any);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(any, true) };
            return new[] { any };
        }

        /// <summary>Entry point for the cloud build: game-ci passes -customBuildPath (.apk or .aab) and the keystore args.</summary>
        public static void CloudBuild()
        {
            int code = 1;
            try
            {
                EnsureUrp();
                SetupAll();
                var scenes = EnsureScene();
                string path = Arg("-customBuildPath") ?? "build/Android/PatenteItaliana.apk";
                bool aab = path.EndsWith(".aab");
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)35;
                var ks = Arg("-androidKeystoreName");
                if (!string.IsNullOrEmpty(ks) && File.Exists(ks) && !string.IsNullOrEmpty(Arg("-androidKeystorePass")))
                {
                    PlayerSettings.Android.useCustomKeystore = true;
                    PlayerSettings.Android.keystoreName = Path.GetFullPath(ks);
                    PlayerSettings.Android.keystorePass = Arg("-androidKeystorePass");
                    PlayerSettings.Android.keyaliasName = Arg("-androidKeyaliasName");
                    PlayerSettings.Android.keyaliasPass = Arg("-androidKeyaliasPass");
                }
                else PlayerSettings.Android.useCustomKeystore = false;
                var vc = System.Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
                if (int.TryParse(vc, out int run)) PlayerSettings.Android.bundleVersionCode = 100 + run;   // always higher than any PC build
                EditorUserBuildSettings.buildAppBundle = aab;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = path, target = BuildTarget.Android, options = BuildOptions.None });
                Debug.Log("[Patente] Cloud build: " + report.summary.result + " -> " + path + "\n" + Errors(report));
                code = report.summary.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (System.Exception e) { Debug.LogError("[Patente] Cloud build failed: " + e); }
            EditorApplication.Exit(code);
        }

        [MenuItem("PatenteArabo/3. Switch platform to Android")]
        public static void SwitchAndroid()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }
    }
}
