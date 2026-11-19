using System.Collections.Generic;
using UnityEngine;

namespace PlantDoctor.Plants
{
    /// <summary>
    /// The zoomed leaf of one species, shaped the way that species' leaf really grows. Tomato, potato and raspberry show the whole
    /// compound leaf; strawberry, soybean and bean show three leaflets on their stalk; corn, wheat and rice show a grass blade; the rest
    /// show one simple leaf. It is built from the same leaf photographs as the plant, laid out in the leaf's plane: x across, z along
    /// (the stalk at z = 0), y the upper face.
    ///
    /// The disease masks (UV1) are spread over the outline box of the whole leaf, so spots run across all its leaflets. A coverage grid
    /// remembers where there is leaf tissue, so labels point at spots on the leaf and not at the gaps between leaflets.
    /// </summary>
    public class HeroLeafModel
    {
        public const int Grid = 128;

        public Mesh Blades;
        public Mesh Stalks;
        /// <summary>Middle of the leaf, in the leaf's own space.</summary>
        public Vector3 Centre;
        /// <summary>Radius of a sphere that holds the leaf.</summary>
        public float Radius = 0.5f;
        /// <summary>Degrees the leaf is turned in its own plane for display (a grass blade lies across the frame).</summary>
        public float Turn;
        /// <summary>The leaf tissue's outline box in the leaf plane (x across, y here = z along the leaf).</summary>
        public Rect Box = new Rect(-0.5f, 0f, 1f, 1f);
        /// <summary>Where that box lands in the disease masks.</summary>
        public Rect Span = new Rect(0.05f, 0.05f, 0.9f, 0.9f);
        /// <summary>Where the stalk meets the leaf, in mask space.</summary>
        public Vector2 BaseMask = new Vector2(0.5f, 0.05f);
        /// <summary>Where labels prefer to point, in mask space (the middle of a leaf; the side of a fruit facing the viewer).</summary>
        public Vector2 Middle = new Vector2(0.5f, 0.55f);
        /// <summary>True when this is a fruit, head or root rather than a leaf.</summary>
        public bool IsOrgan;

        readonly float[] _cover = new float[Grid * Grid];
        readonly Vector3[] _point = new Vector3[Grid * Grid];

        public Vector2 ToMask(float x, float z) => new Vector2(
            Span.x + (x - Box.x) / Mathf.Max(1e-5f, Box.width) * Span.width,
            Span.y + (z - Box.y) / Mathf.Max(1e-5f, Box.height) * Span.height);

        public Vector2 FromMask(Vector2 m) => new Vector2(
            Box.x + (m.x - Span.x) / Mathf.Max(1e-5f, Span.width) * Box.width,
            Box.y + (m.y - Span.y) / Mathf.Max(1e-5f, Span.height) * Box.height);

        static int Cell(float m) => Mathf.Clamp(Mathf.FloorToInt(m * Grid), 0, Grid - 1);

        /// <summary>How much leaf there is at a mask position: 0 = outside or a gap between leaflets, 1 = leaf.</summary>
        public float Coverage(Vector2 m)
        {
            if (m.x < 0f || m.x > 1f || m.y < 0f || m.y > 1f) return 0f;
            return _cover[Cell(m.y) * Grid + Cell(m.x)];
        }

        /// <summary>Leaf all round, <paramref name="ring"/> cells deep: well inside a blade, away from its margin.</summary>
        public bool Interior(Vector2 m, int ring = 3)
        {
            int cx = Cell(m.x), cy = Cell(m.y);
            for (int dy = -ring; dy <= ring; dy++)
                for (int dx = -ring; dx <= ring; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x >= Grid || y >= Grid || _cover[y * Grid + x] < 0.9f) return false;
                }
            return true;
        }

        /// <summary>The leaf's surface at a mask position, in the leaf's own space (the flat plane where there is no leaf).</summary>
        public Vector3 LocalPoint(Vector2 m)
        {
            if (m.x >= 0f && m.x <= 1f && m.y >= 0f && m.y <= 1f)
            {
                int i = Cell(m.y) * Grid + Cell(m.x);
                if (_cover[i] >= 0.5f) return _point[i];
            }
            var p = FromMask(m);
            return new Vector3(p.x, 0f, p.y);
        }

