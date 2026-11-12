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

        public void SetLook(int variant)
        {
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

        public void Say(string line)
        {
            if (speech != null) speech.Show(line);
        }

        public void StopTalking()
        {
            if (speech != null) speech.Hide();
        }

        public void Take(QrBox box)
        {
            if (box != null) box.HandTo(carryAnchor != null ? carryAnchor : transform);
        }

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
