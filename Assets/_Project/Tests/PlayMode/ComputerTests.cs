using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pvz.Runtime.Tests
{
    /// <summary>T033 (#77): the counter computer keeps its keys during the shift and switches screens.</summary>
    public class ComputerTests
    {
        private PvzComputer _computer;
        private ShiftDirector _director;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            PlacementKeeper.StoreOverride = new Pvz.Core.MemoryTextStore(); // nor the saved place of the counter
            CareerKeeper.StoreOverride = (new Pvz.Core.MemoryScoreStore(), new Pvz.Core.MemoryTextStore());
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            _computer = Object.FindFirstObjectByType<PvzComputer>();
            _director = Object.FindFirstObjectByType<ShiftDirector>();
            _director.UseScoreStore(new Pvz.Core.MemoryScoreStore());
        }

        [UnityTearDown]
        public IEnumerator Forget()
        {
            PlacementKeeper.StoreOverride = null;
            CareerKeeper.StoreOverride = null;
            yield return null;
        }

        private static ComputerKey Key(ComputerKey.Action action) => Object
            .FindObjectsByType<ComputerKey>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault(k => k.Kind == action);

        [UnityTest]
        public IEnumerator BeforeTheShift_TheMenuShowsDifficultyAndSettings()
        {
            yield return null;
            Assert.AreEqual(PvzComputer.Screen.Menu, _computer.Current);
            Assert.IsTrue(_director.DifficultyButtons.All(b => b.gameObject.activeInHierarchy));
            Assert.IsNotNull(Key(ComputerKey.Action.OpenSettings));
            Assert.IsNull(Key(ComputerKey.Action.EndShift), "no «Завершить смену» before the shift");
            StringAssert.Contains("сложность", _computer.ScreenText);
        }

        [UnityTest]
        public IEnumerator Settings_OpenAndGoBack_AndOfferTheStandAndWarehouseOnlyBeforeTheShift()
        {
            Key(ComputerKey.Action.OpenSettings).Press();
            yield return null;
            Assert.AreEqual(PvzComputer.Screen.Settings, _computer.Current);
            Assert.IsTrue(Object.FindFirstObjectByType<RecenterButton>(FindObjectsInactive.Include).gameObject.activeInHierarchy);
            Assert.IsTrue(Object.FindFirstObjectByType<ThemeButton>(FindObjectsInactive.Include).gameObject.activeInHierarchy);
            Assert.IsFalse(_director.DifficultyButtons.First().gameObject.activeInHierarchy, "one screen at a time");

            Key(ComputerKey.Action.Back).Press();
            yield return null;
            Assert.AreEqual(PvzComputer.Screen.Menu, _computer.Current);

            _director.Begin();
            yield return null;
            Key(ComputerKey.Action.OpenSettings).Press();
            yield return null;
            Assert.AreEqual(PvzComputer.Screen.Settings, _computer.Current);
            Assert.IsFalse(Object.FindFirstObjectByType<RecenterButton>(FindObjectsInactive.Include).gameObject.activeInHierarchy,
                "moving the play area mid-shift is a separate task (#78)");
            Assert.IsTrue(Object.FindFirstObjectByType<ThemeButton>(FindObjectsInactive.Include).gameObject.activeInHierarchy);
            Key(ComputerKey.Action.Back).Press();
            yield return null;
            Assert.AreEqual(PvzComputer.Screen.Shift, _computer.Current, "back to the shift, not to the menu");
        }

        [UnityTest]
        public IEnumerator DuringTheShift_TheComputerShowsTheOrder_AndKeepsItsKeys()
        {
            _director.Begin();
            yield return null;
            Assert.AreEqual(PvzComputer.Screen.Shift, _computer.Current);
            Assert.IsNotNull(Key(ComputerKey.Action.EndShift));
            Assert.IsNotNull(Key(ComputerKey.Action.OpenSettings));

            var deadline = Time.realtimeSinceStartup + 30f;
            while (!_director.Desk.HasActiveOrder && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSecondsRealtime(0.4f); // the screen refreshes a few times a second
            StringAssert.Contains("Отсканируйте код", _computer.ScreenText, "T035: the order is unknown before the phone's code");
            StringAssert.DoesNotContain(_director.Desk.ActiveOrder.Number, _computer.ScreenText);

            _director.Scanner.ScanDirect(_director.CurrentCustomer.Phone);
            yield return new WaitForSecondsRealtime(0.4f);
            StringAssert.Contains("Заказ №" + _director.Desk.ActiveOrder.Number, _computer.ScreenText);
            StringAssert.Contains("Товаров:", _computer.ScreenText);
        }

        [UnityTest]
        public IEnumerator EndShift_Unconfirmed_ExpiresAndTheKeyReadsAsBefore()
        {
            _director.Begin();
            yield return null;
            var end = Key(ComputerKey.Action.EndShift);
            var label = end.GetComponentInChildren<TMPro.TMP_Text>();
            end.Press();
            Assert.AreEqual("ТОЧНО?", label.text);
            yield return new WaitForSecondsRealtime(3.6f);
            Assert.IsFalse(_computer.IsConfirmingEnd);
            Assert.AreEqual(PvzComputer.EndShiftCaption, label.text, "the key is «Завершить смену» again");
            end.Press();
            yield return null;
            Assert.IsFalse(_director.Shift.IsTimeUp, "a press after the window only asks again");

            _computer.Back(); // not on the settings screen: nothing happens
            Assert.AreEqual(PvzComputer.Screen.Shift, _computer.Current);
        }

        [UnityTest]
        public IEnumerator EndShift_AsksToConfirm_ThenEndsTheShift_AndOffersANewOne()
        {
            _director.Begin();
            yield return null;
            var end = Key(ComputerKey.Action.EndShift);
            end.Press();
            yield return null;
            Assert.IsTrue(_computer.IsConfirmingEnd);
            Assert.IsFalse(_director.Shift.IsTimeUp, "one press only asks");

            end.Press();
            var deadline = Time.realtimeSinceStartup + 20f;
            while (!_director.IsShiftOver && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(_director.IsShiftOver);
            yield return null;
            Assert.AreEqual(PvzComputer.Screen.Summary, _computer.Current);
            Assert.IsTrue(Object.FindFirstObjectByType<RestartButton>(FindObjectsInactive.Include).gameObject.activeInHierarchy);
            StringAssert.Contains("Смена завершена", _computer.ScreenText);
        }
    }
}
