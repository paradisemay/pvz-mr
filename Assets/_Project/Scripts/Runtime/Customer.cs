using System.Collections;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// Virtual visitor. Walks to the counter, talks through a speech bubble, takes the parcel and leaves.
    /// Movement is a straight walk (no navmesh: the real room geometry is unknown to the game).
    /// </summary>
    public sealed class Customer : MonoBehaviour
    {
        [SerializeField] private SpeechBubble speech;
        [SerializeField] private Transform carryAnchor;
        [SerializeField] private Transform body;
        [SerializeField] private Renderer[] tintedRenderers;
        [SerializeField] private float bobHeight = 0.03f;
        [SerializeField] private float bobFrequency = 4f;

        private static readonly Color[] Palette =
        {
            new Color(0.29f, 0.53f, 0.91f),
            new Color(0.91f, 0.45f, 0.29f),
            new Color(0.36f, 0.73f, 0.45f),
            new Color(0.73f, 0.42f, 0.85f),
            new Color(0.95f, 0.76f, 0.26f),
        };
        private static readonly Color[] SkinTones =
        {
            new Color(0.94f, 0.75f, 0.61f),
            new Color(0.75f, 0.50f, 0.34f),
            new Color(0.56f, 0.34f, 0.23f),
            new Color(0.34f, 0.19f, 0.13f),
            new Color(0.83f, 0.64f, 0.46f),
            new Color(0.16f, 0.09f, 0.065f),
        };

        private Vector3 _bodyRestPosition;
        private static readonly Material[] HatLeathers = new Material[5];
        private static Material hatBand, hatBuckle;
        private MeshRenderer[] hatRenderers;
        private static Material eyeWhite, iris;
        private MeshRenderer headRenderer;
        private MeshRenderer[] faceRenderers;

        public bool IsWalking { get; private set; }
        public Transform CarryAnchor => carryAnchor;
        public string LastLine => speech != null ? speech.Text : string.Empty;

        private void Awake()
        {
            if (body != null) _bodyRestPosition = body.localPosition;
            AddHat();
            AddFace();
        }

        private void AddHat()
        {
            if (body == null) return;
            var hat = body.Find("CowboyHat");
            if (hat == null)
            {
                var model = Resources.Load<GameObject>("Hats/CowboyHat");
                if (model == null) return;
                hat = Instantiate(model, body).transform;
                hat.name = "CowboyHat";
            }
            hat.localPosition = new Vector3(0, 1.65f, 0);
            hat.localRotation = Quaternion.identity;
            hatRenderers = hat.GetComponentsInChildren<MeshRenderer>();
            if (hatBand == null) hatBand = HatMaterial(new Color(0.095f, 0.055f, 0.035f));
            if (hatBuckle == null) hatBuckle = HatMaterial(new Color(0.72f, 0.5f, 0.18f));
            foreach (var renderer in hatRenderers)
            {
                if (renderer.name.Contains("Band")) renderer.sharedMaterial = hatBand;
                else if (renderer.name.Contains("Buckle")) renderer.sharedMaterial = hatBuckle;
                else renderer.sharedMaterial = Leather(0);
            }
            var bar = transform.Find("PatienceBar");
            if (bar != null) bar.localPosition = new Vector3(0, 2.08f, 0);
            var bubble = transform.Find("SpeechBubble");
            if (bubble != null) bubble.localPosition = new Vector3(-0.62f, 1.68f, 0);
        }

        private static Material Leather(int index)
        {
            if (HatLeathers[index] == null)
                HatLeathers[index] = HatMaterial(Color.white, Resources.Load<Texture2D>($"Hats/Hat_{index}"));
            return HatLeathers[index];
        }

        private static Material HatMaterial(Color color, Texture2D texture = null)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            if (texture != null) material.SetTexture("_BaseMap", texture);
            return material;
        }

        private void AddFace()
        {
            if (body == null) return;
            headRenderer = body.Find("Head")?.GetComponent<MeshRenderer>();
            var face = body.Find("FaceParts");
            if (face == null)
            {
                var model = Resources.Load<GameObject>("Faces/FaceParts");
                if (model == null) return;
                face = Instantiate(model, body).transform;
                face.name = "FaceParts";
            }
            face.localPosition = new Vector3(0, 1.62f, 0);
            face.localRotation = Quaternion.identity;
            foreach (var oldPart in new[] { "EyeL", "EyeR", "Smile" })
            {
                var part = body.Find(oldPart);
                if (part != null) part.gameObject.SetActive(false);
            }
            faceRenderers = face.GetComponentsInChildren<MeshRenderer>(true);
            if (eyeWhite == null) eyeWhite = HatMaterial(new Color(0.94f, 0.92f, 0.86f));
            if (iris == null) iris = HatMaterial(new Color(0.08f, 0.055f, 0.045f));
            foreach (var renderer in faceRenderers)
            {
                if (renderer.name.Contains("White")) renderer.sharedMaterial = eyeWhite;
                else if (renderer.name.Contains("Iris")) renderer.sharedMaterial = iris;
                else if (headRenderer != null) renderer.sharedMaterial = headRenderer.sharedMaterial;
            }
        }

        private void SetFace(int variant)
        {
            if (faceRenderers == null) return;
            var (mouth, nose, eyes, ears, skinIndex) = FaceStyles(variant);
            var skin = SkinTones[skinIndex];
            var block = new MaterialPropertyBlock();
            if (headRenderer != null)
            {
                headRenderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", skin);
                headRenderer.SetPropertyBlock(block);
            }
            foreach (var renderer in faceRenderers)
            {
                var parts = renderer.name.Split('_');
                if (parts.Length < 2 || !int.TryParse(parts[1], out var style)) continue;
                var selected = parts[0] == "Mouth" ? mouth : parts[0] == "Nose" ? nose
                    : parts[0] == "Eyes" ? eyes : ears;
                renderer.gameObject.SetActive(style == selected);
                if (style != selected || (parts[0] != "Nose" && parts[0] != "Ears" && parts[0] != "Mouth")) continue;
                renderer.GetPropertyBlock(block);
                var color = parts[0] == "Mouth"
                    ? new Color(skin.r * 0.77f, skin.g * 0.58f, skin.b * 0.59f) : skin;
                block.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(block);
            }
        }

        internal static (int mouth, int nose, int eyes, int ears, int skin) FaceStyles(int variant)
        {
            var look = Mathf.Abs(variant);
            var slot = look % 5;
            var cycle = (look / 5) % 5;
            var mouth = (slot + cycle) % 5;
            var nose = (slot * 2 + cycle * 3 + 1) % 5;
            var eyes = (slot * 3 + cycle + 2) % 5;
            var ears = (slot * 4 + cycle * 2 + 3) % 5;
            return (mouth, nose, eyes, ears, look % SkinTones.Length);
        }

        [SerializeField] private CustomerVoice voice;

        public CustomerVoice Voice => voice;

        public void SetLook(int variant)
        {
            if (voice != null) voice.SetVoice(variant); // each look has its own voice
            var color = Palette[Mathf.Abs(variant) % Palette.Length];
            var block = new MaterialPropertyBlock();
            if (tintedRenderers != null)
            {
                foreach (var r in tintedRenderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(block);
                    block.SetColor("_BaseColor", color);
                    r.SetPropertyBlock(block);
                }
            }
            if (hatRenderers != null)
            {
                foreach (var renderer in hatRenderers)
                    if (!renderer.name.Contains("Band") && !renderer.name.Contains("Buckle"))
                        renderer.sharedMaterial = Leather(Mathf.Abs(variant) % HatLeathers.Length);
            }
            SetFace(variant);
        }

        [SerializeField] private PatienceBar patienceBar;

        public PatienceBar PatienceBar => patienceBar;

        /// <summary>Remaining patience 0..1 above the head; null hides the bar.</summary>
        public void ShowPatience(double? remaining)
        {
            if (patienceBar != null) patienceBar.Show(remaining);
        }

        internal void ConfigurePatienceBar(PatienceBar bar) => patienceBar = bar;

        internal void ConfigureVoice(CustomerVoice customerVoice) => voice = customerVoice;

        [SerializeField] private CustomerPhone phone;

        /// <summary>The phone with the order's barcode (T035).</summary>
        public CustomerPhone Phone => phone;

        internal void ConfigurePhone(CustomerPhone customerPhone) => phone = customerPhone;

        public void Say(string line)
        {
            if (speech != null) speech.Show(line);
            if (voice != null) voice.Speak(line);
        }

        public void StopTalking()
        {
            if (speech != null) speech.Hide();
        }

        /// <summary>
        /// Boxes the customer carries are stacked in front of the chest; once the next one would hide the face, it goes
        /// to a stack at the side instead (seen on the headset: two boxes on the chest covered the customer's face).
        /// </summary>
        public void Take(QrBox box)
        {
            if (box == null) return;
            box.HandTo(carryAnchor != null ? carryAnchor : transform);
            var collider = box.GetComponent<BoxCollider>();
            var height = collider != null ? collider.size.y * box.transform.localScale.y : 0.27f;
            box.transform.localPosition = CarryStacks.Place(_stacks, height);
        }

        public void Take(ParcelBag bag)
        {
            if (bag != null) bag.HandTo(carryAnchor != null ? carryAnchor : transform);
        }

        private readonly float[] _stacks = new float[CarryStacks.ColumnX.Length];

        private Coroutine _walk;

        /// <summary>Starts walking to a place (cancels the previous walk). Poll <see cref="IsWalking"/>.</summary>
        public void GoTo(Vector3 target, Quaternion finalRotation, float speed)
        {
            if (_leaving) return; // a leaving customer is no longer part of the line
            if (_walk != null) StopCoroutine(_walk);
            _walk = StartCoroutine(WalkTo(target, finalRotation, speed));
        }

        /// <summary>Walks away and disappears together with everything they carry.</summary>
        public void LeaveAndVanish(Vector3 exit, float speed)
        {
            if (_walk != null) StopCoroutine(_walk);
            _leaving = true;
            StopTalking();
            if (phone != null) phone.Hide();
            _walk = StartCoroutine(Leave(exit, speed));
        }

        private bool _leaving;

        private IEnumerator Leave(Vector3 exit, float speed)
        {
            var walk = WalkTo(exit, transform.rotation, speed);
            while (walk.MoveNext()) yield return walk.Current; // inline, so stopping _walk stops the walk too
            Destroy(gameObject);
        }

        private void OnDisable() => IsWalking = false;

        /// <summary>Walks to <paramref name="target"/> and turns to <paramref name="finalRotation"/>.</summary>
        public IEnumerator WalkTo(Vector3 target, Quaternion finalRotation, float speed)
        {
            IsWalking = true;
            target.y = transform.position.y;
            var toTarget = target - transform.position;
            if (toTarget.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(toTarget, Vector3.up);

            var phase = 0f;
            while ((transform.position - target).sqrMagnitude > 0.0004f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
                phase += Time.deltaTime * bobFrequency * Mathf.PI * 2f;
                if (body != null) body.localPosition = _bodyRestPosition + Vector3.up * (Mathf.Abs(Mathf.Sin(phase)) * bobHeight);
                yield return null;
            }

            transform.position = target;
            transform.rotation = finalRotation;
            if (body != null) body.localPosition = _bodyRestPosition;
            IsWalking = false;
        }

        internal void Configure(SpeechBubble bubble, Transform carry, Transform bodyRoot, Renderer[] tinted)
        {
            speech = bubble;
            carryAnchor = carry;
            body = bodyRoot;
            tintedRenderers = tinted;
        }
    }
}
