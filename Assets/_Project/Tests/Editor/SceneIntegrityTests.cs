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
        [TestCase(typeof(FeedbackEffects))]
        [TestCase(typeof(ThemeButton))]
        [TestCase(typeof(CounterSkin))]
        [TestCase(typeof(CareerKeeper))]
        [TestCase(typeof(HandScanner))]
        [TestCase(typeof(CounterBoard))]
        [TestCase(typeof(ShiftSummaryPanel))]
        [TestCase(typeof(MrSessionSetup))]
        [TestCase(typeof(SupplyHatch))]
        [TestCase(typeof(ScannerHolder))]
        [TestCase(typeof(RoomMesh))]
        [TestCase(typeof(BagDispenser))]
        [TestCase(typeof(DifficultyButton))]
        [TestCase(typeof(RecenterButton))]
        [TestCase(typeof(ScenePermissionGate))]
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
            foreach (var component in new Component[] { prefab, prefab.GetComponent<CustomerVoice>() })
            {
                Assert.IsNotNull(component, "the customer has a voice");
                var it = new SerializedObject(component).GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType == SerializedPropertyType.ObjectReference)
                        Assert.IsNotNull(it.objectReferenceValue, $"{component.GetType().Name}.{it.propertyPath} is not assigned");
                }
            }
        }

        /// <summary>T036: no box stands in the room before the shift; the stock drops out of the supply hatch.</summary>
        [Test]
        public void NoBoxesBeforeTheShift_TheHatchIsThere()
        {
            Assert.IsEmpty(Object.FindObjectsByType<QrBox>(FindObjectsSortMode.None));
            var hatch = Object.FindFirstObjectByType<SupplyHatch>();
            Assert.IsNotNull(hatch);
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            Assert.AreSame(hatch, new SerializedObject(director).FindProperty("hatch").objectReferenceValue);
            var settings = AssetDatabase.LoadAssetAtPath<ShiftSettings>(PvzSceneBuilder.SettingsPath);
            Assert.IsNotEmpty(settings.SpareBoxes, "a few spare boxes fall out with the stock");
            Assert.IsEmpty(settings.SpareBoxes.Select(b => b.orderNumber).Intersect(settings.Boxes.Concat(settings.SupplyBoxes).Select(b => b.orderNumber)),
                "spare boxes are other days' orders");
            Assert.IsEmpty(settings.SpareBoxes.Select(b => b.boxId).Intersect(settings.Boxes.Concat(settings.SupplyBoxes).Select(b => b.boxId)),
                "a spare box id must not repeat a box of the shift or the delivery");
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
        public void DifficultyPresets_ShareTheSceneBoxLayout()
        {
            var normal = AssetDatabase.LoadAssetAtPath<ShiftSettings>(PvzSceneBuilder.SettingsPath);
            foreach (var path in new[] { PvzSceneBuilder.EasySettingsPath, PvzSceneBuilder.HardSettingsPath })
            {
                var preset = AssetDatabase.LoadAssetAtPath<ShiftSettings>(path);
                Assert.IsNotNull(preset, path);
                CollectionAssert.AreEqual(normal.Boxes.Select(b => b.boxId + b.orderNumber),
                    preset.Boxes.Select(b => b.boxId + b.orderNumber), $"{path}: boxes must match the scene");
            }
        }

        [Test]
        public void DifficultyPresets_ShareTheSupplyLayout()
        {
            var normal = AssetDatabase.LoadAssetAtPath<ShiftSettings>(PvzSceneBuilder.SettingsPath);
            foreach (var path in new[] { PvzSceneBuilder.EasySettingsPath, PvzSceneBuilder.HardSettingsPath })
            {
                var preset = AssetDatabase.LoadAssetAtPath<ShiftSettings>(path);
                CollectionAssert.AreEqual(normal.SupplyBoxes.Select(b => b.boxId + b.orderNumber),
                    preset.SupplyBoxes.Select(b => b.boxId + b.orderNumber), $"{path}: supply must match");
            }
        }

        /// <summary>T016: the delivery brings an order easy to mix up with one in the warehouse.</summary>
        [TestCase(PvzSceneBuilder.SettingsPath)]
        [TestCase(PvzSceneBuilder.HardSettingsPath)]
        public void Presets_ContainALookAlikePair(string settingsPath)
        {
            var numbers = AllOrderNumbers(settingsPath);
            Assert.IsTrue(numbers.Any(a => numbers.Any(b => Core.OrderNumbers.LookAlike(a, b))),
                $"{settingsPath}: no «похожие заказы» among {string.Join(", ", numbers)}");
        }

        /// <summary>T023: «заказ состоит из большого количества товаров» — the delivery brings an order of 3+ boxes.</summary>
        [TestCase(PvzSceneBuilder.SettingsPath)]
        [TestCase(PvzSceneBuilder.HardSettingsPath)]
        public void Presets_DeliverABigOrder(string settingsPath)
        {
            var settings = AssetDatabase.LoadAssetAtPath<ShiftSettings>(settingsPath);
            var biggest = settings.SupplyBoxes.GroupBy(b => b.orderNumber).Max(g => g.Count());
            Assert.GreaterOrEqual(biggest, 3, $"{settingsPath}: no big order in the delivery");
        }

        private static string[] AllOrderNumbers(string settingsPath)
        {
            var settings = AssetDatabase.LoadAssetAtPath<ShiftSettings>(settingsPath);
            return settings.Boxes.Concat(settings.SupplyBoxes).Select(b => b.orderNumber).Distinct().ToArray();
        }

        [TestCase(PvzSceneBuilder.SettingsPath)]
        [TestCase(PvzSceneBuilder.EasySettingsPath)]
        [TestCase(PvzSceneBuilder.HardSettingsPath)]
        public void ShiftSettings_DeliveryIsValidForTheWarehouse(string settingsPath)
        {
            var settings = AssetDatabase.LoadAssetAtPath<ShiftSettings>(settingsPath);
            var catalog = new Core.BoxCatalog(settings.Boxes.Select(b => new Core.BoxRecord(new Core.BoxId(b.boxId), b.orderNumber)));
            var plan = settings.CreateSupplyPlan();
            using (var shift = new Core.Shift(catalog, new Core.SystemRandomSource(1), settings.CreateScoringRules(), plan))
            {
                Assert.DoesNotThrow(() => shift.ExpectDelivery(plan.Boxes),
                    "supply boxes must have unique ids and new order numbers");
            }
        }

        [Test]
        public void PlaneDetection_IsSavedDisabled_UntilTheSceneIsPermitted()
        {
            Assert.IsFalse(Object.FindFirstObjectByType<UnityEngine.XR.ARFoundation.ARPlaneManager>(FindObjectsInactive.Include).enabled,
                "Meta requires USE_SCENE before plane detection starts; ScenePermissionGate enables it");
        }

        [Test]
        public void RoomMesh_IsAnInvisibleCollider_UnderTheXrOrigin_SavedDisabled()
        {
            var manager = Object.FindFirstObjectByType<UnityEngine.XR.ARFoundation.ARMeshManager>(FindObjectsInactive.Include);
            Assert.IsNotNull(manager, "T041: the room mesh");
            Assert.IsFalse(manager.enabled, "Meta gives the mesh only with USE_SCENE; ScenePermissionGate enables it");
            Assert.IsNotNull(manager.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>(), "meshes come in session space");
            Assert.IsNull(manager.GetComponent<Unity.XR.CoreUtils.XROrigin>(), "a child of the XR Origin, not on it");
            Assert.IsNotNull(manager.meshPrefab);
            Assert.IsNotNull(manager.meshPrefab.GetComponent<MeshCollider>(), "boxes collide with the real furniture");
            Assert.IsNull(manager.meshPrefab.GetComponent<MeshRenderer>(), "nothing is drawn over the passthrough");
            Assert.AreEqual(manager, new SerializedObject(Object.FindFirstObjectByType<ScenePermissionGate>()).FindProperty("meshes").objectReferenceValue);
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
