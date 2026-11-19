using System.Collections.Generic;
using UnityEngine;

namespace PlantDoctor.Plants
{
    /// <summary>
    /// What the plant stands on instead of a pot: a small heap of soil at the crown, and the root system
    /// spreading over the ground from it.
    ///
    /// The roots are grown the way real fibrous roots are: a few thick primary roots leave the crown, wander
    /// and taper, and carry a dense mesh of side roots (three more orders of them, each thinner, finer and
    /// shorter than its parent) that end in pale hair-thin tips; the finest roots are covered in short root
    /// hairs, and little crumbs of soil cling to them. Everything stays above the ground plane, so the plant
    /// looks planted (not floating, not sunk) on any surface it is placed on.
    ///
    /// The roots are partly buried: near the crown each primary root runs under a ridge of soil (the soil
    /// covers it, and the side roots come out of the ridge); only toward its end does the root leave the soil.
    ///
    /// Roots carry UV0 = (a quarter of the root texture's width, picked per primary root so each one has its own
    /// tint; distance along the root 0..1, dark at the crown and pale at the tip). The soil carries planar UVs.
    /// </summary>
    public static class RootMeshes
    {
        // ------------------------------------------------------------------
        // Soil heap
        // ------------------------------------------------------------------

        /// <summary>A lumpy mound with a few loose clods, centred on the crown. Its rim sits on the ground.</summary>
        public static Mesh Mound(float radius, float height, int seed, int clods = 16)
        {
            var rng = new System.Random(seed);
            var d = new ProceduralMeshes.MeshData();
            const int rings = 9, around = 44;
            float ox = (float)rng.NextDouble() * 50f, oz = (float)rng.NextDouble() * 50f;

            d.Vertices.Add(new Vector3(0f, height, 0f));
            d.Uv0.Add(new Vector2(0.5f, 0.5f)); d.Uv1.Add(Vector2.zero);
            for (int r = 1; r <= rings; r++)
            {
                float t = r / (float)rings;
                for (int j = 0; j < around; j++)
                {
                    float a = j / (float)around * Mathf.PI * 2f;
                    float wobble = 1f + (Mathf.PerlinNoise(ox + Mathf.Cos(a) * 1.6f, oz + Mathf.Sin(a) * 1.6f) - 0.5f) * 0.34f;
                    float rad = radius * t * wobble;
                    float profile = height * Mathf.Pow(1f - t * t, 0.8f);
                    float lump = (Mathf.PerlinNoise(ox + Mathf.Cos(a) * 5f * t + 9f, oz + Mathf.Sin(a) * 5f * t + 4f) - 0.5f) * height * 0.55f * (1f - t * 0.6f);
                    float y = Mathf.Max(0.0015f, profile + lump);
                    var p = new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad);
                    d.Vertices.Add(p);
                    d.Uv0.Add(new Vector2(p.x / 0.14f, p.z / 0.14f));
                    d.Uv1.Add(Vector2.zero);
                }
            }
            for (int j = 0; j < around; j++)
            {
                d.Triangles.Add(0); d.Triangles.Add(1 + (j + 1) % around); d.Triangles.Add(1 + j);
            }
            for (int r = 1; r < rings; r++)
            {
                int inner = 1 + (r - 1) * around, outer = 1 + r * around;
                for (int j = 0; j < around; j++)
                {
                    int j1 = (j + 1) % around;
                    d.Triangles.Add(inner + j); d.Triangles.Add(outer + j1); d.Triangles.Add(outer + j);
                    d.Triangles.Add(inner + j); d.Triangles.Add(inner + j1); d.Triangles.Add(outer + j1);
                }
            }

            // Loose clods around the heap, as on freshly planted ground.
            for (int c = 0; c < clods; c++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = radius * (0.55f + (float)rng.NextDouble() * 0.95f);
                float size = Mathf.Lerp(0.006f, 0.017f, (float)rng.NextDouble());
                AddClod(d, new Vector3(Mathf.Cos(a) * dist, size * 0.45f, Mathf.Sin(a) * dist), size, rng);
            }
            return d.ToMesh("SoilMound");
        }

