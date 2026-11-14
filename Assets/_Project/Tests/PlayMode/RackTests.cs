using System.Collections;
using System.Linq;
using NUnit.Framework;
using Pvz.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pvz.Runtime.Tests
{
    /// <summary>T037 (#81): the shelf rack holds boxes; boxes are solid to each other; the rack is moved like the counter.</summary>
    public class RackTests
    {
        private MemoryTextStore _placementStore;
        private ShiftDirector _director;
        private Transform _rack;

        [UnitySetUp]
        public IEnumerator LoadSceneAndDropThePile()
        {
            _placementStore = new MemoryTextStore();
            PlacementKeeper.StoreOverride = _placementStore;
            CareerKeeper.StoreOverride = (new MemoryScoreStore(), new MemoryTextStore());
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            _director = Object.FindFirstObjectByType<ShiftDirector>();
            _director.UseScoreStore(new MemoryScoreStore());
            _director.Begin();
            _director.UsePatience(0);
            var deadline = Time.realtimeSinceStartup + 20f;
            while (_director.Hatch.IsDropping && Time.realtimeSinceStartup < deadline) yield return null;
            _rack = GameObject.Find("Shelf Rack").transform;
        }

        [UnityTearDown]
        public IEnumerator Forget()
        {
            PlacementKeeper.StoreOverride = null;
            CareerKeeper.StoreOverride = null;
            yield return null;
        }

        private static void Put(QrBox box, Vector3 position, Quaternion rotation)
        {
            var body = box.GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            box.transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            Physics.SyncTransforms();
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator ABoxOnAShelf_StaysThere()
        {
            Assert.AreEqual(4, _rack.GetComponentsInChildren<Transform>().Count(t => t != _rack && t.name.StartsWith("Shelf ")), "four shelves");
            var box = _director.RemainingBoxes.First();
            var shelfTop = _rack.TransformPoint(new Vector3(0f, 0.95f, 0f)); // the third shelf
            Put(box, shelfTop + Vector3.up * 0.16f, _rack.rotation);
            yield return new WaitForSeconds(3f);

            var local = _rack.InverseTransformPoint(box.transform.position);
            Assert.AreEqual(0.95f + 0.13f, local.y, 0.04f, "the box lies on the shelf, not fallen through or off it");
            Assert.Less(Mathf.Abs(local.x), 0.5f);
            Assert.Less(Mathf.Abs(local.z), 0.25f, "it did not slide off the shelf");
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator ABoxDroppedOnAnother_RestsOnTopOfIt()
        {
            var boxes = _director.RemainingBoxes.Take(2).ToArray();
            var floor = _rack.TransformPoint(new Vector3(0f, 0.05f, 0f)); // the bottom shelf
            Put(boxes[0], floor + Vector3.up * 0.14f, _rack.rotation);
            yield return new WaitForSeconds(1f);
            Put(boxes[1], boxes[0].transform.position + Vector3.up * 0.45f, _rack.rotation);
            yield return new WaitForSeconds(2f);

            var gap = boxes[1].transform.position.y - boxes[0].transform.position.y;
            Assert.Greater(gap, 0.24f, "the boxes do not pass through each other");
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator TheRack_IsMovedByItsHandle_WithTheBoxesOnIt()
        {
            var box = _director.RemainingBoxes.First();
            Put(box, _rack.TransformPoint(new Vector3(0f, 0.5f + 0.14f, 0f)), _rack.rotation);
            yield return new WaitForSeconds(1.5f);
            var local = _rack.InverseTransformPoint(box.transform.position);

            var computer = Object.FindFirstObjectByType<PvzComputer>();
            computer.OpenSettings();
            computer.StartMovingRack();
            Assert.AreEqual(PvzComputer.Screen.Moving, computer.Current);
            StringAssert.Contains("стеллаж", computer.ScreenText);
            var mover = Object.FindObjectsByType<PlacementMover>(FindObjectsSortMode.None).First(m => m.Keeper.Rig == _rack);
            mover.Follow(mover.Handle.transform.position + new Vector3(0.3f, 0f, 0.8f), Vector3.forward);
            yield return null;
            Assert.Less(Vector3.Distance(local, _rack.InverseTransformPoint(box.transform.position)), 0.02f, "the box rode along");
            computer.ApplyMove();
            Assert.IsNotNull(_placementStore.Load(PlacementStore.RackKey), "the rack's place is saved under its own key");
        }
    }
}
