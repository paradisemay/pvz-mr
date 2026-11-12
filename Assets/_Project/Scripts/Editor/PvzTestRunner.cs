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
    /// Do not pass -quit: the editor exits by itself.
    /// Exit codes: 0 all passed · 1 failures / errors / no tests found · 2 compilation failed · 3 timeout.
    /// </summary>
    [InitializeOnLoad]
    public static class PvzTestRunner
    {
        private const string ResultsKey = "Pvz.TestRunner.Results";
        private const string ActiveKey = "Pvz.TestRunner.Active";
        private const string DeadlineKey = "Pvz.TestRunner.Deadline";
        private const double DefaultTimeoutSeconds = 900;

        static PvzTestRunner()
        {
            // Play mode tests trigger a domain reload; re-register the callbacks and the watchdog afterwards.
            if (!SessionState.GetBool(ActiveKey, false)) return;
            RegisterCallbacks();
            EditorApplication.update += Watchdog;
        }

        public static void RunEditMode() => Run(TestMode.EditMode, "Pvz.Core.Tests");
        public static void RunPlayMode() => Run(TestMode.PlayMode, "Pvz.Runtime.Tests");

        private static void Run(TestMode mode, string assembly)
        {
            if (EditorUtility.scriptCompilationFailed)
            {
                Debug.LogError("[PvzTestRunner] Script compilation failed; tests not started.");
                Finish(2);
                return;
            }

            var timeout = double.TryParse(ReadArg("-pvzTimeout"), out var t) ? t : DefaultTimeoutSeconds;
            SessionState.SetString(ResultsKey, ReadArg("-pvzResults") ?? $"Logs/{mode.ToString().ToLowerInvariant()}.xml");
            SessionState.SetFloat(DeadlineKey, (float)(EditorApplication.timeSinceStartup + timeout));
            SessionState.SetBool(ActiveKey, true);
            RegisterCallbacks();
            EditorApplication.update += Watchdog;

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var filter = new Filter { testMode = mode, assemblyNames = new[] { assembly } };
            api.Execute(new ExecutionSettings(filter) { runSynchronously = false });
        }

        private static void RegisterCallbacks()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
        }

        private static void Watchdog()
        {
            if (!SessionState.GetBool(ActiveKey, false))
            {
                EditorApplication.update -= Watchdog;
                return;
            }
            if (EditorApplication.timeSinceStartup < SessionState.GetFloat(DeadlineKey, float.MaxValue)) return;
            Debug.LogError("[PvzTestRunner] Timeout: test run did not finish.");
            Finish(3);
        }

        private static void Finish(int exitCode)
        {
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.update -= Watchdog;
            Debug.Log($"[PvzTestRunner] exit code {exitCode}");
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
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

        private sealed class Callbacks : ICallbacks, IErrorCallbacks
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
                var exitCode = 1;
                try
                {
                    var total = result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount;
                    var failed = result.TestStatus == TestStatus.Failed || result.FailCount > 0;
                    exitCode = failed || total == 0 ? 1 : 0;

                    var path = SessionState.GetString(ResultsKey, "Logs/tests.xml");
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
                    File.WriteAllText(path, result.ToXml().OuterXml);
                    Debug.Log($"[PvzTestRunner] passed={result.PassCount} failed={result.FailCount} " +
                              $"skipped={result.SkipCount} total={total} status={result.TestStatus} -> {path}");
                    if (total == 0) Debug.LogError("[PvzTestRunner] No tests were run — check the assembly filter.");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    exitCode = 1;
                }
                finally
                {
                    Finish(exitCode);
                }
            }

            public void OnError(string message)
            {
                Debug.LogError($"[PvzTestRunner] Test framework error: {message}");
                Finish(1);
            }
        }
    }
}
