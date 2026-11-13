using System.Collections;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pvz.Runtime.Tests
{
    /// <summary>Start menu extras: «Стойку ко мне» and the room-permission gate.</summary>
    public class StartMenuTests
    {
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RecenterButton_MovesTheCounterInFrontOfThePlayer()
        {
            var origin = Object.FindFirstObjectByType<XROrigin>();
            var camera = origin.Camera.transform;
            var playArea = Object.FindFirstObjectByType<ShiftDirector>().Zone.transform.root;

            // The player walked 1.5 m to the right and turned around.
            origin.transform.SetPositionAndRotation(new Vector3(1.5f, 0f, 0.3f), Quaternion.Euler(0f, 180f, 0f));
            yield return null;

            Object.FindFirstObjectByType<RecenterButton>().Press();
            yield return null;

            Assert.AreEqual(camera.position.x, playArea.position.x, 0.01f);
            Assert.AreEqual(camera.position.z, playArea.position.z, 0.01f);
            var lookingAt = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            Assert.Greater(Vector3.Dot(playArea.forward, lookingAt), 0.99f, "the counter is in front of the player");
        }

        [UnityTest]
        public IEnumerator StartMenu_IsHidden_OnceTheShiftBegins()
        {
            var recenter = Object.FindFirstObjectByType<RecenterButton>();
            Assert.IsTrue(recenter.gameObject.activeInHierarchy);

            Object.FindFirstObjectByType<ShiftDirector>().Begin();
            yield return null;

            Assert.IsFalse(recenter.gameObject.activeInHierarchy);
        }

        [UnityTest]
        public IEnumerator PermissionGate_DoesNotBlockTheEditor()
        {
            yield return null;
            Assert.IsTrue(Object.FindFirstObjectByType<ScenePermissionGate>().Granted);
        }
    }
}
