using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.ARFoundation;

namespace Pvz.Runtime.Tests
{
    /// <summary>Start menu extras: «Стойку ко мне» and the room-permission gate.</summary>
    public class StartMenuTests
    {
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            PlacementKeeper.StoreOverride = new Pvz.Core.MemoryTextStore(); // nor the saved place of the counter
            CareerKeeper.StoreOverride = (new Pvz.Core.MemoryScoreStore(), new Pvz.Core.MemoryTextStore()); // nor the career
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator ForgetTestStore()
        {
            PlacementKeeper.StoreOverride = null;
            CareerKeeper.StoreOverride = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator RecenterButton_MovesTheWholePlayAreaInFrontOfThePlayer()
        {
            var origin = Object.FindFirstObjectByType<XROrigin>();
            var camera = origin.Camera.transform;
            var playArea = Object.FindFirstObjectByType<MrSessionSetup>().PlayArea;
            var box = Object.FindFirstObjectByType<SupplyHatch>(); // the room things move with the play area (no boxes before the shift)
            var boxLocal = playArea.InverseTransformPoint(box.transform.position);

            // The player stepped back-left into free space and turned to the right.
            origin.transform.SetPositionAndRotation(new Vector3(-0.8f, 0f, -1.0f), Quaternion.Euler(0f, 90f, 0f));
            yield return null;

            Object.FindFirstObjectByType<RecenterButton>(FindObjectsInactive.Include).Press(); // a key of the settings screen
            yield return null;

            Assert.AreEqual(camera.position.x, playArea.position.x, 0.01f);
            Assert.AreEqual(camera.position.z, playArea.position.z, 0.01f);
            var lookingAt = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            Assert.Greater(Vector3.Dot(playArea.forward, lookingAt), 0.99f, "the counter is in front of the player");

            var boxNow = playArea.InverseTransformPoint(box.transform.position);
            Assert.Less(Vector3.Distance(boxLocal, boxNow), 0.15f, "the hatch moves together with the play area");
        }

        [UnityTest]
        public IEnumerator EveryCounterButton_IsPressedThroughPressableButton()
        {
            var buttons = new MonoBehaviour[]
            {
                Object.FindFirstObjectByType<RecenterButton>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<ThemeButton>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<RestartButton>(FindObjectsInactive.Include),
            }.Concat(Object.FindObjectsByType<DifficultyButton>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            foreach (var button in buttons)
            {
                Assert.IsNotNull(button);
                Assert.IsNotNull(button.GetComponent<PressableButton>(), $"{button.name} is pressed by trigger/poke");
            }

            // A trigger press on the «Обычная» button starts the shift.
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            director.DifficultyButtons.First(b => b.Settings.DisplayName == "Обычная").GetComponent<PressableButton>().Press();
            yield return null;
            Assert.IsTrue(director.IsStarted);
        }

        [UnityTest]
        public IEnumerator HowToPlayCard_IsHidden_OnceTheShiftBegins_TheComputerStays()
        {
            var howTo = GameObject.Find("How To Play");
            var computer = Object.FindFirstObjectByType<PvzComputer>();
            Assert.IsTrue(howTo.activeInHierarchy);

            Object.FindFirstObjectByType<ShiftDirector>().Begin();
            yield return null;

            Assert.IsFalse(howTo.activeInHierarchy);
            Assert.IsTrue(computer.gameObject.activeInHierarchy, "the computer and its keys stay on the counter (#77)");
        }

        [UnityTest]
        public IEnumerator PlaneDetection_StartsWithoutPermissionPromptInTheEditor()
        {
            yield return null;
            Assert.IsTrue(Object.FindFirstObjectByType<ScenePermissionGate>().Granted);
            Assert.IsTrue(Object.FindFirstObjectByType<ARPlaneManager>().enabled, "the gate enables plane detection");
        }
    }
}
