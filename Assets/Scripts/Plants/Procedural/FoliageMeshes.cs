using System.Collections.Generic;
using UnityEngine;

namespace PlantDoctor.Plants
{
    /// <summary>
    /// Leaves made from real photographs: every leaf is a lightly folded sheet that samples one cut-out
    /// leaf photo from the species' atlas (the photo's alpha is the outline, see PlantDisease.shader),
    /// so the outline, the veins and the surface are real. A compound leaf (tomato, potato) is a rachis
    /// with leaflet cards on short petiolules; a simple leaf (pepper) is a petiole and one card.
    ///
    /// All cards of a leaf share one mesh, and the stalks another (stem material), so a plant is a
    /// handful of draw calls. Leaves are one unit long, pointing along +Z, and scaled by the caller.
    /// </summary>
    public static class FoliageMeshes
    {
        public struct LeafSet
        {
            public Mesh Blades;   // photographic cards (leaf material, alpha clip)
            public Mesh Stalks;   // rachis, petioles (stem material)
        }

        public static bool HasPhotos(SpeciesProfile p) =>
            p != null && p.leafAtlas != null && p.leafRects != null && p.leafRects.Length > 0 && p.leafAspects != null && p.leafAspects.Length == p.leafRects.Length;

        /// <summary>
        /// One photographic leaf card, base at the origin and tip at z = 1, width = aspect, folded along the
        /// midrib and drooping toward the tip. The caller's matrix places, sizes and turns it.
        /// </summary>
        public static void AddLeafCard(ProceduralMeshes.MeshData d, Matrix4x4 m, float aspect, Rect uvRect, Rect maskWindow,
            float fold, float droop, float ripple, int cols = 6, int rows = 10)
        {
            int start = d.Vertices.Count;
            float half = Mathf.Max(aspect * 0.5f, 1e-3f);
            for (int i = 0; i <= rows; i++)
            {
                float v = i / (float)rows;
                float envelope = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(v * 1.04f))), 0.6f);
                for (int j = 0; j <= cols; j++)
                {
                    float u = j / (float)cols;
                    float x = (u - 0.5f) * aspect;
                    float side = Mathf.Abs(x) / half;
                    float y = -fold * Mathf.Pow(side, 1.15f) * envelope - droop * v * v * v
                              + ripple * Mathf.Sin(v * 15f + x * 22f) * side * side;
                    d.Vertices.Add(m.MultiplyPoint3x4(new Vector3(x, y, v)));
                    d.Uv0.Add(new Vector2(uvRect.x + u * uvRect.width, uvRect.y + v * uvRect.height));
                    d.Uv1.Add(new Vector2(maskWindow.x + u * maskWindow.width, maskWindow.y + v * maskWindow.height));
                }
            }
            d.Grid(start, rows, cols, false);
        }

        static void Pick(SpeciesProfile p, System.Random rng, out Rect uv, out float aspect)
        {
            int v = rng.Next(p.leafRects.Length);
            uv = p.leafRects[v];
            aspect = p.leafAspects[v];
        }

        /// <summary>A short curved tube from a to b (petioles, petiolules, fruit stalks).</summary>
        public static void AddStalk(ProceduralMeshes.MeshData d, Vector3 a, Vector3 b, float radiusA, float radiusB, float sag, int radial = 6)
        {
            var pts = new List<Vector3>();
            var radii = new List<float>();
            for (int i = 0; i <= 4; i++)
            {
                float t = i / 4f;
                pts.Add(Vector3.Lerp(a, b, t) + Vector3.down * (sag * Mathf.Sin(t * Mathf.PI)));
                radii.Add(Mathf.Lerp(radiusA, radiusB, t));
            }
            d.AddTube(pts, radii, radial);
        }

        // ------------------------------------------------------------------
        // Compound leaf: tomato, potato
        // ------------------------------------------------------------------

        /// <param name="variant">Different variants have different leaflet photos, sizes and spot windows, so leaves on one plant are not copies.</param>
        /// <param name="rise">Above 0 (rosettes: carrot, celery) the frond sits on a long leaf stalk (petioleLength) that rises from the crown.</param>
        public static LeafSet CompoundLeaf(SpeciesProfile p, int variant = 0, float rise = 0f)
        {
            var rng = new System.Random(p.seed * 7 + 3 + variant * 101);
            var blades = new ProceduralMeshes.MeshData();
            var stalks = new ProceduralMeshes.MeshData();

            // Rachis: arches slightly and thins toward the tip.
            const float rachisLength = 0.86f;
            Vector3 RachisPoint(float t) => new Vector3(0f, -t * t * 0.10f, t * rachisLength);
            var pts = new List<Vector3>();
            var radii = new List<float>();
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                pts.Add(RachisPoint(t));
                radii.Add(Mathf.Lerp(0.016f, 0.0055f, t));
            }
            stalks.AddTube(pts, radii, 6);

            int pairs = Mathf.Clamp(p.leafletPairs, 1, 5);
            for (int k = 0; k < pairs; k++)
            {
                float f = pairs == 1 ? 0.5f : k / (float)(pairs - 1);
                float t = Mathf.Lerp(0.26f, 0.72f, f);
                var onRachis = RachisPoint(t);
                // Leaflets up to a third of the leaf, the upper pairs largest (tomato: leaflets 2-10 cm on 10-30 cm leaves).
                float length = Mathf.Lerp(0.27f, 0.33f, f) * (0.92f + (float)rng.NextDouble() * 0.16f);
                for (int side = -1; side <= 1; side += 2)
                {
                    float yaw = side * (Mathf.Lerp(62f, 44f, t) + ((float)rng.NextDouble() - 0.5f) * 14f);
                    var outward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                    var end = onRachis + outward * 0.055f + Vector3.up * 0.012f;
                    AddStalk(stalks, onRachis, end, 0.0065f, 0.0045f, 0.004f, 5);
                    Pick(p, rng, out var uv, out float aspect);
                    var m = Matrix4x4.TRS(end, Quaternion.Euler(8f + ((float)rng.NextDouble() - 0.5f) * 8f, yaw, side * -16f), Vector3.one * length);
                    AddLeafCard(blades, m, aspect, uv, ProceduralMeshes.MaskWindow(rng), p.leafFold, p.leafDroop * 1.2f, 0.004f);
                }

                // Tiny leaflets in the gap below this pair (tomato and potato carry them between the main pairs).
                for (int j = 0; j < p.interjectedLeaflets; j++)
                {
                    float below = k == 0 ? 0.16f : Mathf.Lerp(0.26f, 0.72f, (k - 1) / (float)Mathf.Max(1, pairs - 1));
                    float tj = Mathf.Lerp(below, t, (j + 1f) / (p.interjectedLeaflets + 1f));
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float yaw = side * 80f;
                        var at = RachisPoint(tj) + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 0.01f;
                        Pick(p, rng, out var suv, out float sasp);
                        var sm = Matrix4x4.TRS(at, Quaternion.Euler(6f, yaw, side * -12f), Vector3.one * 0.08f);
                        AddLeafCard(blades, sm, sasp, suv, ProceduralMeshes.MaskWindow(rng), p.leafFold, p.leafDroop, 0.002f, 3, 4);
                    }
                }
            }

            // The terminal leaflet continues the rachis and is the largest.
            var tip = RachisPoint(1f);
            var tipDir = (RachisPoint(1f) - RachisPoint(0.92f)).normalized;
            var tipEnd = tip + tipDir * 0.06f;
            AddStalk(stalks, tip, tipEnd, 0.0055f, 0.0042f, 0.003f, 5);
            Pick(p, rng, out var tuv, out float taspect);
            var tm = Matrix4x4.TRS(tipEnd, Quaternion.LookRotation(tipDir, Vector3.up), Vector3.one * 0.38f);
            AddLeafCard(blades, tm, taspect, tuv, ProceduralMeshes.MaskWindow(rng), p.leafFold, p.leafDroop * 1.3f, 0.004f);

            if (rise > 0f)
            {
                // A rosette's frond: the whole compound blade sits at the end of a long stalk rising from the crown, still one unit
                // long overall (petioleLength is the stalk against the frond).
                float frond = 1f / (1f + p.petioleLength);
                var fullBlades = new ProceduralMeshes.MeshData();
                var fullStalks = new ProceduralMeshes.MeshData();
                var end = AddPetiole(fullStalks, 1f - frond, rise, Mathf.Lerp(0.014f, 0.022f, rise));
                float elevation = Mathf.Lerp(-6f, 72f, rise) * 0.55f;
                var m = Matrix4x4.TRS(end, Quaternion.Euler(-elevation, 0f, 0f), Vector3.one * frond);
                Append(fullBlades, blades, m);
                Append(fullStalks, stalks, m);
                blades = fullBlades; stalks = fullStalks;
            }
            return new LeafSet { Blades = blades.ToMesh($"{p.speciesId}_LeafBlades{variant}"), Stalks = stalks.ToMesh($"{p.speciesId}_LeafStalks{variant}") };
        }

        /// <summary>Copies one mesh's data into another through a matrix.</summary>
        static void Append(ProceduralMeshes.MeshData dst, ProceduralMeshes.MeshData src, Matrix4x4 m)
        {
            int offset = dst.Vertices.Count;
            foreach (var v in src.Vertices) dst.Vertices.Add(m.MultiplyPoint3x4(v));
            dst.Uv0.AddRange(src.Uv0);
            dst.Uv1.AddRange(src.Uv1);
            foreach (var t in src.Triangles) dst.Triangles.Add(t + offset);
        }

        // ------------------------------------------------------------------
        // Leaves on a stalk: simple (pepper, grape, squash) and trifoliate (strawberry, soybean)
        // ------------------------------------------------------------------

        /// <summary>
        /// The leaf stalk (petiole). <paramref name="rise"/> 0 = it leaves the stem almost level (pepper); 1 = it rises steeply from a
        /// crown at the soil before the blade turns outward (strawberry, squash). Returns where the blade starts.
        /// </summary>
        static Vector3 AddPetiole(ProceduralMeshes.MeshData stalks, float length, float rise, float radius)
        {
            if (length <= 1e-4f) return Vector3.zero;
            float angle = Mathf.Lerp(-6f, 72f, rise) * Mathf.Deg2Rad;      // elevation of the stalk's end
            float start = Mathf.Lerp(-2f, 84f, rise) * Mathf.Deg2Rad;      // elevation where it leaves the stem / crown
            var end = new Vector3(0f, Mathf.Sin(angle), Mathf.Cos(angle)) * length;
            var control = new Vector3(0f, Mathf.Sin(start), Mathf.Cos(start)) * (length * 0.55f);
            var pts = new List<Vector3>();
            var radii = new List<float>();
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                pts.Add((1 - t) * (1 - t) * Vector3.zero + 2 * (1 - t) * t * control + t * t * end);   // quadratic Bezier
                radii.Add(Mathf.Lerp(radius, radius * 0.6f, t));
            }
            stalks.AddTube(pts, radii, 6);
            return end;
        }

        /// <param name="rise">0 = stalk from a stem (pepper, grape); 1 = stalk rising from a crown at the soil (squash).</param>
        public static LeafSet SimpleLeaf(SpeciesProfile p, int variant = 0, float rise = 0f)
        {
            var rng = new System.Random(p.seed * 5 + 1 + variant * 77);
            var blades = new ProceduralMeshes.MeshData();
            var stalks = new ProceduralMeshes.MeshData();

            // The whole leaf (stalk + blade) is one unit long, so leafSize stays the leaf's real length.
            float blade = 1f / (1f + p.petioleLength), stalk = 1f - blade;
            var to = AddPetiole(stalks, stalk, rise, Mathf.Lerp(0.016f, 0.024f, rise));
            Pick(p, rng, out var uv, out float aspect);
            // A heart-shaped leaf (squash, grape) joins its stalk inside the notch: slide the blade back over the stalk's end.
            var bladeRot = Quaternion.Euler(Mathf.Lerp(4f, 14f, rise) - p.bladeLift, 0f, 0f);
            var at = to - bladeRot * Vector3.forward * (p.petioleInset * blade);
            var m = Matrix4x4.TRS(at, bladeRot, Vector3.one * blade);
            AddLeafCard(blades, m, aspect, uv, ProceduralMeshes.MaskWindow(rng), p.leafFold, p.leafDroop * 1.5f, 0.005f, 7, 12);

            return new LeafSet { Blades = blades.ToMesh($"{p.speciesId}_LeafBlades{variant}"), Stalks = stalks.ToMesh($"{p.speciesId}_LeafStalks{variant}") };
        }

        /// <summary>Three leaflets at the tip of a stalk; the middle one is the largest (strawberry, soybean).</summary>
        public static LeafSet TrifoliateLeaf(SpeciesProfile p, int variant = 0, float rise = 0f)
        {
            var rng = new System.Random(p.seed * 9 + 4 + variant * 53);
            var blades = new ProceduralMeshes.MeshData();
            var stalks = new ProceduralMeshes.MeshData();

            float leaflet = 1f / (1f + p.petioleLength), stalk = 1f - leaflet;
            var tip = AddPetiole(stalks, stalk, rise, Mathf.Lerp(0.012f, 0.02f, rise));
            for (int k = -1; k <= 1; k++)
            {
                float yaw = k * (46f + ((float)rng.NextDouble() - 0.5f) * 8f);
                float size = leaflet * (k == 0 ? 1f : 0.86f);
                var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                var start = tip + dir * (0.035f * leaflet);   // a short leaflet stalk
                AddStalk(stalks, tip, start, 0.006f, 0.0045f, 0.002f, 5);
                Pick(p, rng, out var uv, out float aspect);
                var m = Matrix4x4.TRS(start, Quaternion.Euler(Mathf.Lerp(6f, 16f, rise), yaw, k * -10f), Vector3.one * size);
                AddLeafCard(blades, m, aspect, uv, ProceduralMeshes.MaskWindow(rng), p.leafFold, p.leafDroop * 1.3f, 0.004f, 6, 10);
            }
            return new LeafSet { Blades = blades.ToMesh($"{p.speciesId}_LeafBlades{variant}"), Stalks = stalks.ToMesh($"{p.speciesId}_LeafStalks{variant}") };
        }

        // ------------------------------------------------------------------
        // Strap leaf: corn
        // ------------------------------------------------------------------

        /// <summary>
        /// A grass leaf, one unit long: a sheath that hugs the stalk going up, then the blade arching out and drooping toward its
        /// tip, folded a little along the midrib. Base at the origin, rising along +Y and arching toward +Z.
        /// </summary>
        public static LeafSet StrapLeaf(SpeciesProfile p, int variant = 0)
        {
            var rng = new System.Random(p.seed * 11 + 2 + variant * 31);
            var blades = new ProceduralMeshes.MeshData();
            Pick(p, rng, out var uv, out float aspect);
            var window = ProceduralMeshes.MaskWindow(rng);

            const int rows = 22, cols = 4;
            const float sheath = 0.12f;
            float arch = Mathf.Lerp(88f, 118f, (float)rng.NextDouble()) * p.strapArch;     // how far over the tip ends up
            float width = Mathf.Clamp(aspect, 0.03f, 0.3f);
            float half = width * 0.5f;
            int start = blades.Vertices.Count;
            var pos = Vector3.zero;
            for (int i = 0; i <= rows; i++)
            {
                float v = i / (float)rows;
                // Angle from vertical: upright in the sheath, then bending over more and more toward the tip.
                float bend = v < sheath ? 4f : Mathf.Lerp(18f + p.strapSpread, Mathf.Max(arch, 18f + p.strapSpread), Mathf.Pow((v - sheath) / (1f - sheath), 1.5f));
                var tangent = new Vector3(0f, Mathf.Cos(bend * Mathf.Deg2Rad), Mathf.Sin(bend * Mathf.Deg2Rad));
                var normal = Vector3.Cross(tangent, Vector3.right);
                if (i > 0) pos += tangent * (1f / rows);
                float twist = Mathf.Sin(v * 9f + variant) * 0.06f * v;          // a gentle wave along the blade
                for (int j = 0; j <= cols; j++)
                {
                    float u = j / (float)cols;
                    float x = (u - 0.5f) * width;
                    float fold = -p.leafFold * Mathf.Abs(x) / half * half;      // V-shaped across the midrib
                    var vert = pos + Vector3.right * x + normal * (fold + twist * x / half * half);
                    blades.Vertices.Add(vert);
                    blades.Uv0.Add(new Vector2(uv.x + u * uv.width, uv.y + v * uv.height));
                    blades.Uv1.Add(new Vector2(window.x + u * window.width, window.y + v * window.height));
                }
            }
            blades.Grid(start, rows, cols, false);
            return new LeafSet { Blades = blades.ToMesh($"{p.speciesId}_Strap{variant}"), Stalks = null };
        }

        /// <summary>One leaf of a tree canopy (no stalk: it is hidden in the foliage), added to the canopy mesh.</summary>
        public static void AddCanopyLeaf(ProceduralMeshes.MeshData canopy, SpeciesProfile p, Matrix4x4 m, System.Random rng)
        {
            Pick(p, rng, out var uv, out float aspect);
            AddLeafCard(canopy, m, aspect, uv, ProceduralMeshes.MaskWindow(rng), p.leafFold, p.leafDroop, 0.003f, 3, 5);   // lean: thousands of these in a full crown
        }
    }
}
