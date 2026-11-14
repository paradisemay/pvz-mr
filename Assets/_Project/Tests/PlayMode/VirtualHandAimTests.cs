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
        private GameObject _root;
        private static readonly Vector3 Far = new Vector3(200f, 50f, 200f); // clear of any scene another test left loaded

        [UnitySetUp]
        public IEnumerator EmptyScene()
        {
            SceneManager.SetActiveScene(SceneManager.CreateScene("AimTest" + Time.frameCount));
            _root = new GameObject("Aim Test");
            yield return null;
        }

        [TearDown]
        public void Clean() => Object.Destroy(_root);

        private Transform Cube(string name, Vector3 position, float size)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            cube.name = name;
            cube.SetParent(_root.transform, false);
            cube.position = position;
            cube.localScale = Vector3.one * size;
            return cube;
        }

        [UnityTest]
        public IEnumerator TheMuzzle_NotTheHand_LandsOnTheClearSpot_PointingAtTheTarget()
        {
            var target = Cube("Target", Far + new Vector3(0f, 1f, 2f), 0.2f);
            Physics.SyncTransforms();
            var hand = _root.AddComponent<VirtualHand>(); // no device: the pose is only computed
            var muzzle = new GameObject("Muzzle").transform; // held 14 cm off the hand, turned
            muzzle.SetParent(_root.transform, false);
            muzzle.SetPositionAndRotation(new Vector3(-0.1f, 0.02f, 0.1f), Quaternion.Euler(-20f, 15f, 0f));
            var offset = muzzle.position - hand.WorldPosition;
            var turn = muzzle.rotation;

            Assert.IsTrue(hand.AimHeldAt(muzzle, target.position, target, null));
            var muzzleAt = hand.TargetPosition + hand.TargetRotation * offset;
            var muzzleForward = hand.TargetRotation * turn * Vector3.forward;
            Assert.AreEqual(0.5f, Vector3.Distance(Vector3.ProjectOnPlane(muzzleAt - target.position, Vector3.up), Vector3.zero), 0.01f,
                "the muzzle, not the hand, is 0.5 m from the target");
            Assert.Less(Vector3.Angle(muzzleForward, target.position - muzzleAt), 0.5f, "the scan ray points at the target");
            Assert.Greater(Vector3.Distance(hand.TargetPosition, muzzleAt), 0.1f, "the hand stands off the muzzle");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AThickLine_IsNeeded_AHairThinGapIsNotClear()
        {
            var target = Cube("Target", Far + new Vector3(0f, 1f, 2f), 0.2f);
            // A blocker that a thin line from the first candidate spot just misses (1 cm off the line).
            // The first spot tried: 0.5 m towards the player's head (a scene from another test may have a camera), 0.3 m up.
            var head = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            var from = target.position + Vector3.ProjectOnPlane(head - target.position, Vector3.up).normalized * 0.5f + Vector3.up * 0.3f;
            var side = Vector3.Cross(Vector3.up, target.position - from).normalized;
            var mid = Vector3.Lerp(from, target.position, 0.5f) + side * 0.025f;
            Cube("Blocker", mid, 0.03f);
            Physics.SyncTransforms();

            Assert.IsTrue(VirtualHand.TryClearShot(target.position, 0.5f, target, null, 0f, out var thin));
            Assert.Less(Vector3.Distance(thin, from), 0.01f, "a thin ray squeezes past the blocker");
            Assert.IsTrue(VirtualHand.TryClearShot(target.position, 0.5f, target, null, 0.02f, out var thick));
            Assert.Greater(Vector3.Distance(thick, from), 0.05f, "a 2 cm thick line does not: another spot is chosen");
            yield return null;
        }

        [UnityTest]
        public IEnumerator NoClearSpot_IsReported_AndTheHandStays()
        {
            var target = new GameObject("Unseen").transform; // nothing of it can be hit: no line reaches it first
            target.SetParent(_root.transform, false);
            target.position = Far + new Vector3(0f, 1f, 2f);
            Cube("Wall", Far + new Vector3(0f, 1f, 2.3f), 0.2f);
            Physics.SyncTransforms();
            var hand = _root.AddComponent<VirtualHand>();
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(_root.transform, false);
            var before = hand.TargetPosition;

            Assert.IsFalse(hand.AimHeldAt(muzzle, target.position, target, null));
            Assert.AreEqual(before, hand.TargetPosition);
            yield return null;
        }
    }
}
