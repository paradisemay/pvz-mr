using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// Paints the counter in a career theme (T022). Colours are applied as property blocks over the materials;
    /// a theme without recolouring removes them, so the materials (and an artist's work on them) show as they are.
    /// </summary>
    public sealed class CounterSkin : MonoBehaviour
    {
        [SerializeField] private Renderer[] bodyRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Renderer[] topRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Renderer[] accentRenderers = System.Array.Empty<Renderer>();

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>The theme shown now (tests).</summary>
        public string ThemeId { get; private set; } = string.Empty;

        public void Apply(CounterThemeCatalog.Entry theme)
        {
            ThemeId = theme != null ? theme.id : string.Empty;
            var recolor = theme != null && theme.recolor;
            Paint(bodyRenderers, recolor ? theme.body : (Color?)null);
            Paint(topRenderers, recolor ? theme.top : (Color?)null);
            Paint(accentRenderers, recolor ? theme.accent : (Color?)null);
        }

        private static void Paint(Renderer[] renderers, Color? color)
        {
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (color == null)
                {
                    r.SetPropertyBlock(null);
                    continue;
                }
                var block = new MaterialPropertyBlock();
                block.SetColor(BaseColor, color.Value);
                r.SetPropertyBlock(block);
            }
        }

        internal void Configure(Renderer[] body, Renderer[] top, Renderer[] accent)
        {
            bodyRenderers = body;
            topRenderers = top;
            accentRenderers = accent;
        }
    }
}
