using System.Collections;
using NUnit.Framework;
using Pvz.Runtime.Remote;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pvz.Runtime.Tests
{
    /// <summary>#93: the remote hand aims a held scanner's muzzle, not the hand, along a line clear of other things.</summary>
    public class VirtualHandAimTests
    {
        private static readonly Vector3 Far = new Vector3(200f, 50f, 200f); // clear of any scene another test left loaded
        private Scene _scene;
        private GameObject _root;

        [UnitySetUp]
        public IEnumerator EmptyScene()
        {
            _scene = SceneManager.CreateScene("AimTest" + Time.frameCount);
            SceneManager.SetActiveScene(_scene);
            _root = new GameObject("Aim Test");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Clean()
        {
            Object.Destroy(_root);
            yield return null;
            if (_scene.IsValid() && SceneManager.sceneCount > 1) yield return SceneManager.UnloadSceneAsync(_scene);
        }

        private Transform Cube(string name, Vector3 position, Vector3 size)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            cube.name = name;
            cube.SetParent(_root.transform, false);
            cube.position = position;
            cube.localScale = size;
            return cube;
        }

        private Transform Cube(string name, Vector3 position, float size) => Cube(name, position, Vector3.one * size);

        [UnityTest]
        public IEnumerator TheMuzzle_IsMeasuredOnceTheHeldThingSettles_AndLandsOnTheClearSpot()
        {
            var target = Cube("Target", Far + new Vector3(0f, 1f, 2f), 0.2f);
            Physics.SyncTransforms();
            Assert.IsTrue(VirtualHand.TryClearShot(target.position, 0.5f, target, null, 0.02f, out var clear));
            var hand = _root.AddComponent<VirtualHand>(); // no device: the pose is only computed
            // A held scanner still catching up with the hand (velocity tracking): it drifts and slows down.
            var held = new GameObject("Held").AddComponent<Rigidbody>();
            held.transform.SetParent(_root.transform, false);
            held.useGravity = false;
            held.linearDamping = 4f;
            held.linearVelocity = new Vector3(0.5f, 0f, 0f);
            var muzzle = new GameObject("Muzzle").transform; // 14 cm off the hand, turned
            muzzle.SetParent(held.transform, false);
            muzzle.SetLocalPositionAndRotation(new Vector3(-0.1f, 0.02f, 0.1f), Quaternion.Euler(-20f, 15f, 0f));

            Assert.IsTrue(hand.AimHeldAt(muzzle, target.position, target, held.transform));
            yield return new WaitForSeconds(0.3f);
            Assert.IsNull(hand.PlannedSpot, "not measured while the held thing still moves");
            var deadline = Time.realtimeSinceStartup + 4f;
            while (hand.PlannedSpot == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsNotNull(hand.PlannedSpot);
            Assert.Less(held.linearVelocity.magnitude, 0.05f, "measured after it settled");

            // Where the muzzle ends up once the hand reaches its target pose (the hand is still at the origin, unturned).
            var muzzleAt = hand.TargetPosition + hand.TargetRotation * muzzle.position;
            var muzzleForward = hand.TargetRotation * muzzle.rotation * Vector3.forward;
            Assert.Less(Vector3.Distance(muzzleAt, clear), 0.005f, "the muzzle, not the hand, lands on the clear spot");
            Assert.Less(Vector3.Angle(muzzleForward, target.position - muzzleAt), 0.5f, "the scan ray points at the target");
            Assert.Greater(Vector3.Distance(hand.TargetPosition, muzzleAt), 0.1f, "the hand stands off the muzzle");
        }

        [UnityTest]
        public IEnumerator AThickLine_IsNeeded_AHairThinGapIsNotClear()
        {
            var target = Cube("Target", Far + new Vector3(0f, 1f, 2f), 0.2f);
            // The first spot tried: 0.5 m towards the player's head (a scene from another test may have a camera), 0.3 m up.
            var head = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            var from = target.position + Vector3.ProjectOnPlane(head - target.position, Vector3.up).normalized * 0.5f + Vector3.up * 0.3f;
            var side = Vector3.Cross(Vector3.up, target.position - from).normalized;
            var mid = Vector3.Lerp(from, target.position, 0.5f) + side * 0.025f; // a blocker 1 cm off the line
            Cube("Blocker", mid, 0.03f);
            Physics.SyncTransforms();

            Assert.IsTrue(VirtualHand.TryClearShot(target.position, 0.5f, target, null, 0f, out var thin));
            Assert.Less(Vector3.Distance(thin, from), 0.01f, "a thin ray squeezes past the blocker");
            Assert.IsTrue(VirtualHand.TryClearShot(target.position, 0.5f, target, null, 0.02f, out var thick));
            Assert.Greater(Vector3.Distance(thick, from), 0.05f, "a 2 cm thick line does not: another spot is chosen");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ASpotInsideSomething_IsNotClear()
        {
            var target = Cube("Target", Far + new Vector3(0f, 1f, 2f), 0.2f);
            var head = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            var first = target.position + Vector3.ProjectOnPlane(head - target.position, Vector3.up).normalized * 0.5f + Vector3.up * 0.3f;
            Cube("Around the first spot", first, 0.15f); // a ray from inside never sees it
            Physics.SyncTransforms();

            Assert.IsTrue(VirtualHand.TryClearShot(target.position, 0.5f, target, null, 0f, out var spot));
            Assert.Greater(Vector3.Distance(spot, first), 0.05f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BlockedFromEverySide_IsReported_AndTheHandStays()
        {
            var target = Cube("Target", Far + new Vector3(0f, 1f, 2f), 0.2f);
            const float h = 0.3f; // a closed shell of six walls around the target
            foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
            foreach (var sign in new[] { -1f, 1f })
            {
                var size = Vector3.one * (2f * h + 0.04f) - axis * (2f * h);
                Cube("Wall", target.position + axis * (sign * h), size);
            }
            Physics.SyncTransforms();
            var hand = _root.AddComponent<VirtualHand>();
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(_root.transform, false);
            var before = hand.TargetPosition;

            Assert.IsFalse(VirtualHand.TryClearShot(target.position, 0.5f, target, null, 0f, out _), "no spot even for a thin ray");
            Assert.IsFalse(hand.AimHeldAt(muzzle, target.position, target, null));
            Assert.IsFalse(hand.IsAiming);
            Assert.AreEqual(before, hand.TargetPosition);
            yield return null;
        }
    }
}
