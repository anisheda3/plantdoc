using UnityEngine;

namespace PlantDoctor.AI
{
    /// <summary>
    /// Turns a <see cref="TraceResult"/> into pictures: the outline drawn over the scan photo, and the disease masks that
    /// plant the traced spots on the 3D leaves.
    /// </summary>
    public static class TraceTextures
    {
        public const int MaskSize = 256;
        /// <summary>Margin of the leaf inside the masks; the same numbers as LeafSpecimen.MaskToLeaf (mask = 0.05 + 0.9 * leaf).</summary>
        const float Margin = 0.05f, Span = 0.9f;

        static readonly Color32 LeafColor = new Color32(115, 242, 178, 235);
        static readonly Color32 LesionColor = new Color32(255, 96, 82, 255);
        static readonly Color32 YellowColor = new Color32(255, 204, 64, 255);

        // ------------------------------------------------------------------
        // Outline over the photo
        // ------------------------------------------------------------------

        public static Texture2D BuildOverlay(TraceResult t)
        {
            int w = t.Width, h = t.Height;
            var px = new Color32[w * h];
            Paint(px, Outline(t.Leaf, w, h), w, h, LeafColor);
            Paint(px, Outline(t.YellowMask, w, h), w, h, YellowColor);
            Paint(px, Outline(t.LesionMask, w, h), w, h, LesionColor);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "TraceOverlay", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>Edge pixels of a shape (it has a neighbour outside), thickened to three pixels.</summary>
        static bool[] Outline(bool[] m, int w, int h)
        {
            var edge = new bool[w * h];
            if (m == null) return edge;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!m[i]) continue;
                    bool border = x == 0 || y == 0 || x == w - 1 || y == h - 1
                                  || !m[i - 1] || !m[i + 1] || !m[i - w] || !m[i + w];
                    if (!border) continue;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < w && ny < h) edge[ny * w + nx] = true;
                        }
                }
            return edge;
        }

        /// <summary>Draws edge pixels (image rows from the top) into texture rows (from the bottom).</summary>
        static void Paint(Color32[] px, bool[] edge, int w, int h, Color32 color)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (edge[y * w + x]) px[(h - 1 - y) * w + x] = color;
        }

        // ------------------------------------------------------------------
        // Masks for the 3D leaves
        // ------------------------------------------------------------------

        public static void BuildMasks(TraceResult t, out Texture2D heroLesion, out Texture2D heroYellow,
            out Texture2D twinLesion, out Texture2D twinYellow)
        {
            var hl = LeafLayout(t, t.LesionField);
            var hy = LeafLayout(t, t.YellowField);
            heroLesion = ToTexture(hl, "TracedLesionMask");
            heroYellow = ToTexture(hy, "TracedYellowMask");
            twinLesion = ToTexture(Tile(hl), "TracedLesionMaskTiled");
            twinYellow = ToTexture(Tile(hy), "TracedYellowMaskTiled");
        }

        /// <summary>
        /// Lays the photo's field out like the leaf specimen's texture: the leaf's box fills the area inside the margin, with its
        /// long side running along the leaf (a leaf photographed sideways is turned), base at the bottom.
        /// </summary>
        static float[] LeafLayout(TraceResult t, float[] field)
        {
            var o = new float[MaskSize * MaskSize];
            var box = t.LeafBoxPx;
            bool turn = box.width > box.height * 1.15f;
            for (int my = 0; my < MaskSize; my++)
            {
                float lv = ((my + 0.5f) / MaskSize - Margin) / Span;
                if (lv < 0f || lv > 1f) continue;
                for (int mx = 0; mx < MaskSize; mx++)
                {
                    float lu = ((mx + 0.5f) / MaskSize - Margin) / Span;
                    if (lu < 0f || lu > 1f) continue;
                    float px, py;
                    if (!turn) { px = box.x + lu * box.width; py = box.y + (1f - lv) * box.height; }
                    else { px = box.x + lv * box.width; py = box.y + lu * box.height; }
                    o[my * MaskSize + mx] = Sample(field, t.Width, t.Height, px, py);
                }
            }
            return o;
        }

        /// <summary>The leaf's layout repeated 2 x 2, mirrored in turn, so a small leaflet's window onto the mask almost always holds spots.</summary>
        static float[] Tile(float[] hero)
        {
            var o = new float[hero.Length];
            int half = MaskSize / 2;
            for (int y = 0; y < MaskSize; y++)
                for (int x = 0; x < MaskSize; x++)
                {
                    int tx = x / half, ty = y / half;
                    float u = ((x % half) + 0.5f) / half, v = ((y % half) + 0.5f) / half;
                    if (tx == 1) u = 1f - u;
                    if (ty == 1) v = 1f - v;
                    o[y * MaskSize + x] = Sample(hero, MaskSize, MaskSize, u * (MaskSize - 1), v * (MaskSize - 1));
                }
            return o;
        }

        static float Sample(float[] a, int w, int h, float x, float y)
        {
            x = Mathf.Clamp(x - 0.5f, 0f, w - 1.001f);
            y = Mathf.Clamp(y - 0.5f, 0f, h - 1.001f);
            int x0 = (int)x, y0 = (int)y;
            float fx = x - x0, fy = y - y0;
            float top = Mathf.Lerp(a[y0 * w + x0], a[y0 * w + x0 + 1], fx);
            float bottom = Mathf.Lerp(a[(y0 + 1) * w + x0], a[(y0 + 1) * w + x0 + 1], fx);
            return Mathf.Lerp(top, bottom, fy);
        }

        static Texture2D ToTexture(float[] a, string name)
        {
            var tex = new Texture2D(MaskSize, MaskSize, TextureFormat.R8, false, true)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var data = new byte[a.Length];
            for (int i = 0; i < a.Length; i++) data[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01(a[i]) * 255f);
            tex.SetPixelData(data, 0);
            tex.Apply(false, false);
            return tex;
        }
    }
}
