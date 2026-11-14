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
            var settings = LoadOrCreateSettings(SettingsPath, s => s.ConfigureDifficulty("Обычная", 90f, 1, 50, 1.2f, 2f, 2, tutorial: false, returnChance: 20, urgentChance: 15, forgotNumberChance: 15, shiftSeconds: 420f, careerPercent: 100));
            var easy = LoadOrCreateSettings(EasySettingsPath, s => { s.CopyLayoutFrom(settings); s.ConfigureDifficulty("Лёгкая", 0f, 0, 0, 1.0f, 3f, 1, tutorial: true, returnChance: 0, urgentChance: 0, forgotNumberChance: 0, shiftSeconds: 0f, careerPercent: 50); });
            var hard = LoadOrCreateSettings(HardSettingsPath, s => { s.CopyLayoutFrom(settings); s.ConfigureDifficulty("Сложная", 45f, 1, 80, 1.6f, 1f, 3, tutorial: false, returnChance: 35, urgentChance: 25, forgotNumberChance: 20, shiftSeconds: 300f, careerPercent: 150); });
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

            // Shown before the shift only: the «Как играть» card. The keys are on the counter computer (T033).
            var startMenu = new GameObject("Start Menu").transform;
            startMenu.SetParent(playArea, false);
            var pc = CreateComputer(counter.transform, font);

            var director = new GameObject("Shift Director").AddComponent<ShiftDirector>();
            var restart = CreateRestartKey(pc.Summary, font);
            director.Configure(settings, boxes.ToArray(), scanner,
                counter.GetComponentInChildren<HandoverZone>(true),
                customerPrefab, spawn, service,
                counter.GetComponentInChildren<CounterBoard>(true),
                counter.GetComponentInChildren<ShiftSummaryPanel>(true),
                restart,
                counter.GetComponentInChildren<FeedbackAudio>(true),
                supplyDrop,
                CreateDifficultyButtons(pc.Menu, new[] { easy, settings, hard }, font));
            CreateComputerKey(pc.Menu, 4, "НАСТРОЙКИ", ComputerKey.Action.OpenSettings, pc.Computer, font, KeyGrey);
            CreateComputerKey(pc.Summary, 4, "НАСТРОЙКИ", ComputerKey.Action.OpenSettings, pc.Computer, font, KeyGrey);
            CreateComputerKey(pc.Shift, 0, "НАСТРОЙКИ", ComputerKey.Action.OpenSettings, pc.Computer, font, KeyGrey);
            var endShift = CreateComputerKey(pc.Shift, 4, PvzComputer.EndShiftCaption, ComputerKey.Action.EndShift, pc.Computer, font,
                new Color(0.92f, 0.35f, 0.3f));
            CreateComputerKey(pc.Settings, 4, "НАЗАД", ComputerKey.Action.Back, pc.Computer, font, KeyGrey);
            pc.Computer.Configure(director, pc.Screen, pc.Title, pc.Menu.gameObject, pc.Settings.gameObject,
                pc.SettingsBeforeShift.gameObject, pc.Shift.gameObject, pc.Summary.gameObject, endShift);
            CreateRecenterButton(pc.SettingsBeforeShift, mrSetup, font);
            var counterBoard = counter.GetComponentInChildren<CounterBoard>(true);
            counterBoard.ConfigureHowToPlay(CreateHowToPlayCard(startMenu, font));
            PrefabUtility.RecordPrefabInstancePropertyModifications(counterBoard); // an override on the counter prefab instance
            director.ConfigureStartMenu(startMenu.gameObject);
            director.ConfigureReturns(supplyBoxPrefab); // returned boxes use the generic box with a runtime QR
            director.ConfigureEffects(counter.GetComponentInChildren<FeedbackEffects>(true));

            // The player's arrangement of the stock boxes is kept between sessions (T017).
            var keeper = new GameObject("Warehouse Layout").AddComponent<WarehouseLayoutKeeper>();
            keeper.Configure(boxes.ToArray(), counter.GetComponentInChildren<HandoverZone>(true));
            CreateResetWarehouseButton(pc.SettingsBeforeShift, keeper, font);
            // The career between shifts and the counter themes it opens (T022).
            var careerKeeper = new GameObject("Career").AddComponent<CareerKeeper>();
            careerKeeper.Configure(LoadOrCreateThemeCatalog(), counter.GetComponentInChildren<CounterSkin>(true),
                CreateThemeButton(pc.Settings, careerKeeper, font));
            director.ConfigureCareer(careerKeeper);

            // Saved as the menu screen; done last, so every key label was laid out while its group was still active.
            pc.Computer.Preview(PvzComputer.Screen.Menu, PvzComputer.MenuText, beforeShift: true);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[PvzSceneBuilder] Built {ScenePath} with {boxes.Count} boxes.");
        }

        public const string ThemeCatalogPath = "Assets/_Project/Settings/CounterThemes.asset";

        /// <summary>Counter themes of the career; created once, then left to designers (thresholds, names, colours).</summary>
        private static CounterThemeCatalog LoadOrCreateThemeCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CounterThemeCatalog>(ThemeCatalogPath);
            if (catalog != null) return catalog;
            catalog = ScriptableObject.CreateInstance<CounterThemeCatalog>();
            catalog.Configure(new[]
            {
                new CounterThemeCatalog.Entry { id = "classic", displayName = "Классика", unlockPoints = 0, recolor = false },
                new CounterThemeCatalog.Entry { id = "mint", displayName = "Мята", unlockPoints = 1000, recolor = true,
                    body = new Color(0.14f, 0.34f, 0.31f), top = new Color(0.88f, 0.95f, 0.9f), accent = new Color(0.35f, 0.85f, 0.7f) },
                new CounterThemeCatalog.Entry { id = "night", displayName = "Ночь", unlockPoints = 2500, recolor = true,
                    body = new Color(0.08f, 0.08f, 0.15f), top = new Color(0.3f, 0.32f, 0.42f), accent = new Color(0.58f, 0.46f, 1f) },
                new CounterThemeCatalog.Entry { id = "gold", displayName = "Золото", unlockPoints = 5000, recolor = true,
                    body = new Color(0.27f, 0.19f, 0.08f), top = new Color(0.95f, 0.86f, 0.58f), accent = new Color(1f, 0.8f, 0.2f) },
            });
            AssetDatabase.CreateAsset(catalog, ThemeCatalogPath);
            return catalog;
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

        /// <summary>
        /// «Как играть» to the left of the board (mirrors the order list on the right): the loop in five steps, for the
        /// first run on a headset without anyone to explain. Part of the start menu, so it is hidden when the shift starts (T024).
        /// </summary>
        private static TMPro.TMP_Text CreateHowToPlayCard(Transform parent, TMPro.TMP_FontAsset font)
        {
            var go = new GameObject("How To Play");
            go.transform.SetParent(parent, false);
            go.transform.position = CounterPosition + new Vector3(-1.2f, 2.14f, 0.08f);
            go.transform.rotation = Quaternion.Euler(0f, 18f, 0f);
            Primitive(PrimitiveType.Cube, "Panel", go.transform, Vector3.zero, new Vector3(0.74f, 0.62f, 0.03f),
                Unlit("HowToPlayPanel", new Color(0.07f, 0.09f, 0.13f)));
            Primitive(PrimitiveType.Cube, "Frame", go.transform, new Vector3(0, -0.325f, 0), new Vector3(0.74f, 0.03f, 0.035f),
                Lit("HowToPlayFrame", new Color(0.35f, 0.6f, 0.95f)));
            var text = Text("Text", go.transform, new Vector3(0, 0, -0.02f), new Vector2(0.68f, 0.56f), ShiftPresenter.HowToPlayText(),
                font, new Color(0.88f, 0.9f, 0.94f), 0.8f, TMPro.TextAlignmentOptions.TopLeft);
            text.textWrappingMode = TMPro.TextWrappingModes.NoWrap; // one step per line: auto-size shrinks instead of wrapping
            return text; // the presenter writes the text at runtime (ShowDifficultyMenu)
        }

        // ---------- The counter computer (T033, #77) ----------

        private static readonly Color KeyGrey = new Color(0.72f, 0.76f, 0.82f);
        private static readonly Color KeyLabel = new Color(0.08f, 0.1f, 0.14f);

        /// <summary>Where key <paramref name="slot"/> (0..4) of the computer sits: a row on the player side of the counter.</summary>
        public static Vector3 KeySlot(int slot) =>
            CounterPosition + new Vector3(-0.40f + slot * 0.095f, PvzPrefabBuilder.CounterHeight + 0.025f, -0.2f);

        private readonly struct ComputerParts
        {
            public ComputerParts(PvzComputer computer, TMPro.TMP_Text screen, TMPro.TMP_Text title, Transform menu, Transform settings,
                Transform settingsBeforeShift, Transform shift, Transform summary)
            {
                Computer = computer;
                Screen = screen;
                Title = title;
                Menu = menu;
                Settings = settings;
                SettingsBeforeShift = settingsBeforeShift;
                Shift = shift;
                Summary = summary;
            }

            public PvzComputer Computer { get; }
            public TMPro.TMP_Text Screen { get; }
            public TMPro.TMP_Text Title { get; }
            public Transform Menu { get; }
            public Transform Settings { get; }
            public Transform SettingsBeforeShift { get; }
            public Transform Shift { get; }
            public Transform Summary { get; }
        }

        /// <summary>
        /// A chunky monitor on the counter (a box with a bulging back, like an old terminal), turned to the player, and the
        /// groups of keys its screens switch between. It is part of the counter, so it moves with it.
        /// </summary>
        private static ComputerParts CreateComputer(Transform counter, TMPro.TMP_FontAsset font)
        {
            var root = new GameObject("Computer");
            root.transform.SetParent(counter, false);
            root.transform.position = CounterPosition + new Vector3(-0.14f, PvzPrefabBuilder.CounterHeight, 0.13f); // clear of the bag dispenser
            var body = Lit("ComputerBody", new Color(0.86f, 0.87f, 0.84f));
            Primitive(PrimitiveType.Cube, "Foot", root.transform, new Vector3(0f, 0.01f, 0f), new Vector3(0.2f, 0.02f, 0.14f), body, true);
            Primitive(PrimitiveType.Cube, "Neck", root.transform, new Vector3(0f, 0.06f, 0.02f), new Vector3(0.05f, 0.09f, 0.05f), body);
            var head = new GameObject("Monitor").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0f, 0.25f, 0f);
            head.localRotation = Quaternion.Euler(-8f, 0f, 0f); // tilted back a little, towards the eyes of the player
            Primitive(PrimitiveType.Cube, "Case", head, Vector3.zero, new Vector3(0.46f, 0.32f, 0.06f), body, true);
            Primitive(PrimitiveType.Cube, "Back", head, new Vector3(0f, 0.01f, 0.08f), new Vector3(0.36f, 0.24f, 0.12f), body, true);
            Primitive(PrimitiveType.Sphere, "Bulge", head, new Vector3(0f, 0.01f, 0.13f), new Vector3(0.3f, 0.2f, 0.12f), body);
            Primitive(PrimitiveType.Cube, "Screen", head, new Vector3(0f, 0.005f, -0.031f), new Vector3(0.41f, 0.27f, 0.004f),
                Unlit("ComputerScreen", new Color(0.05f, 0.09f, 0.12f)));
            var title = Text("Title", head, new Vector3(0f, 0.115f, -0.034f), new Vector2(0.39f, 0.04f), "ПВЗ", font,
                new Color(0.45f, 0.95f, 0.6f), 0.35f, TMPro.TextAlignmentOptions.Left);
            var screen = Text("Text", head, new Vector3(0f, -0.015f, -0.034f), new Vector2(0.39f, 0.21f), string.Empty, font,
                new Color(0.82f, 0.95f, 0.86f), 0.3f, TMPro.TextAlignmentOptions.TopLeft);
            var computer = root.AddComponent<PvzComputer>();

            Transform Group(string name)
            {
                var group = new GameObject(name).transform;
                group.SetParent(root.transform, false);
                return group;
            }
            return new ComputerParts(computer, screen, title, Group("Menu Keys"), Group("Settings Keys"),
                Group("Settings Before Shift Keys"), Group("Shift Keys"), Group("Summary Keys"));
        }

        /// <summary>A key cap in a slot of the computer row, with a label on top. Returns the key object.</summary>
        private static GameObject CreateKey(Transform group, int slot, string name, string caption, Color color, TMPro.TMP_FontAsset font,
            out TMPro.TMP_Text label, string material = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(group, false);
            go.transform.position = KeySlot(slot);
            Primitive(PrimitiveType.Cube, "Cap", go.transform, Vector3.zero, new Vector3(0.088f, 0.04f, 0.1f), Lit(material ?? name.Replace(" ", ""), color), true);
            label = Text("Label", go.transform, new Vector3(0, 0.0215f, 0), new Vector2(0.084f, 0.094f), caption, font, KeyLabel, 0.36f,
                localRot: Quaternion.Euler(90, 0, 0));
            go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            return go;
        }

        private static TMPro.TMP_Text CreateComputerKey(Transform group, int slot, string caption, ComputerKey.Action action,
            PvzComputer computer, TMPro.TMP_FontAsset font, Color color)
        {
            var go = CreateKey(group, slot, $"Key {action}", caption, color, font, out var label);
            go.AddComponent<ComputerKey>().Configure(computer, action);
            return label;
        }

        /// <summary>«Новая смена» on the summary screen of the computer.</summary>
        private static GameObject CreateRestartKey(Transform group, TMPro.TMP_FontAsset font)
        {
            var go = CreateKey(group, 0, "RestartButton", "НОВАЯ\nСМЕНА", new Color(0.35f, 0.8f, 0.45f), font, out _);
            go.AddComponent<RestartButton>();
            return go;
        }

        /// <summary>«Стойку ко мне» (settings, before the shift): moves the play area in front of the player.</summary>
        private static void CreateRecenterButton(Transform group, MrSessionSetup session, TMPro.TMP_FontAsset font)
        {
            var go = CreateKey(group, 0, "Recenter Button", "СТОЙКУ\nКО МНЕ", new Color(0.35f, 0.6f, 0.95f), font, out _);
            go.AddComponent<RecenterButton>().Configure(session);
        }

        /// <summary>«Сбросить склад» (settings, before the shift): boxes back to their first places, saved layout forgotten (T020).</summary>
        private static void CreateResetWarehouseButton(Transform group, WarehouseLayoutKeeper keeper, TMPro.TMP_FontAsset font)
        {
            var go = CreateKey(group, 1, "Reset Warehouse Button", "СБРОСИТЬ\nСКЛАД", new Color(0.95f, 0.6f, 0.25f), font, out _);
            go.AddComponent<ResetWarehouseButton>().Configure(keeper);
        }

        /// <summary>«Тема» (settings, also during the shift): the counter theme opened by the career (T022).</summary>
        private static ThemeButton CreateThemeButton(Transform group, CareerKeeper keeper, TMPro.TMP_FontAsset font)
        {
            var go = CreateKey(group, 2, "Theme Button", "ТЕМА:\nКЛАССИКА", new Color(0.62f, 0.5f, 0.95f), font, out var label);
            var button = go.AddComponent<ThemeButton>();
            button.Configure(keeper, label);
            return button;
        }

        /// <summary>The menu screen of the computer: three difficulty keys in slots 0–2.</summary>
        private static DifficultyButton[] CreateDifficultyButtons(Transform group, ShiftSettings[] presets, TMPro.TMP_FontAsset font)
        {
            var colors = new[] { new Color(0.35f, 0.8f, 0.45f), new Color(0.95f, 0.75f, 0.2f), new Color(0.92f, 0.35f, 0.3f) };
            var buttons = new DifficultyButton[presets.Length];
            for (var i = 0; i < presets.Length; i++)
            {
                var go = CreateKey(group, i, $"Difficulty {presets[i].DisplayName}", presets[i].DisplayName.ToUpperInvariant(),
                    colors[i % colors.Length], font, out _, material: $"Difficulty{i}");
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
