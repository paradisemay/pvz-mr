using System.Linq;
using Pvz.Runtime;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using static Pvz.Editor.PvzAssetFactory;

namespace Pvz.Editor
{
    /// <summary>Builds the prototype prefabs from primitives. Re-running overwrites them.</summary>
    internal sealed class PvzPrefabBuilder
    {
        // Box dimensions (m)
        public static readonly Vector3 BoxSize = new Vector3(0.34f, 0.26f, 0.34f);
        public const float CounterHeight = 1.04f;

        private readonly TMP_FontAsset _font;

        private Material _cardboard, _tape, _counterBody, _counterTop, _accent, _boardBack, _zone, _scannerBody,
            _scannerAccent, _screen, _beam, _skin, _shirt, _eyes, _bubble, _button;

        public PvzPrefabBuilder(TMP_FontAsset font)
        {
            _font = font;
            CreateMaterials();
        }

        private void CreateMaterials()
        {
            _cardboard = Lit("Cardboard", new Color(0.76f, 0.6f, 0.42f));
            _tape = Lit("Tape", new Color(0.88f, 0.82f, 0.66f), 0.5f);
            _counterBody = Lit("CounterBody", new Color(0.2f, 0.24f, 0.3f));
            _counterTop = Lit("CounterTop", new Color(0.86f, 0.76f, 0.6f), 0.35f);
            _accent = Lit("Accent", new Color(0.95f, 0.55f, 0.15f));
            _boardBack = Unlit("BoardBack", new Color(0.07f, 0.09f, 0.13f));
            _zone = UnlitTransparent("HandoverZone", new Color(0.3f, 0.95f, 0.45f, 0.28f));
            _scannerBody = Lit("ScannerBody", new Color(0.16f, 0.17f, 0.19f), 0.4f);
            _scannerAccent = Lit("ScannerAccent", new Color(0.98f, 0.8f, 0.15f));
            _screen = Unlit("ScannerScreen", new Color(0.03f, 0.05f, 0.06f));
            _beam = Unlit("ScannerBeam", new Color(1f, 0.15f, 0.1f));
            _skin = Lit("Skin", new Color(0.96f, 0.8f, 0.68f));
            _shirt = Lit("CustomerShirt", Color.white);
            _eyes = Unlit("Eyes", new Color(0.05f, 0.05f, 0.07f));
            _bubble = Unlit("SpeechBubble", new Color(0.98f, 0.98f, 0.96f));
            _button = Lit("RestartButton", new Color(0.95f, 0.45f, 0.15f));
        }

        /// <summary>Generic box for supply deliveries: its id and QR texture are assigned at runtime.</summary>
        public string SupplyBox() => Box("BOX-SUPPLY", Unlit("QR_Supply", Color.white), "Box_Supply");

        public string Box(string boxId) => Box(boxId, Unlit($"QR_{boxId}", Color.white, QrTexture(boxId)), $"Box_{boxId}");

        private string Box(string boxId, Material qrMat, string prefabName)
        {
            var root = new GameObject(prefabName);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1.2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var collider = root.AddComponent<BoxCollider>();
            collider.size = BoxSize;

            Primitive(PrimitiveType.Cube, "Body", root.transform, Vector3.zero, BoxSize, _cardboard);
            Primitive(PrimitiveType.Cube, "Tape", root.transform, new Vector3(0, BoxSize.y / 2f, 0),
                new Vector3(BoxSize.x + 0.002f, 0.004f, 0.07f), _tape);

            var label = 0.15f;
            var hx = BoxSize.x / 2f + 0.002f;
            var hy = BoxSize.y / 2f + 0.003f;
            var hz = BoxSize.z / 2f + 0.002f;
            var labels = new System.Collections.Generic.List<Renderer>();
            labels.Add(Primitive(PrimitiveType.Quad, "QR_Top", root.transform, new Vector3(0.09f, hy, 0.06f), Vector3.one * label * 0.8f, qrMat,
                localRot: Quaternion.Euler(90, 0, 0)).GetComponent<Renderer>());
            labels.Add(Primitive(PrimitiveType.Quad, "QR_Front", root.transform, new Vector3(0, 0, -hz), Vector3.one * label, qrMat).GetComponent<Renderer>());
            labels.Add(Primitive(PrimitiveType.Quad, "QR_Back", root.transform, new Vector3(0, 0, hz), Vector3.one * label, qrMat,
                localRot: Quaternion.Euler(0, 180, 0)).GetComponent<Renderer>());
            labels.Add(Primitive(PrimitiveType.Quad, "QR_Left", root.transform, new Vector3(-hx, 0, 0), Vector3.one * label, qrMat,
                localRot: Quaternion.Euler(0, 90, 0)).GetComponent<Renderer>());
            labels.Add(Primitive(PrimitiveType.Quad, "QR_Right", root.transform, new Vector3(hx, 0, 0), Vector3.one * label, qrMat,
                localRot: Quaternion.Euler(0, -90, 0)).GetComponent<Renderer>());

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.useDynamicAttach = true;
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = false;

            var box = root.AddComponent<QrBox>();
            box.Configure(boxId, grab, rb, labels.ToArray());
            return SavePrefab(root, prefabName);
        }

