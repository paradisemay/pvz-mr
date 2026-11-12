using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Pvz.Editor
{
    /// <summary>
    /// Runs tests through <see cref="TestRunnerApi"/> from -executeMethod (the plain -runTests CLI path
    /// refuses to start without a headless licence on this machine).
    ///   Unity.exe -batchmode -projectPath . -executeMethod Pvz.Editor.PvzTestRunner.RunEditMode -pvzResults Logs/editmode.xml
    ///   Unity.exe -batchmode -projectPath . -executeMethod Pvz.Editor.PvzTestRunner.RunPlayMode -pvzResults Logs/playmode.xml
    /// Do not pass -quit: the editor exits by itself with code 0 (all passed) or 1 (failures).
    /// </summary>
    [InitializeOnLoad]
    public static class PvzTestRunner
    {
        private const string ResultsKey = "Pvz.TestRunner.Results";
        private const string ActiveKey = "Pvz.TestRunner.Active";

        static PvzTestRunner()
        {
            // Play mode tests trigger a domain reload; re-register the callbacks afterwards.
            if (SessionState.GetBool(ActiveKey, false)) RegisterCallbacks();
        }

        public static void RunEditMode() => Run(TestMode.EditMode, "Pvz.Core.Tests");
        public static void RunPlayMode() => Run(TestMode.PlayMode, "Pvz.Runtime.Tests");

        private static void Run(TestMode mode, string assembly)
        {
            SessionState.SetString(ResultsKey, ReadArg("-pvzResults") ?? $"Logs/{mode.ToString().ToLowerInvariant()}.xml");
            SessionState.SetBool(ActiveKey, true);
            RegisterCallbacks();

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var filter = new Filter { testMode = mode, assemblyNames = new[] { assembly } };
            api.Execute(new ExecutionSettings(filter) { runSynchronously = mode == TestMode.EditMode });
        }

        private static void RegisterCallbacks()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
        }

        private static string ReadArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }
            return null;
        }

        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren) return;
                Debug.Log($"[PvzTestRunner] {result.TestStatus}: {result.FullName} {result.Message}");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var path = SessionState.GetString(ResultsKey, "Logs/tests.xml");
                SessionState.SetBool(ActiveKey, false);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
                File.WriteAllText(path, result.ToXml().OuterXml);
                Debug.Log($"[PvzTestRunner] passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} -> {path}");
                if (Application.isBatchMode) EditorApplication.Exit(result.FailCount > 0 ? 1 : 0);
            }
        }
    }
}
