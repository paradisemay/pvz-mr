using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Pvz.Editor
{
    /// <summary>
    /// Device builds for Meta Quest (Android, IL2CPP, ARM64):
    ///   Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod Pvz.Editor.PvzBuild.BuildQuestApk
    ///   … -executeMethod Pvz.Editor.PvzBuild.BuildQuestApkDev   (debug remote, see Runtime/Remote/PvzRemote)
    /// Output: Builds/Android/pvz-mr.apk and pvz-mr-dev.apk (ignored by git).
    /// </summary>
    public static class PvzBuild
    {
        public const string ApkPath = "Builds/Android/pvz-mr.apk";
        public const string DevApkPath = "Builds/Android/pvz-mr-dev.apk";

        [MenuItem("PVZ/Build Quest APK")]
        public static void BuildQuestApk() => Build(ApkPath, dev: false);

        /// <summary>
        /// The same game plus the debug remote (scripting define PVZ_REMOTE) and a hand-tracking declaration in the
        /// manifest, so the headset starts it without controllers (otherwise Quest shows «controllers required»).
        /// Never published as the release APK.
        /// </summary>
        [MenuItem("PVZ/Build Quest APK (debug remote)")]
        public static void BuildQuestApkDev() => Build(DevApkPath, dev: true);

        private static void Build(string path, bool dev)
        {
            PvzProjectSetup.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Builds");

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
                extraScriptingDefines = dev ? new[] { "PVZ_REMOTE" } : null,
            };

            DevManifest.Enabled = dev;
            BuildReport report;
            try { report = BuildPipeline.BuildPlayer(options); }
            finally { DevManifest.Enabled = false; }
            var summary = report.summary;
            Debug.Log($"[PvzBuild] {summary.result}: {summary.outputPath}, {summary.totalSize / (1024 * 1024)} MB, " +
                      $"{summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>Debug builds only: declares (optional) hand tracking, so Quest launches the app with controllers off.</summary>
        private sealed class DevManifest : IPostGenerateGradleAndroidProject
        {
            public static bool Enabled;
            public int callbackOrder => 1000;

            public void OnPostGenerateGradleAndroidProject(string path)
            {
                if (!Enabled) return;
                var manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
                if (!File.Exists(manifest)) return;
                var text = File.ReadAllText(manifest);
                var patched = DeclareHandTracking(text);
                if (patched == text) return;
                File.WriteAllText(manifest, patched);
                Debug.Log("[PvzBuild] debug build: hand tracking declared in the manifest");
            }
        }

        /// <summary>
        /// Adds an optional hand-tracking declaration before &lt;application&gt; (once; a manifest that already has it,
        /// or has no application element, is returned unchanged).
        /// </summary>
        public static string DeclareHandTracking(string manifest)
        {
            if (string.IsNullOrEmpty(manifest) || manifest.Contains("oculus.software.handtracking")) return manifest;
            var at = manifest.IndexOf("<application", System.StringComparison.Ordinal);
            if (at < 0) return manifest;
            const string lines =
                "  <uses-feature android:name=\"oculus.software.handtracking\" android:required=\"false\" />\n" +
                "  <uses-permission android:name=\"com.oculus.permission.HAND_TRACKING\" />\n";
            return manifest.Insert(at, lines);
        }
    }
}
