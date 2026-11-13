using Pvz.Core;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// Bar above a customer's head: green while they wait calmly, yellow from the moment they start hurrying
    /// the player (<see cref="Shift.HurryFraction"/>), red just before they leave. Faces the player via <see cref="Billboard"/>.
    /// </summary>
    public sealed class PatienceBar : MonoBehaviour
    {
        private const float MinChange = 1f / 255f;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private GameObject root;
        [SerializeField] private Transform fill;
        [SerializeField] private Renderer fillRenderer;
        [SerializeField] private Color calm = new Color(0.3f, 0.85f, 0.4f);
        [SerializeField] private Color hurrying = new Color(0.98f, 0.8f, 0.2f);
        [SerializeField] private Color leaving = new Color(0.95f, 0.3f, 0.25f);

        private MaterialPropertyBlock _block;

        public bool IsVisible => root != null && root.activeSelf;
        public float Value { get; private set; } = -1f;

        /// <summary>Remaining share of patience at which the customer starts hurrying the player.</summary>
        private static float HurryAt => (float)(1.0 - Shift.HurryFraction);

        /// <summary>Shows the remaining share of patience (0..1), or hides the bar for null.</summary>
        public void Show(double? remaining)
        {
            if (root == null) return;
            if (!remaining.HasValue)
            {
                if (root.activeSelf) root.SetActive(false);
                Value = -1f;
                return;
            }
            if (!root.activeSelf) root.SetActive(true);

            var value = Mathf.Clamp01((float)remaining.Value);
            if (Mathf.Abs(value - Value) < MinChange) return; // nothing visible changed
            Value = value;

            if (fill != null)
            {
                // The fill is anchored at its left edge: scale X and shift it so it shrinks to the left.
                var scale = fill.localScale;
                scale.x = Mathf.Max(0.0001f, value);
                fill.localScale = scale;
                fill.localPosition = new Vector3((value - 1f) * 0.5f, fill.localPosition.y, fill.localPosition.z);
            }
            if (fillRenderer != null)
            {
                _block ??= new MaterialPropertyBlock();
                fillRenderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColor, ColorFor(value));
                fillRenderer.SetPropertyBlock(_block);
            }
        }

        private Color ColorFor(float value)
        {
            if (value >= HurryAt) return Color.Lerp(hurrying, calm, (value - HurryAt) / (1f - HurryAt));
            return Color.Lerp(leaving, hurrying, value / HurryAt);
        }

        internal void Configure(GameObject barRoot, Transform fillTransform, Renderer fillRend)
        {
            root = barRoot;
            fill = fillTransform;
            fillRenderer = fillRend;
        }
    }
}
