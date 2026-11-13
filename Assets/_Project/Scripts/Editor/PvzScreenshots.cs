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
            Directory.CreateDirectory(OutputDir);
            var scene = EditorSceneManager.OpenScene(PvzSceneBuilder.ScenePath, OpenSceneMode.Single);
            HideEditorOnlyClutter();

            // Before the shift: the difficulty menu on the counter.
            ShiftPresenter.ShowDifficultyMenu(Object.FindFirstObjectByType<CounterBoard>());
            Capture("00_difficulty_menu", new Vector3(0f, 1.65f, -0.35f), new Vector3(-0.1f, 1.5f, 1.1f), 75f);
            foreach (var button in Object.FindObjectsByType<DifficultyButton>(FindObjectsSortMode.None)) button.gameObject.SetActive(false);

            // Stage a moment of play: a customer at the counter asking for an order.
            var customerPrefab = AssetDatabase.LoadAssetAtPath<Customer>(PvzAssetFactory.PrefabsDir + "/Customer.prefab");
            var customer = Object.Instantiate(customerPrefab, PvzSceneBuilder.CustomerServicePosition, Quaternion.Euler(0, 180, 0));
            customer.SetLook(0);
            customer.Say("Здравствуйте!\nЗаказ №2781");
            var board = Object.FindFirstObjectByType<CounterBoard>();
            board.ShowHeadline("Заказ №2781", CounterBoard.Accent);
            board.ShowStatus("Найдите коробку", CounterBoard.Neutral);
            board.ShowScore(150, 1, 3);
            var scanner = Object.FindFirstObjectByType<HandScanner>();
            scanner.ShowResult("Заказ №2781\nНУЖНАЯ КОРОБКА", CounterBoard.Good);

            HideEditorOnlyClutter();
            Object.FindFirstObjectByType<BagDispenser>().Refill(); // a bag lies on the counter as in play mode

            Capture("01_overview", new Vector3(-2.6f, 2.5f, -2.9f), new Vector3(0.1f, 0.8f, 0.6f), 55f);
            Capture("02_player_view", new Vector3(0f, 1.6f, -0.2f), new Vector3(0.1f, 1.62f, 1.6f), 72f);
            Capture("03_counter_closeup", new Vector3(0.35f, 1.55f, 0.15f), new Vector3(0.15f, 1.2f, 1.1f), 60f);

            var box = Object.FindFirstObjectByType<QrBox>();
            Capture("04_box_qr", box.transform.position + new Vector3(0.35f, 0.35f, -0.45f), box.transform.position, 45f);
            Capture("05_scanner", scanner.transform.position + new Vector3(-0.18f, 0.22f, -0.25f), scanner.transform.position, 40f);

            // Two-box order handed over; the customer now asks for a bag.
            var heldBoxes = Object.FindObjectsByType<QrBox>(FindObjectsSortMode.InstanceID);
            var demoCustomer = Object.Instantiate(customerPrefab, PvzSceneBuilder.CustomerServicePosition,
                Quaternion.Euler(0, 180, 0));
            demoCustomer.SetLook(2);
            customer.gameObject.SetActive(false);
            var originalPoses = new (Transform t, Transform parent, Vector3 pos, Quaternion rot)[2];
            for (var i = 0; i < 2 && i < heldBoxes.Length; i++)
            {
                var t = heldBoxes[i].transform;
                originalPoses[i] = (t, t.parent, t.position, t.rotation);
                t.SetParent(demoCustomer.CarryAnchor, false);
                t.localPosition = Vector3.up * (0.27f * i);
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
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Debug.Log($"[PvzScreenshots] Saved to {Path.GetFullPath(OutputDir)} (scene {scene.name} not modified).");
        }

        private static void SetSummaryDemoText(ShiftSummaryPanel summary)
        {
            var text = summary.GetComponentInChildren<TMP_Text>(true);
            text.text = "<b>СМЕНА ЗАВЕРШЕНА</b>\nОбслужено клиентов: 3/3\nОшибочных выдач: 1\nНеверных сканирований: 2\n" +
                        "Среднее время выдачи: 24.6 с\nОчки: 525\nРейтинг смены: <b>Хорошо</b>";
        }

        private static void HideEditorOnlyClutter()
        {
            var simulator = GameObject.Find("XR Interaction Simulator");
            if (simulator != null) simulator.SetActive(false);
        }

        private static void Capture(string name, Vector3 from, Vector3 lookAt, float fov)
        {
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                t.ForceMeshUpdate(true, true);

            var go = new GameObject("Screenshot Camera");
            var cam = go.AddComponent<Camera>();
            cam.transform.position = from;
            cam.transform.LookAt(lookAt);
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
