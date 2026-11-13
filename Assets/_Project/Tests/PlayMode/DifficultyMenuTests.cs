using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pvz.Runtime.Tests
{
    /// <summary>The shift waits for a difficulty choice; each button starts it with its preset.</summary>
    public class DifficultyMenuTests
    {
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShiftWaits_UntilADifficultyIsChosen()
        {
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(director.IsStarted);
            Assert.AreEqual(3, director.DifficultyButtons.Count);
            Assert.IsTrue(director.DifficultyButtons.All(b => b.gameObject.activeInHierarchy));
            StringAssert.StartsWith("Рекорды:", Object.FindFirstObjectByType<CounterBoard>().Status, "the menu shows best scores");
        }

        [UnityTest]
        public IEnumerator HardButton_StartsAHardShift_AndHidesTheMenu()
        {
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            var hard = director.DifficultyButtons.First(b => b.Settings.DisplayName == "Сложная");

            hard.Press();
            yield return null;

            Assert.IsTrue(director.IsStarted);
            Assert.AreSame(hard.Settings, director.ActiveSettings);
            Assert.AreEqual(45, director.Shift.PatienceSeconds, 1e-6);
            Assert.AreEqual(3, director.ActiveSettings.QueueLength);
            Assert.IsTrue(director.DifficultyButtons.All(b => !b.gameObject.activeInHierarchy));

            // A second press cannot restart the running shift.
            var easy = director.DifficultyButtons.First(b => b.Settings.DisplayName == "Лёгкая");
            easy.Press();
            Assert.AreSame(hard.Settings, director.ActiveSettings);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator EasyShift_GuidesTheFirstOrder_StepByStep()
        {
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            var board = Object.FindFirstObjectByType<CounterBoard>();
            Time.timeScale = 4f;
            try
            {
                director.DifficultyButtons.First(b => b.Settings.DisplayName == "Лёгкая").Press();
                var deadline = Time.realtimeSinceStartup + 30f;
                while (!director.Desk.HasActiveOrder && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue(director.Desk.HasActiveOrder, "first customer at the counter");

                StringAssert.Contains("возьмите сканер", board.Status);
                director.Scanner.SimulateGrab();
                StringAssert.Contains("курок", board.Status);

                var wanted = director.Desk.ActiveOrder.Boxes[0];
                var other = director.RemainingBoxes.First(b => b.Id != wanted);
                director.Scanner.ScanDirect(other);
                StringAssert.Contains("не та коробка", board.Status);
                Assert.IsTrue(director.TryGetBox(wanted, out var box));
                director.Scanner.ScanDirect(box);
                StringAssert.Contains("зелёную зону", board.Status);
            }
            finally
            {
                Time.timeScale = 1f;
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator NormalShift_HasNoHints()
        {
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            var board = Object.FindFirstObjectByType<CounterBoard>();
            Time.timeScale = 4f;
            try
            {
                director.DifficultyButtons.First(b => b.Settings.DisplayName == "Обычная").Press();
                var deadline = Time.realtimeSinceStartup + 30f;
                while (!director.Desk.HasActiveOrder && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue(director.Desk.HasActiveOrder);
                StringAssert.DoesNotContain("Подсказка", board.Status);
            }
            finally
            {
                Time.timeScale = 1f;
            }
        }

        [UnityTest]
        public IEnumerator EasyShift_HasNoDeliveryAndNoBags()
        {
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            director.DifficultyButtons.First(b => b.Settings.DisplayName == "Лёгкая").Press();
            yield return null;

            Assert.IsFalse(director.Shift.HasScheduledDelivery);
            Assert.AreEqual(0, director.Shift.BagChancePercent);
            Assert.AreEqual(0, director.Shift.PatienceSeconds, 1e-6);
            Assert.AreEqual(1, director.ActiveSettings.QueueLength, "easy: one customer at a time");
        }
    }
}
