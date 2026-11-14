using System.IO;
using Pvz.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pvz.Editor
{
    /// <summary>
    /// Renders preview images of the generated scene into docs/screenshots (used in PR descriptions).
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Pvz.Editor.PvzScreenshots.CaptureAll
    /// Must run without -nographics. The scene is not saved.
    /// </summary>
    public static class PvzScreenshots
    {
        private const string OutputDir = "docs/screenshots";
        private const int Width = 1600;
        private const int Height = 900;

        [MenuItem("PVZ/Capture Screenshots")]
        public static void CaptureAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            // Otherwise shaders not compiled yet render as the cyan async-compilation placeholder.
            var asyncCompilation = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try { CaptureAllInternal(); }
            finally { ShaderUtil.allowAsyncCompilation = asyncCompilation; }
        }

        private static void CaptureAllInternal()
        {
            Directory.CreateDirectory(OutputDir);
            var scene = EditorSceneManager.OpenScene(PvzSceneBuilder.ScenePath, OpenSceneMode.Single);
            HideEditorOnlyClutter();

            // Before the shift: the difficulty menu on the counter.
            ShiftPresenter.ShowDifficultyMenu(Object.FindFirstObjectByType<CounterBoard>(),
                new[] { ("Лёгкая", 450), ("Обычная", 525), ("Сложная", 0) });
            Capture("00_difficulty_menu", new Vector3(0f, 1.65f, -0.35f), new Vector3(-0.1f, 1.5f, 1.1f), 75f);
            Capture("16_how_to_play", new Vector3(-0.75f, 1.8f, -0.3f), new Vector3(-1.2f, 2.14f, 1.08f), 45f);

            // The counter computer (T033): its screens and the keys each one shows.
            var computer = Object.FindFirstObjectByType<PvzComputer>();
            computer.Preview(PvzComputer.Screen.Menu, PvzComputer.MenuText, beforeShift: true);
            Capture("17_computer_menu", ComputerCameraFrom, ComputerCameraAt, 48f);
            computer.Preview(PvzComputer.Screen.Settings, PvzComputer.SettingsText, beforeShift: true);
            Capture("18_computer_settings", ComputerCameraFrom, ComputerCameraAt, 48f);
            // Moving the counter (T034): the orange handle above its front edge and the moving screen.
            var handle = Object.FindFirstObjectByType<CounterMover>().Handle.gameObject;
            handle.SetActive(true);
            computer.Preview(PvzComputer.Screen.MovingCounter, PvzComputer.MovingText, beforeShift: true);
            Capture("21_counter_move", new Vector3(-0.95f, 1.85f, -0.55f), new Vector3(0.05f, 1.25f, 0.75f), 60f);
            handle.SetActive(false);
            computer.Preview(PvzComputer.Screen.Shift, PvzComputer.OrderScreen("2781", 2, 1, false, null, 2, 263, false), beforeShift: false);

            // Stage a moment of play: a customer at the counter asking for an order.
            var customerPrefab = AssetDatabase.LoadAssetAtPath<Customer>(PvzAssetFactory.PrefabsDir + "/Customer.prefab");
            var customer = Object.Instantiate(customerPrefab, PvzSceneBuilder.CustomerServicePosition, Quaternion.Euler(0, 180, 0));
            customer.SetLook(0);
            customer.ShowPatience(0.55); // waited a while: the bar is yellow
            customer.Say("Здравствуйте!\nЗаказ №2781");
            var board = Object.FindFirstObjectByType<CounterBoard>();
            board.ShowHeadline("Заказ №2781", CounterBoard.Accent);
            board.ShowStatus("Найдите коробку", CounterBoard.Neutral);
            board.ShowScore(150, 1, 3);
            board.ShowOrders(DemoOrderList());
            var scanner = Object.FindFirstObjectByType<HandScanner>();
            scanner.ShowResult("Заказ №2781\nНУЖНАЯ КОРОБКА", CounterBoard.Good);

            HideEditorOnlyClutter();
            Object.FindFirstObjectByType<BagDispenser>().Refill(); // a bag lies on the counter as in play mode

            Capture("01_overview", new Vector3(-2.6f, 2.5f, -2.9f), new Vector3(0.1f, 0.8f, 0.6f), 55f);
            Capture("02_player_view", new Vector3(0f, 1.6f, -0.2f), new Vector3(0.1f, 1.62f, 1.6f), 72f);
            Capture("19_computer_shift", ComputerCameraFrom, ComputerCameraAt, 48f);
            Capture("03_counter_closeup", new Vector3(0.35f, 1.55f, 0.15f), new Vector3(0.15f, 1.2f, 1.1f), 60f);

            var box = Object.FindFirstObjectByType<QrBox>();
            Capture("04_box_qr", box.transform.position + new Vector3(0.35f, 0.35f, -0.45f), box.transform.position, 45f);
            Capture("05_scanner", scanner.transform.position + new Vector3(-0.18f, 0.22f, -0.25f), scanner.transform.position, 40f);

            // «Похожие заказы»: the customer wants №2781, the scanner caught a box of №2718.
            var (lookAlikeText, lookAlikeColor) = ShiftPresenter.ScanResultText(new Pvz.Core.ScanOutcome(Pvz.Core.ScanStatus.OtherOrder, new Pvz.Core.BoxId("BOX-103"), "2718", looksLikeTheWantedOrder: true));
            scanner.ShowResult(lookAlikeText, lookAlikeColor);
            // Straight at the scanner screen (TMP text reads from behind its forward axis).
            var screen = scanner.GetComponentInChildren<TMP_Text>(true).transform;
            Capture("13_lookalike", screen.position - screen.forward * 0.2f, screen.position, 35f, screen.up);
            scanner.ShowResult("Заказ №2781\nНУЖНАЯ КОРОБКА", CounterBoard.Good);

            // Two-box order handed over; the customer now asks for a bag.
            var heldBoxes = Object.FindObjectsByType<QrBox>(FindObjectsSortMode.InstanceID);
            var demoCustomer = Object.Instantiate(customerPrefab, PvzSceneBuilder.CustomerServicePosition,
                Quaternion.Euler(0, 180, 0));
            demoCustomer.SetLook(2);
            customer.gameObject.SetActive(false);
            var originalPoses = new (Transform t, Transform parent, Vector3 pos, Quaternion rot)[2];
            var stacks = new float[CarryStacks.ColumnX.Length];
            for (var i = 0; i < 2 && i < heldBoxes.Length; i++)
            {
                var t = heldBoxes[i].transform;
                originalPoses[i] = (t, t.parent, t.position, t.rotation);
                t.SetParent(demoCustomer.CarryAnchor, false);
                // Laid out as Customer.Take does, without handing the box over.
                t.localPosition = CarryStacks.Place(stacks, PvzPrefabBuilder.BoxSize.y);
                t.localRotation = Quaternion.identity;
            }
            demoCustomer.Say("Спасибо! Мне ещё\nнужен пакет");
            board.ShowHeadline("Заказ №3307", CounterBoard.Accent);
            board.ShowStatus("Клиенту нужен пакет", CounterBoard.Accent);
            Capture("08_bag_request", new Vector3(-0.3f, 1.6f, -0.2f), new Vector3(0.1f, 1.35f, 1.4f), 70f);
            foreach (var pose in originalPoses)
            {
                if (pose.t == null) continue;
                pose.t.SetParent(pose.parent, true);
                pose.t.SetPositionAndRotation(pose.pos, pose.rot);
            }
            Object.DestroyImmediate(demoCustomer.gameObject);
            customer.gameObject.SetActive(true);

            // A line of customers (hard difficulty: three).
            var lineDirection = (PvzSceneBuilder.CustomerSpawnPosition - PvzSceneBuilder.CustomerServicePosition).normalized;
            var queued = new System.Collections.Generic.List<Customer>();
            for (var i = 1; i <= 2; i++)
            {
                var waiting = Object.Instantiate(customerPrefab, PvzSceneBuilder.CustomerServicePosition + lineDirection * (0.65f * i),
                    Quaternion.Euler(0, 180, 0));
                waiting.SetLook(i + 2);
                queued.Add(waiting);
            }
            board.ShowHeadline("Заказ №2781", CounterBoard.Accent);
            board.ShowStatus("Клиент торопится!", CounterBoard.Bad);
            customer.Say("Побыстрее,\nпожалуйста!");
            Capture("09_queue", new Vector3(-1.3f, 1.75f, -0.4f), new Vector3(0f, 1.2f, 2.0f), 62f);
            foreach (var waiting in queued) Object.DestroyImmediate(waiting.gameObject);

            // A returning customer has put a box on the counter.
            var returnPrefab = AssetDatabase.LoadAssetAtPath<QrBox>(PvzAssetFactory.PrefabsDir + "/Box_Supply.prefab");
            var spot = Object.FindFirstObjectByType<HandoverZone>().ReturnSpot;
            var returnBox = Object.Instantiate(returnPrefab, spot.position, spot.rotation);
            returnBox.AssignId("BOX-001", QrTextures.Create("BOX-001"));
            customer.Say("Хочу вернуть товар\nиз заказа №1435");
            customer.ShowPatience(null);
            board.ShowHeadline("Возврат: заказ №1435", CounterBoard.Accent);
            board.ShowStatus("Отсканируйте коробку клиента на стойке", CounterBoard.Neutral);
            scanner.ShowResult("Заказ №1435\nВОЗВРАТ ПРИНЯТ", CounterBoard.Good);
            Capture("10_return", new Vector3(0.1f, 1.6f, -0.25f), new Vector3(0.25f, 1.25f, 1.3f), 70f);
            Object.DestroyImmediate(returnBox.gameObject);
            customer.ShowPatience(0.55);

            // Random events: an urgent customer who forgot the order number.
            customer.Say("Срочно, опаздываю!\nЗабыл номер… кончается на 81");
            customer.ShowPatience(0.3);
            board.ShowHeadline("СРОЧНО: заказ №••81", CounterBoard.Bad);
            board.ShowStatus("Найдите коробку сканером", CounterBoard.Neutral);
            scanner.ShowResult("Заказ №2781\nНУЖНАЯ КОРОБКА", CounterBoard.Good);
            Capture("11_events", new Vector3(0.1f, 1.6f, -0.25f), new Vector3(0.1f, 1.55f, 1.3f), 70f);
            customer.ShowPatience(0.55);

            // An order handed over: confetti over the handover zone (T018).
            var confetti = Object.FindFirstObjectByType<FeedbackEffects>().GetComponentInChildren<ParticleSystem>(true);
            confetti.Emit(45);
            confetti.Simulate(0.35f, true, false);
            customer.Say("Спасибо!\nХорошего дня!");
            board.ShowHeadline("Заказ выдан", CounterBoard.Good);
            board.ShowStatus("+250 очков", CounterBoard.Good);
            Capture("14_confetti", new Vector3(0.1f, 1.6f, -0.25f), new Vector3(0.3f, 1.35f, 1.1f), 70f);
            confetti.Clear(true);

            // Career theme «Мята» (T022).
            var skin = Object.FindFirstObjectByType<CounterSkin>();
            var themes = AssetDatabase.LoadAssetAtPath<CounterThemeCatalog>(PvzSceneBuilder.ThemeCatalogPath);
            skin.Apply(themes.Find("mint"));
            Capture("15_theme_mint", new Vector3(-1.2f, 1.8f, -0.9f), new Vector3(0.1f, 1.3f, 1.0f), 62f);
            skin.Apply(themes.Find("classic"));

            // Shift timer: the time ran out, the last customer is still served.
            customer.Say("Здравствуйте!\nЗаказ №2781");
            board.ShowHeadline("Время смены вышло!", CounterBoard.Bad);
            board.ShowStatus("Обслужите последнего клиента", CounterBoard.Neutral);
            board.ShowScore(475, 3, 5, ShiftPresenter.ClockText(0));
            Capture("12_time_up", new Vector3(0.1f, 1.6f, -0.25f), new Vector3(0.1f, 1.55f, 1.3f), 70f);
            board.ShowScore(150, 1, 3);

            // Supply delivery: boxes just appeared under the ceiling.
            var supply = Object.FindFirstObjectByType<SupplyDrop>();
            var dropped = supply.Drop(new[] { "BOX-101", "BOX-102", "BOX-103" }, 7, 2.4f);
            board.ShowHeadline("ПОСТАВКА!", CounterBoard.Accent);
            board.ShowStatus("Отсканируйте новые коробки: 0/3", CounterBoard.Neutral);
            Capture("07_supply", new Vector3(-2.9f, 1.9f, -3.1f), new Vector3(0f, 1.3f, 0f), 62f);
            foreach (var droppedBox in dropped) Object.DestroyImmediate(droppedBox.gameObject);

            customer.Say("Спасибо!\nХорошего дня!");
            var summary = Object.FindFirstObjectByType<ShiftSummaryPanel>();
            var desk = new Pvz.Core.PickupDesk(new Pvz.Core.BoxCatalog());
            using (var stats = new Pvz.Core.ShiftStats(desk))
            {
                summary.Show(stats, 3);
                SetSummaryDemoText(summary);
                Capture("06_shift_summary", new Vector3(0f, 1.6f, -0.4f), new Vector3(0f, 1.75f, 1.1f), 65f);
                computer.Preview(PvzComputer.Screen.Summary,
                    "Смена завершена\nОчки: 1220\nВыдано заказов: 5\n\n«Новая смена» — сыграть ещё.", beforeShift: false);
                Capture("20_computer_summary", ComputerCameraFrom, ComputerCameraAt, 48f);
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Debug.Log($"[PvzScreenshots] Saved to {Path.GetFullPath(OutputDir)} (scene {scene.name} not modified).");
        }

        /// <summary>The order list of a staged shift: one served, one at the counter, two in line (real Core + presenter).</summary>
        private static string DemoOrderList()
        {
            var catalog = new Pvz.Core.BoxCatalog(new[]
            {
                new Pvz.Core.BoxRecord(new Pvz.Core.BoxId("BOX-001"), "1435"),
                new Pvz.Core.BoxRecord(new Pvz.Core.BoxId("BOX-002"), "2781"),
                new Pvz.Core.BoxRecord(new Pvz.Core.BoxId("BOX-003"), "5812"),
                new Pvz.Core.BoxRecord(new Pvz.Core.BoxId("BOX-103"), "2718"),
            });
            using (var demo = new Pvz.Core.Shift(catalog, new Pvz.Core.SystemRandomSource(5)) { QueueLength = 3 })
            {
                demo.TryEnqueue(out var served);
                demo.OpenNext(0);
                foreach (var box in served.Order.Boxes) demo.Desk.Handover(box, 1);
                demo.TryEnqueue(out _);
                demo.UrgentChancePercent = 100;
                demo.TryEnqueue(out _);
                demo.TryEnqueue(out _);
                demo.OpenNext(2);
                return ShiftPresenter.OrderListText(demo.Book.Entries, ShiftPresenter.OrderListLines);
            }
        }

        private static void SetSummaryDemoText(ShiftSummaryPanel summary)
        {
            var text = summary.GetComponentInChildren<TMP_Text>(true);
            text.text = "<b>СМЕНА ЗАВЕРШЕНА</b>\nОбслужено клиентов: 5/5\nОбработано товаров: 6\nМакс. очередь: 2\nОшибочных выдач: 1\nНеверных сканирований: 2\n" +
                        "Среднее время выдачи: 24.6 с\nОчки: 525\nРейтинг смены: <b>Хорошо</b>\n<b>НОВЫЙ РЕКОРД!</b> Было: 450";
        }

        private static void HideEditorOnlyClutter()
        {
            var simulator = GameObject.Find("XR Interaction Simulator");
            if (simulator != null) simulator.SetActive(false);
        }

        /// <summary>Where the player stands looking down at the computer and its keys.</summary>
        private static readonly Vector3 ComputerCameraFrom = new Vector3(-0.12f, 1.62f, 0.38f);
        private static readonly Vector3 ComputerCameraAt = new Vector3(-0.14f, 1.16f, 1.0f);

        private static void Capture(string name, Vector3 from, Vector3 lookAt, float fov, Vector3? up = null)
        {
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                t.ForceMeshUpdate(true, true);
                // A text that does not fit its box would also not fit in the game: report it.
                if (t.isActiveAndEnabled && t.isTextOverflowing)
                    Debug.LogWarning($"[PvzScreenshots] {name}: text overflows '{t.name}' (size {t.fontSize:0.###}, min {t.fontSizeMin:0.###}, rect {t.rectTransform.rect.size}): {t.text.Replace('\n', '|')}");
            }

            var go = new GameObject("Screenshot Camera");
            var cam = go.AddComponent<Camera>();
            cam.transform.position = from;
            cam.transform.LookAt(lookAt, up ?? Vector3.up);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.02f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.18f, 0.22f);

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;

            File.WriteAllBytes(Path.Combine(OutputDir, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
        }
    }
}
