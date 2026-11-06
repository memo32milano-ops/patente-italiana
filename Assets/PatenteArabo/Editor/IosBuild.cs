#if UNITY_IOS
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace PatenteArabo.EditorTools
{
    /// <summary>
    /// iPhone build: game-ci exports the Xcode project on Linux with CloudBuildIos, then a macOS runner
    /// runs pod install, archives and uploads to TestFlight (see .github/workflows/ios.yml).
    /// </summary>
    public static class IosBuild
    {
        const string BundleId = "com.memo.patentemondo";

        static void Settings()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.iOS.targetOSVersionString = "15.5";          // StoreKit 2 + Google ML Kit
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;     // signing is done by xcodebuild on the Mac runner
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;   // iPhone only: no iPad screenshots needed (runs on iPad in compatibility mode)
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.iOS, 1);      // arm64
            var run = System.Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
            if (int.TryParse(run, out int n)) PlayerSettings.iOS.buildNumber = n.ToString();

            // App Store icon: square, opaque, no rounded corners
            const string path = "Assets/PatenteArabo/Icon/AppIcon_ios.png";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && (imp.textureCompression != TextureImporterCompression.Uncompressed || imp.mipmapEnabled))
            {
                imp.textureCompression = TextureImporterCompression.Uncompressed; imp.mipmapEnabled = false; imp.alphaIsTransparency = false;
                imp.SaveAndReimport();
            }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (icon != null)
            {
                foreach (var kind in new[] { IconKind.Application, IconKind.Store })
                {
                    int count = PlayerSettings.GetIconSizes(NamedBuildTarget.iOS, kind).Length;
                    if (count > 0) PlayerSettings.SetIcons(NamedBuildTarget.iOS, Enumerable.Repeat(icon, count).ToArray(), kind);
                }
            }
        }

        /// <summary>Entry point for game-ci (targetPlatform iOS, buildMethod PatenteArabo.EditorTools.IosBuild.CloudBuildIos).</summary>
        public static void CloudBuildIos()
        {
            int code = 1;
            try
            {
                PatenteSetup.EnsureUrp();
                PatenteSetup.SetupAll();
                Settings();
                var scenes = PatenteSetup.EnsureScene();
                string path = PatenteSetup.Arg("-customBuildPath") ?? "build/iOS/PatenteItaliana";
                Directory.CreateDirectory(path);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = path, target = BuildTarget.iOS, options = BuildOptions.None });
                Debug.Log("[Patente] iOS export: " + report.summary.result + " -> " + path + "\n" + PatenteSetup.Errors(report));
                code = report.summary.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (System.Exception e) { Debug.LogError("[Patente] iOS build failed: " + e); }
            EditorApplication.Exit(code);
        }

        [PostProcessBuild(100)]
        public static void OnPostprocess(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;

            // ---- Xcode project: Swift plugin, frameworks, In-App Purchase capability
            string projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            string fw = proj.GetUnityFrameworkTargetGuid();
            string app = proj.GetUnityMainTargetGuid();
            foreach (var t in new[] { fw, app })
            {
                proj.SetBuildProperty(t, "SWIFT_VERSION", "5.0");
                proj.SetBuildProperty(t, "IPHONEOS_DEPLOYMENT_TARGET", "15.5");
                proj.SetBuildProperty(t, "ENABLE_BITCODE", "NO");
            }
            proj.SetBuildProperty(fw, "CLANG_ENABLE_MODULES", "YES");
            proj.AddBuildProperty(app, "LD_RUNPATH_SEARCH_PATHS", "@executable_path/Frameworks");
            proj.SetBuildProperty(app, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
            proj.SetBuildProperty(fw, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");
            foreach (var f in new[] { "StoreKit.framework", "AVFoundation.framework", "UserNotifications.framework", "PhotosUI.framework" })
                proj.AddFrameworkToProject(fw, f, false);
            proj.WriteToFile(projPath);

            var caps = new ProjectCapabilityManager(projPath, "Unity-iPhone/app.entitlements", null, app);
            caps.AddInAppPurchase();
            caps.WriteToFile();

            // ---- Info.plist
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            root.SetString("CFBundleDisplayName", "Patente Italiana");
            root.SetString("NSPhotoLibraryUsageDescription", "Choose a photo for your profile in the weekly challenge (optional).");
            var langs = root.CreateArray("CFBundleLocalizations");
            foreach (var l in new[] { "it", "en", "ar", "fr", "es", "de", "pt", "ro", "sq", "uk", "ru", "pl", "bn", "ur", "hi", "zh-Hans", "tr", "fa", "ta", "te", "gu", "ja", "ko", "th", "vi", "id", "ms", "tl", "sw", "el", "bg", "hr", "cs", "sk", "sl", "hu", "nl", "sv", "da", "no", "fi", "et", "lv", "lt", "he", "ka", "mk", "be", "ca", "kn", "mr" })
                langs.AddString(l);
            if (root.values.ContainsKey("UIApplicationExitsOnSuspend")) root.values.Remove("UIApplicationExitsOnSuspend");
            plist.WriteToFile(plistPath);

            // ---- CocoaPods: Google ML Kit on-device translation (pod install runs on the Mac runner)
            File.WriteAllText(Path.Combine(path, "Podfile"),
@"platform :ios, '15.5'
install! 'cocoapods', :deterministic_uuids => false

target 'UnityFramework' do
  pod 'GoogleMLKit/Translate', '7.0.0'
end

target 'Unity-iPhone' do
end

post_install do |installer|
  installer.pods_project.targets.each do |t|
    t.build_configurations.each do |c|
      c.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '15.5'
    end
  end
end
");
        }
    }
}
#endif
