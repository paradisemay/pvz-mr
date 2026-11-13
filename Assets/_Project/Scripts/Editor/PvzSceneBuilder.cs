using System.Collections.Generic;
using System.Linq;
using Pvz.Runtime;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using static Pvz.Editor.PvzAssetFactory;

namespace Pvz.Editor
{
    /// <summary>
    /// Generates the MVP scene and all its assets. Idempotent: re-running rebuilds everything.
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Pvz.Editor.PvzSceneBuilder.BuildAll
    /// </summary>
    public static class PvzSceneBuilder
    {
        public const string ScenePath = ScenesDir + "/Game.unity";
        public const string SettingsPath = SettingsDir + "/ShiftSettings.asset";
        public const string EasySettingsPath = SettingsDir + "/ShiftSettings_Easy.asset";
        public const string HardSettingsPath = SettingsDir + "/ShiftSettings_Hard.asset";

        private const string SamplesRoot = "Assets/Samples/XR Interaction Toolkit/3.2.2";
        private const string XrOriginPrefabPath = SamplesRoot + "/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        private const string SimulatorPrefabPath = SamplesRoot + "/XR Interaction Simulator/XR Interaction Simulator.prefab";

        public static readonly Vector3 CounterPosition = new Vector3(0f, 0f, 1.0f);
        public static readonly Vector3 CustomerServicePosition = new Vector3(0f, 0f, 1.72f);
        public static readonly Vector3 CustomerSpawnPosition = new Vector3(0f, 0f, 3.1f);


        [MenuItem("PVZ/Build Scene And Assets")]
        public static void BuildAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            // Create the scene first: NewScene(Single) unloads unused assets and would invalidate loaded prefabs.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnsureFolders();
            var font = CyrillicFont();
            // Difficulty presets; existing assets are kept as they are (designers may have tuned them).
            var settings = LoadOrCreateSettings(SettingsPath, s => s.ConfigureDifficulty("Обычная", 90f, 1, 50, 1.2f, 2f, 2, returnChance: 20));
            var easy = LoadOrCreateSettings(EasySettingsPath, s => { s.CopyLayoutFrom(settings); s.ConfigureDifficulty("Лёгкая", 0f, 0, 0, 1.0f, 3f, 1, tutorial: true); });
            var hard = LoadOrCreateSettings(HardSettingsPath, s => { s.CopyLayoutFrom(settings); s.ConfigureDifficulty("Сложная", 45f, 1, 80, 1.6f, 1f, 3, returnChance: 35); });
            var prefabs = new PvzPrefabBuilder(font);

            var layout = settings.Boxes; // single source of truth: ids, orders and start positions
            var boxPaths = layout.Select(b => prefabs.Box(b.boxId)).ToArray();
            var scannerPath = prefabs.Scanner();
            var counterPath = prefabs.Counter();
            var customerPath = prefabs.Customer();
            var planePath = prefabs.ArPlane();
            var supplyBoxPath = prefabs.SupplyBox();
            var bagPath = prefabs.Bag();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var boxPrefabs = boxPaths.Select(Load<QrBox>).ToArray();
            var scannerPrefab = Load<HandScanner>(scannerPath);
            var counterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(counterPath);
            var customerPrefab = Load<Customer>(customerPath);
            var planePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(planePath);
            var supplyBoxPrefab = Load<QrBox>(supplyBoxPath);
            var bagPrefab = Load<ParcelBag>(bagPath);

            CreateLighting();
            var (camera, simulator, locomotion) = CreateMrRig(planePrefab);
            var mockRoom = CreateMockRoom();
            CreateFloorCollider();

            // Everything the game places in the room lives under the play area, which is aligned to the player on the headset.
            var playArea = new GameObject("Play Area").transform;

