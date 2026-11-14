using System.Collections;
using System.Linq;
using NUnit.Framework;
using Pvz.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pvz.Runtime.Tests
{
    /// <summary>T045 (#85): a refused box comes back to the counter, goes into the returns bin and is handed in.</summary>
    public class ReturnsBinPlayTests
    {
        private ShiftDirector _director;

        [UnitySetUp]
        public IEnumerator BeginAShiftWhereEveryoneRefuses()
        {
            PlacementKeeper.StoreOverride = new MemoryTextStore();
            CareerKeeper.StoreOverride = (new MemoryScoreStore(), new MemoryTextStore());
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            _director = Object.FindFirstObjectByType<ShiftDirector>();
            _director.UseScoreStore(new MemoryScoreStore());
            _director.UseStockSeed(12345);
            _director.Begin();
            _director.UseRandomSeed(12345);
            _director.UsePatience(0);
            _director.UseReturnChance(0);
            _director.UseEventChances(0);
            _director.UseBagChance(0);
            _director.UseAutoCustomerCode();
            _director.UseTimeLimit(0);
            _director.UseRefusalChance(100); // every customer refuses one box
            Time.timeScale = 4f;
            var deadline = Time.realtimeSinceStartup + 20f;
            while (_director.Hatch.IsDropping && Time.realtimeSinceStartup < deadline) yield return null;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.timeScale = 1f;
            PlacementKeeper.StoreOverride = null;
            CareerKeeper.StoreOverride = null;
            yield return null;
        }

        private static void Put(Component thing, Vector3 position)
        {
            var body = thing.GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            thing.transform.position = position;
            if (body != null) body.position = position;
            Physics.SyncTransforms();
        }

        private IEnumerator ServeTheFirstCustomer()
        {
            var deadline = Time.realtimeSinceStartup + 40f;
            while (!_director.Desk.HasActiveOrder && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(_director.Desk.HasActiveOrder, "a customer at the counter");
            foreach (var id in _director.Desk.ActiveOrder.Boxes.ToArray())
            {
                Assert.IsTrue(_director.TryGetBox(id, out var box));
                Put(box, _director.Zone.transform.position + Vector3.up * 0.05f);
                yield return new WaitForFixedUpdate();
                yield return null;
            }
            Assert.AreEqual(1, _director.Stats.OrdersCompleted, "handed over — with a refusal");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ARefusedBox_ComesBackToTheCounter_AndIsHandedInFromTheBin()
        {
            yield return ServeTheFirstCustomer();
            Assert.AreEqual(1, _director.Stats.Refusals);
            StringAssert.Contains("не подходит", _director.CurrentCustomer.LastLine.ToLowerInvariant());
            var refused = _director.ReturnBoxes.Single();
            Assert.IsTrue(refused.IsReturn);
            Assert.IsFalse(refused.transform.IsChildOf(_director.CurrentCustomer.transform), "the customer did not take it");
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(refused != null && refused.transform.position.y > 0.5f, "it lies on the counter");

            var computer = Object.FindFirstObjectByType<PvzComputer>();
            StringAssert.Contains("Возвраты: в ящике 0 из 1", computer.ScreenText);
            Assert.AreEqual(0, _director.HandInReturns(), "the bin is empty: nothing is handed in");

            Put(refused, _director.ReturnsBin.transform.position + Vector3.up * 0.3f);
            yield return new WaitForSeconds(1.5f);
            Assert.IsTrue(_director.ReturnsBin.Contains(refused), "it fell into the bin");
            Assert.AreEqual(1, _director.Shift.Returns.InBin);
            StringAssert.Contains("Возвраты: в ящике 1 из 1", computer.ScreenText);

            var before = _director.Stats.Score;
            var key = Object.FindObjectsByType<ComputerKey>(FindObjectsSortMode.None).First(k => k.Kind == ComputerKey.Action.HandInReturns);
            Assert.IsTrue(key.gameObject.activeInHierarchy, "«Сдать возвраты» is on the shift screen");
            key.Press();
            yield return null;
            Assert.IsTrue(refused == null, "the handed-in box is gone");
            Assert.AreEqual(1, _director.Stats.ReturnsHandedIn);
            Assert.Greater(_director.Stats.Score, before);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AReturnLeftOutsideTheBin_IsLostAtTheEndOfTheShift()
        {
            yield return ServeTheFirstCustomer();
            Assert.AreEqual(1, _director.ReturnBoxes.Count());
            _director.EndShiftNow();
            var deadline = Time.realtimeSinceStartup + 10f;
            while (!_director.IsShiftOver && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(1, _director.Stats.ReturnsLost);
            Assert.IsEmpty(_director.ReturnBoxes, "the lost box is gone with the shift");
            var summary = Object.FindFirstObjectByType<ShiftSummaryPanel>(FindObjectsInactive.Include);
            StringAssert.Contains("Не сдано возвратов: 1", summary.Text);
            StringAssert.Contains("Отказов от товара: 1", summary.Text);
        }
    }
}
