using System.Collections;
using System.Linq;
using NUnit.Framework;
using Pvz.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pvz.Runtime.Tests
{
    /// <summary>T017: the player's arrangement of the warehouse survives a new session (no shift is started here).</summary>
    public class WarehouseLayoutPlayTests
    {
        private MemoryTextStore _store;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _store = new MemoryTextStore();
            yield return Load();
        }

        [UnityTearDown]
        public IEnumerator ForgetTestStore()
        {
            WarehouseLayoutKeeper.StoreOverride = null;
            CounterPlacementKeeper.StoreOverride = null;
            CareerKeeper.StoreOverride = null;
            yield return null;
        }

        /// <summary>A new session: the same saved data, a freshly loaded scene.</summary>
        private IEnumerator Load()
        {
            WarehouseLayoutKeeper.StoreOverride = _store;
            CounterPlacementKeeper.StoreOverride = new Pvz.Core.MemoryTextStore();
            CareerKeeper.StoreOverride = (new Pvz.Core.MemoryScoreStore(), new Pvz.Core.MemoryTextStore());
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; // Start: the keeper restores the layout
        }

        private static QrBox Box(string id) =>
            Object.FindObjectsByType<QrBox>(FindObjectsSortMode.None).First(b => b.Id == new BoxId(id));

        private static void PutDown(QrBox box, Vector3 worldPosition)
        {
            box.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
            var body = box.GetComponent<Rigidbody>();
            body.position = worldPosition;
            body.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            box.RecordPlacement(); // as if the player let it go and it came to rest
        }

        [UnityTest]
        public IEnumerator ABoxPutOnTheShelf_StandsThereInTheNextSession_ButNotOneLeftInTheHandoverZone()
        {
            var shelfSpot = new Vector3(0.6f, 0.13f, -1.2f); // on the floor, in the boxes' (play-area) coordinates
            var shelfBox = Box("BOX-002");
            var zoneBox = Box("BOX-003");
            var zoneDefault = zoneBox.transform.localPosition;
            PutDown(shelfBox, shelfBox.transform.parent.TransformPoint(shelfSpot));
            PutDown(zoneBox, Object.FindFirstObjectByType<HandoverZone>().transform.position + Vector3.down * 0.07f);

            yield return Load();

            var keeper = Object.FindFirstObjectByType<WarehouseLayoutKeeper>();
            Assert.AreEqual(1, keeper.Restored, "only the shelf box: the handover zone is not a warehouse place");
            Assert.Less(Vector3.Distance(shelfSpot, Box("BOX-002").transform.localPosition), 0.01f);
            Assert.Less(Vector3.Distance(zoneDefault, Box("BOX-003").transform.localPosition), 0.01f, "default place");
        }

        [UnityTest]
        public IEnumerator ResetButton_PutsTheBoxesBack_AndForgetsTheSavedLayout()
        {
            var box = Box("BOX-001");
            var defaultPlace = box.transform.localPosition;
            PutDown(box, box.transform.parent.TransformPoint(new Vector3(0.7f, 0.13f, -1.3f)));

            yield return Load(); // the box comes back at the player's place
            Assert.Greater(Vector3.Distance(defaultPlace, Box("BOX-001").transform.localPosition), 0.3f, "restored first");

            Object.FindFirstObjectByType<ResetWarehouseButton>(FindObjectsInactive.Include).Press();
            Assert.Less(Vector3.Distance(defaultPlace, Box("BOX-001").transform.localPosition), 0.01f, "back where it started");

            yield return Load(); // and the next session no longer remembers the old place
            Assert.AreEqual(0, Object.FindFirstObjectByType<WarehouseLayoutKeeper>().Restored);
            Assert.Less(Vector3.Distance(defaultPlace, Box("BOX-001").transform.localPosition), 0.01f);
        }

        [UnityTest]
        public IEnumerator Reset_OntoAnOccupiedDefaultPlace_PutsTheBoxAboveIt_InsteadOfInsideIt()
        {
            var box = Box("BOX-001");
            var defaultPlace = box.transform.localPosition;
            PutDown(box, box.transform.parent.TransformPoint(new Vector3(0.7f, 0.13f, -1.3f)));
            // The player left the scanner exactly where the box used to stand.
            var scanner = Object.FindFirstObjectByType<HandScanner>();
            var scannerBody = scanner.GetComponent<Rigidbody>();
            scannerBody.isKinematic = true;
            scanner.transform.position = box.transform.parent.TransformPoint(defaultPlace);
            Physics.SyncTransforms();

            Object.FindFirstObjectByType<ResetWarehouseButton>(FindObjectsInactive.Include).Press();

            var local = Box("BOX-001").transform.localPosition;
            Assert.Less(Vector2.Distance(new Vector2(defaultPlace.x, defaultPlace.z), new Vector2(local.x, local.z)), 0.01f,
                "back over its place");
            Assert.Greater(local.y, defaultPlace.y + 0.2f, "above the scanner, not inside it");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ABoxThrownOutOfReach_ComesBack_ButOneJustInsideStays()
        {
            var far = Box("BOX-001");
            var near = Box("BOX-002");
            var farStart = far.transform.localPosition;
            // T025: the warehouse rule (6 m from the play area's centre) decides when a box is lost.
            PutDown(far, far.transform.parent.TransformPoint(new Vector3(Pvz.Core.WarehouseBounds.MaxReach + 0.5f, 0.5f, 0f)));
            PutDown(near, near.transform.parent.TransformPoint(new Vector3(Pvz.Core.WarehouseBounds.MaxReach - 0.5f, 0.5f, 0f)));
            yield return null;
            yield return null;

            Assert.Less(Vector3.Distance(farStart, far.transform.localPosition), 0.01f, "out of reach: back to its place");
            var nearLocal = near.transform.localPosition;
            Assert.Greater(new Vector2(nearLocal.x, nearLocal.z).magnitude, Pvz.Core.WarehouseBounds.MaxReach - 1f, "within reach: left where it is");
        }

        [UnityTest]
        public IEnumerator TwoBoxesSavedOnTheSameSpot_OnlyOneIsRestoredThere()
        {
            var spot = new Vector3(-0.4f, 0.13f, -1.4f);
            var first = Box("BOX-001");
            var second = Box("BOX-002");
            PutDown(first, first.transform.parent.TransformPoint(spot));
            PutDown(second, second.transform.parent.TransformPoint(spot + new Vector3(0.05f, 0f, 0f)));

            yield return Load();

            Assert.AreEqual(1, Object.FindFirstObjectByType<WarehouseLayoutKeeper>().Restored,
                "the second box would stand inside the first one: it keeps its default place");
            var distance = Vector3.Distance(Box("BOX-001").transform.position, Box("BOX-002").transform.position);
            Assert.Greater(distance, 0.3f, "the boxes do not overlap");
        }
    }
}
