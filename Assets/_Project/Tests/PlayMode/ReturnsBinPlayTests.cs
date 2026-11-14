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
            _director.UseLostChance(0); // every box of the stock is in the room (T047)
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
            // The board keeps saying so until the next customer comes (the customer's line lasts only their thanks).
            StringAssert.Contains("отказом", Object.FindFirstObjectByType<CounterBoard>().Headline);
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

        /// <summary>
        /// Everything sold, but the refused boxes still lie on the counter: the shift waits for them instead of losing
        /// the last ones at once; once they are in the bin it ends and hands them in.
        /// </summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator ASoldOutShift_WaitsForTheReturns_ThenHandsInTheBin()
        {
            var deadline = Time.realtimeSinceStartup + 150f;
            while (!_director.WaitingForReturns && !_director.IsShiftOver && Time.realtimeSinceStartup < deadline)
            {
                // The supply delivery: its orders are for sale once its boxes are received.
                foreach (var supply in _director.RemainingBoxes.Where(b => b != null && _director.Shift.IsPendingIntake(b.Id)).ToArray())
                    _director.Scanner.ScanDirect(supply);
                if (_director.Desk.HasActiveOrder && _director.CurrentCustomer != null && !_director.CurrentCustomer.IsWalking)
                {
                    foreach (var id in _director.Desk.ActiveOrder.Boxes.ToArray())
                        if (!_director.Desk.ActiveSession.IsDelivered(id) && _director.TryGetBox(id, out var box))
                            Put(box, _director.Zone.transform.position + Vector3.up * 0.05f);
                }
                yield return null;
            }
            Assert.IsTrue(_director.WaitingForReturns, "every order sold; the shift waits for the returns");
            Assert.IsFalse(_director.IsShiftOver);
            var returns = _director.ReturnBoxes.ToArray();
            Assert.AreEqual(_director.OrdersTotal, returns.Length, "everyone refused one box");
            StringAssert.Contains("ящик", Object.FindFirstObjectByType<CounterBoard>().Headline);

            // As a player would: a couple of boxes into the bin, «Сдать возвраты», the next couple — the last ones are
            // handed in by the end of the shift itself.
            for (var i = 0; i < returns.Length; i++)
            {
                Put(returns[i], _director.ReturnsBin.transform.TransformPoint(new Vector3((i % 2 - 0.5f) * 0.3f, 0.35f, 0f)));
                yield return new WaitForSeconds(0.6f);
                if (i % 2 == 1 && i < returns.Length - 2) Assert.AreEqual(2, _director.HandInReturns(), "two boxes handed in");
            }
            deadline = Time.realtimeSinceStartup + 20f;
            while (!_director.IsShiftOver && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(_director.IsShiftOver, "all returns in the bin: the shift ends");
            Assert.AreEqual(0, _director.Stats.ReturnsLost);
            Assert.AreEqual(returns.Length, _director.Stats.ReturnsHandedIn);
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
