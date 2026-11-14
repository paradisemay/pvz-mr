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
        private CounterMover _mover;
        private CounterPlacementKeeper _placement;
        private ShiftDirector _director;

        [UnitySetUp]
        public IEnumerator LoadScene() => Load(new MemoryTextStore());

        private IEnumerator Load(MemoryTextStore placementStore)
        {
            _placementStore = placementStore;
            WarehouseLayoutKeeper.StoreOverride = new MemoryTextStore();
            CounterPlacementKeeper.StoreOverride = _placementStore;
            CareerKeeper.StoreOverride = (new MemoryScoreStore(), new MemoryTextStore());
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            _computer = Object.FindFirstObjectByType<PvzComputer>();
            _mover = Object.FindFirstObjectByType<CounterMover>();
            _placement = Object.FindFirstObjectByType<CounterPlacementKeeper>();
            _director = Object.FindFirstObjectByType<ShiftDirector>();
            _director.UseScoreStore(new MemoryScoreStore());
        }

        [UnityTearDown]
        public IEnumerator Forget()
        {
            WarehouseLayoutKeeper.StoreOverride = null;
            CounterPlacementKeeper.StoreOverride = null;
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
            Assert.AreEqual(PvzComputer.Screen.MovingCounter, _computer.Current);
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
            new CounterPlacementStore(store).Save(new CounterPlacement(0.8f, 2.1f, 200f));
            yield return Load(store);
            yield return null;

            var rig = _placement.Rig;
            Assert.AreEqual(0.8f, rig.position.x, 1e-3);
            Assert.AreEqual(2.1f, rig.position.z, 1e-3);
            Assert.AreEqual(200f, rig.eulerAngles.y, 0.1f);
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
