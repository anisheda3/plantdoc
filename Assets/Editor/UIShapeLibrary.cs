#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// Generates the UI's shape sprites (rounded panels, pills, rings, icons) as PNG assets
    /// from signed-distance functions, so every edge is anti-aliased and nothing binary has
    /// to ship with the project. Re-running overwrites them in place (GUIDs are kept).
    /// </summary>
    public static class UIShapeLibrary
    {
        public const string Folder = "Assets/UI/Generated";
        public const string RoundedPath = Folder + "/S_Rounded.png";
        public const string OutlinePath = Folder + "/S_RoundedOutline.png";
        public const string CirclePath = Folder + "/S_Circle.png";
        public const string RingPath = Folder + "/S_Ring.png";
        public const string ShadowPath = Folder + "/S_SoftShadow.png";
        public const string LeafPath = Folder + "/S_Leaf.png";
        public const string CheckPath = Folder + "/S_Check.png";
        public const string GearPath = Folder + "/S_Gear.png";
        public const string CornerPath = Folder + "/S_Corner.png";
        public const string GradientPath = Folder + "/S_Gradient.png";
        public const string GlowPath = Folder + "/S_Glow.png";
        public const string ArcPath = Folder + "/S_Arc.png";

        /// <summary>Corner radius baked into S_Rounded, in pixels (= canvas units at multiplier 1).</summary>
        public const float RoundedRadius = 48f;

        [MenuItem("Tools/AI Plant Doctor/Build UI Sprites")]
        public static void BuildAll()
        {
            EnsureFolder("Assets/UI");
            EnsureFolder(Folder);

            Save(RoundedPath, 128, 128, (x, y) => Fill(RoundedBox(x - 64, y - 64, 64, 64, RoundedRadius)), border: 52);
            Save(OutlinePath, 128, 128, (x, y) => Stroke(RoundedBox(x - 64, y - 64, 64, 64, RoundedRadius), 3.2f), border: 52);
            Save(CirclePath, 128, 128, (x, y) => Fill(Length(x - 64, y - 64) - 63f));
            Save(RingPath, 128, 128, (x, y) => Stroke(Length(x - 64, y - 64) - 58f, 5f));
            Save(ShadowPath, 160, 160, (x, y) =>
            {
                float d = RoundedBox(x - 80, y - 80, 52, 52, RoundedRadius);
                float t = Mathf.Clamp01((d + 6f) / 32f);
                float a = 1f - t * t * (3f - 2f * t);
                return a * a * 0.9f;
            }, border: 76);
            Save(GlowPath, 256, 256, (x, y) =>
            {
                // A soft radial falloff for the light behind the plant.
                float r = Length(x - 128f, y - 128f) / 126f;
                float a = Mathf.Clamp01(1f - r);
                return a * a * (3f - 2f * a);
            });
            Save(LeafPath, 128, 128, LeafGlyph);
            Save(CheckPath, 96, 96, (x, y) =>
            {
                float d = Mathf.Min(Segment(x, y, 22, 50, 40, 32), Segment(x, y, 40, 32, 74, 68));
                return Fill(d - 7f);
            });
            Save(GearPath, 128, 128, GearGlyph);
            Save(CornerPath, 128, 128, (x, y) =>
            {
                // An L bracket in the bottom-left; the UI rotates it for the other corners.
                float d = Mathf.Min(Segment(x, y, 14, 14, 14, 92), Segment(x, y, 14, 14, 92, 14));
                return Fill(d - 7f);
            });
            Save(ArcPath, 128, 128, (x, y) =>
            {
                // 270° spinner arc with round caps.
                float a = Mathf.Atan2(y - 64, x - 64);
                float ring = Mathf.Abs(Length(x - 64, y - 64) - 54f);
                float capA = Length(x - (64 + 54), y - 64);
                float capB = Length(x - 64, y - (64 - 54));
                bool inArc = !(a > -Mathf.PI * 0.5f && a < 0f);   // excludes the bottom-right quarter
                float d = inArc ? ring : Mathf.Min(capA, capB);
                return Fill(d - 6f);
            });
            Save(GradientPath, 8, 256, (x, y) => Mathf.SmoothStep(0f, 1f, y / 255f), border: 0, wrap: TextureWrapMode.Clamp);

            AssetDatabase.Refresh();
            Debug.Log($"[AI Plant Doctor] UI sprites generated in {Folder}");
        }

        // ------------------------------------------------------------------
        // Glyphs
        // ------------------------------------------------------------------

        /// <summary>Brand mark: a lens-shaped leaf on a 45° diagonal with a cut midrib.</summary>
        static float LeafGlyph(int px, int py)
        {
            float x = px - 64f, y = py - 64f;
            // Rotate 45° so the leaf points to the top-right.
            float c = 0.7071f;
            float u = x * c + y * c, v = -x * c + y * c;
            // Vesica (intersection of two circles) = leaf silhouette.
            float r = 66f, offset = 40f;
            float d1 = Length(u, v - offset) - r;
            float d2 = Length(u, v + offset) - r;
            float leaf = Mathf.Max(d1, d2);
            // Thin cut along the axis that stops short of the tips.
            float d = Mathf.Abs(u) < 40f ? Mathf.Max(leaf, 2.6f - Mathf.Abs(v)) : leaf;
            return Fill(d);
        }

        static float GearGlyph(int px, int py)
        {
            float x = px - 64f, y = py - 64f;
            float a = Mathf.Atan2(y, x);
            float teeth = Mathf.SmoothStep(-0.25f, 0.25f, Mathf.Cos(a * 8f));
            float outer = Length(x, y) - (44f + teeth * 11f);
            float hole = 18f - Length(x, y);
            return Fill(Mathf.Max(outer, hole));
        }

        // ------------------------------------------------------------------
        // SDF helpers (pixels)
        // ------------------------------------------------------------------

        static float Length(float x, float y) => Mathf.Sqrt(x * x + y * y);

        static float RoundedBox(float x, float y, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(x) - (hw - r), qy = Mathf.Abs(y) - (hh - r);
            return Length(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static float Segment(float px, float py, float ax, float ay, float bx, float by)
        {
            float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            return Length(pax - bax * h, pay - bay * h);
        }

        static float Fill(float sd) => Mathf.Clamp01(0.5f - sd);
        static float Stroke(float sd, float width) => Fill(Mathf.Abs(sd) - width * 0.5f);

        // ------------------------------------------------------------------
        // Writing + importing
        // ------------------------------------------------------------------

        static void Save(string path, int w, int h, Func<int, int, float> alpha, int border = 0,
            TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // Sample at the pixel centre.
                    float a = alpha(x, y);
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.SaveAndReimport();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
