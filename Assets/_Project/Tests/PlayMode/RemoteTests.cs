using System.Collections;
using System.IO;
using System.Net.Sockets;
using System.Text;
using NUnit.Framework;
using Pvz.Runtime.Remote;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pvz.Runtime.Tests
{
    /// <summary>T026: the debug remote drives the real scene — the same commands a computer sends to the headset.</summary>
    public class RemoteTests
    {
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            WarehouseLayoutKeeper.StoreOverride = new Pvz.Core.MemoryTextStore();
            CareerKeeper.StoreOverride = (new Pvz.Core.MemoryScoreStore(), new Pvz.Core.MemoryTextStore());
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
        }

        private static (UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode editor,
            UnityEngine.InputSystem.InputSettings.BackgroundBehavior background)? _savedInput;

        [UnityTearDown]
        public IEnumerator Forget()
        {
            if (PvzRemote.Instance != null) Object.Destroy(PvzRemote.Instance.gameObject);
            Time.timeScale = 1f;
            if (_savedInput.HasValue)
            {
                var settings = UnityEngine.InputSystem.InputSystem.settings;
                settings.editorInputBehaviorInPlayMode = _savedInput.Value.editor;
                settings.backgroundBehavior = _savedInput.Value.background;
                _savedInput = null;
            }
            WarehouseLayoutKeeper.StoreOverride = null;
            CareerKeeper.StoreOverride = null;
            yield return null;
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Autoplay_PlaysAWholeShift_AndStateReportsIt()
        {
            var remote = PvzRemote.Launch(0);
            Object.FindFirstObjectByType<ShiftDirector>().UseScoreStore(new Pvz.Core.MemoryScoreStore());
            StringAssert.Contains("\"ok\":true", remote.Execute("begin normal"));
            StringAssert.Contains("\"started\":true", remote.Execute("state"));
            StringAssert.Contains("\"difficulty\":\"Обычная\"", remote.Execute("state"));
            remote.Execute("timescale 4");
            StringAssert.Contains("\"autoplay\":true", remote.Execute("autoplay on"));

            var deadline = Time.realtimeSinceStartup + 200f;
            while (!Object.FindFirstObjectByType<ShiftDirector>().IsShiftOver && Time.realtimeSinceStartup < deadline) yield return null;

            var state = remote.Execute("state");
            StringAssert.Contains("\"shiftOver\":true", state, state);
            var stats = Object.FindFirstObjectByType<ShiftDirector>().Stats;
            Assert.Greater(stats.OrdersCompleted, 0, state);
            Assert.AreEqual(0, stats.WrongHandovers, "autoplay never gives a box to the wrong customer");
            Assert.AreEqual(0, stats.OrdersAbandoned, "nobody waits too long");
            StringAssert.Contains("no shift to play", remote.Execute("autoplay on"), "the shift is over: nothing to autoplay");
            StringAssert.Contains("\"autoplay\":false", remote.Execute("state"));
        }

        [UnityTest]
        public IEnumerator Shot_SavesAPicture_AndBadCommandsAnswerWithAnError()
        {
            var remote = PvzRemote.Launch(0);
            var reply = remote.Execute("shot remote-test counter");
            StringAssert.Contains("\"ok\":true", reply);
            var path = Path.Combine(Application.persistentDataPath, "pvz-media", "shots", "remote-test.png");
            Assert.IsTrue(File.Exists(path), reply);
            Assert.Greater(new FileInfo(path).Length, 10000, "a real picture, not an empty file");
            File.Delete(path);

            StringAssert.Contains("unknown command", remote.Execute("fly"));
            StringAssert.Contains("has not started", remote.Execute("scan BOX-001"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheSocket_AnswersCommands_LineByLine()
        {
            var port = UnityEngine.Random.Range(20000, 40000);
            var remote = PvzRemote.Launch(port);
            var token = remote.Token;
            string auth = null, reply = null, refused = null;
            var thread = new System.Threading.Thread(() =>
            {
                using (var client = new TcpClient("127.0.0.1", port))
                using (var stream = client.GetStream())
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" })
                using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
                {
                    writer.WriteLine("token " + token);
                    auth = reader.ReadLine();
                    writer.WriteLine("ping");
                    reply = reader.ReadLine();
                }
                using (var stranger = new TcpClient("127.0.0.1", port))
                using (var stream = stranger.GetStream())
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" })
                using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
                {
                    writer.WriteLine("ping"); // no token
                    refused = reader.ReadLine();
                }
            });
            thread.Start();
            var deadline = Time.realtimeSinceStartup + 10f;
            while (thread.IsAlive && Time.realtimeSinceStartup < deadline) yield return null; // the main thread answers
            Assert.IsFalse(thread.IsAlive, "no reply");
            Assert.AreEqual("{\"ok\":true,\"auth\":true}", auth);
            Assert.AreEqual("{\"ok\":true,\"pong\":true}", reply);
            StringAssert.Contains("token", refused, "another app on the headset without the token is refused");
        }

        /// <summary>
        /// T032 (#75): the virtual controller goes through the real XRI path — the ray grabs the scanner, the hand aims it,
        /// the trigger scans the box of the customer's order. No shortcut (<c>scan</c>) is used.
        /// </summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator VirtualHand_GrabsTheScanner_AndScansTheOrderBox()
        {
            // Batch-mode editor: the Game view has no focus, and the Input System would drop device input without this.
            // Restored in the tear-down, which runs after a failure or a timeout too.
            var settings = UnityEngine.InputSystem.InputSystem.settings;
            _savedInput = (settings.editorInputBehaviorInPlayMode, settings.backgroundBehavior);
            settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;

            var remote = PvzRemote.Launch(0);
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            director.UseScoreStore(new Pvz.Core.MemoryScoreStore());
            remote.Execute("begin normal");
            StringAssert.Contains("\"hand\":true", remote.Execute("hand on"));

            remote.Execute("hand aim scanner");
            yield return WaitUntilStill(remote);
            remote.Execute("hand grip on");
            yield return WaitFrames(10);
            Assert.IsTrue(director.Scanner.IsHeld, remote.Execute("hand debug"));

            var deadline = Time.realtimeSinceStartup + 40f;
            while (!director.Desk.HasActiveOrder && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(director.Desk.HasActiveOrder, "a customer came to the counter");
            var box = director.Desk.ActiveOrder.Boxes[0].Value;

            remote.Execute($"hand aim {box}");
            yield return WaitUntilStill(remote);
            remote.Execute("hand trigger on");
            yield return WaitFrames(5);
            remote.Execute("hand trigger off");
            yield return WaitFrames(5);
            var status = remote.Execute("hand");
            StringAssert.Contains("НУЖНАЯ КОРОБКА", director.Scanner.ScreenText, status);
        }

        private static IEnumerator WaitUntilStill(PvzRemote remote)
        {
            yield return WaitFrames(3);
            var deadline = Time.realtimeSinceStartup + 20f;
            while (remote.Execute("hand").Contains("\"moving\":true") && Time.realtimeSinceStartup < deadline) yield return null;
        }

        private static IEnumerator WaitFrames(int frames)
        {
            for (var i = 0; i < frames; i++) yield return null;
        }
    }
}