        internal void Splat(Vector2 m, float alpha, Vector3 local)
        {
            if (m.x < 0f || m.x > 1f || m.y < 0f || m.y > 1f) return;
            int i = Cell(m.y) * Grid + Cell(m.x);
            // Where leaflets overlap, the one on top (higher y) is the one the viewer sees.
            if (alpha >= 0.5f && (_cover[i] < 0.5f || local.y > _point[i].y)) _point[i] = local;
            _cover[i] = Mathf.Max(_cover[i], alpha);
        }
    }

    public static class HeroLeafBuilder
    {
        /// <summary>One photographic card: which photo, where it sits in the leaf, how it is curved.</summary>
        struct Card
        {
            public Matrix4x4 M;
            public Rect Uv;
            public float Aspect, Fold, Droop;
            public bool Mirror;
            public int Cols, Rows;
        }

        /// <summary>The leaf of <paramref name="p"/>; without photographs, one card of <paramref name="fallback"/> (alpha = outline).</summary>
        public static HeroLeafModel Build(SpeciesProfile p, Texture2D fallback, float fallbackAspect)
        {
            bool photos = FoliageMeshes.HasPhotos(p);
            Texture2D atlas = photos ? p.leafAtlas : fallback;
            var cards = new List<Card>();
            var stalks = new ProceduralMeshes.MeshData();
            float turn = 0f;

            if (!photos)
                cards.Add(new Card { M = Matrix4x4.identity, Uv = new Rect(0f, 0f, 1f, 1f), Aspect = fallbackAspect, Fold = 0.075f, Droop = 0.08f, Cols = 32, Rows = 80 });
            else
                switch (p.leafForm)
                {
                    case LeafForm.Compound: Pinnate(p, cards, stalks); break;
                    case LeafForm.Trifoliate: Trifoliate(p, cards, stalks); break;
                    case LeafForm.Strap: Blade(p, cards); turn = 34f; break;
                    default: Simple(p, cards); break;
                }

            var model = new HeroLeafModel { Turn = turn };
            Finish(model, cards, stalks, atlas, p != null ? p.speciesId : "default");
            return model;
        }

        // ------------------------------------------------------------------
        // Layouts
        // ------------------------------------------------------------------

        static Rect Photo(SpeciesProfile p, int i, out float aspect)
        {
            int n = p.leafRects.Length;
            int k = ((i % n) + n) % n;
            aspect = p.leafAspects[k];
            return p.leafRects[k];
        }

        /// <summary>One simple leaf: the species' best photograph.</summary>
        static void Simple(SpeciesProfile p, List<Card> cards)
        {
            var uv = Photo(p, p.heroLeaf, out float aspect);
            cards.Add(new Card { M = Matrix4x4.identity, Uv = uv, Aspect = aspect, Fold = 0.075f, Droop = 0.08f, Cols = 32, Rows = 80 });
        }

        /// <summary>
        /// A pinnate leaf: a terminal leaflet and pairs of lateral leaflets on short stalks along the rachis, smaller toward the base,
        /// with tiny leaflets between the pairs on tomato and potato (interjected leaflets).
        /// </summary>
        static void Pinnate(SpeciesProfile p, List<Card> cards, ProceduralMeshes.MeshData stalks)
        {
            int pairs = Mathf.Clamp(p.leafletPairs, 1, 5);
            bool few = pairs <= 2;                                   // raspberry: 3 or 5 leaflets, the terminal much the largest
            float terminal = few ? 0.46f : 0.38f;
            float rachisEnd = 1f - terminal * 0.96f;
            float firstPair = few ? 0.22f : 0.10f;
            float Arch(float z) => -0.035f * z * z;                   // the rachis bows down a little toward the tip

            // Rachis and the bare petiole below the first pair.
            var pts = new List<Vector3>();
            var radii = new List<float>();
            for (int i = 0; i <= 10; i++)
            {
                float z = rachisEnd * i / 10f;
                pts.Add(new Vector3(0f, Arch(z) - 0.004f, z));
                radii.Add(Mathf.Lerp(0.0105f, 0.0055f, i / 10f));
            }
            stalks.AddTube(pts, radii, 8, 12f);

            int photo = p.heroLeaf;
            float layer = 0f;
            for (int k = 0; k < pairs; k++)
            {
                float f = pairs == 1 ? 1f : k / (float)(pairs - 1);
                float z = Mathf.Lerp(firstPair + 0.06f, rachisEnd - 0.07f, f);
                float length = few ? 0.34f : Mathf.Lerp(0.27f, 0.33f, f);   // leaflets up to a third of the leaf (tomato: 2-10 cm on 10-30 cm)
                float angle = Mathf.Lerp(66f, 48f, f);              // the lowest leaflets stand out widest
                for (int side = -1; side <= 1; side += 2)
                {
                    var dir = Quaternion.Euler(0f, side * angle, 0f) * Vector3.forward;
                    var onRachis = new Vector3(0f, Arch(z), z);
                    var start = onRachis + dir * (few ? 0.006f : 0.02f);
                    FoliageMeshes.AddStalk(stalks, onRachis, start, 0.0055f, 0.004f, 0f, 6);
                    layer += 0.0018f;
                    var uv = Photo(p, ++photo, out float aspect);
                    var rot = Quaternion.Euler(-3f, side * angle, side * 7f);
                    cards.Add(new Card { M = Matrix4x4.TRS(start + Vector3.up * layer, rot, Vector3.one * length), Uv = uv, Aspect = aspect,
                                         Fold = 0.06f, Droop = 0.05f, Mirror = side < 0, Cols = 20, Rows = 32 });
                }

                // Small leaflets in the gap below this pair (tomato, potato).
                if (p.interjectedLeaflets <= 0) continue;
                float below = k == 0 ? firstPair : Mathf.Lerp(firstPair + 0.06f, rachisEnd - 0.07f, (k - 1) / (float)Mathf.Max(1, pairs - 1));
                for (int j = 0; j < p.interjectedLeaflets; j++)
                {
                    float zj = Mathf.Lerp(below, z, (j + 1f) / (p.interjectedLeaflets + 1f));
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var dir = Quaternion.Euler(0f, side * 78f, 0f) * Vector3.forward;
                        var start = new Vector3(0f, Arch(zj), zj) + dir * 0.008f;
                        var uv = Photo(p, photo + j + side, out float aspect);
                        cards.Add(new Card { M = Matrix4x4.TRS(start, Quaternion.Euler(0f, side * 78f, 0f), Vector3.one * 0.075f), Uv = uv, Aspect = aspect,
                                             Fold = 0.04f, Droop = 0.02f, Mirror = side > 0, Cols = 8, Rows = 12 });
                    }
                }
            }

            // The terminal leaflet continues the rachis and lies on top.
            var tipStart = new Vector3(0f, Arch(rachisEnd), rachisEnd);
            var tuv = Photo(p, p.heroLeaf, out float taspect);
            cards.Add(new Card { M = Matrix4x4.TRS(tipStart + Vector3.up * (layer + 0.002f), Quaternion.Euler(-4f, 0f, 0f), Vector3.one * terminal),
                                 Uv = tuv, Aspect = taspect, Fold = 0.06f, Droop = 0.06f, Cols = 24, Rows = 40 });
        }

        /// <summary>
        /// Three leaflets at the top of the leaf stalk. On strawberry all three meet at one point (palmately trifoliate); on soybean and
        /// bean the middle leaflet sits on a short stalk of its own beyond the side pair (pinnately trifoliate, trifoliateRachis).
        /// </summary>
        static void Trifoliate(SpeciesProfile p, List<Card> cards, ProceduralMeshes.MeshData stalks)
        {
            float rachis = Mathf.Clamp(p.trifoliateRachis, 0f, 0.3f);
            bool palmate = rachis < 0.02f;
            const float petiole = 0.2f;                               // how much of the stalk is shown below the leaflets
            float terminal = palmate ? 0.5f : 0.46f;
            float lateral = palmate ? 0.45f : 0.38f;
            float angle = palmate ? 56f : 76f;

            var top = new Vector3(0f, 0f, petiole);
            FoliageMeshes.AddStalk(stalks, Vector3.zero, top, 0.012f, 0.009f, 0f, 8);
            for (int side = -1; side <= 1; side += 2)
            {
                var dir = Quaternion.Euler(0f, side * angle, 0f) * Vector3.forward;
                var start = top + dir * (palmate ? 0.012f : 0.03f);
                FoliageMeshes.AddStalk(stalks, top, start, 0.006f, 0.005f, 0f, 6);
                var uv = Photo(p, p.heroLeaf + (side < 0 ? 1 : 2), out float aspect);
                cards.Add(new Card { M = Matrix4x4.TRS(start + Vector3.up * 0.002f, Quaternion.Euler(-3f, side * angle, side * 8f), Vector3.one * lateral),
                                     Uv = uv, Aspect = aspect, Fold = 0.06f, Droop = 0.05f, Mirror = side < 0, Cols = 24, Rows = 36 });
            }
            var tipStart = top + Vector3.forward * (palmate ? 0.012f : rachis);
            if (!palmate) FoliageMeshes.AddStalk(stalks, top, tipStart, 0.007f, 0.0055f, 0f, 6);
            var tuv = Photo(p, p.heroLeaf, out float taspect);
            cards.Add(new Card { M = Matrix4x4.TRS(tipStart + Vector3.up * 0.005f, Quaternion.Euler(-4f, 0f, 0f), Vector3.one * terminal),
                                 Uv = tuv, Aspect = taspect, Fold = 0.06f, Droop = 0.06f, Cols = 24, Rows = 40 });
        }

        /// <summary>A grass blade, whole: long, narrow, gently arched; shown lying across the frame.</summary>
        static void Blade(SpeciesProfile p, List<Card> cards)
        {
            var uv = Photo(p, p.heroLeaf, out float aspect);
            // Wheat, rice and garlic blades are 20-60 times as long as wide: whole, their spots would be a few pixels. Show the middle of
            // the blade magnified instead (as a hand lens would), long enough to read as a blade (at least a third of its length).
            if (aspect < 0.08f)
            {
                float keep = Mathf.Clamp(aspect / 0.1f, 0.34f, 1f);
                uv = new Rect(uv.x, uv.y + uv.height * (0.5f - keep * 0.5f), uv.width, uv.height * keep);
                aspect /= keep;
            }
            cards.Add(new Card { M = Matrix4x4.identity, Uv = uv, Aspect = aspect, Fold = 0.006f, Droop = 0.04f, Cols = 6, Rows = 90 });
        }

        // ------------------------------------------------------------------
        // Meshes, mask mapping, coverage
        // ------------------------------------------------------------------

        /// <summary>A point of a card before its matrix: x across (aspect wide), z along, folded at the midrib and drooping at the tip.</summary>
        static Vector3 CardPoint(in Card c, float u, float v)
        {
            float half = Mathf.Max(c.Aspect * 0.5f, 1e-3f);
            float x = (u - 0.5f) * c.Aspect;
            float side = Mathf.Abs(x) / half;
            float envelope = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(v * 1.04f))), 0.6f);
            float y = -c.Fold * Mathf.Pow(side, 1.15f) * envelope - c.Droop * v * v * v
                      + 0.004f * Mathf.Sin(v * 24f + x * 38f) * side * side;
            return c.M.MultiplyPoint3x4(new Vector3(x, y, v));
        }

        static Vector2 PhotoUv(in Card c, float u, float v) =>
            new Vector2(c.Uv.x + (c.Mirror ? 1f - u : u) * c.Uv.width, c.Uv.y + v * c.Uv.height);

        static void Finish(HeroLeafModel model, List<Card> cards, ProceduralMeshes.MeshData stalks, Texture2D atlas, string id)
        {
            // Sample every card's photo once: where its tissue is in the leaf plane, and how opaque.
            bool readable = atlas != null && atlas.isReadable;
            var samples = new List<(Vector3 pos, float alpha)>();
            float xMin = float.MaxValue, xMax = float.MinValue, zMin = float.MaxValue, zMax = float.MinValue;
            foreach (var c in cards)
            {
                float length = c.M.MultiplyVector(Vector3.forward).magnitude;
                const float step = 1f / (HeroLeafModel.Grid * 1.6f);
                int nu = Mathf.Clamp(Mathf.CeilToInt(c.Aspect * length / step), 6, 420);
                int nv = Mathf.Clamp(Mathf.CeilToInt(length / step), 6, 420);
                for (int j = 0; j <= nv; j++)
                    for (int i = 0; i <= nu; i++)
                    {
                        float u = i / (float)nu, v = j / (float)nv;
                        float a = Alpha(atlas, ref readable, PhotoUv(c, u, v), u, v);
                        var pos = CardPoint(c, u, v);
                        samples.Add((pos, a));
                        if (a < 0.5f) continue;
                        xMin = Mathf.Min(xMin, pos.x); xMax = Mathf.Max(xMax, pos.x);
                        zMin = Mathf.Min(zMin, pos.z); zMax = Mathf.Max(zMax, pos.z);
                    }
            }
            if (xMin > xMax) { xMin = -0.5f; xMax = 0.5f; zMin = 0f; zMax = 1f; }

            // The masks cover the tissue's box; a very long, narrow leaf (a grass blade) is stretched at most 3:1, so its spots stay
            // spots (a little elongated along the blade, as grass lesions are) instead of long streaks.
            float w = xMax - xMin, h = zMax - zMin, ext = Mathf.Max(w, h);
            float spanW = 0.9f * Mathf.Min(1f, 3f * w / ext), spanH = 0.9f * Mathf.Min(1f, 3f * h / ext);
            model.Box = new Rect(xMin, zMin, w, h);
            model.Span = new Rect(0.5f - spanW * 0.5f, 0.5f - spanH * 0.5f, spanW, spanH);
            model.BaseMask = model.ToMask(0f, 0f);
            model.Middle = new Vector2(0.5f, model.Span.y + model.Span.height * 0.58f);
            model.Centre = new Vector3(xMin + w * 0.5f, 0f, zMin + h * 0.5f);
            model.Radius = 0.5f * Mathf.Sqrt(w * w + h * h) * 1.02f;
            foreach (var (pos, a) in samples) model.Splat(model.ToMask(pos.x, pos.z), a, pos);

            // The blades: every card a fine grid, UV0 into its photo, UV1 where it lies in the masks.
            var blades = new ProceduralMeshes.MeshData();
            foreach (var c in cards)
            {
                int start = blades.Vertices.Count;
                for (int r = 0; r <= c.Rows; r++)
                    for (int col = 0; col <= c.Cols; col++)
                    {
                        float u = col / (float)c.Cols, v = r / (float)c.Rows;
                        var pos = CardPoint(c, u, v);
                        blades.Vertices.Add(pos);
                        blades.Uv0.Add(PhotoUv(c, u, v));
                        blades.Uv1.Add(model.ToMask(pos.x, pos.z));
                    }
                blades.Grid(start, c.Rows, c.Cols, false);
            }
            model.Blades = blades.ToMesh($"{id}_HeroLeaf");
            model.Stalks = stalks.IsEmpty ? null : stalks.ToMesh($"{id}_HeroStalks");
        }

        /// <summary>
        /// The species' fruit, head or root, for diseases that show there: its mesh stood upright (stalk up, along the model's z) with
        /// the side that carries the first lesion of the fruit-rot masks (u about 0.72) facing the viewer (+y). The disease masks are the
        /// fruit's own UVs (UV1), so the coverage grid records, for every mask position on the visible side, where it lies on the surface.
        /// </summary>
        public static HeroLeafModel BuildOrgan(SpeciesProfile p)
        {
            // A banana bunch is too big and busy to zoom on: show one finger, where the fruit rots appear.
            bool finger = p.fruit == FruitForm.BananaBunch;
            var body = finger ? ProceduralMeshes.WithMaskUv(CropMeshes.BananaFinger(), FruitForm.BananaBunch) : ProceduralMeshes.Fruit(p.fruit);
            var calyx = finger ? null : ProceduralMeshes.Calyx(p.fruit);
            // Fruit space: +y to the stalk, the fruit's -z side toward the viewer. Model space: z up, +y toward the viewer.
            Vector3 Map(Vector3 v) => new Vector3(v.x, -v.z, v.y);
            var model = new HeroLeafModel { IsOrgan = true, Turn = 0f, BaseMask = new Vector2(-5f, -5f), Middle = new Vector2(0.72f, 0.5f) };
            model.Blades = Remap(body, Map, $"{p.speciesId}_HeroOrgan");
            model.Stalks = calyx != null && calyx.vertexCount > 0 ? Remap(calyx, Map, $"{p.speciesId}_HeroCalyx") : null;
            // Organs come in fruit units (a carrot root is seven units long): scale to the size of a leaf so the zoom camera frames
            // every organ the same way.
            float k = 0.55f / Mathf.Max(1e-3f, model.Blades.bounds.extents.magnitude);
            Rescale(model.Blades, k);
            if (model.Stalks != null) Rescale(model.Stalks, k);
            var b = model.Blades.bounds;
            model.Centre = b.center;
            model.Radius = b.extents.magnitude * 1.02f;
            model.Box = new Rect(b.min.x, b.min.z, b.size.x, b.size.z);
            model.Span = new Rect(0f, 0f, 1f, 1f);

            // Fill the coverage grid from the visible side of every triangle (several points per triangle, so small fruit still
            // cover the grid densely).
            var verts = model.Blades.vertices;
            var uv1 = model.Blades.uv2;
            var tris = model.Blades.triangles;
            var axis = new Vector2(model.Centre.x, model.Centre.y);
            const int steps = 12;
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = tris[t], c = tris[t + 1], d = tris[t + 2];
                // Whether this bit of surface faces the viewer, from where it lies round the organ's upright axis (the meshes' triangle
                // winding differs between forms, so their normals cannot be trusted for this).
                var mid = (verts[a] + verts[c] + verts[d]) / 3f;
                var round = new Vector2(mid.x, mid.y) - axis;
                if (round.sqrMagnitude < 1e-8f || round.normalized.y < 0.2f) continue;
                for (int i = 0; i <= steps; i++)
                    for (int j = 0; j <= steps - i; j++)
                    {
                        float wa = i / (float)steps, wc = j / (float)steps, wd = 1f - wa - wc;
                        var uv = uv1[a] * wa + uv1[c] * wc + uv1[d] * wd;
                        model.Splat(uv, 1f, verts[a] * wa + verts[c] * wc + verts[d] * wd);
                    }
            }
            Object.DestroyImmediate(body);
            if (calyx != null) Object.DestroyImmediate(calyx);
            return model;
        }

        static void Rescale(Mesh mesh, float k)
        {
            var v = mesh.vertices;
            for (int i = 0; i < v.Length; i++) v[i] *= k;
            mesh.vertices = v;
            mesh.RecalculateBounds();
        }

        static Mesh Remap(Mesh src, System.Func<Vector3, Vector3> map, string name)
        {
            var v = src.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = map(v[i]);
            var mesh = new Mesh { name = name, indexFormat = src.indexFormat };
            mesh.vertices = v;
            mesh.uv = src.uv;
            mesh.uv2 = src.uv2 != null && src.uv2.Length == v.Length ? src.uv2 : new Vector2[v.Length];
            mesh.triangles = src.triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The photo's opacity; an ellipse when the atlas cannot be read on this device.</summary>
        static float Alpha(Texture2D atlas, ref bool readable, Vector2 uv, float u, float v)
        {
            if (readable)
            {
                try { return atlas.GetPixelBilinear(uv.x, uv.y).a; }
                catch (UnityException) { readable = false; }
            }
            float dx = (u - 0.5f) / 0.46f, dy = (v - 0.5f) / 0.48f;
            return dx * dx + dy * dy <= 1f ? 1f : 0f;
        }
    }
}
