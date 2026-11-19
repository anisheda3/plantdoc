using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlantDoctor.AI
{
    public enum TracedKind { Lesion, Yellowing }

    /// <summary>One spot or patch found on the scanned leaf. Coordinates are normalised (0..1) with a top-left origin, like <see cref="DetectedRegion"/>.</summary>
    public class TracedFeature
    {
        public TracedKind Kind;
        public int PixelCount;
        /// <summary>Share of the leaf's area this feature covers.</summary>
        public float AreaOfLeaf;
        public Vector2 Centroid;
        public Rect Bounds;
    }

    /// <summary>
    /// Finds the leaf in a scan photo and traces what is wrong on it: dark or brown lesions and yellowed patches,
    /// pixel by pixel, with plain colour analysis (no model needed). The result is used to
    ///   * outline those spots on the scan photo (<see cref="TraceResult.Overlay"/>), and
    ///   * plant them on the 3D plant: the same spots become the disease masks of the twin's leaves and of the
    ///     magnified leaf, so the plant shows the damage where the user's leaf has it (<see cref="TraceTextures"/>).
    ///
    /// It is deliberately simple and says so when it is unsure: if no leaf is found, or nothing stands out,
    /// <see cref="TraceResult.IsUsable"/> is false and the app keeps its authored disease look.
    /// </summary>
    public static class LeafFeatureTracer
    {
        const int MaxLesions = 10;
        const int MaxYellow = 3;

        public static TraceResult Trace(Texture2D image)
        {
            if (image == null || !image.isReadable) return null;
            return Trace(image.GetPixels32(), image.width, image.height);
        }

        /// <param name="pixels">As returned by Texture2D.GetPixels32 (rows from the bottom).</param>
        public static TraceResult Trace(Color32[] pixels, int w, int h)
        {
            var result = new TraceResult { Width = w, Height = h };
            if (pixels == null || w < 16 || h < 16 || pixels.Length < w * h) return result;
            int n = w * h;

            // ---- classify every pixel (top-left origin from here on) ----
            var green = new bool[n];
            var yellow = new bool[n];
            var dark = new bool[n];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = pixels[(h - 1 - y) * w + x];
                    Hsv(c, out float hue, out float s, out float v);
                    int i = y * w + x;
                    if (hue >= 65f && hue <= 170f && s >= TracerSettings.LeafMinSaturation && v >= 0.12f) green[i] = true;
                    else if (hue >= 38f && hue < 65f && s >= 0.35f && v >= 0.45f) yellow[i] = true;
                    else if (v < 0.38f || (hue < 40f && s > 0.2f && v < 0.7f)) dark[i] = true;
                }

            // ---- the leaf: the biggest green/yellow body, closed and with its holes (the lesions) filled ----
            var seed = new bool[n];
            for (int i = 0; i < n; i++) seed[i] = green[i] || (TracerSettings.LeafSeedYellow && yellow[i]);
            seed = Close(seed, w, h, TracerSettings.LeafCloseRadius);
            var leaf = LargestComponent(seed, w, h);
            if (leaf == null) return result;
            leaf = FillHoles(leaf, w, h);

            int leafArea = 0, x0 = w, y0 = h, x1 = -1, y1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (leaf[y * w + x])
                    {
                        leafArea++;
                        if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
                    }
            result.LeafCoverage = leafArea / (float)n;
            if (result.LeafCoverage < 0.06f) return result;   // no leaf worth tracing in the picture
            result.Leaf = leaf;
            result.LeafBoxPx = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            result.LeafBounds = new Rect(x0 / (float)w, y0 / (float)h, (x1 - x0 + 1) / (float)w, (y1 - y0 + 1) / (float)h);

            // ---- the spots ----
            var lesion = new bool[n];
            var yellowPatch = new bool[n];
            if (TracerSettings.UseLab)
                ClassifyByColourDistance(pixels, w, h, leaf, green, leafArea, lesion, yellowPatch);
            else
                for (int i = 0; i < n; i++)
                {
                    lesion[i] = leaf[i] && !green[i] && !yellow[i] && dark[i];
                    yellowPatch[i] = leaf[i] && yellow[i];
                }
            lesion = Open(lesion, w, h, TracerSettings.OpenRadius);
            yellowPatch = Open(yellowPatch, w, h, TracerSettings.OpenRadius);

            var lesionKept = new bool[n];
            var lesionLabels = new int[n];
            int minLesion = Math.Max(10, (int)(TracerSettings.MinLesionFraction * leafArea));
            CollectComponents(lesion, w, h, leafArea, minLesion, MaxLesions, TracedKind.Lesion, result, lesionKept, lesionLabels);

            var yellowKept = new bool[n];
            var yellowLabels = new int[n];
            int minYellow = Math.Max(30, (int)(TracerSettings.MinYellowFraction * leafArea));
            CollectComponents(yellowPatch, w, h, leafArea, minYellow, MaxYellow, TracedKind.Yellowing, result, yellowKept, yellowLabels);

            result.LesionMask = lesionKept;
            result.YellowMask = yellowKept;
            int affected = 0;
            for (int i = 0; i < n; i++) if (lesionKept[i] || yellowKept[i]) affected++;
            result.AffectedFraction = affected / (float)leafArea;
            result.LesionField = BuildLesionField(lesionKept, lesionLabels, result.LesionCount, w, h);
            result.YellowField = BuildYellowField(yellowKept, lesionKept, w, h);
            return result;
        }

        // ------------------------------------------------------------------
        // Affected pixels = far from the leaf's own healthy colour
        // ------------------------------------------------------------------

        static float Srgb(float c) => c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4f);

        static void Lab(Color32 c, out float L, out float a, out float b)
        {
            float r = Srgb(c.r / 255f), g = Srgb(c.g / 255f), bl = Srgb(c.b / 255f);
            float x = (0.4124564f * r + 0.3575761f * g + 0.1804375f * bl) / 0.95047f;
            float y = 0.2126729f * r + 0.7151522f * g + 0.0721750f * bl;
            float z = (0.0193339f * r + 0.1191920f * g + 0.9503041f * bl) / 1.08883f;
            float fx = F(x), fy = F(y), fz = F(z);
            L = 116f * fy - 16f; a = 500f * (fx - fy); b = 200f * (fy - fz);
        }

        static float F(float t) => t > 0.008856f ? (float)Math.Pow(t, 1.0 / 3.0) : 7.787f * t + 16f / 116f;

        static float Median(List<float> v) { v.Sort(); return v[v.Count / 2]; }

        /// <summary>
        /// The leaf's healthy colour is the median CIELAB colour of its green pixels. Every leaf pixel is then given its colour distance
        /// from that (lightness counts for less), the distances are split at their Otsu threshold (not below a minimum), and what is far from
        /// healthy is the damage: lighter and yellower = yellowing, otherwise = lesion. It adapts to each photo's light and to each leaf's green.
        /// </summary>
        static void ClassifyByColourDistance(Color32[] pixels, int w, int h, bool[] leaf, bool[] green, int leafArea, bool[] lesion, bool[] yellowPatch)
        {
            int n = w * h;
            var L = new float[n]; var A = new float[n]; var B = new float[n];
            var rl = new List<float>(); var ra = new List<float>(); var rb = new List<float>();
            int greenInLeaf = 0;
            for (int i = 0; i < n; i++) if (leaf[i] && green[i]) greenInLeaf++;
            bool onlyGreen = greenInLeaf >= 0.05f * leafArea;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!leaf[i]) continue;
                    Lab(pixels[(h - 1 - y) * w + x], out L[i], out A[i], out B[i]);
                    if (!onlyGreen || green[i]) { rl.Add(L[i]); ra.Add(A[i]); rb.Add(B[i]); }
                }
            if (rl.Count == 0) return;
            float lr = Median(rl), ar = Median(ra), br = Median(rb);

            if (TracerSettings.RednessOnly)
            {
                // Otsu split of a* inside the leaf: the redder part of the leaf is the damage.
                if (TracerSettings.ThresholdMode == 1)
                {
                    FlagByDeviation(A, B, L, leaf, br, lr, lesion, yellowPatch);
                    return;
                }
                var ahist = new int[161];
                int acount = 0;
                for (int i = 0; i < n; i++) if (leaf[i]) { ahist[Math.Max(0, Math.Min(160, (int)(A[i] + 80f)))]++; acount++; }
                float at = OtsuThreshold(ahist, acount) - 80f + TracerSettings.RednessOffset;
                double lowSum = 0, highSum = 0; int lowN = 0, highN = 0;
                for (int i = 0; i < n; i++)
                    if (leaf[i]) { if (A[i] > at) { highSum += A[i]; highN++; } else { lowSum += A[i]; lowN++; } }
                if (lowN == 0 || highN == 0 || highSum / highN - lowSum / lowN < TracerSettings.MinRednessGap)
                {
                    // No clear split: either the leaf is healthy, or the damage is too small to move the Otsu split. In mode 2 look
                    // for small spots by how far they stand out from the leaf's own median colour (strict, so a healthy leaf stays clean).
                    if (TracerSettings.ThresholdMode == 2) FlagByDeviation(A, B, L, leaf, br, lr, lesion, yellowPatch);
                    return;
                }
                for (int i = 0; i < n; i++)
                {
                    if (!leaf[i] || A[i] <= at) continue;
                    bool yellowish = B[i] - br > TracerSettings.YellowDeltaB && L[i] - lr > TracerSettings.YellowMinLightnessDrop;
                    if (yellowish) yellowPatch[i] = true; else lesion[i] = true;
                }
                return;
            }

            var delta = new float[n];
            var hist = new int[101];
            int count = 0;
            float wl = TracerSettings.LightnessWeight;
            for (int i = 0; i < n; i++)
            {
                if (!leaf[i]) continue;
                float dl = L[i] - lr, da = A[i] - ar, db = B[i] - br;
                float d = (float)Math.Sqrt(wl * dl * dl + da * da + db * db);
                delta[i] = d;
                hist[Math.Min(100, (int)d)]++;
                count++;
            }
            float threshold = TracerSettings.MinDelta;
            if (TracerSettings.OtsuBlend > 0f) threshold = Math.Max(threshold, TracerSettings.OtsuBlend * OtsuThreshold(hist, count));

            for (int i = 0; i < n; i++)
            {
                if (!leaf[i] || delta[i] <= threshold) continue;
                bool yellowish = B[i] - br > TracerSettings.YellowDeltaB && L[i] - lr > TracerSettings.YellowMinLightnessDrop;
                if (yellowish) yellowPatch[i] = true; else lesion[i] = true;
            }
        }

        /// <summary>Healthy tissue is most of the leaf: its a* is the median and its spread the robust deviation; damage sticks out redder than both allow.</summary>
        static void FlagByDeviation(float[] A, float[] B, float[] L, bool[] leaf, float br, float lr, bool[] lesion, bool[] yellowPatch)
        {
            int n = A.Length;
            var av = new List<float>();
            for (int i = 0; i < n; i++) if (leaf[i]) av.Add(A[i]);
            if (av.Count == 0) return;
            float med = Median(av);
            for (int i = 0; i < av.Count; i++) av[i] = Math.Abs(av[i] - med);
            float mad = Median(av) * 1.4826f;
            float limit = med + Math.Max(TracerSettings.MinDeviation, TracerSettings.MadK * mad);
            for (int i = 0; i < n; i++)
            {
                if (!leaf[i] || A[i] <= limit) continue;
                bool yellowish = B[i] - br > TracerSettings.YellowDeltaB && L[i] - lr > TracerSettings.YellowMinLightnessDrop;
                if (yellowish) yellowPatch[i] = true; else lesion[i] = true;
            }
        }

        static float OtsuThreshold(int[] hist, int total)
        {
            double sum = 0;
            for (int i = 0; i < hist.Length; i++) sum += i * (double)hist[i];
            double sumB = 0, best = -1, threshold = 0;
            int wB = 0;
            for (int t = 0; t < hist.Length; t++)
            {
                wB += hist[t];
                if (wB == 0) continue;
                int wF = total - wB;
                if (wF == 0) break;
                sumB += t * (double)hist[t];
                double mB = sumB / wB, mF = (sum - sumB) / wF;
                double between = (double)wB * wF * (mB - mF) * (mB - mF);
                if (between > best) { best = between; threshold = t; }
            }
            return (float)threshold;
        }

        // ------------------------------------------------------------------
        // Colour
        // ------------------------------------------------------------------

        static void Hsv(Color32 c, out float hue, out float s, out float v)
        {
            float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
            float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            float d = max - min;
            v = max;
            s = max <= 1e-5f ? 0f : d / max;
            if (d <= 1e-5f) { hue = 0f; return; }
            if (max == r) hue = 60f * (((g - b) / d) % 6f);
            else if (max == g) hue = 60f * ((b - r) / d + 2f);
            else hue = 60f * ((r - g) / d + 4f);
            if (hue < 0f) hue += 360f;
        }

        // ------------------------------------------------------------------
        // Morphology and components
        // ------------------------------------------------------------------

        static bool[] Dilate(bool[] m, int w, int h, int r)
        {
            var tmp = new bool[m.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool on = false;
                    for (int k = Math.Max(0, x - r); k <= Math.Min(w - 1, x + r) && !on; k++) on = m[y * w + k];
                    tmp[y * w + x] = on;
                }
            var o = new bool[m.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool on = false;
                    for (int k = Math.Max(0, y - r); k <= Math.Min(h - 1, y + r) && !on; k++) on = tmp[k * w + x];
                    o[y * w + x] = on;
                }
            return o;
        }

        static bool[] Erode(bool[] m, int w, int h, int r)
        {
            var inv = new bool[m.Length];
            for (int i = 0; i < m.Length; i++) inv[i] = !m[i];
            var d = Dilate(inv, w, h, r);
            for (int i = 0; i < d.Length; i++) d[i] = !d[i];
            return d;
        }

        static bool[] Close(bool[] m, int w, int h, int r) => Erode(Dilate(m, w, h, r), w, h, r);
        static bool[] Open(bool[] m, int w, int h, int r) => Dilate(Erode(m, w, h, r), w, h, r);

        static bool[] LargestComponent(bool[] m, int w, int h)
        {
            var labels = new int[m.Length];
            int best = 0, bestSize = 0, next = 0;
            var stack = new Stack<int>();
            for (int s = 0; s < m.Length; s++)
            {
                if (!m[s] || labels[s] != 0) continue;
                next++;
                int size = 0;
                labels[s] = next; stack.Push(s);
                while (stack.Count > 0)
                {
                    int p = stack.Pop(); size++;
                    int px = p % w, py = p / w;
                    if (px > 0 && m[p - 1] && labels[p - 1] == 0) { labels[p - 1] = next; stack.Push(p - 1); }
                    if (px < w - 1 && m[p + 1] && labels[p + 1] == 0) { labels[p + 1] = next; stack.Push(p + 1); }
                    if (py > 0 && m[p - w] && labels[p - w] == 0) { labels[p - w] = next; stack.Push(p - w); }
                    if (py < h - 1 && m[p + w] && labels[p + w] == 0) { labels[p + w] = next; stack.Push(p + w); }
                }
                if (size > bestSize) { bestSize = size; best = next; }
            }
            if (best == 0) return null;
            var o = new bool[m.Length];
            for (int i = 0; i < o.Length; i++) o[i] = labels[i] == best;
            return o;
        }

        /// <summary>Everything not connected to the image border through empty pixels is inside the shape.</summary>
        static bool[] FillHoles(bool[] m, int w, int h)
        {
            var outside = new bool[m.Length];
            var stack = new Stack<int>();
            void Seed(int p) { if (!m[p] && !outside[p]) { outside[p] = true; stack.Push(p); } }
            for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }
            while (stack.Count > 0)
            {
                int p = stack.Pop();
                int px = p % w, py = p / w;
                if (px > 0) Seed(p - 1);
                if (px < w - 1) Seed(p + 1);
                if (py > 0) Seed(p - w);
                if (py < h - 1) Seed(p + w);
            }
            var o = new bool[m.Length];
            for (int i = 0; i < o.Length; i++) o[i] = !outside[i];
            return o;
        }

        /// <summary>Labels 8-connected components, keeps the biggest ones above <paramref name="minPixels"/> and records them as features.</summary>
        static void CollectComponents(bool[] m, int w, int h, int leafArea, int minPixels, int max, TracedKind kind,
            TraceResult result, bool[] kept, int[] keptLabels)
        {
            var seen = new bool[m.Length];
            var found = new List<(List<int> px, int minX, int minY, int maxX, int maxY)>();
            var stack = new Stack<int>();
            for (int s = 0; s < m.Length; s++)
            {
                if (!m[s] || seen[s]) continue;
                var px = new List<int>();
                int minX = w, minY = h, maxX = -1, maxY = -1;
                seen[s] = true; stack.Push(s);
                while (stack.Count > 0)
                {
                    int p = stack.Pop(); px.Add(p);
                    int x = p % w, y = p / w;
                    if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            int q = ny * w + nx;
                            if (m[q] && !seen[q]) { seen[q] = true; stack.Push(q); }
                        }
                }
                if (px.Count >= minPixels) found.Add((px, minX, minY, maxX, maxY));
            }
            found.Sort((a, b) => b.px.Count.CompareTo(a.px.Count));
            int label = 0;
            foreach (var f in found)
            {
                if (label >= max) break;
                label++;
                double cx = 0, cy = 0;
                foreach (int p in f.px) { kept[p] = true; keptLabels[p] = label; cx += p % w; cy += p / w; }
                cx /= f.px.Count; cy /= f.px.Count;
                result.Features.Add(new TracedFeature
                {
                    Kind = kind,
                    PixelCount = f.px.Count,
                    AreaOfLeaf = f.px.Count / (float)leafArea,
                    Centroid = new Vector2((float)((cx + 0.5) / w), (float)((cy + 0.5) / h)),
                    Bounds = new Rect(f.minX / (float)w, f.minY / (float)h, (f.maxX - f.minX + 1) / (float)w, (f.maxY - f.minY + 1) / (float)h)
                });
            }
            if (kind == TracedKind.Lesion) result.LesionCount = label; else result.YellowCount = label;
        }

        // ------------------------------------------------------------------
        // Distance fields: how the traced spots become disease masks
        // ------------------------------------------------------------------

        /// <summary>Distance in pixels from every pixel to the nearest true pixel of <paramref name="target"/> (two-pass chamfer).</summary>
        internal static float[] DistanceTo(bool[] target, int w, int h)
        {
            const float Far = 1e6f;
            var d = new float[target.Length];
            for (int i = 0; i < d.Length; i++) d[i] = target[i] ? 0f : Far;
            const float D = 1.4142f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x; float v = d[i];
                    if (x > 0) v = Math.Min(v, d[i - 1] + 1f);
                    if (y > 0)
                    {
                        v = Math.Min(v, d[i - w] + 1f);
                        if (x > 0) v = Math.Min(v, d[i - w - 1] + D);
                        if (x < w - 1) v = Math.Min(v, d[i - w + 1] + D);
                    }
                    d[i] = v;
                }
            for (int y = h - 1; y >= 0; y--)
                for (int x = w - 1; x >= 0; x--)
                {
                    int i = y * w + x; float v = d[i];
                    if (x < w - 1) v = Math.Min(v, d[i + 1] + 1f);
                    if (y < h - 1)
                    {
                        v = Math.Min(v, d[i + w] + 1f);
                        if (x < w - 1) v = Math.Min(v, d[i + w + 1] + D);
                        if (x > 0) v = Math.Min(v, d[i + w - 1] + D);
                    }
                    d[i] = v;
                }
            return d;
        }

        /// <summary>
        /// The shader shows a lesion pixel once the lesion density passes 1 - mask value, so spots start at their
        /// centres (value 1) and grow toward their rims (about 0.3) and a little beyond as the disease advances.
        /// </summary>
        static float[] BuildLesionField(bool[] kept, int[] labels, int count, int w, int h)
        {
            var field = new float[kept.Length];
            if (count == 0) return field;
            var inv = new bool[kept.Length];
            for (int i = 0; i < inv.Length; i++) inv[i] = !kept[i];
            var inside = DistanceTo(inv, w, h);       // depth below the rim
            var outside = DistanceTo(kept, w, h);     // distance from the rim
            var depthMax = new float[count + 1];
            for (int i = 0; i < kept.Length; i++)
                if (kept[i] && inside[i] > depthMax[labels[i]]) depthMax[labels[i]] = inside[i];
            for (int i = 0; i < field.Length; i++)
            {
                if (kept[i]) field[i] = 0.32f + 0.68f * Mathf.Clamp01(inside[i] / Mathf.Max(1f, depthMax[labels[i]]));
                else if (outside[i] < 4.5f) field[i] = 0.3f * (1f - outside[i] / 4.5f);
            }
            return field;
        }

        /// <summary>Chlorosis: the yellowed patches themselves, plus a soft halo around every lesion, then smoothed.</summary>
        static float[] BuildYellowField(bool[] yellowKept, bool[] lesionKept, int w, int h)
        {
            var field = new float[yellowKept.Length];
            var near = DistanceTo(lesionKept, w, h);
            for (int i = 0; i < field.Length; i++)
            {
                float v = yellowKept[i] ? 1f : 0f;
                if (near[i] < 9f) v = Math.Max(v, 0.6f * (1f - near[i] / 9f));
                field[i] = v;
            }
            return Blur(Blur(field, w, h), w, h);
        }

        static float[] Blur(float[] a, int w, int h)
        {
            var t = new float[a.Length];
            var o = new float[a.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float s = 0; int c = 0;
                    for (int k = Math.Max(0, x - 2); k <= Math.Min(w - 1, x + 2); k++) { s += a[y * w + k]; c++; }
                    t[y * w + x] = s / c;
                }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float s = 0; int c = 0;
                    for (int k = Math.Max(0, y - 2); k <= Math.Min(h - 1, y + 2); k++) { s += t[k * w + x]; c++; }
                    o[y * w + x] = s / c;
                }
            return o;
        }
    }

    /// <summary>What was traced on one scan. Owns the textures made from it; dispose when the scan is replaced.</summary>
    public sealed class TraceResult : IDisposable
    {
        public int Width, Height;
        /// <summary>The leaf's bounding box, normalised, top-left origin.</summary>
        public Rect LeafBounds;
        public RectInt LeafBoxPx;
        /// <summary>Share of the picture covered by the leaf.</summary>
        public float LeafCoverage;
        public readonly List<TracedFeature> Features = new List<TracedFeature>();
        public int LesionCount, YellowCount;
        /// <summary>Share of the leaf's area covered by the traced spots and patches (0..1), measured from the photo's colours.</summary>
        public float AffectedFraction;

        public bool[] Leaf, LesionMask, YellowMask;   // per pixel, rows from the top (for evaluation)
        internal float[] LesionField, YellowField;

        Texture2D _heroLesion, _heroYellow, _twinLesion, _twinYellow, _overlay;

        /// <summary>True when a leaf was found and something on it was traced; only then is the trace used to look like the scan.</summary>
        public bool IsUsable => Leaf != null && (LesionCount > 0 || YellowCount > 0);

        /// <summary>Masks for the magnified leaf: the leaf's own layout, one to one.</summary>
        public Texture2D HeroLesionMask { get { EnsureTextures(); return _heroLesion; } }
        public Texture2D HeroYellowMask { get { EnsureTextures(); return _heroYellow; } }
        /// <summary>Masks for the plant's many small leaflets: the same spots, repeated and mirrored so every leaflet shows some.</summary>
        public Texture2D TwinLesionMask { get { EnsureTextures(); return _twinLesion; } }
        public Texture2D TwinYellowMask { get { EnsureTextures(); return _twinYellow; } }
        /// <summary>Transparent picture, as big as the scan, with the leaf and its traced spots outlined.</summary>
        public Texture2D Overlay { get { if (_overlay == null && IsUsable) _overlay = TraceTextures.BuildOverlay(this); return _overlay; } }

        void EnsureTextures()
        {
            if (_heroLesion != null || !IsUsable) return;
            TraceTextures.BuildMasks(this, out _heroLesion, out _heroYellow, out _twinLesion, out _twinYellow);
        }

        /// <summary>The traced spots in the form the rest of the app already understands.</summary>
        public List<DetectedRegion> ToRegions()
        {
            var list = new List<DetectedRegion>();
            foreach (var f in Features)
                list.Add(new DetectedRegion
                {
                    type = f.Kind == TracedKind.Lesion ? "lesion" : "yellowing",
                    bounds = f.Bounds,
                    score = Mathf.Clamp(0.35f + f.AreaOfLeaf * 3f, 0.35f, 0.95f)
                });
            return list;
        }

        /// <summary>"4 lesions, 1 yellow area" for captions.</summary>
        public string Summary()
        {
            var parts = new List<string>();
            if (LesionCount > 0) parts.Add(LesionCount == 1 ? "1 lesion" : $"{LesionCount} lesions");
            if (YellowCount > 0) parts.Add(YellowCount == 1 ? "1 yellow area" : $"{YellowCount} yellow areas");
            return string.Join(", ", parts);
        }

        /// <summary>
        /// "7 lesions, 1 yellow area · about 15% of the leaf" - the share is rounded to the nearest 5% because a colour-based
        /// measurement does not deserve more digits (on real in-the-wild photos it is only a rough guide; see docs/DECISIONS.md).
        /// </summary>
        public string Caption()
        {
            int pct = Mathf.RoundToInt(AffectedFraction * 100f / 5f) * 5;
            string share = AffectedFraction < 0.03f ? "under 5% of the leaf" : $"about {pct}% of the leaf";
            string what = Summary();
            return string.IsNullOrEmpty(what) ? share : $"{what} · {share}";
        }

        public void Dispose()
        {
            foreach (var t in new[] { _heroLesion, _heroYellow, _twinLesion, _twinYellow, _overlay })
                if (t != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(t); else UnityEngine.Object.DestroyImmediate(t); }
            _heroLesion = _heroYellow = _twinLesion = _twinYellow = _overlay = null;
        }
    }
}
