using System.Collections;
using System.Linq;
using NUnit.Framework;
using Pvz.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pvz.Runtime.Tests
{
    /// <summary>T038 (#76): the scanner lies in its holder and comes back to it when dropped or lost.</summary>
    public class ScannerHolderTests
    {
        private ScannerHolder _holder;
        private HandScanner _scanner;
        private Rigidbody _body;
        private Transform _cradle;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            PlacementKeeper.StoreOverride = new MemoryTextStore();
            CareerKeeper.StoreOverride = (new MemoryScoreStore(), new MemoryTextStore());
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            _holder = Object.FindFirstObjectByType<ScannerHolder>();
            _scanner = Object.FindFirstObjectByType<HandScanner>();
            _body = _scanner.GetComponent<Rigidbody>();
            _cradle = _holder.transform.Find("Cradle");
        }

        [UnityTearDown]
        public IEnumerator Forget()
        {
            PlacementKeeper.StoreOverride = null;
            CareerKeeper.StoreOverride = null;
            yield return null;
        }

        /// <summary>What a let-go does: the body is free again where the hand left it.</summary>
        private void Drop(Vector3 position)
        {
            _body.isKinematic = false;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _scanner.transform.position = position;
            _body.position = position;
            Physics.SyncTransforms();
            _holder.Release();
        }

        [UnityTest, Timeout(20000)]
        public IEnumerator TheShift_StartsWithTheScannerInItsHolder()
        {
            Assert.IsNotNull(_cradle, "the holder has a cradle");
            Assert.IsTrue(_holder.IsDocked);
            Assert.Less(Vector3.Distance(_scanner.transform.position, _cradle.position), 0.01f);
            yield return new WaitForSeconds(1f);
            Assert.Less(Vector3.Distance(_scanner.transform.position, _cradle.position), 0.01f, "it stays put, not knocked off by the tray");
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator AScannerDroppedOnTheFloor_ComesBackAfterAFewSeconds()
        {
            var floorSpot = _holder.transform.parent.TransformPoint(new Vector3(0f, 0.3f, -1.2f));
            Drop(floorSpot);
            yield return new WaitForSeconds(2f);
            Assert.IsFalse(_holder.IsDocked, "not at once: the player may pick it up themselves");
            Assert.Greater(Vector3.Distance(_scanner.transform.position, _cradle.position), 0.5f);
            yield return new WaitForSeconds(4.5f);
            Assert.IsTrue(_holder.IsDocked, "back in the holder");
            Assert.AreEqual(1, _holder.Returns);
            Assert.Less(Vector3.Distance(_scanner.transform.position, _cradle.position), 0.01f);
        }

        [UnityTest, Timeout(20000)]
        public IEnumerator AScannerBelowTheFloor_ComesBackAtOnce()
        {
            Drop(_holder.transform.parent.position + Vector3.down * 2f);
            _body.isKinematic = true; // keep it there: no floor to stop it anyway
            yield return null;
            yield return null;
            Assert.IsTrue(_holder.IsDocked);
            Assert.AreEqual(1, _holder.Returns);
        }

        [UnityTest, Timeout(20000)]
        public IEnumerator AScannerLetGoNearTheHolder_ClicksIn()
        {
            Drop(_cradle.position + new Vector3(0.05f, 0.08f, 0.03f));
            yield return null;
            yield return null;
            Assert.IsTrue(_holder.IsDocked);
            Assert.AreEqual(0, _holder.Returns, "a click-in is not a return");
            Assert.Less(Quaternion.Angle(_scanner.transform.rotation, _cradle.rotation), 1f);
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator AScannerLeftAtTheFarEndOfTheCounter_StaysThere()
        {
            var spot = _cradle.position + _holder.transform.parent.right * 1.1f + Vector3.up * 0.02f; // the other end of the top, > 1 m away
            Drop(spot);
            yield return new WaitForSeconds(6f);
            Assert.IsFalse(_holder.IsDocked);
            Assert.AreEqual(0, _holder.Returns);
            Assert.Greater(Vector3.Distance(_scanner.transform.position, _cradle.position), 0.9f);
        }

        [UnityTest, Timeout(20000)]
        public IEnumerator MovingTheCounter_DocksAScannerLyingOnTheFloor()
        {
            Drop(_holder.transform.parent.TransformPoint(new Vector3(0f, 0.3f, -1.2f)));
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(_holder.IsDocked);
            var computer = Object.FindFirstObjectByType<PvzComputer>();
            computer.OpenSettings();
            computer.StartMovingCounter();
            yield return null;
            Assert.IsTrue(_holder.IsDocked, "not dragged through the room with the counter");
            computer.CancelMove();
        }

        [UnityTest, Timeout(20000)]
        public IEnumerator AReturn_WaitsWhileSomethingLiesInTheTray()
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); // something left in the tray (no boxes before the shift)
            box.transform.localScale = Vector3.one * 0.1f;
            box.transform.position = _cradle.position;
            Physics.SyncTransforms();
            Drop(_holder.transform.parent.position + Vector3.down * 2f);
            _body.isKinematic = true;
            yield return null;
            yield return null;
            Assert.IsFalse(_holder.IsDocked, "not teleported into the box");
            Assert.IsTrue(_holder.CradleBlocked);
            box.transform.position = _cradle.position + Vector3.right * 2f;
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            Assert.IsTrue(_holder.IsDocked, "back once the tray is clear");
        }

        [UnityTest, Timeout(20000)]
        public IEnumerator ARealGrabAndRelease_LeavesTheScannerFreeToFall()
        {
            var interactor = Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>(FindObjectsSortMode.None)
                .First(i => i is UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor && i.isActiveAndEnabled);
            var grab = _scanner.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            var manager = grab.interactionManager;
            manager.SelectEnter((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)interactor, grab);
            Assert.IsTrue(grab.isSelected); // same frame: the manager drops a select whose grip is not pressed on its next update
            _scanner.transform.position = _body.position = _cradle.position + Vector3.right * 0.6f + Vector3.up * 0.5f;
            manager.SelectExit((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)interactor, grab);
            yield return null;
            Assert.IsFalse(_body.isKinematic, "XRI restored the docked (kinematic) state, the holder must undo it");
            Assert.IsFalse(_holder.IsDocked);
        }
    }
}
