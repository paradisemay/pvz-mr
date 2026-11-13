using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Pvz.Editor
{
    /// <summary>
    /// Device build for Meta Quest (Android, IL2CPP, ARM64):
    ///   Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod Pvz.Editor.PvzBuild.BuildQuestApk
    /// Output: Builds/Android/pvz-mr.apk (ignored by git).
    /// </summary>
    public static class PvzBuild
    {
        public const string ApkPath = "Builds/Android/pvz-mr.apk";

        [MenuItem("PVZ/Build Quest APK")]
        public static void BuildQuestApk()
        {
            PvzProjectSetup.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath) ?? "Builds");

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[PvzBuild] {summary.result}: {summary.outputPath}, {summary.totalSize / (1024 * 1024)} MB, " +
                      $"{summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
