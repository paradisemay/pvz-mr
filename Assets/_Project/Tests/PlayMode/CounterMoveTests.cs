using System.Collections;
using System.Linq;
using NUnit.Framework;
using Pvz.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pvz.Runtime.Tests
{
    /// <summary>T034 (#78): the counter is moved by its handle at any time, and its place in the room is kept.</summary>
    public class CounterMoveTests
    {
        private MemoryTextStore _placementStore;
        private PvzComputer _computer;
        private PlacementMover _mover;
        private PlacementKeeper _placement;
        private ShiftDirector _director;

        [UnitySetUp]
        public IEnumerator LoadScene() => Load(new MemoryTextStore());

        private IEnumerator Load(MemoryTextStore placementStore)
        {
            _placementStore = placementStore;
            PlacementKeeper.StoreOverride = _placementStore;
            CareerKeeper.StoreOverride = (new MemoryScoreStore(), new MemoryTextStore());
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            _computer = Object.FindFirstObjectByType<PvzComputer>();
            // The counter's (the supply hatch is moved the same way, T036).
            _placement = Object.FindObjectsByType<PlacementKeeper>(FindObjectsSortMode.None).First(k => k.Rig != null && k.Rig.name == "Counter Rig");
            _mover = Object.FindObjectsByType<PlacementMover>(FindObjectsSortMode.None).First(m => m.Keeper == _placement);
            _director = Object.FindFirstObjectByType<ShiftDirector>();
            _director.UseScoreStore(new MemoryScoreStore());
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

        private IEnumerator StartMoving()
        {
            Key(ComputerKey.Action.OpenSettings).Press();
            yield return null;
            Key(ComputerKey.Action.MoveCounter).Press();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Handle_CarriesTheCounterOnTheFloor_AndApplyKeepsThePlace()
        {
            var rig = _placement.Rig;
            var height = rig.position.y;
            var counterToZone = rig.InverseTransformPoint(_director.Zone.transform.position);
            yield return StartMoving();
            Assert.AreEqual(PvzComputer.Screen.Moving, _computer.Current);
            Assert.IsTrue(_mover.Handle.gameObject.activeInHierarchy, "the handle appears");

            // The player carries the handle 1.5 m to the right and turns it to face right (tilting it a little).
            var target = _mover.Handle.transform.position + new Vector3(1.5f, 0.3f, 0.4f);
            _mover.Follow(target, new Vector3(1f, -0.3f, 0f));
            yield return null;

            Assert.AreEqual(height, rig.position.y, 1e-4, "the counter stays on the floor");
            Assert.AreEqual(90f, rig.eulerAngles.y, 0.5f, "turned around the vertical only");
            Assert.AreEqual(0f, Vector3.Angle(rig.up, Vector3.up), 0.01f);
            Assert.AreEqual(counterToZone.x, rig.InverseTransformPoint(_director.Zone.transform.position).x, 1e-3, "the zone moves with the counter");
            Assert.IsFalse(_placement.HasSaved, "nothing is saved before «Применить»");

            Key(ComputerKey.Action.ApplyMove).Press();
            yield return null;
            Assert.AreEqual(PvzComputer.Screen.Settings, _computer.Current);
            Assert.IsFalse(_mover.Handle.gameObject.activeInHierarchy, "the handle is gone");
            Assert.IsTrue(_placement.HasSaved);
            Assert.AreEqual(_placement.Current, _placement.Saved.Value);
        }

        [UnityTest]
        public IEnumerator Cancel_PutsTheCounterBack()
        {
            var rig = _placement.Rig;
            var before = new Pose(rig.position, rig.rotation);
            yield return StartMoving();
            _mover.Follow(_mover.Handle.transform.position + new Vector3(-1f, 0f, 0.5f), Vector3.left);
            yield return null;
            Assert.Greater(Vector3.Distance(before.position, rig.position), 0.5f);

            Key(ComputerKey.Action.CancelMove).Press();
            yield return null;
            Assert.Less(Vector3.Distance(before.position, rig.position), 1e-3);
            Assert.Less(Quaternion.Angle(before.rotation, rig.rotation), 0.01f);
            Assert.IsFalse(_placement.HasSaved);
        }

        [UnityTest]
        public IEnumerator DuringTheShift_TheLineWalksToTheMovedCounter()
        {
            _director.Begin();
            var deadline = Time.realtimeSinceStartup + 30f;
            while (!_director.Desk.HasCustomer && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(_director.Desk.HasCustomer, "a customer is at the counter");
            var customer = _director.CurrentCustomer;

            yield return StartMoving();
            _mover.Follow(_mover.Handle.transform.position + new Vector3(1.2f, 0f, 0f), _placement.Rig.forward);
            Key(ComputerKey.Action.ApplyMove).Press();
            Assert.AreEqual(PvzComputer.Screen.Settings, _computer.Current);

            var service = GameObject.Find("CustomerService").transform;
            deadline = Time.realtimeSinceStartup + 10f;
            while (Vector3.Distance(Flat(customer.transform.position), Flat(service.position)) > 0.05f && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.Less(Vector3.Distance(Flat(customer.transform.position), Flat(service.position)), 0.05f,
                "the customer walked to the counter's new place");
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        [UnityTest]
        public IEnumerator SavedPlace_IsRestored_WhenTheGameStartsAgain()
        {
            var store = new MemoryTextStore();
            new PlacementStore(store).Save(new Placement(0.8f, 2.1f, 200f));
            yield return Load(store);
            yield return null;

            var rig = _placement.Rig;
            Assert.AreEqual(0.8f, rig.position.x, 1e-3);
            Assert.AreEqual(2.1f, rig.position.z, 1e-3);
            Assert.AreEqual(200f, rig.eulerAngles.y, 0.1f);
        }

        [UnityTest]
        public IEnumerator APlaceFarAway_IsNotRestored_TheRoomChanged()
        {
            var store = new MemoryTextStore();
            new PlacementStore(store).Save(new Placement(20f, -14f, 0f));
            yield return Load(store);
            yield return null;
            Assert.Less(_placement.Rig.position.magnitude, 0.01f, "the counter stays in front of the player, within reach");
        }

        [UnityTest]
        public IEnumerator ABoxOnTheCounter_RidesAlong()
        {
            var rig = _placement.Rig;
            _director.Begin(); // boxes come out of the supply hatch with the shift (T036)
            var pileDeadline = Time.realtimeSinceStartup + 20f;
            while (_director.Hatch.IsDropping && Time.realtimeSinceStartup < pileDeadline) yield return null;
            var box = _director.RemainingBoxes.First();
            var body = box.GetComponent<Rigidbody>();
            // Put a box down on the counter top and let it settle, as a player would.
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = rig.TransformPoint(new Vector3(0.38f, 1.25f, 1.02f)); // on the handover zone, clear of the keys
            body.rotation = rig.rotation;
            yield return new WaitForSeconds(1.5f);
            var local = rig.InverseTransformPoint(box.transform.position);
            Assert.Greater(local.y, 1.0f, "the box lies on the counter top");

            yield return StartMoving();
            _mover.Follow(_mover.Handle.transform.position + new Vector3(1f, 0f, 0.5f), Vector3.right);
            yield return null;
            Assert.Less(Vector3.Distance(local, rig.InverseTransformPoint(box.transform.position)), 0.02f, "the box moved with the counter");
            Key(ComputerKey.Action.ApplyMove).Press();
            yield return new WaitForSeconds(1f);
            Assert.Greater(rig.InverseTransformPoint(box.transform.position).y, 1.0f, "and still lies on it");
        }

        /// <summary>T036 (#80): «Место люка» moves the supply hatch the same way; the pile falls where it is now.</summary>
        [UnityTest, Timeout(60000)]
        public IEnumerator Hatch_IsMovedByItsHandle_AndThePileFallsThere()
        {
            var hatchKeeper = Object.FindObjectsByType<PlacementKeeper>(FindObjectsSortMode.None).First(k => k.Rig != null && k.Rig.GetComponent<SupplyHatch>() != null);
            var hatchMover = Object.FindObjectsByType<PlacementMover>(FindObjectsSortMode.None).First(m => m.Keeper == hatchKeeper);
            var height = hatchKeeper.Rig.position.y;

            Key(ComputerKey.Action.OpenSettings).Press();
            yield return null;
            Key(ComputerKey.Action.MoveHatch).Press();
            yield return null;
            Assert.AreEqual(PvzComputer.Screen.Moving, _computer.Current);
            StringAssert.Contains("люк", _computer.ScreenText);
            Assert.IsTrue(hatchMover.Handle.gameObject.activeInHierarchy);
            hatchMover.Follow(hatchMover.Handle.transform.position + new Vector3(0.6f, 0f, -0.4f), hatchKeeper.Rig.forward);
            // Carried far away: it stops where its boxes still land within reach.
            var centre = hatchKeeper.Rig.parent.position;
            hatchMover.Follow(hatchMover.Handle.transform.position + new Vector3(-30f, 0f, 0f), hatchKeeper.Rig.forward);
            Assert.LessOrEqual(new Vector2(hatchKeeper.Rig.position.x - centre.x, hatchKeeper.Rig.position.z - centre.z).magnitude,
                Pvz.Core.WarehouseBounds.MaxReach - 1f + 1e-3f);
            hatchMover.Follow(hatchMover.Handle.transform.position + new Vector3(0.6f, 0f, -0.4f), hatchKeeper.Rig.forward); // back to the chosen place
            Key(ComputerKey.Action.ApplyMove).Press();
            yield return null;
            Assert.AreEqual(height, hatchKeeper.Rig.position.y, 1e-4, "the hatch stays at its height");
            Assert.IsTrue(_placementStore.Load(PlacementStore.HatchKey) != null, "the hatch's place is saved under its own key");
            Assert.IsFalse(_placement.HasSaved, "the counter's place is untouched");

            _director.Begin();
            var deadline = Time.realtimeSinceStartup + 20f;
            while (_director.Hatch.IsDropping && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSeconds(1f);
            var under = _director.Hatch.DropPoint;
            foreach (var box in _director.RemainingBoxes)
                Assert.Less(new Vector2(box.transform.position.x - under.x, box.transform.position.z - under.z).magnitude, 1.6f,
                    $"{box.RawId} fell under the moved hatch");
        }

        [UnityTest]
        public IEnumerator Recenter_PutsTheCounterInFrontOfThePlayer_AndSavesIt()
        {
            yield return StartMoving();
            _mover.Follow(_mover.Handle.transform.position + new Vector3(2f, 0f, 0f), Vector3.left);
            Key(ComputerKey.Action.ApplyMove).Press();
            yield return null;

            Object.FindFirstObjectByType<RecenterButton>(FindObjectsInactive.Include).Press();
            yield return null;
            Assert.Less(Vector3.Distance(_placement.Rig.localPosition, Vector3.zero), 1e-3, "back in its place in the play area");
            Assert.AreEqual(_placement.Current, _placement.Saved.Value);
        }
    }
}
