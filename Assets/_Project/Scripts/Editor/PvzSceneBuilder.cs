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

        private const string SamplesRoot = "Assets/Samples/XR Interaction Toolkit/3.2.2";
        private const string XrOriginPrefabPath = SamplesRoot + "/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        private const string SimulatorPrefabPath = SamplesRoot + "/XR Interaction Simulator/XR Interaction Simulator.prefab";

        public static readonly Vector3 CounterPosition = new Vector3(0f, 0f, 1.0f);
        public static readonly Vector3 CustomerServicePosition = new Vector3(0f, 0f, 1.72f);
        public static readonly Vector3 CustomerSpawnPosition = new Vector3(0f, 0f, 3.1f);


        [MenuItem("PVZ/Build Scene And Assets")]
        public static void BuildAll()
        {
            // Create the scene first: NewScene(Single) unloads unused assets and would invalidate loaded prefabs.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnsureFolders();
            var font = CyrillicFont();
            var settings = LoadOrCreateSettings();
            var prefabs = new PvzPrefabBuilder(font);

            var layout = settings.Boxes; // single source of truth: ids, orders and start positions
            var boxPaths = layout.Select(b => prefabs.Box(b.boxId)).ToArray();
            var scannerPath = prefabs.Scanner();
            var counterPath = prefabs.Counter();
            var customerPath = prefabs.Customer();
            var planePath = prefabs.ArPlane();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var boxPrefabs = boxPaths.Select(Load<QrBox>).ToArray();
            var scannerPrefab = Load<HandScanner>(scannerPath);
            var counterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(counterPath);
            var customerPrefab = Load<Customer>(customerPath);
            var planePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(planePath);

            CreateLighting();
            var (camera, simulator) = CreateMrRig(planePrefab);
            var mockRoom = CreateMockRoom();
            CreateFloorCollider();

            var mrSetup = new GameObject("MR Session Setup").AddComponent<MrSessionSetup>();
            mrSetup.Configure(camera, mockRoom, simulator);

            var counter = (GameObject)PrefabUtility.InstantiatePrefab(counterPrefab);
            counter.transform.position = CounterPosition;

            var scanner = ((GameObject)PrefabUtility.InstantiatePrefab(scannerPrefab.gameObject)).GetComponent<HandScanner>();
            scanner.transform.SetPositionAndRotation(CounterPosition + new Vector3(-0.48f, PvzPrefabBuilder.CounterHeight + 0.04f, -0.05f),
                Quaternion.Euler(0f, -90f, 90f));

            var boxesRoot = new GameObject("Boxes").transform;
            var boxes = new List<QrBox>();
            for (var i = 0; i < layout.Length; i++)
            {
                var box = ((GameObject)PrefabUtility.InstantiatePrefab(boxPrefabs[i].gameObject, boxesRoot)).GetComponent<QrBox>();
                box.transform.SetPositionAndRotation(layout[i].startPosition, Quaternion.Euler(0f, layout[i].startYaw, 0f));
                boxes.Add(box);
            }

            var points = new GameObject("Customer Points").transform;
            var spawn = Marker("CustomerSpawn", points, CustomerSpawnPosition, 180f);
            var service = Marker("CustomerService", points, CustomerServicePosition, 180f);

            var director = new GameObject("Shift Director").AddComponent<ShiftDirector>();
            director.Configure(settings, boxes.ToArray(), scanner,
                counter.GetComponentInChildren<HandoverZone>(true),
                customerPrefab, spawn, service,
                counter.GetComponentInChildren<CounterBoard>(true),
                counter.GetComponentInChildren<ShiftSummaryPanel>(true),
                counter.GetComponentInChildren<RestartButton>(true).gameObject,
                counter.GetComponentInChildren<FeedbackAudio>(true));

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

        private static ShiftSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ShiftSettings>(SettingsPath);
            if (settings != null) return settings;
            settings = ScriptableObject.CreateInstance<ShiftSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
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

        private static (Camera camera, GameObject simulator) CreateMrRig(GameObject planePrefab)
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
            if (simulatorPrefab != null) simulator = (GameObject)PrefabUtility.InstantiatePrefab(simulatorPrefab);
            else Debug.LogWarning("[PvzSceneBuilder] XR Interaction Simulator sample not found.");

            return (camera, simulator);
        }

        /// <summary>Stand-in for the player's real room, active only in the editor (see MrSessionSetup).</summary>
        private static GameObject CreateMockRoom()
        {
            var floorMat = Lit("MockFloor", new Color(0.52f, 0.47f, 0.42f));
            var wallMat = Lit("MockWall", new Color(0.86f, 0.85f, 0.82f));
            var woodMat = Lit("MockWood", new Color(0.55f, 0.38f, 0.25f));
            var bedMat = Lit("MockBed", new Color(0.36f, 0.48f, 0.66f));

            var room = new GameObject("Editor Mock Room").transform;
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
