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

        private Vector3 _bodyRestPosition;

        public bool IsWalking { get; private set; }
        public Transform CarryAnchor => carryAnchor;
        public string LastLine => speech != null ? speech.Text : string.Empty;

        private void Awake()
        {
            if (body != null) _bodyRestPosition = body.localPosition;
        }

        [SerializeField] private CustomerVoice voice;

        public CustomerVoice Voice => voice;

        public void SetLook(int variant)
        {
            if (voice != null) voice.SetVoice(variant); // each look has its own voice
            if (tintedRenderers == null || tintedRenderers.Length == 0) return;
            var color = Palette[Mathf.Abs(variant) % Palette.Length];
            var block = new MaterialPropertyBlock();
            foreach (var r in tintedRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                r.SetPropertyBlock(block);
            }
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

        public void Say(string line)
        {
            if (speech != null) speech.Show(line);
            if (voice != null) voice.Speak(line);
        }

        public void StopTalking()
        {
            if (speech != null) speech.Hide();
        }

        /// <summary>Boxes the customer carries are stacked on top of each other.</summary>
        public void Take(QrBox box)
        {
            if (box == null) return;
            box.HandTo(carryAnchor != null ? carryAnchor : transform);
            var collider = box.GetComponent<BoxCollider>();
            var height = collider != null ? collider.size.y * box.transform.localScale.y : 0.27f;
            box.transform.localPosition = Vector3.up * (_stackHeight + height / 2f);
            _stackHeight += height + 0.005f;
        }

        public void Take(ParcelBag bag)
        {
            if (bag != null) bag.HandTo(carryAnchor != null ? carryAnchor : transform);
        }

        private float _stackHeight;

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
