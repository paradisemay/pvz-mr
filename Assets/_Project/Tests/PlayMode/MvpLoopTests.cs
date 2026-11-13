using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pvz.Runtime.Tests
{
    /// <summary>
    /// End-to-end check of the MVP loop in the real Game scene (no headset): customers arrive, the scanner judges
    /// boxes, a wrong box is rejected, the right box placed in the handover zone completes the order,
    /// and after the last box the shift summary is shown.
    /// </summary>
    public class MvpLoopTests
    {
        private const string SceneName = "Game";
        private const float TimeScale = 4f;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            WarehouseLayoutKeeper.StoreOverride = new Pvz.Core.MemoryTextStore(); // never the real saved layout
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            director.UseScoreStore(new Pvz.Core.MemoryScoreStore()); // never touch the real PlayerPrefs
            director.Begin();                // default difficulty, skipping the menu
            director.UseRandomSeed(12345);   // same order sequence every run
            director.UsePatience(0);         // customers wait as long as the test needs
            director.UseReturnChance(0);     // only pickups unless a test asks for returns
            director.UseEventChances(0, 0);  // no urgent or forgetful customers unless a test asks for them
            director.UseTimeLimit(0);        // the shift lasts as long as the test needs
            Time.timeScale = TimeScale;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.timeScale = 1f;
            WarehouseLayoutKeeper.StoreOverride = null;
            yield return null;
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator FullShift_ServesEveryCustomer_AndShowsSummary()
        {
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            Assert.IsNotNull(director, "ShiftDirector missing in the Game scene");
            Assert.AreEqual(3, director.OrdersTotal);
            director.UseBagChance(100); // every customer also asks for a bag
            var dispenser = Object.FindFirstObjectByType<BagDispenser>();

            var wrongBoxTried = false;
            var received = 0;
            var multiBoxOrders = 0;
            bool HasUnreceivedBoxes() => director.RemainingBoxes.Any(b => director.Shift.IsPendingIntake(b.Id));

            while (!director.IsShiftOver)
            {
                yield return WaitUntil(() => director.Desk.HasActiveOrder || director.IsShiftOver || HasUnreceivedBoxes(),
                    60f, "customer at the desk or a supply delivery");
                if (director.IsShiftOver) break;

                // Supply delivery: boxes fell from the ceiling; accept each one with the scanner.
                foreach (var supplyBox in director.RemainingBoxes.Where(b => director.Shift.IsPendingIntake(b.Id)).ToArray())
                {
                    director.Scanner.ScanDirect(supplyBox);
                    StringAssert.Contains("ПРИНЯТО", director.Scanner.ScreenText);
                    received++;
                }
                if (!director.Desk.HasActiveOrder) continue;

                var order = director.Desk.ActiveOrder;
                if (order.Boxes.Count > 1) multiBoxOrders++;

                var other = director.RemainingBoxes.FirstOrDefault(b => !order.Contains(b.Id) && !director.Shift.IsPendingIntake(b.Id));
                if (!wrongBoxTried && other != null)
                {
                    director.Scanner.ScanDirect(other);
                    StringAssert.Contains("НЕ ТОТ", director.Scanner.ScreenText);

                    var home = other.transform.position;
                    yield return PlaceInZone(director, other);
                    Assert.AreEqual(1, director.Stats.WrongHandovers);
                    Assert.IsTrue(director.Desk.HasActiveOrder, "a wrong box must not close the order");
                    StringAssert.Contains("не мой заказ", director.CurrentCustomer.LastLine);
                    yield return MoveTo(other, home);
                    wrongBoxTried = true;
                }

                var completedBefore = director.Stats.OrdersCompleted;
                foreach (var wanted in order.Boxes)
                {
                    Assert.IsTrue(director.TryGetBox(wanted, out var box), $"box {wanted} not found in the scene");
                    director.Scanner.ScanDirect(box);
                    StringAssert.Contains("НУЖНАЯ", director.Scanner.ScreenText);
                    yield return PlaceInZone(director, box);
                    Assert.IsTrue(box.IsHandedOver);
                }

                Assert.IsTrue(director.Desk.HasActiveOrder, "the customer still waits for a bag");
                Assert.IsTrue(director.Desk.ActiveSession.AwaitingBag);
                StringAssert.Contains("пакет", director.CurrentCustomer.LastLine);
                var bag = dispenser.Current;
                Assert.IsNotNull(bag, "the dispenser always offers a bag");
                yield return MoveTo(bag, director.Zone.transform.position + Vector3.up * 0.05f);
                Assert.IsTrue(bag.IsHandedOver);

                Assert.AreEqual(completedBefore + 1, director.Stats.OrdersCompleted);
                Assert.IsFalse(director.Desk.HasActiveOrder);
                yield return null; // let the dispenser refill
            }

            Assert.IsTrue(wrongBoxTried);
            Assert.AreEqual(3, received, "the supply delivery has 3 boxes");
            Assert.AreEqual(1, multiBoxOrders, "BOX-101 and BOX-102 form one order");
            Assert.AreEqual(5, director.OrdersTotal);
            Assert.AreEqual(5, director.Stats.OrdersCompleted);
            Assert.AreEqual(5, director.Stats.BagsGiven);
            Assert.AreEqual(3, director.Stats.BoxesReceived);
            Assert.Greater(director.Stats.Score, 0);
            Assert.AreEqual(0, director.Desk.Catalog.Count);

            var summary = Object.FindFirstObjectByType<ShiftSummaryPanel>(FindObjectsInactive.Include);
            Assert.IsTrue(summary.IsVisible, "shift summary should be visible");
            StringAssert.Contains("Обслужено клиентов: 5/5", summary.Text);
            StringAssert.Contains("Принято из поставки: 3", summary.Text);
            StringAssert.Contains("Выдано пакетов: 5", summary.Text);
            StringAssert.Contains("Обработано товаров: 6", summary.Text, "3 + 3 boxes, the two-box order counts twice");
            StringAssert.Contains("Макс. очередь: 2", summary.Text);
            StringAssert.Contains("НОВЫЙ РЕКОРД", summary.Text, "the first shift on an empty store is a record");
            Assert.AreEqual(director.Stats.Score, director.BestScores.Get(director.ActiveSettings.DifficultyId));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator BoxPlacedBeforeCustomerArrives_IsHandedOverOnArrival()
        {
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            director.UseBagChance(0); // only boxes: the first order completes as soon as the customer arrives
            Assert.IsFalse(director.Desk.HasActiveOrder);

            // Put every box on the counter before anyone arrives: the first customer should take theirs at once.
            var boxes = director.RemainingBoxes.ToArray();
            var zone = director.Zone.transform.position;
            for (var i = 0; i < boxes.Length; i++)
            {
                boxes[i].GetComponent<Rigidbody>().isKinematic = true; // keep them from toppling off the counter
                yield return MoveTo(boxes[i], zone + new Vector3((i - 1) * 0.1f, 0f, 0f));
            }

            yield return WaitUntil(() => director.Stats.OrdersCompleted >= 1, 40f, "first order completed automatically");
            Assert.AreEqual(0, director.Stats.WrongHandovers, "boxes waiting in the zone must not count as mistakes");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Queue_TwoCustomersWaitInLine_WithDifferentOrders()
        {
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            Assert.AreEqual(2, director.ActiveSettings.QueueLength, "the default (normal) difficulty has a line of two");

            yield return WaitUntil(() => director.Desk.HasActiveOrder, 40f, "first customer at the counter");
            Assert.AreEqual(2, director.QueuedOrders.Count);
            Assert.AreNotEqual(director.QueuedOrders[0].Number, director.QueuedOrders[1].Number);
            Assert.AreEqual(2, Object.FindObjectsByType<Customer>(FindObjectsSortMode.None).Length);

            var front = director.CurrentCustomer;
            var second = Object.FindObjectsByType<Customer>(FindObjectsSortMode.None).First(c => c != front);
            Assert.Greater(Vector3.Distance(second.transform.position, director.Zone.transform.position),
                Vector3.Distance(front.transform.position, director.Zone.transform.position), "the second customer waits behind");

            // Serve the first customer: the second one steps up to the counter.
            var secondOrder = director.QueuedOrders[1];
            foreach (var id in director.Desk.ActiveOrder.Boxes)
            {
                Assert.IsTrue(director.TryGetBox(id, out var box));
                yield return MoveTo(box, director.Zone.transform.position + Vector3.up * 0.05f);
            }
            if (director.Desk.HasActiveOrder && director.Desk.ActiveSession.AwaitingBag)
                yield return MoveTo(Object.FindFirstObjectByType<BagDispenser>().Current, director.Zone.transform.position);

            yield return WaitUntil(() => director.Desk.HasActiveOrder && director.Desk.ActiveOrder == secondOrder, 40f,
                "second customer at the counter");
            Assert.AreSame(second, director.CurrentCustomer);
            yield return WaitUntil(() => front == null, 20f, "the served customer leaves and vanishes");
            Assert.AreEqual(2, director.QueuedOrders.Count, "a new customer joined the end of the line");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ReturningCustomer_PutsTheBoxOnTheCounter_AndScanningAcceptsIt()
        {
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            director.UseBagChance(0);
            director.UseReturnChance(100); // from now on every new customer in line comes to return a sold box

            // Serve pickups until a returning customer reaches the counter.
            var deadline = Time.realtimeSinceStartup + 90f;
            while (!director.Desk.HasActiveReturn && Time.realtimeSinceStartup < deadline)
            {
                if (director.Desk.HasActiveOrder)
                {
                    foreach (var id in director.Desk.ActiveSession.Order.Boxes)
                    {
                        if (director.Desk.ActiveSession.IsDelivered(id)) continue;
                        Assert.IsTrue(director.TryGetBox(id, out var box));
                        yield return MoveTo(box, director.Zone.transform.position + Vector3.up * 0.05f);
                    }
                }
                yield return null;
            }
            Assert.IsTrue(director.Desk.HasActiveReturn, "a returning customer came to the counter");

            var request = director.Desk.ActiveReturn;
            StringAssert.Contains("вернуть", director.CurrentCustomer.LastLine);
            var returned = director.CurrentReturnBox;
            Assert.IsNotNull(returned, "the customer put their box on the counter");
            Assert.AreEqual(request.Box, returned.Id);
            Assert.Less(Vector3.Distance(returned.transform.position, director.Zone.ReturnSpot.position), 0.01f,
                "the box lies next to the handover zone");
            Assert.IsFalse(director.Zone.BoxesInside.Contains(returned), "the returned box is not stock to hand over");

            director.Scanner.ScanDirect(returned);
            StringAssert.Contains("ВОЗВРАТ ПРИНЯТ", director.Scanner.ScreenText);
            Assert.AreEqual(1, director.Stats.ReturnsAccepted);
            Assert.IsFalse(director.Desk.HasCustomer);
            yield return WaitUntil(() => returned == null, 10f, "the returned box is taken back");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ForgetfulCustomer_GivesOnlyTheEndOfTheNumber_AndIsServedAfterScanning()
        {
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            var board = Object.FindFirstObjectByType<CounterBoard>();
            director.UseBagChance(0);
            director.UseEventChances(0, 100); // every customer forgets the number (where the end is unambiguous)
            yield return WaitUntil(() => director.Desk.HasActiveOrder, 40f, "first customer at the counter");

            var order = director.Desk.ActiveOrder;
            Assert.IsTrue(order.ForgotNumber);
            StringAssert.Contains("Забыл номер", director.CurrentCustomer.LastLine);
            StringAssert.Contains(order.NumberHint, director.CurrentCustomer.LastLine);
            StringAssert.DoesNotContain(order.Number, director.CurrentCustomer.LastLine, "the customer does not know the full number");
            StringAssert.Contains("••" + order.NumberHint, board.Headline);
            StringAssert.DoesNotContain(order.Number, board.Headline);

            // A wrong box: the customer objects, still knowing only the end of the number.
            var other = director.RemainingBoxes.First(b => !order.Contains(b.Id));
            var home = other.transform.position;
            yield return PlaceInZone(director, other);
            StringAssert.Contains("не мой заказ", director.CurrentCustomer.LastLine);
            StringAssert.Contains("••" + order.NumberHint, director.CurrentCustomer.LastLine);
            StringAssert.DoesNotContain(order.Number, director.CurrentCustomer.LastLine);
            yield return MoveTo(other, home);

            foreach (var id in order.Boxes)
            {
                Assert.IsTrue(director.TryGetBox(id, out var box));
                director.Scanner.ScanDirect(box);
                StringAssert.Contains("НУЖНАЯ КОРОБКА", director.Scanner.ScreenText);
                StringAssert.Contains(order.Number, director.Scanner.ScreenText, "the scanner tells the full number");
                yield return MoveTo(box, director.Zone.transform.position + Vector3.up * 0.05f);
            }
            yield return WaitUntil(() => director.Stats.OrdersCompleted == 1, 10f, "order handed over");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator WhenTimeRunsOut_TheLineGoesHome_AndTheShiftEndsAfterTheLastCustomer()
        {
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            var board = Object.FindFirstObjectByType<CounterBoard>();
            director.UseBagChance(0);
            yield return WaitUntil(() => director.Desk.HasActiveOrder && director.QueuedVisits.Count == 2, 40f,
                "a customer at the counter and one waiting in line");
            var waiting = director.QueuedVisits[1];

            // The time runs out half a second from now.
            director.UseTimeLimit(Time.timeAsDouble - director.Shift.StartedAt.Value + 0.5);
            yield return WaitUntil(() => director.Shift.IsTimeUp, 5f, "time up");
            StringAssert.Contains("Время смены вышло", board.Headline);
            Assert.AreEqual(1, director.QueuedVisits.Count, "only the customer at the counter stays");
            CollectionAssert.DoesNotContain(director.QueuedVisits, waiting);

            var order = director.Desk.ActiveOrder;
            foreach (var id in order.Boxes)
            {
                Assert.IsTrue(director.TryGetBox(id, out var box));
                yield return MoveTo(box, director.Zone.transform.position + Vector3.up * 0.05f);
            }
            yield return WaitUntil(() => director.IsShiftOver, 20f, "the shift ends after the last customer");
            Assert.IsTrue(director.Shift.HasOrdersLeft, "unserved orders stayed in the warehouse");
            var summary = Object.FindFirstObjectByType<ShiftSummaryPanel>(FindObjectsInactive.Include);
            StringAssert.Contains("время вышло", summary.Text);
            StringAssert.Contains("Обслужено клиентов: 1/", summary.Text);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator WhenTimeRunsOut_WhileTheFirstCustomerWalksIn_TheShiftEndsAtOnce()
        {
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            yield return WaitUntil(() => director.CurrentCustomer != null && director.CurrentCustomer.IsWalking && !director.Desk.HasCustomer,
                20f, "the first customer walks to the counter");

            director.UseTimeLimit(Time.timeAsDouble - director.Shift.StartedAt.Value); // the time is up right now
            yield return WaitUntil(() => director.IsShiftOver, 10f, "the shift ends");
            Assert.IsTrue(director.Shift.IsTimeUp);
            Assert.IsFalse(director.Desk.HasCustomer, "the customer never reached the counter");
            Assert.AreEqual(0, director.QueuedVisits.Count);
            var summary = Object.FindFirstObjectByType<ShiftSummaryPanel>(FindObjectsInactive.Include);
            StringAssert.Contains("время вышло", summary.Text);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator PatienceBar_ShrinksWhileTheCustomerWaits_AndHidesWithoutPatience()
        {
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            director.UsePatience(30);
            yield return WaitUntil(() => director.Desk.HasActiveOrder, 40f, "first customer at the counter");
            yield return null;

            var bar = director.CurrentCustomer.PatienceBar;
            Assert.IsTrue(bar.IsVisible, "patience is shown at the counter");
            var first = bar.Value;
            yield return new WaitForSeconds(2f);
            Assert.Less(bar.Value, first, "the bar shrinks over time");

            director.UsePatience(0);
            yield return null;
            Assert.IsFalse(bar.IsVisible, "no bar when customers wait forever");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ImpatientCustomer_HurriesThenLeaves_AndTheOrderStaysInStock()
        {
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            director.UsePatience(4);
            yield return WaitUntil(() => director.Desk.HasActiveOrder, 40f, "first customer");
            var order = director.Desk.ActiveOrder;
            var customer = director.CurrentCustomer;

            yield return WaitUntil(() => customer.LastLine.Contains("Побыстрее"), 20f, "customer hurries");
            yield return WaitUntil(() => director.Stats.OrdersAbandoned == 1, 20f, "customer leaves");

            StringAssert.Contains("ухожу", customer.LastLine);
            Assert.IsFalse(director.Desk.HasActiveOrder);
            Assert.IsTrue(director.Desk.Catalog.Contains(order.Boxes[0]), "the order's boxes stay for the next customer");
            Assert.AreEqual(0, director.Stats.OrdersCompleted);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator BagLeftInZoneTooEarly_IsTakenOnceTheCustomerAsks()
        {
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            director.UseBagChance(100);
            var dispenser = Object.FindFirstObjectByType<BagDispenser>();
            yield return WaitUntil(() => director.Desk.HasActiveOrder, 40f, "first customer");

            var bag = dispenser.Current;
            bag.GetComponent<Rigidbody>().isKinematic = true; // keep it inside the zone
            yield return MoveTo(bag, director.Zone.transform.position + new Vector3(-0.15f, 0f, 0f));
            Assert.IsFalse(bag.IsHandedOver, "a bag before the boxes is too early");
            StringAssert.Contains("коробки", director.CurrentCustomer.LastLine);

            var order = director.Desk.ActiveOrder;
            foreach (var id in order.Boxes)
            {
                Assert.IsTrue(director.TryGetBox(id, out var box));
                yield return MoveTo(box, director.Zone.transform.position + new Vector3(0.15f, 0.05f, 0f));
            }

            Assert.IsTrue(bag.IsHandedOver, "the waiting bag is handed over as soon as the customer asks for it");
            Assert.AreEqual(1, director.Stats.OrdersCompleted);
            Assert.AreEqual(1, director.Stats.BagsGiven);
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator BagDispenser_RefillsOnlyWhenTheSpotIsFree_AndCapsBags()
        {
            var dispenser = Object.FindFirstObjectByType<BagDispenser>();
            yield return null;
            var first = dispenser.Current;
            Assert.IsNotNull(first);

            // Hold the bag right above its spot: no new bag may spawn inside it.
            first.GetComponent<Rigidbody>().isKinematic = true;
            yield return MoveTo(first, dispenser.transform.position + Vector3.up * 0.27f); // lifted, but still over the spot
            for (var i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<ParcelBag>(FindObjectsSortMode.None).Length);

            // Take bags away repeatedly: the dispenser never keeps more than 3 around.
            for (var i = 0; i < 6; i++)
            {
                var bag = dispenser.Current;
                bag.GetComponent<Rigidbody>().isKinematic = true;
                yield return MoveTo(bag, new Vector3(-2f + i * 0.4f, 0.2f, -2f));
                for (var f = 0; f < 3; f++) yield return null;
            }
            Assert.LessOrEqual(Object.FindObjectsByType<ParcelBag>(FindObjectsSortMode.None).Length, 3);
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator ScannerRaycast_ReadsTheBoxInFront_AndIgnoresItself()
        {
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            var box = director.RemainingBoxes.First();
            Assert.IsTrue(director.Desk.Catalog.TryGet(box.Id, out var record));

            var scanner = director.Scanner;
            scanner.GetComponent<Rigidbody>().isKinematic = true;
            // Hold the scanner 0.8 m in front of the box, nose pointing at it.
            var target = box.transform.position;
            var from = target + new Vector3(0f, 0.3f, -0.8f);
            scanner.transform.SetPositionAndRotation(from, Quaternion.LookRotation(target - from));
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            scanner.Fire();
            StringAssert.Contains(record.OrderNumber, scanner.ScreenText);

            // Pointing at the empty ceiling: a miss, not a hit on the scanner's own colliders.
            scanner.transform.rotation = Quaternion.LookRotation(Vector3.up);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            scanner.Fire();
            StringAssert.Contains("Нет кода", scanner.ScreenText);
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator LostBox_ReturnsToItsStartPlace()
        {
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            var box = director.RemainingBoxes.First();
            var start = box.transform.position;

            yield return MoveTo(box, new Vector3(0f, -5f, 0f)); // fell through the floor
            yield return null;

            Assert.Less(Vector3.Distance(start, box.transform.position), 0.3f);
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator SupplyDrop_PlacesBoxesWhereThePlayerCanReachThem()
        {
            var drop = Object.FindFirstObjectByType<SupplyDrop>();
            var counter = Object.FindFirstObjectByType<HandoverZone>().transform.parent;
            yield return null;

            for (var seed = 1; seed <= 5; seed++)
            {
                var ids = Enumerable.Range(0, 5).Select(i => $"BOX-T{seed}{i}").ToArray();
                var boxes = drop.Drop(ids, seed, 2.4f);
                foreach (var box in boxes)
                {
                    var p = box.transform.position;
                    Assert.Less(Mathf.Abs(p.x), 3.3f, $"{box.RawId} outside the mock room");
                    Assert.Less(Mathf.Abs(p.z), 3.3f, $"{box.RawId} outside the mock room");
                    var toCounter = new Vector2(p.x - counter.position.x, p.z - counter.position.z).magnitude;
                    Assert.Greater(toCounter, 1.0f, $"{box.RawId} dropped onto the counter");
                    Assert.IsFalse(Mathf.Abs(p.x) < 0.6f && p.z > 1.0f, $"{box.RawId} dropped onto the customers' walkway");
                    Assert.Greater(p.y, 1.5f, "boxes start under the ceiling");
                }
                foreach (var box in boxes) Object.Destroy(box.gameObject);
                yield return null;
            }
        }

        private static IEnumerator PlaceInZone(ShiftDirector director, QrBox box)
        {
            yield return MoveTo(box, director.Zone.transform.position + Vector3.up * 0.05f);
        }

        private static IEnumerator MoveTo(Component box, Vector3 position)
        {
            var body = box.GetComponent<Rigidbody>();
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            box.transform.position = position;
            body.position = position;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string what)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timed out waiting for: {what}");
                yield return null;
            }
        }
    }
}