        public string Scanner()
        {
            var root = new GameObject("HandScanner");
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var grip = Primitive(PrimitiveType.Cube, "Grip", root.transform, new Vector3(0, -0.055f, -0.03f),
                new Vector3(0.035f, 0.11f, 0.04f), _scannerBody, true, Quaternion.Euler(-15, 0, 0));
            Primitive(PrimitiveType.Cube, "Head", root.transform, new Vector3(0, 0.015f, 0.01f),
                new Vector3(0.065f, 0.05f, 0.15f), _scannerBody, true);
            Primitive(PrimitiveType.Cube, "Nose", root.transform, new Vector3(0, 0.015f, 0.09f),
                new Vector3(0.07f, 0.055f, 0.012f), _scannerAccent);
            Primitive(PrimitiveType.Cube, "Trigger", root.transform, new Vector3(0, -0.02f, 0.0f),
                new Vector3(0.012f, 0.025f, 0.012f), _scannerAccent);

            var screenRoot = new GameObject("Screen").transform;
            screenRoot.SetParent(root.transform, false);
            // Tilted towards the player and lifted so the whole screen stays above the head (top at y = 0.04):
            // at 75° its lower edge is 0.0375 * cos 75° ≈ 0.01 below the centre.
            screenRoot.localPosition = new Vector3(0, 0.052f, -0.005f);
            screenRoot.localRotation = Quaternion.Euler(75, 0, 0);
            var screenBack = Primitive(PrimitiveType.Quad, "ScreenBack", screenRoot, Vector3.zero, new Vector3(0.06f, 0.075f, 1f), _screen,
                localRot: Quaternion.identity);
            var screenText = Text("ScreenText", screenRoot, new Vector3(0, 0, -0.001f), new Vector2(0.055f, 0.068f),
                "Наведите на QR", _font, Color.white, 0.25f);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root.transform, false);
            muzzle.localPosition = new Vector3(0, 0.015f, 0.1f);

            var attach = new GameObject("Attach").transform;
            attach.SetParent(root.transform, false);
            attach.localPosition = grip.transform.localPosition;

            var beamGo = new GameObject("Beam");
            beamGo.transform.SetParent(root.transform, false);
            var beam = beamGo.AddComponent<LineRenderer>();
            beam.sharedMaterial = _beam;
            beam.widthMultiplier = 0.004f;
            beam.positionCount = 2;
            beam.useWorldSpace = true;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.enabled = false;

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.attachTransform = attach;
            grab.useDynamicAttach = false;
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = false;

            var scanner = root.AddComponent<HandScanner>();
            scanner.Configure(muzzle, beam, screenText);
            scanner.ConfigureScreenBack(screenBack.GetComponent<Renderer>());
            return SavePrefab(root, "HandScanner");
        }