        static void AddClod(ProceduralMeshes.MeshData d, Vector3 centre, float size, System.Random rng)
        {
            const int seg = 8, ring = 5;
            int start = d.Vertices.Count;
            float seed = (float)rng.NextDouble() * 30f;
            var stretch = new Vector3(1f + (float)rng.NextDouble() * 0.5f, 0.7f + (float)rng.NextDouble() * 0.3f, 1f + (float)rng.NextDouble() * 0.5f);
            for (int i = 0; i <= ring; i++)
            {
                float phi = i / (float)ring * Mathf.PI;
                for (int j = 0; j <= seg; j++)
                {
                    float th = j / (float)seg * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                    float bump = 0.75f + Mathf.PerlinNoise(seed + n.x * 2.2f, seed + n.z * 2.2f + n.y) * 0.5f;
                    var p = centre + Vector3.Scale(n * (size * bump), stretch);
                    p.y = Mathf.Max(p.y, 0.001f);
                    d.Vertices.Add(p);
                    d.Uv0.Add(new Vector2(p.x / 0.14f, p.z / 0.14f));
                    d.Uv1.Add(Vector2.zero);
                }
            }
            d.Grid(start, ring, seg, true);
        }

        // ------------------------------------------------------------------
        // Roots
        // ------------------------------------------------------------------

        public class RootSystem
        {
            public Mesh Roots;
            /// <summary>Crumbs of soil clinging to the roots (soil material).</summary>
            public Mesh Soil;
        }

        class Growth
        {
            public ProceduralMeshes.MeshData D;
            public System.Random Rng;
            public readonly List<Vector3> Anchors = new List<Vector3>();   // where soil can cling
            public readonly List<(List<Vector3> pts, List<float> radii)> Primaries = new List<(List<Vector3>, List<float>)>();
            public int Budget = 62000;                                      // vertices; the finest roots and hairs stop when it is spent
            public bool Hairs = true;                                       // blueberry roots have none
        }

        const int Bands = 4;   // tints in the root texture, one per quarter of its width

