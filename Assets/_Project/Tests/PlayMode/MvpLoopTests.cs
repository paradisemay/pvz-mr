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
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            Object.FindFirstObjectByType<ShiftDirector>().UseRandomSeed(12345); // same order sequence every run
            Time.timeScale = TimeScale;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator FullShift_ServesEveryCustomer_AndShowsSummary()
        {
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            Assert.IsNotNull(director, "ShiftDirector missing in the Game scene");
            Assert.AreEqual(3, director.OrdersTotal);

            var wrongBoxTried = false;
            while (!director.IsShiftOver)
            {
                yield return WaitUntil(() => director.Desk.HasActiveOrder || director.IsShiftOver, 40f, "customer at the desk");
                if (director.IsShiftOver) break;

                var wanted = director.Desk.ActiveOrder.Boxes[0];
                Assert.IsTrue(director.TryGetBox(wanted, out var box), $"box {wanted} not found in the scene");

                director.Scanner.ScanDirect(box);
                StringAssert.Contains("НУЖНАЯ", director.Scanner.ScreenText);

                var other = director.RemainingBoxes.FirstOrDefault(b => b.Id != wanted);
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
                yield return PlaceInZone(director, box);
                Assert.AreEqual(completedBefore + 1, director.Stats.OrdersCompleted);
                Assert.IsTrue(box.IsHandedOver);
                Assert.IsFalse(director.Desk.HasActiveOrder);
            }

            Assert.IsTrue(wrongBoxTried);
            Assert.AreEqual(3, director.Stats.OrdersCompleted);
            Assert.Greater(director.Stats.Score, 0);
            Assert.AreEqual(0, director.Desk.Catalog.Count);

            var summary = Object.FindFirstObjectByType<ShiftSummaryPanel>(FindObjectsInactive.Include);
            Assert.IsTrue(summary.IsVisible, "shift summary should be visible");
            StringAssert.Contains("Обслужено клиентов: 3/3", summary.Text);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator BoxPlacedBeforeCustomerArrives_IsHandedOverOnArrival()
        {
            var director = Object.FindFirstObjectByType<ShiftDirector>();
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

        private static IEnumerator PlaceInZone(ShiftDirector director, QrBox box)
        {
            yield return MoveTo(box, director.Zone.transform.position + Vector3.up * 0.05f);
        }

        private static IEnumerator MoveTo(QrBox box, Vector3 position)
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
