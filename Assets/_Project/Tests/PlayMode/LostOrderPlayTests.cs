using System.Collections;
using System.Linq;
using NUnit.Framework;
using Pvz.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pvz.Runtime.Tests
{
    /// <summary>T047 (#86): «потеряшки» in the scene — the missing box never drops out of the hatch, «Не найден» closes the order.</summary>
    public class LostOrderPlayTests
    {
        private ShiftDirector _director;

        private IEnumerator Begin(int lostChance)
        {
            PlacementKeeper.StoreOverride = new MemoryTextStore();
            CareerKeeper.StoreOverride = (new MemoryScoreStore(), new MemoryTextStore());
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            _director = Object.FindFirstObjectByType<ShiftDirector>();
            _director.UseScoreStore(new MemoryScoreStore());
            _director.UseStockSeed(12345);
            _director.UseLostChance(lostChance);
            _director.Begin();
            _director.UseRandomSeed(12345);
            _director.UsePatience(0);
            _director.UseReturnChance(0);
            _director.UseRefusalChance(0);
            _director.UseEventChances(0);
            _director.UseAutoCustomerCode();
            _director.UseTimeLimit(0);
            Time.timeScale = 4f;
            var deadline = Time.realtimeSinceStartup + 20f;
            while (_director.Hatch.IsDropping && Time.realtimeSinceStartup < deadline) yield return null;
            deadline = Time.realtimeSinceStartup + 40f;
            while (!_director.Desk.HasActiveOrder && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(_director.Desk.HasActiveOrder, "a customer at the counter");
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.timeScale = 1f;
            PlacementKeeper.StoreOverride = null;
            CareerKeeper.StoreOverride = null;
            yield return null;
        }

        private static ComputerKey NotFoundKey() => Object.FindObjectsByType<ComputerKey>(FindObjectsSortMode.None)
            .First(k => k.Kind == ComputerKey.Action.NotFound);

        [UnityTest, Timeout(90000)]
        public IEnumerator AMissingBox_IsNotInTheRoom_AndNotFoundClosesTheOrderAsLost()
        {
            yield return Begin(lostChance: 100); // every order misses a box
            var order = _director.Desk.ActiveOrder;
            var missing = order.Boxes.Where(_director.Shift.IsMissing).ToArray();
            Assert.AreEqual(1, missing.Length, "one box of the order is missing");
            Assert.IsFalse(_director.TryGetBox(missing[0], out _), "it never dropped out of the hatch");
            Assert.IsFalse(_director.RemainingBoxes.Any(b => b.Id == missing[0]));

            var key = NotFoundKey();
            Assert.IsTrue(key.gameObject.activeInHierarchy, "«Не найден» is on the shift screen");
            key.Press();
            yield return null;
            Assert.IsFalse(_director.Desk.HasCustomer, "the order is closed as lost");
            Assert.AreEqual(1, _director.Stats.LostOrders);
            StringAssert.Contains("Потеряшка", Object.FindFirstObjectByType<CounterBoard>().Headline);
            yield return new WaitForSeconds(0.5f);
            StringAssert.Contains($"№{order.Number} — потеряшка", Object.FindFirstObjectByType<CounterBoard>().Orders);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator NotFound_WhenTheBoxIsThere_CostsPoints_AndTheCustomerWaits()
        {
            yield return Begin(lostChance: 0);
            var order = _director.Desk.ActiveOrder;
            var before = _director.Stats.Score;
            NotFoundKey().Press();
            yield return null;
            Assert.IsTrue(_director.Desk.HasActiveOrder && _director.Desk.ActiveOrder == order, "the customer keeps waiting");
            Assert.AreEqual(1, _director.Stats.FalseNotFound);
            Assert.Less(_director.Stats.Score, before);
            StringAssert.Contains("есть на складе", Object.FindFirstObjectByType<CounterBoard>().Headline);
        }
    }
}
