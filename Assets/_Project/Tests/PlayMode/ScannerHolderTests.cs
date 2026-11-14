using System.Collections;
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
        public IEnumerator AScannerLeftOnTheCounter_StaysThere()
        {
            var spot = _cradle.position + new Vector3(0.6f, 0.02f, -0.05f); // further along the counter top
            Drop(spot);
            yield return new WaitForSeconds(6f);
            Assert.IsFalse(_holder.IsDocked);
            Assert.AreEqual(0, _holder.Returns);
            Assert.Greater(Vector3.Distance(_scanner.transform.position, _cradle.position), 0.4f);
        }
    }
}
