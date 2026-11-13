using Pvz.Core;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// Paints the counter in a career theme (T022). Colours are applied as property blocks over the materials;
    /// «Классика» removes them, so the materials (and an artist's work on them) show as they are.
    /// </summary>
    public sealed class CounterSkin : MonoBehaviour
    {
        [SerializeField] private Renderer[] bodyRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Renderer[] topRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Renderer[] accentRenderers = System.Array.Empty<Renderer>();

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>The theme shown now (tests).</summary>
        public string ThemeId { get; private set; } = "classic";

        public void Apply(CounterTheme theme)
        {
            var id = theme != null ? theme.Id : "classic";
            ThemeId = id;
            if (!TryPalette(id, out var body, out var top, out var accent))
            {
                Paint(bodyRenderers, null);
                Paint(topRenderers, null);
                Paint(accentRenderers, null);
                return;
            }
            Paint(bodyRenderers, body);
            Paint(topRenderers, top);
            Paint(accentRenderers, accent);
        }

        /// <summary>Colours of the themes (look only; which theme is open is a Core rule).</summary>
        private static bool TryPalette(string id, out Color body, out Color top, out Color accent)
        {
            switch (id)
            {
                case "mint":
                    body = new Color(0.14f, 0.34f, 0.31f); top = new Color(0.88f, 0.95f, 0.9f); accent = new Color(0.35f, 0.85f, 0.7f);
                    return true;
                case "night":
                    body = new Color(0.08f, 0.08f, 0.15f); top = new Color(0.3f, 0.32f, 0.42f); accent = new Color(0.58f, 0.46f, 1f);
                    return true;
                case "gold":
                    body = new Color(0.27f, 0.19f, 0.08f); top = new Color(0.95f, 0.86f, 0.58f); accent = new Color(1f, 0.8f, 0.2f);
                    return true;
                default:
                    body = top = accent = default;
                    return false; // classic: the materials themselves
            }
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