            var mrSetup = new GameObject("MR Session Setup").AddComponent<MrSessionSetup>();
            mrSetup.Configure(camera, mockRoom, simulator, locomotion, playArea);
            // Room planes need the USE_SCENE permission on Quest; plane detection waits for it.
            // Saved disabled: Meta requires USE_SCENE before plane detection starts; the gate enables it.
            var planeManager = Object.FindFirstObjectByType<ARPlaneManager>();
            planeManager.enabled = false;
            mrSetup.gameObject.AddComponent<ScenePermissionGate>().Configure(planeManager);

            var counter = (GameObject)PrefabUtility.InstantiatePrefab(counterPrefab, playArea);
            counter.transform.localPosition = CounterPosition;

            var scanner = ((GameObject)PrefabUtility.InstantiatePrefab(scannerPrefab.gameObject, playArea)).GetComponent<HandScanner>();
            scanner.transform.SetPositionAndRotation(CounterPosition + new Vector3(-0.48f, PvzPrefabBuilder.CounterHeight + 0.04f, -0.05f),
                Quaternion.Euler(0f, -90f, 90f));

            var boxesRoot = new GameObject("Boxes").transform;
            boxesRoot.SetParent(playArea, false);
            var boxes = new List<QrBox>();
            for (var i = 0; i < layout.Length; i++)
            {
                var box = ((GameObject)PrefabUtility.InstantiatePrefab(boxPrefabs[i].gameObject, boxesRoot)).GetComponent<QrBox>();
                box.transform.SetPositionAndRotation(layout[i].startPosition, Quaternion.Euler(0f, layout[i].startYaw, 0f));
                boxes.Add(box);
            }

            var points = new GameObject("Customer Points").transform;
            points.SetParent(playArea, false);
            var spawn = Marker("CustomerSpawn", points, CustomerSpawnPosition, 180f);
            var service = Marker("CustomerService", points, CustomerServicePosition, 180f);

            var dispenser = new GameObject("Bag Dispenser").AddComponent<BagDispenser>();
            dispenser.transform.SetParent(playArea, false);
            dispenser.transform.SetPositionAndRotation(CounterPosition + new Vector3(-0.58f, PvzPrefabBuilder.CounterHeight + 0.17f, 0.18f),
                Quaternion.Euler(0f, 20f, 0f));
            dispenser.Configure(bagPrefab);

            var supplyDrop = new GameObject("Supply Drop").AddComponent<SupplyDrop>();
            supplyDrop.transform.SetParent(playArea, false);
            supplyDrop.Configure(supplyBoxPrefab, camera.transform, counter.transform, spawn, Object.FindFirstObjectByType<ARPlaneManager>());

            var startMenu = new GameObject("Start Menu").transform;
            startMenu.SetParent(playArea, false);

            var director = new GameObject("Shift Director").AddComponent<ShiftDirector>();
            director.Configure(settings, boxes.ToArray(), scanner,
                counter.GetComponentInChildren<HandoverZone>(true),
                customerPrefab, spawn, service,
                counter.GetComponentInChildren<CounterBoard>(true),
                counter.GetComponentInChildren<ShiftSummaryPanel>(true),
                counter.GetComponentInChildren<RestartButton>(true).gameObject,
                counter.GetComponentInChildren<FeedbackAudio>(true),
                supplyDrop,
                CreateDifficultyButtons(startMenu, new[] { easy, settings, hard }, font));
            CreateRecenterButton(startMenu, mrSetup, font);
            director.ConfigureStartMenu(startMenu.gameObject);
            director.ConfigureReturns(supplyBoxPrefab); // returned boxes use the generic box with a runtime QR

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[PvzSceneBuilder] Built {ScenePath} with {boxes.Count} boxes.");
        }