        /// <summary>
        /// The root system lying on the ground around the crown. <paramref name="crownRadius"/> is the stem's
        /// radius at the ground; the main roots leave from just outside it.
        /// </summary>
        public static RootSystem Roots(SpeciesProfile p, float crownRadius, System.Random rng)
        {
            var g = new Growth { D = new ProceduralMeshes.MeshData(), Rng = rng, Hairs = p.rootHairs };
            int count = Mathf.Clamp(p.rootCount, 3, 28);
            float offsetAngle = (float)rng.NextDouble() * 360f;
            for (int i = 0; i < count; i++)
            {
                float angle = (offsetAngle + i * 137.5f) * Mathf.Deg2Rad;
                float jitter = ((float)rng.NextDouble() - 0.5f) * 0.5f;
                var dir = new Vector3(Mathf.Cos(angle + jitter), -0.05f, Mathf.Sin(angle + jitter));
                float length = p.rootSpread * Mathf.Lerp(0.6f, 1.0f, (float)rng.NextDouble());
                float radius = p.rootRadius * Mathf.Lerp(0.75f, 1.2f, (float)rng.NextDouble());
                var start = new Vector3(Mathf.Cos(angle) * crownRadius * 0.6f, radius * 0.9f, Mathf.Sin(angle) * crownRadius * 0.6f);
                Grow(g, start, dir, length, radius, 0, 0f, rng.Next(Bands));
            }

            // Brace (prop) roots: from the lowest stalk nodes of a grass such as corn, angling down into the soil in whorls.
            for (int k = 0; k < p.braceRoots; k++)
            {
                int whorl = k % 2;
                float angle = (k * 360f / Mathf.Max(1, p.braceRoots) + whorl * 17f) * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float nodeY = p.moundHeight * 0.8f + (whorl == 0 ? 0.03f : 0.065f) * Mathf.Max(0.6f, p.height / 1.5f);
                float reach = nodeY * Mathf.Lerp(0.9f, 1.4f, (float)rng.NextDouble());
                var a = outward * (crownRadius * 0.8f) + Vector3.up * nodeY;
                var mid = outward * (crownRadius + reach * 0.45f) + Vector3.up * (nodeY * 0.55f);
                var end = outward * (crownRadius + reach) + Vector3.up * 0.002f;
                float r0 = p.rootRadius * Mathf.Lerp(0.55f, 0.8f, (float)rng.NextDouble());
                AddRootTube(g.D, new[] { a, Vector3.Lerp(a, mid, 0.5f), mid, Vector3.Lerp(mid, end, 0.5f), end },
                    new[] { r0, r0 * 0.97f, r0 * 0.9f, r0 * 0.8f, r0 * 0.7f }, new[] { 0.05f, 0.12f, 0.2f, 0.3f, 0.4f }, 7, rng.Next(Bands));
                Grow(g, end, outward + Vector3.down * 0.05f, p.rootSpread * 0.25f, r0 * 0.6f, 1, 0.4f, rng.Next(Bands));
            }

            // Nodules (legumes): small round swellings on the main and side roots, holding nitrogen-fixing bacteria.
            for (int k = 0; k < p.rootNodules && g.Anchors.Count > 0; k++)
            {
                var at = g.Anchors[rng.Next(g.Anchors.Count)];
                float r = Mathf.Lerp(0.0016f, 0.0032f, (float)rng.NextDouble());
                var c = at + new Vector3(((float)rng.NextDouble() - 0.5f) * 0.004f, 0f, ((float)rng.NextDouble() - 0.5f) * 0.004f);
                c.y = r * 0.95f;
                AddBead(g.D, c, r, rng);
            }

            // Soil ridges: each primary root is buried under soil for the first part of its length.
            var soil = new ProceduralMeshes.MeshData();
            foreach (var (pts, radii) in g.Primaries)
                AddRidge(soil, pts, radii, Mathf.Lerp(0.5f, 0.68f, (float)rng.NextDouble()), rng);

            // Crumbs of soil stuck to the roots, thickest near the crown.
            int crumbs = Mathf.Clamp(Mathf.RoundToInt(p.rootSpread * 220f), 30, 110);
            for (int k = 0; k < crumbs && g.Anchors.Count > 0; k++)
            {
                var a = g.Anchors[rng.Next(g.Anchors.Count)];
                float size = Mathf.Lerp(0.002f, 0.0062f, Mathf.Pow((float)rng.NextDouble(), 1.8f));
                var at = a + new Vector3(((float)rng.NextDouble() - 0.5f) * 0.006f, 0f, ((float)rng.NextDouble() - 0.5f) * 0.006f);
                at.y = size * 0.4f;
                AddClod(soil, at, size, rng);
            }
            return new RootSystem { Roots = g.D.ToMesh($"{p.speciesId}_Roots"), Soil = soil.ToMesh($"{p.speciesId}_RootSoil") };
        }

        // Side roots per order: how likely at each step, and the heading wander per step in degrees.
        static readonly float[] LateralChance = { 0.55f, 0.55f, 0.4f, 0f };
        static readonly float[] Wander = { 16f, 26f, 36f, 44f };
        static readonly float[] StepLength = { 0.014f, 0.0095f, 0.0065f, 0.0045f };
        static readonly int[] Radial = { 8, 6, 4, 3 };

