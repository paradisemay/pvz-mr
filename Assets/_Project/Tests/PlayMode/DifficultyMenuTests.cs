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
            Assert.IsTrue(director.DifficultyButtons.All(b => !b.gameObject.activeInHierarchy));

            // A second press cannot restart the running shift.
            var easy = director.DifficultyButtons.First(b => b.Settings.DisplayName == "Лёгкая");
            easy.Press();
            Assert.AreSame(hard.Settings, director.ActiveSettings);
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
        }
    }
}
