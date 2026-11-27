using TMPro;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// Paints the pickup point in a theme (T022; T048 #87 — «Азот», «Разборы», «Рынок»): the counter's body, top and
    /// accents (the board's frame too), the computer's case, the rack's posts, and the name of the pickup point on the
    /// counter's signs and in the computer's title. Colours are applied as property blocks over the materials; a theme
    /// without recolouring removes them, so the materials show as they are.
    /// </summary>
    public sealed class CounterSkin : MonoBehaviour
    {
        [SerializeField] private Renderer[] bodyRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Renderer[] topRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Renderer[] accentRenderers = System.Array.Empty<Renderer>();
        [Header("T048: the rest of the pickup point")]
        [SerializeField] private Renderer[] computerRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Renderer[] rackRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private TMP_Text[] signs = System.Array.Empty<TMP_Text>();
        [SerializeField] private PvzComputer computer;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>The theme shown now (tests).</summary>
        public string ThemeId { get; private set; } = string.Empty;

        /// <summary>The name of the pickup point shown now (tests).</summary>
        public string Brand { get; private set; } = PvzComputer.DefaultBrand;

        public void Apply(CounterThemeCatalog.Entry theme)
        {
            ThemeId = theme != null ? theme.id : string.Empty;
            var recolor = theme != null && theme.recolor;
            Paint(bodyRenderers, recolor ? theme.body : (Color?)null);
            Paint(topRenderers, recolor ? theme.top : (Color?)null);
            Paint(accentRenderers, recolor ? theme.accent : (Color?)null);
            Paint(computerRenderers, recolor ? theme.computer : (Color?)null);
            Paint(rackRenderers, recolor ? theme.body : (Color?)null);

            Brand = theme != null && !string.IsNullOrWhiteSpace(theme.brand) ? theme.brand : PvzComputer.DefaultBrand;
            foreach (var sign in signs)
                if (sign != null) sign.text = SignText(Brand);
            if (computer != null) computer.SetBrand(Brand);
        }

        /// <summary>The counter's sign: the name of the pickup point, and what it is under it.</summary>
        public static string SignText(string brand) => $"{brand.ToUpperInvariant()}\n<size=45%>ПУНКТ ВЫДАЧИ</size>";

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

        internal void Configure(Renderer[] body, Renderer[] top, Renderer[] accent, TMP_Text[] signTexts = null)
        {
            bodyRenderers = body;
            topRenderers = top;
            accentRenderers = accent;
            if (signTexts != null) signs = signTexts;
        }

        /// <summary>T048: the parts of the pickup point outside the counter prefab (built in the scene).</summary>
        internal void ConfigureRoom(Renderer[] computerParts, Renderer[] rackParts, PvzComputer pvzComputer)
        {
            computerRenderers = computerParts;
            rackRenderers = rackParts;
            computer = pvzComputer;
        }
    }
}
