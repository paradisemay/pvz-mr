using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// Bar above a customer's head: full green when they arrive, red when they are about to leave.
    /// Faces the player like the speech bubble.
    /// </summary>
    public sealed class PatienceBar : MonoBehaviour
    {
        private static readonly Color Calm = new Color(0.3f, 0.85f, 0.4f);
        private static readonly Color Nervous = new Color(0.98f, 0.8f, 0.2f);
        private static readonly Color Angry = new Color(0.95f, 0.3f, 0.25f);
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private GameObject root;
        [SerializeField] private Transform fill;
        [SerializeField] private Renderer fillRenderer;

        private MaterialPropertyBlock _block;
        private Camera _camera;

        public bool IsVisible => root != null && root.activeSelf;
        public float Value { get; private set; } = 1f;

        /// <summary>Shows the remaining share of patience (0..1), or hides the bar for null.</summary>
        public void Show(double? remaining)
        {
            if (root == null) return;
            if (!remaining.HasValue)
            {
                if (root.activeSelf) root.SetActive(false);
                return;
            }
            if (!root.activeSelf) root.SetActive(true);

            Value = Mathf.Clamp01((float)remaining.Value);
            if (fill != null)
            {
                // The fill is anchored at its left edge: scale X and shift it so it shrinks to the left.
                var scale = fill.localScale;
                scale.x = Mathf.Max(0.0001f, Value);
                fill.localScale = scale;
                fill.localPosition = new Vector3((Value - 1f) * 0.5f, fill.localPosition.y, fill.localPosition.z);
            }
            if (fillRenderer != null)
            {
                _block ??= new MaterialPropertyBlock();
                fillRenderer.GetPropertyBlock(_block);
                var color = Value > 0.5f ? Color.Lerp(Nervous, Calm, (Value - 0.5f) * 2f) : Color.Lerp(Angry, Nervous, Value * 2f);
                _block.SetColor(BaseColor, color);
                fillRenderer.SetPropertyBlock(_block);
            }
        }

        private void LateUpdate()
        {
            if (root == null || !root.activeSelf) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            var away = transform.position - _camera.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        internal void Configure(GameObject barRoot, Transform fillTransform, Renderer fillRend)
        {
            root = barRoot;
            fill = fillTransform;
            fillRenderer = fillRend;
        }
    }
}
