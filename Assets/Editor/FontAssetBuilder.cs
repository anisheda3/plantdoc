#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// Builds TextMeshPro font assets from the bundled Inter TTFs (SIL Open Font
    /// License, see Assets/Fonts/Inter/OFL.txt). Assets are dynamic, so any glyph used
    /// at runtime is rasterised on demand; the common set is baked up front so the first
    /// frame never stutters. LiberationSans is kept as a fallback for rare symbols.
    /// </summary>
    public static class FontAssetBuilder
    {
        const string SourceFolder = "Assets/Fonts/Inter";
        const string OutputFolder = "Assets/Fonts/Inter/SDF";

        const string Prebake =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "°·•–—‘’“”…×→←↑↓";

        public static Dictionary<string, TMP_FontAsset> BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder(SourceFolder, "SDF");

            var result = new Dictionary<string, TMP_FontAsset>();
            foreach (var weight in new[] { "Regular", "Medium", "SemiBold", "Bold" })
            {
                var asset = Build("Inter", "Inter", weight);
                if (asset != null) result[weight] = asset;
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        [MenuItem("Tools/AI Plant Doctor/Build Fonts")]
        static void BuildMenu() => BuildAll();

        static TMP_FontAsset Build(string family, string folder, string weight)
        {
            string sdfFolder = $"Assets/Fonts/{folder}/SDF";
            if (!AssetDatabase.IsValidFolder(sdfFolder)) AssetDatabase.CreateFolder($"Assets/Fonts/{folder}", "SDF");
            string ttfPath = $"Assets/Fonts/{folder}/{family}-{weight}.ttf";
            string assetPath = $"{sdfFolder}/{family}-{weight} SDF.asset";

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogWarning($"[AI Plant Doctor] Font not found: {ttfPath}. The UI will use LiberationSans.");
                return null;
            }

            var asset = TMP_FontAsset.CreateFontAsset(font, 72, 7, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);
            if (asset == null)
            {
                Debug.LogWarning($"[AI Plant Doctor] Could not create a font asset from {ttfPath}.");
                return null;
            }

            asset.name = $"{family}-{weight} SDF";
            AssetDatabase.CreateAsset(asset, assetPath);

            // The atlas texture and material live inside the font asset file.
            var atlas = asset.atlasTexture;
            atlas.name = $"{asset.name} Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);
            asset.material.name = $"{asset.name} Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);

            asset.TryAddCharacters(Prebake);

            var fallback = TMP_Settings.defaultFontAsset;
            if (fallback != null && fallback != asset)
                asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };

            EditorUtility.SetDirty(atlas);
            EditorUtility.SetDirty(asset);
            Debug.Log($"[AI Plant Doctor] Created {assetPath}");
            return asset;
        }
    }
}
#endif
