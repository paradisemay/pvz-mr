using System.Linq;
using NUnit.Framework;
using Pvz.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pvz.Editor.Tests
{
    /// <summary>
    /// Cheap guard for manual scene/prefab edits (e.g. an artist replacing models):
    /// every serialized reference of the game objects is set and the boxes match the shift settings.
    /// </summary>
    public class SceneIntegrityTests
    {
        [OneTimeSetUp]
        public void OpenScene() => EditorSceneManager.OpenScene(PvzSceneBuilder.ScenePath, OpenSceneMode.Single);

        [OneTimeTearDown]
        public void CloseScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void SceneIsInBuildSettings()
        {
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == PvzSceneBuilder.ScenePath));
        }

        [TestCase(typeof(ShiftDirector))]
        [TestCase(typeof(HandScanner))]
        [TestCase(typeof(CounterBoard))]
        [TestCase(typeof(ShiftSummaryPanel))]
        [TestCase(typeof(MrSessionSetup))]
        public void AllObjectReferencesAreAssigned(System.Type componentType)
        {
            var components = Object.FindObjectsByType(componentType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.IsNotEmpty(components, $"{componentType.Name} missing in the scene");
            foreach (var component in components)
            {
                var so = new SerializedObject(component);
                var it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Assert.IsNotNull(it.objectReferenceValue, $"{componentType.Name}.{it.propertyPath} is not assigned");
                }
            }
        }

        [Test]
        public void CustomerPrefabReferencesAreAssigned()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Customer>("Assets/_Project/Prefabs/Customer.prefab");
            Assert.IsNotNull(prefab);
            var it = new SerializedObject(prefab).GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType == SerializedPropertyType.ObjectReference)
                    Assert.IsNotNull(it.objectReferenceValue, $"Customer.{it.propertyPath} is not assigned");
            }
        }

        [Test]
        public void SceneBoxesMatchShiftSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ShiftSettings>(PvzSceneBuilder.SettingsPath);
            var expected = settings.Boxes.Select(b => new Core.BoxId(b.boxId)).OrderBy(b => b.Value).ToArray();
            var inScene = Object.FindObjectsByType<QrBox>(FindObjectsSortMode.None).Select(b => b.Id).OrderBy(b => b.Value).ToArray();
            CollectionAssert.AreEqual(expected, inScene);

            var director = Object.FindFirstObjectByType<ShiftDirector>();
            var listed = new SerializedObject(director).FindProperty("boxes");
            Assert.AreEqual(expected.Length, listed.arraySize, "ShiftDirector must list every box in the scene");
        }

        [Test]
        public void EditorOnlyHelpers_AreStrippedFromBuilds_AndSimulatorStartsInactive()
        {
            var setup = new SerializedObject(Object.FindFirstObjectByType<MrSessionSetup>());
            var simulator = (GameObject)setup.FindProperty("editorSimulator").objectReferenceValue;
            var mockRoom = (GameObject)setup.FindProperty("editorMockRoom").objectReferenceValue;

            Assert.AreEqual("EditorOnly", simulator.tag);
            Assert.IsFalse(simulator.activeSelf, "an active simulator would replace the real HMD on the headset");
            Assert.AreEqual("EditorOnly", mockRoom.tag);
        }

        [Test]
        public void GameplayObjects_LiveUnderThePlayArea()
        {
            var playArea = (Transform)new SerializedObject(Object.FindFirstObjectByType<MrSessionSetup>())
                .FindProperty("playArea").objectReferenceValue;
            foreach (var box in Object.FindObjectsByType<QrBox>(FindObjectsSortMode.None))
                Assert.IsTrue(box.transform.IsChildOf(playArea), box.name);
            Assert.IsTrue(Object.FindFirstObjectByType<HandScanner>().transform.IsChildOf(playArea));
            Assert.IsTrue(Object.FindFirstObjectByType<HandoverZone>().transform.IsChildOf(playArea));
        }

        [Test]
        public void BoxesHaveGrabAndPhysics()
        {
            foreach (var box in Object.FindObjectsByType<QrBox>(FindObjectsSortMode.None))
            {
                Assert.IsNotNull(box.GetComponent<Rigidbody>(), box.name);
                Assert.IsNotNull(box.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(), box.name);
                Assert.IsNotNull(box.GetComponent<Collider>(), box.name);
            }
        }
    }
}