        private static T Load<T>(string prefabPath) where T : Component
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var component = go != null ? go.GetComponent<T>() : null;
            if (component == null) throw new System.InvalidOperationException($"Prefab {prefabPath} has no {typeof(T).Name}.");
            return component;
        }

        private static ShiftSettings LoadOrCreateSettings(string path, System.Action<ShiftSettings> configureNew)
        {
            var settings = AssetDatabase.LoadAssetAtPath<ShiftSettings>(path);
            if (settings != null) return settings;
            settings = ScriptableObject.CreateInstance<ShiftSettings>();
            configureNew(settings);
            AssetDatabase.CreateAsset(settings, path);
            return settings;
        }

        /// <summary>«Стойку ко мне»: moves the play area in front of the player before the shift starts.</summary>
        private static void CreateRecenterButton(Transform parent, MrSessionSetup session, TMPro.TMP_FontAsset font)
        {
            var go = new GameObject("Recenter Button");
            go.transform.SetParent(parent, false);
            go.transform.position = CounterPosition + new Vector3(-0.15f, PvzPrefabBuilder.CounterHeight + 0.03f, -0.04f);
            Primitive(PrimitiveType.Cube, "Cap", go.transform, Vector3.zero, new Vector3(0.22f, 0.05f, 0.13f),
                Lit("RecenterButton", new Color(0.35f, 0.6f, 0.95f)), true);
            Text("Label", go.transform, new Vector3(0, 0.027f, 0), new Vector2(0.21f, 0.11f), "СТОЙКУ\nКО МНЕ", font,
                new Color(0.08f, 0.1f, 0.14f), 0.6f, localRot: Quaternion.Euler(90, 0, 0));
            go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            go.AddComponent<RecenterButton>().Configure(session);
        }

        /// <summary>Three buttons on the player's side of the counter; hidden once the shift starts.</summary>
        private static DifficultyButton[] CreateDifficultyButtons(Transform parent, ShiftSettings[] presets, TMPro.TMP_FontAsset font)
        {
            var colors = new[] { new Color(0.35f, 0.8f, 0.45f), new Color(0.95f, 0.75f, 0.2f), new Color(0.92f, 0.35f, 0.3f) };
            var buttons = new DifficultyButton[presets.Length];
            for (var i = 0; i < presets.Length; i++)
            {
                var go = new GameObject($"Difficulty {presets[i].DisplayName}");
                go.transform.SetParent(parent, false);
                go.transform.position = CounterPosition + new Vector3(-0.62f + i * 0.25f, PvzPrefabBuilder.CounterHeight + 0.03f, -0.22f);
                Primitive(PrimitiveType.Cube, "Cap", go.transform, Vector3.zero, new Vector3(0.22f, 0.05f, 0.13f),
                    Lit($"Difficulty{i}", colors[i % colors.Length]), true);
                Text("Label", go.transform, new Vector3(0, 0.027f, 0), new Vector2(0.22f, 0.1f), presets[i].DisplayName.ToUpperInvariant(),
                    font, new Color(0.1f, 0.1f, 0.12f), 0.6f, localRot: Quaternion.Euler(90, 0, 0));
                go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
                buttons[i] = go.AddComponent<DifficultyButton>();
                buttons[i].Configure(presets[i]);
            }
            return buttons;
        }

        private static void CreateLighting()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.62f);
        }

        private static (Camera camera, GameObject simulator, GameObject locomotion) CreateMrRig(GameObject planePrefab)
        {
            new GameObject("AR Session").AddComponent<ARSession>();

            var originPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrOriginPrefabPath);
            var origin = (GameObject)PrefabUtility.InstantiatePrefab(originPrefab);
            var xrOrigin = origin.GetComponent<XROrigin>();
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

            var camera = origin.GetComponentInChildren<Camera>(true);
            camera.nearClipPlane = 0.05f;
            if (camera.GetComponent<ARCameraManager>() == null) camera.gameObject.AddComponent<ARCameraManager>();

            var planes = origin.GetComponent<ARPlaneManager>();
            if (planes == null) planes = origin.AddComponent<ARPlaneManager>();
            planes.planePrefab = planePrefab;
            planes.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;

            GameObject simulator = null;
            var simulatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPrefabPath);
            if (simulatorPrefab != null)
            {
                simulator = (GameObject)PrefabUtility.InstantiatePrefab(simulatorPrefab);
                // Stripped from device builds; saved inactive because once enabled it replaces the real HMD.
                // MrSessionSetup enables it only in the editor without a headset.
                simulator.tag = "EditorOnly";
                simulator.SetActive(false);
            }
            else
            {
                Debug.LogWarning("[PvzSceneBuilder] XR Interaction Simulator sample not found.");
            }

            var locomotion = origin.transform.Find("Locomotion");
            if (locomotion == null) Debug.LogWarning("[PvzSceneBuilder] 'Locomotion' not found in the XR Origin prefab.");

            return (camera, simulator, locomotion != null ? locomotion.gameObject : null);
        }

        /// <summary>Stand-in for the player's real room, active only in the editor (see MrSessionSetup).</summary>
        private static GameObject CreateMockRoom()
        {
            var floorMat = Lit("MockFloor", new Color(0.52f, 0.47f, 0.42f));
            var wallMat = Lit("MockWall", new Color(0.86f, 0.85f, 0.82f));
            var woodMat = Lit("MockWood", new Color(0.55f, 0.38f, 0.25f));
            var bedMat = Lit("MockBed", new Color(0.36f, 0.48f, 0.66f));

            var room = new GameObject("Editor Mock Room") { tag = "EditorOnly" }.transform;
            const float half = 3.5f;
            const float height = 2.6f;
            Primitive(PrimitiveType.Cube, "Floor", room, new Vector3(0, -0.01f, 0), new Vector3(half * 2, 0.02f, half * 2), floorMat, true);
            Primitive(PrimitiveType.Cube, "Wall N", room, new Vector3(0, height / 2, half), new Vector3(half * 2, height, 0.1f), wallMat, true);
            Primitive(PrimitiveType.Cube, "Wall S", room, new Vector3(0, height / 2, -half), new Vector3(half * 2, height, 0.1f), wallMat, true);
            Primitive(PrimitiveType.Cube, "Wall E", room, new Vector3(half, height / 2, 0), new Vector3(0.1f, height, half * 2), wallMat, true);
            Primitive(PrimitiveType.Cube, "Wall W", room, new Vector3(-half, height / 2, 0), new Vector3(0.1f, height, half * 2), wallMat, true);

            var table = new GameObject("Table").transform;
            table.SetParent(room, false);
            table.localPosition = new Vector3(1.45f, 0f, -0.55f);
            Primitive(PrimitiveType.Cube, "Top", table, new Vector3(0, 0.72f, 0), new Vector3(0.9f, 0.04f, 0.6f), woodMat, true);
            foreach (var x in new[] { -0.4f, 0.4f })
            foreach (var z in new[] { -0.25f, 0.25f })
                Primitive(PrimitiveType.Cube, "Leg", table, new Vector3(x, 0.35f, z), new Vector3(0.04f, 0.7f, 0.04f), woodMat, true);

            var bed = new GameObject("Bed").transform;
            bed.SetParent(room, false);
            bed.localPosition = new Vector3(-1.55f, 0f, -1.45f);
            Primitive(PrimitiveType.Cube, "Frame", bed, new Vector3(0, 0.2f, 0), new Vector3(1.0f, 0.4f, 1.9f), woodMat, true);
            Primitive(PrimitiveType.Cube, "Mattress", bed, new Vector3(0, 0.44f, 0), new Vector3(0.96f, 0.08f, 1.86f), bedMat, true);

            return room.gameObject;
        }

        /// <summary>Always-on floor at y = 0 so boxes never fall forever when no plane is detected yet.</summary>
        private static void CreateFloorCollider()
        {
            var floor = new GameObject("Safety Floor Collider");
            floor.transform.position = new Vector3(0, -0.01f, 0);
            floor.AddComponent<BoxCollider>().size = new Vector3(30f, 0.02f, 30f);
        }

        private static Transform Marker(string name, Transform parent, Vector3 position, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }
    }
}