        /// <summary>Confetti that bursts up from the handover zone and falls back (emitted on demand, never looping).</summary>
        private static ParticleSystem Confetti(Transform zone)
        {
            var go = new GameObject("Confetti");
            go.transform.SetParent(zone, false);
            go.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // emit upwards
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.03f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.9f;
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(new Color(0.35f, 0.9f, 0.45f), 0f), new GradientColorKey(new Color(1f, 0.8f, 0.3f), 0.33f),
                    new GradientColorKey(new Color(0.3f, 0.6f, 1f), 0.66f), new GradientColorKey(new Color(1f, 0.42f, 0.35f), 1f),
                },
            }) { mode = ParticleSystemGradientMode.RandomColor };
            var emission = particles.emission;
            emission.enabled = false; // only explicit bursts (FeedbackEffects.Celebrate)
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.12f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = PvzAssetFactory.ParticlesUnlit("Confetti");
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return particles;
        }

        public string Counter()
        {
            var root = new GameObject("PickupCounter");
            var bodyHeight = CounterHeight - 0.04f;
            Primitive(PrimitiveType.Cube, "Body", root.transform, new Vector3(0, bodyHeight / 2f, 0),
                new Vector3(1.4f, bodyHeight, 0.55f), _counterBody, true);
            Primitive(PrimitiveType.Cube, "Top", root.transform, new Vector3(0, CounterHeight - 0.02f, 0),
                new Vector3(1.5f, 0.04f, 0.65f), _counterTop, true);
            Primitive(PrimitiveType.Cube, "StripeFront", root.transform, new Vector3(0, 0.8f, -0.276f),
                new Vector3(1.4f, 0.08f, 0.004f), _accent);
            Primitive(PrimitiveType.Cube, "StripeBack", root.transform, new Vector3(0, 0.8f, 0.276f),
                new Vector3(1.4f, 0.08f, 0.004f), _accent);
            Text("SignFront", root.transform, new Vector3(0, 0.5f, -0.28f), new Vector2(1.2f, 0.3f), "ПУНКТ ВЫДАЧИ",
                _font, new Color(0.95f, 0.95f, 0.95f), 3f);
            Text("SignBack", root.transform, new Vector3(0, 0.5f, 0.28f), new Vector2(1.2f, 0.3f), "ПУНКТ ВЫДАЧИ",
                _font, new Color(0.95f, 0.95f, 0.95f), 3f, localRot: Quaternion.Euler(0, 180, 0));

            // Handover zone (right side of the counter top, from the player's view)
            var zoneGo = new GameObject("HandoverZone");
            zoneGo.transform.SetParent(root.transform, false);
            zoneGo.transform.localPosition = new Vector3(0.38f, CounterHeight + 0.2f, 0.02f);
            var zoneCollider = zoneGo.AddComponent<BoxCollider>();
            zoneCollider.isTrigger = true;
            zoneCollider.size = new Vector3(0.6f, 0.4f, 0.55f);
            zoneGo.AddComponent<HandoverZone>();
            Primitive(PrimitiveType.Quad, "ZonePlate", zoneGo.transform, new Vector3(0, -0.198f, 0),
                new Vector3(0.58f, 0.52f, 1f), _zone, localRot: Quaternion.Euler(90, 0, 0));
            Text("ZoneLabel", zoneGo.transform, new Vector3(0, -0.197f, -0.2f), new Vector2(0.55f, 0.08f), "ЗОНА ВЫДАЧИ",
                _font, Color.white, 1f, localRot: Quaternion.Euler(90, 0, 0));
            zoneGo.AddComponent<FeedbackEffects>().Configure(Confetti(zoneGo.transform));

            // Board above the counter
            var boardGo = new GameObject("CounterBoard");
            boardGo.transform.SetParent(root.transform, false);
            boardGo.transform.localPosition = new Vector3(0, 2.2f, 0.15f);
            Primitive(PrimitiveType.Cube, "Panel", boardGo.transform, Vector3.zero, new Vector3(1.4f, 0.56f, 0.03f), _boardBack);
            Primitive(PrimitiveType.Cube, "Frame", boardGo.transform, new Vector3(0, -0.295f, 0), new Vector3(1.4f, 0.03f, 0.035f), _accent);
            foreach (var side in new[] { -0.76f, 0.76f })
                Primitive(PrimitiveType.Cube, "Post", boardGo.transform, new Vector3(side, -0.58f, 0.0f), new Vector3(0.04f, 1.16f, 0.04f), _counterBody);
            var headline = Text("Headline", boardGo.transform, new Vector3(0, 0.14f, -0.02f), new Vector2(1.3f, 0.2f),
                "Заказ №1435", _font, CounterBoard.Accent, 3f);
            var status = Text("Status", boardGo.transform, new Vector3(0, -0.04f, -0.02f), new Vector2(1.3f, 0.14f),
                "Найдите коробку", _font, CounterBoard.Neutral, 2f);
            var score = Text("Score", boardGo.transform, new Vector3(0, -0.2f, -0.02f), new Vector2(1.3f, 0.09f),
                "Очки: 0    Выдано: 0/3", _font, new Color(0.7f, 0.75f, 0.82f), 1.4f);
            var board = boardGo.AddComponent<CounterBoard>();
            board.Configure(headline, status, score);

            // Order list to the right of the board, turned a little towards the player (hidden until the first customer).
            var listGo = new GameObject("OrderList");
            listGo.transform.SetParent(boardGo.transform, false);
            listGo.transform.localPosition = new Vector3(1.2f, -0.06f, -0.07f);
            listGo.transform.localRotation = Quaternion.Euler(0f, -18f, 0f);
            Primitive(PrimitiveType.Cube, "Panel", listGo.transform, Vector3.zero, new Vector3(0.74f, 0.74f, 0.03f), _boardBack);
            Primitive(PrimitiveType.Cube, "Frame", listGo.transform, new Vector3(0, -0.385f, 0), new Vector3(0.74f, 0.03f, 0.035f), _accent);
            var orderList = Text("Orders", listGo.transform, new Vector3(0, 0, -0.02f), new Vector2(0.68f, 0.68f),
                "<b>ЗАКАЗЫ</b>", _font, new Color(0.88f, 0.9f, 0.94f), 0.8f, TextAlignmentOptions.TopLeft);
            orderList.textWrappingMode = TextWrappingModes.NoWrap; // one visit per line: auto-size shrinks instead of wrapping
            board.ConfigureOrders(orderList, listGo);
            listGo.SetActive(false);

            // Summary panel (covers the board when the shift ends)
            var summaryGo = new GameObject("ShiftSummary");
            summaryGo.transform.SetParent(root.transform, false);
            summaryGo.transform.localPosition = new Vector3(0, 2.05f, 0.1f);
            var summaryRoot = new GameObject("Content");
            summaryRoot.transform.SetParent(summaryGo.transform, false);
            Primitive(PrimitiveType.Cube, "Panel", summaryRoot.transform, Vector3.zero, new Vector3(1.6f, 0.95f, 0.02f), _boardBack);
            var summaryText = Text("Text", summaryRoot.transform, new Vector3(0, 0, -0.015f), new Vector2(1.45f, 0.85f),
                "СМЕНА ЗАВЕРШЕНА", _font, Color.white, 1.6f);
            summaryGo.AddComponent<ShiftSummaryPanel>().Configure(summaryRoot, summaryText);
            summaryRoot.SetActive(false);

            // Restart button (player side of the counter top)
            var button = new GameObject("RestartButton");
            button.transform.SetParent(root.transform, false);
            button.transform.localPosition = new Vector3(-0.15f, CounterHeight + 0.03f, -0.2f);
            Primitive(PrimitiveType.Cube, "Cap", button.transform, Vector3.zero, new Vector3(0.24f, 0.06f, 0.14f), _button, true);
            Text("Label", button.transform, new Vector3(0, 0.032f, 0), new Vector2(0.22f, 0.11f), "НОВАЯ\nСМЕНА", _font,
                Color.white, 0.6f, localRot: Quaternion.Euler(90, 0, 0));
            button.AddComponent<XRSimpleInteractable>();
            button.AddComponent<RestartButton>();
            button.SetActive(false);

            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0.6f;
            root.AddComponent<FeedbackAudio>();

            // Career themes (T022) repaint the counter's body, top and accents.
            Renderer[] Named(Transform under, params string[] names) => under.GetComponentsInChildren<Renderer>(true)
                .Where(r => System.Array.IndexOf(names, r.name) >= 0).ToArray();
            root.AddComponent<CounterSkin>().Configure(Named(root.transform, "Body", "Post"), Named(root.transform, "Top"),
                Named(root.transform, "StripeFront", "StripeBack", "Frame"));
            return SavePrefab(root, "PickupCounter");
        }

        public string Customer()
        {
            var root = new GameObject("Customer");
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);

            var torso = Primitive(PrimitiveType.Capsule, "Torso", body, new Vector3(0, 1.0f, 0), new Vector3(0.42f, 0.5f, 0.3f), _shirt);
            Primitive(PrimitiveType.Capsule, "Legs", body, new Vector3(0, 0.42f, 0), new Vector3(0.32f, 0.42f, 0.24f), _counterBody);
            var armL = Primitive(PrimitiveType.Capsule, "ArmL", body, new Vector3(-0.27f, 1.05f, 0.05f), new Vector3(0.11f, 0.3f, 0.11f), _shirt,
                localRot: Quaternion.Euler(-20, 0, 8));
            var armR = Primitive(PrimitiveType.Capsule, "ArmR", body, new Vector3(0.27f, 1.05f, 0.05f), new Vector3(0.11f, 0.3f, 0.11f), _shirt,
                localRot: Quaternion.Euler(-20, 0, -8));
            Primitive(PrimitiveType.Sphere, "Head", body, new Vector3(0, 1.62f, 0), Vector3.one * 0.26f, _skin);
            Primitive(PrimitiveType.Sphere, "EyeL", body, new Vector3(-0.055f, 1.66f, 0.115f), Vector3.one * 0.035f, _eyes);
            Primitive(PrimitiveType.Sphere, "EyeR", body, new Vector3(0.055f, 1.66f, 0.115f), Vector3.one * 0.035f, _eyes);
            Primitive(PrimitiveType.Cube, "Smile", body, new Vector3(0, 1.57f, 0.122f), new Vector3(0.07f, 0.012f, 0.01f), _eyes);

            var carry = new GameObject("CarryAnchor").transform;
            carry.SetParent(body, false);
            carry.localPosition = new Vector3(0, 1.0f, 0.36f);

            var bubbleGo = new GameObject("SpeechBubble");
            bubbleGo.transform.SetParent(root.transform, false);
            bubbleGo.transform.localPosition = new Vector3(-0.47f, 1.68f, 0); // beside the head, below the counter board
            // Customers face the player, so the bubble is turned around to read correctly before the billboard kicks in.
            bubbleGo.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var bubbleRoot = new GameObject("Content");
            bubbleRoot.transform.SetParent(bubbleGo.transform, false);
            Primitive(PrimitiveType.Quad, "Background", bubbleRoot.transform, Vector3.zero, new Vector3(0.6f, 0.26f, 1f), _bubble);
            var bubbleText = Text("Text", bubbleRoot.transform, new Vector3(0, 0, -0.002f), new Vector2(0.56f, 0.22f),
                "Здравствуйте!", _font, new Color(0.1f, 0.1f, 0.12f), 1.2f);
            bubbleGo.AddComponent<Billboard>();
            var bubble = bubbleGo.AddComponent<SpeechBubble>();
            bubble.Configure(bubbleRoot, bubbleText);
            bubbleRoot.SetActive(false);

            var customer = root.AddComponent<Customer>();
            customer.Configure(bubble, carry, body, new[] { torso.GetComponent<Renderer>(), armL.GetComponent<Renderer>(), armR.GetComponent<Renderer>() });
            // Voice from where the customer stands: 3D sound, heard across a room.
            var voiceSource = root.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 1f;
            voiceSource.minDistance = 0.6f;
            voiceSource.maxDistance = 8f;
            voiceSource.volume = 0.8f;
            voiceSource.dopplerLevel = 0f; // the listener is the head: turning it must not bend the voice
            var voice = root.AddComponent<CustomerVoice>();
            voice.Configure(voiceSource);
            customer.ConfigureVoice(voice);
            // Patience bar above the head (hidden until the customer is at the counter and patience is ticking).
            var barGo = new GameObject("PatienceBar");
            barGo.transform.SetParent(root.transform, false);
            barGo.transform.localPosition = new Vector3(0, 1.88f, 0);
            barGo.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var barRoot = new GameObject("Content");
            barRoot.transform.SetParent(barGo.transform, false);
            barRoot.transform.localScale = new Vector3(0.42f, 0.045f, 1f);
            Primitive(PrimitiveType.Quad, "Background", barRoot.transform, Vector3.zero, Vector3.one, _boardBack);
            var fill = Primitive(PrimitiveType.Quad, "Fill", barRoot.transform, new Vector3(0, 0, -0.002f), new Vector3(1f, 0.7f, 1f),
                Unlit("PatienceFill", Color.white));
            foreach (var quad in barRoot.GetComponentsInChildren<Renderer>()) quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            barGo.AddComponent<Billboard>();
            var bar = barGo.AddComponent<PatienceBar>();
            bar.Configure(barRoot, fill.transform, fill.GetComponent<Renderer>());
            barRoot.SetActive(false);
            customer.ConfigurePatienceBar(bar);

            return SavePrefab(root, "Customer");
        }

        public string Bag()
        {
            var root = new GameObject("ParcelBag");
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var size = new Vector3(0.28f, 0.3f, 0.08f);
            root.AddComponent<BoxCollider>().size = size;

            var bagMat = Lit("Bag", new Color(0.96f, 0.96f, 0.93f), 0.5f);
            Primitive(PrimitiveType.Cube, "Body", root.transform, Vector3.zero, size, bagMat);
            Primitive(PrimitiveType.Cube, "HandleL", root.transform, new Vector3(-0.06f, size.y / 2f + 0.03f, 0f), new Vector3(0.015f, 0.06f, 0.015f), bagMat);
            Primitive(PrimitiveType.Cube, "HandleR", root.transform, new Vector3(0.06f, size.y / 2f + 0.03f, 0f), new Vector3(0.015f, 0.06f, 0.015f), bagMat);
            Primitive(PrimitiveType.Cube, "HandleTop", root.transform, new Vector3(0f, size.y / 2f + 0.06f, 0f), new Vector3(0.135f, 0.015f, 0.015f), bagMat);
            Primitive(PrimitiveType.Cube, "Stripe", root.transform, new Vector3(0f, -0.06f, 0f), new Vector3(size.x + 0.002f, 0.04f, size.z + 0.002f), _accent);
            Text("Logo", root.transform, new Vector3(0, 0.04f, -size.z / 2f - 0.002f), new Vector2(0.24f, 0.08f), "ПАКЕТ", _font,
                new Color(0.2f, 0.24f, 0.3f), 0.5f);

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.useDynamicAttach = true;
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = false;

            root.AddComponent<ParcelBag>().Configure(grab, rb);
            return SavePrefab(root, "ParcelBag");
        }

        /// <summary>Invisible plane with a collider so virtual boxes can rest on real tables/floor detected by the headset.</summary>
        public string ArPlane()
        {
            var root = new GameObject("ARPlane");
            root.AddComponent<ARPlane>();
            root.AddComponent<MeshFilter>();
            root.AddComponent<MeshCollider>();
            root.AddComponent<ARPlaneMeshVisualizer>();
            return SavePrefab(root, "ARPlane");
        }

        /// <summary>Saves the prefab and returns its asset path. Reload it by path once all prefabs are saved:
        /// references returned by SaveAsPrefabAsset can go stale when later prefabs are imported.</summary>
        private static string SavePrefab(GameObject root, string name)
        {
            var path = $"{PrefabsDir}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path, out var success);
            Object.DestroyImmediate(root);
            if (!success) throw new System.InvalidOperationException($"Could not save prefab {path}");
            return path;
        }
    }
}
