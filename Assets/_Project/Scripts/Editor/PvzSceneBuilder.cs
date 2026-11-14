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
            var settings = LoadOrCreateSettings(SettingsPath, s => s.ConfigureDifficulty("Обычная", 90f, 1, 50, 1.2f, 2f, 2, tutorial: false, returnChance: 20, urgentChance: 15, shiftSeconds: 420f, careerPercent: 100));
            var easy = LoadOrCreateSettings(EasySettingsPath, s => { s.CopyLayoutFrom(settings); s.ConfigureDifficulty("Лёгкая", 0f, 0, 0, 1.0f, 3f, 1, tutorial: true, returnChance: 0, urgentChance: 0, shiftSeconds: 0f, careerPercent: 50); });
            var hard = LoadOrCreateSettings(HardSettingsPath, s => { s.CopyLayoutFrom(settings); s.ConfigureDifficulty("Сложная", 45f, 1, 80, 1.6f, 1f, 3, tutorial: false, returnChance: 35, urgentChance: 25, shiftSeconds: 300f, careerPercent: 150); });
            var prefabs = new PvzPrefabBuilder(font);

            var scannerPath = prefabs.Scanner();
            var counterPath = prefabs.Counter();
            var customerPath = prefabs.Customer();
            var planePath = prefabs.ArPlane();
            var roomMeshChunkPath = prefabs.RoomMeshChunk();
            var supplyBoxPath = prefabs.SupplyBox();
            var bagPath = prefabs.Bag();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

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
            // T041: the room mesh (Meta Space Setup) as invisible colliders; also behind USE_SCENE, enabled by the gate.
            var roomMesh = CreateRoomMesh(AssetDatabase.LoadAssetAtPath<GameObject>(roomMeshChunkPath).GetComponent<MeshFilter>());
            mrSetup.gameObject.AddComponent<ScenePermissionGate>().Configure(planeManager, roomMesh.GetComponent<ARMeshManager>());

            // The counter and everything that belongs to it (computer, scanner, bags, the customers' path) is one rig that
            // the player can move around the room (T034); boxes in the room are not part of it.
            var rig = new GameObject("Counter Rig").transform;
            rig.SetParent(playArea, false);
            var counter = (GameObject)PrefabUtility.InstantiatePrefab(counterPrefab, rig);
            counter.transform.localPosition = CounterPosition;

            var scanner = ((GameObject)PrefabUtility.InstantiatePrefab(scannerPrefab.gameObject, rig)).GetComponent<HandScanner>();
            var scannerHolder = CreateScannerHolder(rig, scanner, font); // T038: the scanner lies in a holder and comes back to it when dropped

            // T036: no boxes stand in the room before the shift — the stock of every shift drops out of the supply hatch.
            var boxesRoot = new GameObject("Boxes").transform;
            boxesRoot.SetParent(playArea, false);

            var points = new GameObject("Customer Points").transform;
            points.SetParent(rig, false);
            var spawn = Marker("CustomerSpawn", points, CustomerSpawnPosition, 180f);
            var service = Marker("CustomerService", points, CustomerServicePosition, 180f);

            var dispenser = new GameObject("Bag Dispenser").AddComponent<BagDispenser>();
            dispenser.transform.SetParent(rig, false);
            dispenser.transform.SetPositionAndRotation(CounterPosition + new Vector3(-0.58f, PvzPrefabBuilder.CounterHeight + 0.17f, 0.18f),
                Quaternion.Euler(0f, 20f, 0f));
            dispenser.Configure(bagPrefab);

            var hatch = CreateSupplyHatch(playArea, supplyBoxPrefab, Object.FindFirstObjectByType<ARPlaneManager>(), boxesRoot, settings);

            // Shown before the shift only: the «Как играть» card. The keys are on the counter computer (T033).
            var startMenu = new GameObject("Start Menu").transform;
            startMenu.SetParent(rig, false);
            var pc = CreateComputer(counter.transform, font);

            var director = new GameObject("Shift Director").AddComponent<ShiftDirector>();
            var restart = CreateRestartKey(pc.Summary, font);
            director.Configure(settings, scanner,
                counter.GetComponentInChildren<HandoverZone>(true),
                customerPrefab, spawn, service,
                counter.GetComponentInChildren<CounterBoard>(true),
                counter.GetComponentInChildren<ShiftSummaryPanel>(true),
                restart,
                counter.GetComponentInChildren<FeedbackAudio>(true),
                hatch,
                CreateDifficultyButtons(pc.Menu, new[] { easy, settings, hard }, font));
            CreateComputerKey(pc.Menu, 4, "НАСТРОЙКИ", ComputerKey.Action.OpenSettings, pc.Computer, font, KeyGrey);
            CreateComputerKey(pc.Summary, 4, "НАСТРОЙКИ", ComputerKey.Action.OpenSettings, pc.Computer, font, KeyGrey);
            CreateComputerKey(pc.Shift, 0, "НАСТРОЙКИ", ComputerKey.Action.OpenSettings, pc.Computer, font, KeyGrey);
            var endShift = CreateComputerKey(pc.Shift, 4, PvzComputer.EndShiftCaption, ComputerKey.Action.EndShift, pc.Computer, font,
                new Color(0.92f, 0.35f, 0.3f));
            CreateComputerKey(pc.Settings, 4, "НАЗАД", ComputerKey.Action.Back, pc.Computer, font, KeyGrey);
            pc.Computer.Configure(director, pc.Screen, pc.Title, pc.Menu.gameObject, pc.Settings.gameObject,
                pc.SettingsBeforeShift.gameObject, pc.Shift.gameObject, pc.Summary.gameObject, endShift);
            // The counter can be moved at any time and its place in the room is kept (T034).
            var placement = new GameObject("Counter Placement").AddComponent<PlacementKeeper>();
            placement.Configure(rig, mrSetup);
            var mover = placement.gameObject.AddComponent<PlacementMover>();
            var counterTop = new Bounds(CounterPosition + new Vector3(0f, PvzPrefabBuilder.CounterHeight + 0.26f, 0f), new Vector3(1.5f, 0.5f, 0.65f));
            mover.Configure(placement, CreatePlacementHandle(playArea, "Counter Handle", CounterHandleOffset), CounterHandleOffset,
                counterTop.center, counterTop.size); // what lies on the top rides along
            scannerHolder.Configure(scanner, scannerHolder.transform.Find("Cradle"), mover, counterTop); // a scanner on the top stays there
            var movingKeys = new GameObject("Moving Keys").transform;
            movingKeys.SetParent(pc.Computer.transform, false);
            CreateComputerKey(pc.Settings, 0, "ПЕРЕНЕСТИ\nСТОЙКУ", ComputerKey.Action.MoveCounter, pc.Computer, font, new Color(0.98f, 0.55f, 0.15f));
            CreateComputerKey(movingKeys, 0, "ОТМЕНА", ComputerKey.Action.CancelMove, pc.Computer, font, KeyGrey);
            CreateComputerKey(movingKeys, 4, "ПРИМЕНИТЬ", ComputerKey.Action.ApplyMove, pc.Computer, font, new Color(0.35f, 0.8f, 0.45f));
            pc.Computer.ConfigureMover(mover, movingKeys.gameObject);
            director.ConfigureCounterPlacement(placement);
            CreateRecenterButton(pc.SettingsBeforeShift, mrSetup, placement, mover, font); // on the counter-moving screen, before the shift
            var counterBoard = counter.GetComponentInChildren<CounterBoard>(true);
            counterBoard.ConfigureHowToPlay(CreateHowToPlayCard(startMenu, font));
            PrefabUtility.RecordPrefabInstancePropertyModifications(counterBoard); // an override on the counter prefab instance
            director.ConfigureStartMenu(startMenu.gameObject);
            director.ConfigureReturns(supplyBoxPrefab); // returned boxes use the generic box with a runtime QR
            director.ConfigureEffects(counter.GetComponentInChildren<FeedbackEffects>(true));

            // The supply hatch is moved like the counter (T036): settings → «Место люка», a handle hanging from it.
            var hatchPlacement = hatch.gameObject.AddComponent<PlacementKeeper>();
            hatchPlacement.Configure(hatch.transform, mrSetup, Pvz.Core.PlacementStore.HatchKey, "люк",
                Pvz.Core.WarehouseBounds.MaxReach - 1f); // its boxes must land within reach (a box farther is lost)
            var hatchMover = hatch.gameObject.AddComponent<PlacementMover>();
            hatchMover.Configure(hatchPlacement, CreatePlacementHandle(playArea, "Hatch Handle", hatch.transform.TransformPoint(HatchHandleOffset)),
                HatchHandleOffset);
            pc.Computer.ConfigureHatchMover(hatchMover);
            CreateComputerKey(pc.Settings, 3, "МЕСТО\nЛЮКА", ComputerKey.Action.MoveHatch, pc.Computer, font, new Color(0.98f, 0.55f, 0.15f));

            // The shelf rack (T037): placed and moved like the counter; boxes lying on its shelves ride along.
            var rack = CreateShelfRack(playArea);
            var rackPlacement = rack.gameObject.AddComponent<PlacementKeeper>();
            rackPlacement.Configure(rack, mrSetup, Pvz.Core.PlacementStore.RackKey, "стеллаж", Pvz.Core.WarehouseBounds.MaxReach - 1f);
            var rackMover = rack.gameObject.AddComponent<PlacementMover>();
            var rackTop = ShelfHeights[ShelfHeights.Length - 1] + 0.3f;
            rackMover.Configure(rackPlacement, CreatePlacementHandle(playArea, "Rack Handle", rack.TransformPoint(RackHandleOffset)), RackHandleOffset,
                new Vector3(0f, rackTop / 2f + 0.02f, 0f), new Vector3(RackWidth - 0.1f, rackTop, RackDepth - 0.08f)); // on the shelves, not beside the posts
            pc.Computer.ConfigureRackMover(rackMover);
            pc.Computer.ConfigureRoomMesh(roomMesh); // the menu says when the room was not scanned
            CreateComputerKey(pc.Settings, 1, "ПЕРЕНЕСТИ\nСТЕЛЛАЖ", ComputerKey.Action.MoveRack, pc.Computer, font, new Color(0.98f, 0.55f, 0.15f));
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
            Debug.Log($"[PvzSceneBuilder] Built {ScenePath}: the stock drops out of the supply hatch.");
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
        private static void CreateRecenterButton(Transform group, MrSessionSetup session, PlacementKeeper placement, PlacementMover mover,
            TMPro.TMP_FontAsset font)
        {
            var go = CreateKey(group, 1, "Recenter Button", "СТОЙКУ\nКО МНЕ", new Color(0.35f, 0.6f, 0.95f), font, out _);
            go.AddComponent<RecenterButton>().Configure(session, placement, mover);
        }

        /// <summary>Where the handle of the counter hangs, relative to the counter rig: above the front edge, in reach.</summary>
        public static readonly Vector3 CounterHandleOffset = CounterPosition + new Vector3(0f, 1.42f, -0.5f);

        /// <summary>
        /// The handle to carry the counter (T034): a big orange knob with a ring, grabbed with grip, kinematic — it follows
        /// the hand, the counter follows it. Lives outside the rig (the rig moves after it), hidden until «Переместить стойку».
        /// </summary>
        private static UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable CreatePlacementHandle(Transform playArea,
            string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(playArea, false);
            go.transform.position = position;
            var orange = Lit("CounterHandle", new Color(0.98f, 0.55f, 0.15f), 0.6f);
            Primitive(PrimitiveType.Sphere, "Knob", go.transform, Vector3.zero, Vector3.one * 0.12f, orange, true);
            Primitive(PrimitiveType.Cylinder, "Ring", go.transform, Vector3.zero, new Vector3(0.22f, 0.006f, 0.22f), orange);
            Primitive(PrimitiveType.Cylinder, "Stem", go.transform, new Vector3(0f, -0.2f, 0f), new Vector3(0.02f, 0.2f, 0.02f),
                Lit("CounterHandleStem", new Color(0.3f, 0.3f, 0.32f)));
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var grab = go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true; // held where the ray caught it
            grab.farAttachMode = UnityEngine.XR.Interaction.Toolkit.Attachment.InteractableFarAttachMode.Far; // stays at arm's length
            go.SetActive(false);
            return grab;
        }

        /// <summary>«Тема» (settings, also during the shift): the counter theme opened by the career (T022).</summary>
        private static ThemeButton CreateThemeButton(Transform group, CareerKeeper keeper, TMPro.TMP_FontAsset font)
        {
            var go = CreateKey(group, 2, "Theme Button", "ТЕМА:\nКЛАССИКА", new Color(0.62f, 0.5f, 0.95f), font, out var label);
            var button = go.AddComponent<ThemeButton>();
            button.Configure(keeper, label);
            return button;
        }

        /// <summary>Where the supply hatch is at first (play area): to the left of the player, clear of the counter and the line.</summary>
        public static readonly Vector3 HatchPosition = new Vector3(-1.45f, 0f, 0.45f);

        /// <summary>The hatch's handle hangs on a cord at hand height (relative to the hatch rig on the floor).</summary>
        public static readonly Vector3 HatchHandleOffset = new Vector3(0f, 1.55f, 0f);

        /// <summary>
        /// The supply hatch (T036): a dark square with an orange frame and two doors under the ceiling, a cord down to an
        /// orange handle. The rig stands on the floor (it is moved like the counter); the panel is lifted to the ceiling.
        /// </summary>
        private static SupplyHatch CreateSupplyHatch(Transform playArea, QrBox boxPrefab, ARPlaneManager planes, Transform boxesRoot,
            ShiftSettings settings)
        {
            var root = new GameObject("Supply Hatch");
            root.transform.SetParent(playArea, false);
            root.transform.localPosition = HatchPosition;
            var orange = Lit("HatchFrame", new Color(0.98f, 0.55f, 0.15f));
            var dark = Unlit("HatchHole", new Color(0.06f, 0.06f, 0.08f));
            var door = Lit("HatchDoor", new Color(0.42f, 0.44f, 0.48f));
            var panel = new GameObject("Panel").transform;
            panel.SetParent(root.transform, false);
            const float size = 0.8f, bar = 0.06f;
            foreach (var (x, z, sx, sz) in new[] { (0f, size / 2f, size + bar, bar), (0f, -size / 2f, size + bar, bar), (size / 2f, 0f, bar, size), (-size / 2f, 0f, bar, size) })
                Primitive(PrimitiveType.Cube, "Frame", panel, new Vector3(x, 0f, z), new Vector3(sx, 0.05f, sz), orange);
            Primitive(PrimitiveType.Cube, "Hole", panel, new Vector3(0f, 0.02f, 0f), new Vector3(size, 0.01f, size), dark);
            Transform Door(string name, float side)
            {
                var hinge = new GameObject(name).transform;
                hinge.SetParent(panel, false);
                hinge.localPosition = new Vector3(side * size / 2f, -0.01f, 0f);
                Primitive(PrimitiveType.Cube, "Leaf", hinge, new Vector3(-side * size / 4f, 0f, 0f), new Vector3(size / 2f - 0.01f, 0.02f, size - 0.02f), door);
                return hinge;
            }
            var left = Door("Door Left", -1f);
            var right = Door("Door Right", 1f);
            var cord = Primitive(PrimitiveType.Cylinder, "Cord", root.transform, Vector3.zero, new Vector3(0.012f, 0.5f, 0.012f),
                Lit("HatchCord", new Color(0.2f, 0.2f, 0.22f))).transform;
            var hatch = root.AddComponent<SupplyHatch>();
            hatch.Configure(boxPrefab, planes, boxesRoot, panel, left, right, cord, HatchHandleOffset.y);
            hatch.FitToCeiling(settings.DefaultCeilingHeight); // the editor has no AR ceiling: the default height
            return hatch;
        }

        /// <summary>Where the shelf rack stands at first (play area): to the right of the player, its shelves facing them.</summary>
        public static readonly Vector3 RackPosition = new Vector3(1.45f, 0f, 0.35f);
        public const float RackYaw = 90f;

        /// <summary>The rack's handle: above the middle of its top shelf, in reach (rack space).</summary>
        public static readonly Vector3 RackHandleOffset = new Vector3(0f, 1.75f, -0.1f);

        /// <summary>Heights of the shelf boards (their tops), metres.</summary>
        public static readonly float[] ShelfHeights = { 0.05f, 0.5f, 0.95f, 1.4f };
        public const float RackWidth = 1.0f, RackDepth = 0.45f;

        /// <summary>
        /// The shelf rack (T037, #81): four shelves on four posts, a low lip at the front and back of each shelf so a box does
        /// not slide off, a grippy surface. The player arranges boxes on it; it is moved like the counter.
        /// </summary>
        /// <summary>
        /// T041 (#82): the room mesh from the headset. ARMeshManager must be a child of the XR Origin (the meshes come in
        /// session space); saved disabled — Meta gives the mesh only with USE_SCENE, the gate enables it.
        /// </summary>
        private static RoomMesh CreateRoomMesh(MeshFilter chunk)
        {
            var origin = Object.FindFirstObjectByType<XROrigin>();
            var go = new GameObject("Room Mesh");
            go.transform.SetParent(origin.transform, false);
            var manager = go.AddComponent<ARMeshManager>();
            manager.meshPrefab = chunk;
            manager.normals = false; // colliders only, nothing is drawn
            manager.enabled = false;
            var room = go.AddComponent<RoomMesh>();
            room.Configure(manager);
            return room;
        }

        /// <summary>Where the scanner lies on the counter (counter rig space): on its side, the nose to the left.</summary>
        public static readonly Vector3 ScannerHolderPosition = CounterPosition + new Vector3(-0.54f, PvzPrefabBuilder.CounterHeight, -0.05f);

        /// <summary>
        /// T038 (#76): a dark tray marked «СКАНЕР» on the counter top. The scanner starts in it, clicks into it
        /// when let go nearby, and returns to it when dropped on the floor or lost.
        /// </summary>
        private static ScannerHolder CreateScannerHolder(Transform rig, HandScanner scanner, TMPro.TMP_FontAsset font)
        {
            var root = new GameObject("Scanner Holder").transform;
            root.SetParent(rig, false);
            root.localPosition = ScannerHolderPosition;
            var dark = Lit("ScannerHolder", new Color(0.16f, 0.17f, 0.19f), 0.35f);
            // The scanner's footprint lying on its side: nose at -x, grip away from the player (+z); the front strip is free for the label,
            // the tray stops short of the computer's «Настройки» key in front of it, and its rims stay below the bag dispenser's spot check.
            Primitive(PrimitiveType.Cube, "Tray", root, new Vector3(-0.0125f, 0.003f, 0.03f), new Vector3(0.195f, 0.006f, 0.2f), dark, true);
            foreach (var side in new[] { -1f, 1f })
                Primitive(PrimitiveType.Cube, "Stop", root, new Vector3(-0.0125f + side * 0.1025f, 0.008f, 0.03f), new Vector3(0.01f, 0.016f, 0.2f), dark, true);
            Text("Label", root, new Vector3(-0.0125f, 0.0065f, -0.053f), new Vector2(0.17f, 0.026f), "СКАНЕР", font, new Color(0.98f, 0.8f, 0.3f), 0.3f,
                localRot: Quaternion.Euler(90f, 0f, 0f)); // lies flat in front of the scanner, read by the player

            var cradle = new GameObject("Cradle").transform;
            cradle.SetParent(root, false);
            cradle.localPosition = new Vector3(0f, 0.044f, 0f);
            cradle.localRotation = Quaternion.Euler(0f, -90f, 90f);
            scanner.transform.SetPositionAndRotation(cradle.position, cradle.rotation);

            return root.gameObject.AddComponent<ScannerHolder>(); // configured once the counter mover exists
        }

        private static Transform CreateShelfRack(Transform playArea)
        {
            var root = new GameObject("Shelf Rack").transform;
            root.SetParent(playArea, false);
            root.localPosition = RackPosition;
            root.localRotation = Quaternion.Euler(0f, RackYaw, 0f);
            var metal = Lit("RackMetal", new Color(0.36f, 0.38f, 0.42f), 0.5f);
            var board = Lit("RackShelf", new Color(0.84f, 0.73f, 0.55f));
            var grip = PhysicMaterialAsset("ShelfBoard", 0.8f, 0.95f, 0f);
            var top = ShelfHeights[ShelfHeights.Length - 1] + 0.25f;
            foreach (var (x, z) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
                Primitive(PrimitiveType.Cube, "Post", root, new Vector3(x * (RackWidth / 2f - 0.02f), top / 2f, z * (RackDepth / 2f - 0.02f)),
                    new Vector3(0.04f, top, 0.04f), metal, true);
            for (var i = 0; i < ShelfHeights.Length; i++)
            {
                var y = ShelfHeights[i];
                var shelf = Primitive(PrimitiveType.Cube, $"Shelf {i + 1}", root, new Vector3(0f, y - 0.015f, 0f),
                    new Vector3(RackWidth, 0.03f, RackDepth), board, true);
                shelf.GetComponent<Collider>().sharedMaterial = grip;
                foreach (var side in new[] { -1f, 1f })
                    Primitive(PrimitiveType.Cube, "Lip", root, new Vector3(0f, y + 0.015f, side * (RackDepth / 2f - 0.01f)),
                        new Vector3(RackWidth, 0.03f, 0.02f), metal, true);
            }
            return root;
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
