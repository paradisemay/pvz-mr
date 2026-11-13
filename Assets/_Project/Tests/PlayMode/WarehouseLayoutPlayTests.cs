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
            yield return null;
        }

        /// <summary>A new session: the same saved data, a freshly loaded scene.</summary>
        private IEnumerator Load()
        {
            WarehouseLayoutKeeper.StoreOverride = _store;
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
