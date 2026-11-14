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
        public const string VoiceDir = Root + "/Audio/Voice"; // T043: recorded by tools/make-voice.ps1
        public const string SettingsDir = Root + "/Settings";
        public const string ScenesDir = Root + "/Scenes";

        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string UnlitShader = "Universal Render Pipeline/Unlit";

        private const string FontCharacters =
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
            " .,:;!?-–—+−=/\\()[]«»\"'№%*#@&_<>…·";

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

        /// <summary>
        /// A physics material (T037): cardboard boxes that grip the shelves and each other instead of sliding like ice.
        /// Created once in the materials folder; kept if it exists (it may have been tuned).
        /// </summary>
        public static PhysicsMaterial PhysicMaterialAsset(string name, float dynamicFriction, float staticFriction, float bounciness)
        {
            var path = $"{MaterialsDir}/{name}.physicMaterial";
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (existing != null) return existing;
            var material = new PhysicsMaterial(name)
            {
                dynamicFriction = dynamicFriction,
                staticFriction = staticFriction,
                bounciness = bounciness,
                frictionCombine = PhysicsMaterialCombine.Maximum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

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

        /// <summary>Particles that take their colour from the particle (confetti of many colours).</summary>
        public static Material ParticlesUnlit(string name)
        {
            var mat = LoadOrCreateMaterial(name, "Universal Render Pipeline/Particles/Unlit", out var created);
            if (created) mat.SetColor("_BaseColor", Color.white);
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

        /// <summary>Saves the QR label of a box as a PNG asset (same picture as QrTextures.Create at runtime).</summary>
        public static Texture2D QrTexture(string boxId)
        {
            var path = $"{TexturesDir}/QR_{boxId}.png";
            var tex = Pvz.Runtime.QrTextures.Create(boxId);
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

        /// <summary>
        /// The barcode on a customer's phone (T035): black bars of varying width on white, with quiet margins. Decorative —
        /// the scanner recognises the phone itself, like the QR on boxes.
        /// </summary>
        public static Texture2D BarcodeTexture()
        {
            const int width = 128, height = 32;
            var path = $"{TexturesDir}/Barcode.png";
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var random = new System.Random(4207);
            var bars = new bool[width];
            for (var x = 10; x < width - 10;)
            {
                var run = random.Next(1, 4);
                var black = random.Next(2) == 0;
                for (var i = 0; i < run && x < width - 10; i++, x++) bars[x] = black;
            }
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                tex.SetPixel(x, y, bars[x] && y > 3 && y < height - 4 ? Color.black : Color.white);
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
