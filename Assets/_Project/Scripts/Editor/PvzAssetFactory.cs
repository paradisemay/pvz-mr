using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;

namespace Pvz.Editor
{
    /// <summary>Creates (or reuses) textures, materials and the Cyrillic font used by the prototype.</summary>
    internal static class PvzAssetFactory
    {
        public const string Root = "Assets/_Project";
        public const string MaterialsDir = Root + "/Art/Materials";
        public const string TexturesDir = Root + "/Art/Textures";
        public const string FontsDir = Root + "/Art/Fonts";
        public const string PrefabsDir = Root + "/Prefabs";
        public const string SettingsDir = Root + "/Settings";
        public const string ScenesDir = Root + "/Scenes";

        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string UnlitShader = "Universal Render Pipeline/Unlit";

        private const string FontCharacters =
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
            " .,:;!?-–—+−=/\\()[]«»\"'№%*#@&_<>…";

        public static void EnsureFolders()
        {
            foreach (var dir in new[] { MaterialsDir, TexturesDir, FontsDir, PrefabsDir, SettingsDir, ScenesDir })
            {
                if (AssetDatabase.IsValidFolder(dir)) continue;
                var parent = Path.GetDirectoryName(dir)?.Replace('\\', '/');
                var name = Path.GetFileName(dir);
                if (parent != null && !AssetDatabase.IsValidFolder(parent))
                {
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(parent)?.Replace('\\', '/'), Path.GetFileName(parent));
                }
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        // ---------- Materials ----------

        // Materials are only initialised when created: an artist may retune existing ones and a rebuild keeps that.

        public static Material Lit(string name, Color color, float smoothness = 0.2f)
        {
            var mat = LoadOrCreateMaterial(name, LitShader, out var created);
            if (!created) return mat;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material Unlit(string name, Color color, Texture texture = null)
        {
            var mat = LoadOrCreateMaterial(name, UnlitShader, out var created);
            if (texture != null) mat.SetTexture("_BaseMap", texture);
            if (created) mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material UnlitTransparent(string name, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/{name}.mat");
            if (existing != null) return existing;
            var mat = Unlit(name, color);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material LoadOrCreateMaterial(string name, string shaderName, out bool created)
        {
            var path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            created = mat == null;
            if (created)
            {
                mat = new Material(Shader.Find(shaderName)) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        // ---------- QR textures ----------

        /// <summary>
        /// A QR-looking label (finder patterns, timing lines, pseudo-random data from the id).
        /// It is decorative: the game identifies boxes by component, not by decoding the image.
        /// </summary>
        public static Texture2D QrTexture(string boxId)
        {
            const int modules = 29;
            const int quiet = 3;
            const int scale = 8;
            var size = (modules + quiet * 2) * scale;
            var path = $"{TexturesDir}/QR_{boxId}.png";

            var grid = new bool[modules, modules];
            var rng = new System.Random(StableHash(boxId));
            for (var y = 0; y < modules; y++)
            for (var x = 0; x < modules; x++)
                grid[x, y] = rng.Next(100) < 48;

            void Finder(int ox, int oy)
            {
                for (var y = -1; y <= 7; y++)
                for (var x = -1; x <= 7; x++)
                {
                    var gx = ox + x;
                    var gy = oy + y;
                    if (gx < 0 || gy < 0 || gx >= modules || gy >= modules) continue;
                    var ring = Mathf.Max(Mathf.Abs(x - 3), Mathf.Abs(y - 3));
                    grid[gx, gy] = ring != 2 && ring != 4; // 7x7 outer ring, white gap, 3x3 core, white separator
                }
            }

            Finder(0, 0);
            Finder(modules - 7, 0);
            Finder(0, modules - 7);
            for (var i = 8; i < modules - 8; i++)
            {
                grid[i, 6] = i % 2 == 0;
                grid[6, i] = i % 2 == 0;
            }
            // alignment pattern
            for (var y = -2; y <= 2; y++)
            for (var x = -2; x <= 2; x++)
            {
                var ring = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                grid[modules - 7 + x, modules - 7 + y] = ring != 1;
            }

            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            var black = new Color32(15, 15, 18, 255);
            var white = new Color32(250, 250, 250, 255);
            var pixels = new Color32[size * size];
            for (var py = 0; py < size; py++)
            for (var px = 0; px < size; px++)
            {
                var mx = px / scale - quiet;
                var my = (size - 1 - py) / scale - quiet; // row 0 at the top of the image
                var dark = mx >= 0 && my >= 0 && mx < modules && my < modules && grid[mx, my];
                pixels[py * size + px] = dark ? black : white;
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static int StableHash(string s)
        {
            unchecked
            {
                var h = (int)2166136261;
                foreach (var c in s) h = (h ^ c) * 16777619;
                return h;
            }
        }

        // ---------- Font ----------

        /// <summary>Dynamic TMP font asset from Liberation Sans with Cyrillic glyphs pre-baked.</summary>
        public static TMP_FontAsset CyrillicFont()
        {
            var path = $"{FontsDir}/PvzSans SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;

            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            if (source == null)
            {
                Debug.LogWarning("[PvzAssetFactory] LiberationSans.ttf not found; using TMP default font.");
                return TMP_Settings.defaultFontAsset;
            }

            var font = TMP_FontAsset.CreateFontAsset(source, 72, 7, GlyphRenderMode.SDFAA, 2048, 2048,
                AtlasPopulationMode.Dynamic, false);
            font.name = "PvzSans SDF";
            AssetDatabase.CreateAsset(font, path);
            font.atlasTextures[0].name = "PvzSans Atlas";
            AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
            font.material.name = "PvzSans Material";
            AssetDatabase.AddObjectToAsset(font.material, font);

            if (!font.TryAddCharacters(FontCharacters, out var missing))
                Debug.LogWarning($"[PvzAssetFactory] Font is missing glyphs: {missing}");
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }

        // ---------- Prefab helpers ----------

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos,
            Vector3 localScale, Material material, bool keepCollider = false, Quaternion? localRot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        public static TextMeshPro Text(string name, Transform parent, Vector3 localPos, Vector2 size, string text,
            TMP_FontAsset font, Color color, float maxFontSize, TextAlignmentOptions alignment = TextAlignmentOptions.Center,
            Quaternion? localRot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font;
            tmp.text = text;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.05f;
            tmp.fontSizeMax = maxFontSize;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.rectTransform.sizeDelta = size;
            return tmp;
        }
    }
}
