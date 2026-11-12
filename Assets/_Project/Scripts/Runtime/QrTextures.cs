using Pvz.Core;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>Turns a <see cref="QrPattern"/> into a texture (used by the editor builder and for supply boxes at runtime).</summary>
    public static class QrTextures
    {
        public const int QuietModules = 3;

        public static Texture2D Create(string id, int pixelsPerModule = 8)
        {
            var grid = QrPattern.Generate(id);
            var modules = QrPattern.Modules;
            var size = (modules + QuietModules * 2) * pixelsPerModule;
            var dark = new Color32(15, 15, 18, 255);
            var light = new Color32(250, 250, 250, 255);

            var pixels = new Color32[size * size];
            for (var py = 0; py < size; py++)
            for (var px = 0; px < size; px++)
            {
                var mx = px / pixelsPerModule - QuietModules;
                var my = (size - 1 - py) / pixelsPerModule - QuietModules; // texture rows go bottom-up
                var isDark = mx >= 0 && my >= 0 && mx < modules && my < modules && grid[mx, my];
                pixels[py * size + px] = isDark ? dark : light;
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = $"QR_{id}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(true);
            return texture;
        }
    }
}