        static void Grow(Growth g, Vector3 start, Vector3 dir, float length, float radius, int order, float startV, int band)
        {
            if (radius < 0.00035f || length < 0.008f || g.D.Vertices.Count > g.Budget) return;
            var rng = g.Rng;
            int steps = Mathf.Max(3, Mathf.CeilToInt(length / StepLength[order]));
            float step = length / steps;
            float noiseSeed = (float)rng.NextDouble() * 100f;
            var pts = new List<Vector3>();
            var radii = new List<float>();
            var vs = new List<float>();
            var pos = start;
            dir.Normalize();
            float vSpan = order == 0 ? 0.5f : 0.4f;

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                // Thick at the crown, thin at the tip, with small swellings along the way.
                float r = Mathf.Max(0.0003f, radius * Mathf.Lerp(1f, 0.2f, Mathf.Pow(t, 0.8f)) * (1f + 0.12f * Mathf.Sin(i * 1.7f + noiseSeed)));
                pts.Add(pos); radii.Add(r); vs.Add(Mathf.Clamp01(startV + t * vSpan));

                // The heading meanders, and the root settles onto the ground it lies on.
                float yaw = (Mathf.PerlinNoise(noiseSeed + i * 0.42f, noiseSeed * 0.3f) - 0.5f) * 2f * Wander[order];
                dir = Quaternion.AngleAxis(yaw, Vector3.up) * dir;
                float restY = r * 0.9f;
                dir.y = Mathf.Lerp(dir.y, Mathf.Clamp((restY - pos.y) * 30f, -0.5f, 0.5f), 0.5f);
                if (order >= 2) dir.y += ((float)rng.NextDouble() - 0.4f) * 0.25f;   // fine roots lift and curl a little
                dir.Normalize();
                pos += dir * step;
                pos.y = Mathf.Max(pos.y, restY);

                if (order < 2 && i % 2 == 0) g.Anchors.Add(pos);

                // Side roots: dense along the root, fewer toward its tip.
                if (i >= 1 && i < steps - 1 && order < 3 && rng.NextDouble() < LateralChance[order] * (1f - t * 0.55f))
                {
                    float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                    float swing = Mathf.Lerp(38f, 82f, (float)rng.NextDouble());
                    var sideDir = Quaternion.AngleAxis(side * swing, Vector3.up) * dir;
                    sideDir.y = Mathf.Min(sideDir.y, 0.05f);
                    float childLength = length * (1f - t) * Mathf.Lerp(0.3f, 0.7f, (float)rng.NextDouble());
                    if (order == 1) childLength = Mathf.Min(childLength, 0.1f);
                    if (order == 2) childLength = Mathf.Min(childLength, 0.035f);
                    Grow(g, pos, sideDir, childLength, r * (order == 0 ? 0.5f : 0.6f), order + 1, vs[vs.Count - 1], band);
                }

                // Root hairs on the fine roots.
                if (g.Hairs && order >= 1 && i > 0 && g.D.Vertices.Count < g.Budget && rng.NextDouble() < (order == 1 ? 0.3 : 0.5))
                    AddHair(g, pos, dir, band);
            }
            AddRootTube(g.D, pts, radii, vs, Radial[order], band);
            if (order == 0) g.Primaries.Add((pts, radii));
        }

        /// <summary>
        /// A lumpy ridge of soil over the first <paramref name="cover"/> of a root: wide at the crown, narrowing until it is
        /// just thinner than the root, which then comes out of the soil. Its underside is flattened onto the ground.
        /// </summary>
        static void AddRidge(ProceduralMeshes.MeshData d, IList<Vector3> pts, IList<float> radii, float cover, System.Random rng)
        {
            int last = Mathf.Clamp(Mathf.RoundToInt((pts.Count - 1) * cover), 2, pts.Count - 1);
            const int radial = 9;
            int start = d.Vertices.Count;
            float seed = (float)rng.NextDouble() * 40f;
            var dir0 = (pts[1] - pts[0]).normalized;
            var n1 = Mathf.Abs(Vector3.Dot(dir0, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            for (int i = 0; i <= last; i++)
            {
                var dir = (i < last ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]).normalized;
                n1 = (n1 - Vector3.Dot(n1, dir) * dir).normalized;
                var n2 = Vector3.Cross(n1, dir);
                float t = i / (float)last;
                float baseR = Mathf.Lerp(2.6f * radii[i] + 0.004f, 0.8f * radii[i], Mathf.Pow(t, 1.4f));
                float lump = 1f + 0.28f * (Mathf.PerlinNoise(seed + i * 0.55f, seed * 0.5f) - 0.5f);
                var centre = pts[i] + Vector3.up * (baseR * 0.25f * (1f - t));
                for (int j = 0; j <= radial; j++)
                {
                    float a = j / (float)radial * Mathf.PI * 2f;
                    float wob = 1f + 0.12f * (Mathf.PerlinNoise(seed + j * 0.9f, i * 0.7f) - 0.5f);
                    var p = centre + (Mathf.Cos(a) * n1 + Mathf.Sin(a) * n2) * (baseR * lump * wob);
                    p.y = Mathf.Max(p.y, 0.0008f);
                    d.Vertices.Add(p);
                    d.Uv0.Add(new Vector2(p.x / 0.14f, p.z / 0.14f));
                    d.Uv1.Add(Vector2.zero);
                }
            }
            d.Grid(start, last, radial, false);
        }

        /// <summary>A small lumpy sphere on the roots (a nodule), coloured from the pale end of the root texture.</summary>
        static void AddBead(ProceduralMeshes.MeshData d, Vector3 centre, float radius, System.Random rng)
        {
            const int seg = 8, ring = 6;
            int start = d.Vertices.Count;
            float seed = (float)rng.NextDouble() * 20f;
            for (int i = 0; i <= ring; i++)
            {
                float phi = i / (float)ring * Mathf.PI;
                for (int j = 0; j <= seg; j++)
                {
                    float th = j / (float)seg * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                    float bump = 0.85f + 0.3f * Mathf.PerlinNoise(seed + n.x * 2f, seed + n.z * 2f + n.y);
                    d.Vertices.Add(centre + n * radius * bump);
                    d.Uv0.Add(new Vector2(0.55f + 0.1f * j / seg, 0.75f));
                    d.Uv1.Add(Vector2.zero);
                }
            }
            d.Grid(start, ring, seg, true);
        }

        /// <summary>A short, hair-thin thread growing out sideways from a fine root.</summary>
        static void AddHair(Growth g, Vector3 from, Vector3 along, int band)
        {
            var rng = g.Rng;
            var side = Quaternion.AngleAxis(Mathf.Lerp(50f, 130f, (float)rng.NextDouble()) * (rng.NextDouble() < 0.5 ? 1f : -1f), Vector3.up) * along;
            side.y = Mathf.Lerp(0.05f, 0.5f, (float)rng.NextDouble());
            side.Normalize();
            float length = Mathf.Lerp(0.004f, 0.011f, (float)rng.NextDouble());
            var a = from;
            var m = from + side * (length * 0.5f) + Vector3.up * (length * 0.1f);
            var b = from + side * length + Vector3.down * (length * 0.12f);
            if (b.y < 0.0004f) b.y = 0.0004f;
            AddRootTube(g.D, new[] { a, m, b }, new[] { 0.00028f, 0.0002f, 0.00008f }, new[] { 0.9f, 0.95f, 1f }, 3, band);
        }

        static void AddRootTube(ProceduralMeshes.MeshData d, IList<Vector3> points, IList<float> radii, IList<float> vs, int radial, int band)
        {
            if (points.Count < 2) return;
            int start = d.Vertices.Count;
            var dir0 = (points[1] - points[0]).normalized;
            var n1 = Mathf.Abs(Vector3.Dot(dir0, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            float u0 = band / (float)Bands, uSpan = 1f / Bands * 0.9f;
            for (int i = 0; i < points.Count; i++)
            {
                var dir = (i < points.Count - 1 ? points[i + 1] - points[i] : points[i] - points[i - 1]).normalized;
                n1 = (n1 - Vector3.Dot(n1, dir) * dir).normalized;
                var n2 = Vector3.Cross(n1, dir);
                for (int j = 0; j <= radial; j++)
                {
                    float a = j / (float)radial * Mathf.PI * 2f;
                    d.Vertices.Add(points[i] + (Mathf.Cos(a) * n1 + Mathf.Sin(a) * n2) * radii[i]);
                    d.Uv0.Add(new Vector2(u0 + 0.05f / Bands + j / (float)radial * uSpan, vs[i]));
                    d.Uv1.Add(Vector2.zero);
                }
            }
            d.Grid(start, points.Count - 1, radial, false);
        }
    }
}
