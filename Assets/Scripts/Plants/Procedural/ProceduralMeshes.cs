using System.Collections.Generic;
using UnityEngine;

namespace PlantDoctor.Plants
{
    /// <summary>
    /// Runtime mesh generation for the plant twin: leaves, stems, pot, soil, fruit, and the
    /// tubes trees are made of. Runs on the phone, so the twin is rebuilt for whichever
    /// species the AI identifies without shipping a model per species.
    ///
    /// UV0 lays each blade out 0..1 for the albedo (midrib at u = 0.5). UV1 carries the
    /// *disease mask* window: every leaflet samples a different part of the lesion and
    /// chlorosis masks, so spots differ leaf to leaf. Parts with UV1 = 0 (stalks, bark)
    /// never show lesions (see PlantDisease.shader).
    /// </summary>
    public static class ProceduralMeshes
    {
        /// <summary>Accumulates geometry from many parts into one mesh (one draw call per canopy).</summary>
        public class MeshData
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Uv0 = new List<Vector2>();
            public readonly List<Vector2> Uv1 = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();

            public bool IsEmpty => Vertices.Count == 0;

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (Vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(Vertices);
                mesh.SetUVs(0, Uv0);
                mesh.SetUVs(1, Uv1);
                mesh.SetTriangles(Triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }

            public void Grid(int start, int rows, int cols, bool flip)
            {
                int row = cols + 1;
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                    {
                        int a = start + i * row + j, b = a + 1, c = a + row, e = c + 1;
                        if (flip) { Triangles.Add(a); Triangles.Add(b); Triangles.Add(c); Triangles.Add(b); Triangles.Add(e); Triangles.Add(c); }
                        else { Triangles.Add(a); Triangles.Add(c); Triangles.Add(b); Triangles.Add(b); Triangles.Add(c); Triangles.Add(e); }
                    }
            }

            /// <summary>One blade along +Z: ovate, pointed, serrated, cupped along the midrib, drooping.</summary>
            public void AddBlade(Matrix4x4 m, int along, int across, float width, float cup, float droop,
                Rect maskWindow, float serration, int teeth, bool mirror = false, float roundness = 0.7f)
            {
                int start = Vertices.Count;
                for (int i = 0; i <= along; i++)
                {
                    float t = i / (float)along;   // 0 base .. 1 tip
                    float profile = Mathf.Sin(Mathf.Pow(t, roundness) * Mathf.PI) * (1f - 0.15f * t);
                    float teethMask = 1f - serration * Mathf.Abs(Mathf.Sin(t * Mathf.PI * Mathf.Max(1, teeth)));
                    float halfWidth = profile * teethMask * width * 0.5f;
                    float arc = -t * t * droop;

                    for (int j = 0; j <= across; j++)
                    {
                        float s = j / (float)across;
                        float a = (s - 0.5f) * 2f;
                        float cupping = (1f - Mathf.Abs(a)) * -cup + cup * 0.3f;   // midrib crease
                        Vertices.Add(m.MultiplyPoint3x4(new Vector3(a * halfWidth, arc + cupping * profile, t)));
                        float u = mirror ? 1f - s : s;
                        Uv0.Add(new Vector2(u, t));
                        Uv1.Add(new Vector2(maskWindow.x + u * maskWindow.width, maskWindow.y + t * maskWindow.height));
                    }
                }
                Grid(start, along, across, false);
            }

            /// <summary>A narrow leaf stalk: two crossed ribbons, visible from every side, never diseased.</summary>
            public void AddStalk(Matrix4x4 m, float length, float baseWidth, float tipWidth, float droop)
            {
                const int segments = 10;
                for (int ribbon = 0; ribbon < 2; ribbon++)
                {
                    int start = Vertices.Count;
                    for (int i = 0; i <= segments; i++)
                    {
                        float t = i / (float)segments;
                        float w = Mathf.Lerp(baseWidth, tipWidth, t) * 0.5f;
                        var centre = new Vector3(0f, -t * t * droop, t * length);
                        var offset = ribbon == 0 ? new Vector3(w, 0f, 0f) : new Vector3(0f, w, 0f);
                        Vertices.Add(m.MultiplyPoint3x4(centre - offset));
                        Vertices.Add(m.MultiplyPoint3x4(centre + offset));
                        Uv0.Add(new Vector2(0.49f, 0.2f + t * 0.6f));
                        Uv0.Add(new Vector2(0.51f, 0.2f + t * 0.6f));
                        Uv1.Add(Vector2.zero);
                        Uv1.Add(Vector2.zero);
                    }
                    for (int i = 0; i < segments; i++)
                    {
                        int a = start + i * 2;
                        Triangles.Add(a); Triangles.Add(a + 2); Triangles.Add(a + 1);
                        Triangles.Add(a + 1); Triangles.Add(a + 2); Triangles.Add(a + 3);
                    }
                }
            }

            /// <summary>
            /// A tapered tube through a list of points (stems, trunks, branches). Rings are
            /// oriented by parallel transport so the tube never twists as it bends. UV0 is
            /// (around the tube 0..1, distance along it * <paramref name="uvPerMeter"/>), for the
            /// tiling stem and bark textures.
            /// </summary>
            public void AddTube(IList<Vector3> points, IList<float> radii, int radial = 10, float uvPerMeter = 6f)
            {
                if (points.Count < 2) return;
                int start = Vertices.Count;
                Vector3 dir0 = (points[1] - points[0]).normalized;
                Vector3 n1 = Mathf.Abs(Vector3.Dot(dir0, Vector3.right)) > 0.9f ? Vector3.forward : Vector3.right;

                float along = 0f;
                for (int i = 0; i < points.Count; i++)
                {
                    if (i > 0) along += Vector3.Distance(points[i], points[i - 1]);
                    Vector3 d = i < points.Count - 1 ? points[i + 1] - points[i] : points[i] - points[i - 1];
                    d.Normalize();
                    n1 = (n1 - Vector3.Dot(n1, d) * d).normalized;
                    Vector3 n2 = Vector3.Cross(n1, d);
                    for (int j = 0; j <= radial; j++)
                    {
                        float ang = j / (float)radial * Mathf.PI * 2f;
                        Vertices.Add(points[i] + (Mathf.Cos(ang) * n1 + Mathf.Sin(ang) * n2) * radii[i]);
                        Uv0.Add(new Vector2(j / (float)radial, along * uvPerMeter));
                        Uv1.Add(Vector2.zero);
                    }
                }
                Grid(start, points.Count - 1, radial, false);
            }
        }

        // ------------------------------------------------------------------
        // Leaves
        // ------------------------------------------------------------------

        /// <summary>Random 58% window into the disease masks, so spots differ per leaflet.</summary>
        public static Rect MaskWindow(System.Random rng)
        {
            const float size = 0.58f;
            return new Rect(0.01f + (float)rng.NextDouble() * (0.99f - size), 0.01f + (float)rng.NextDouble() * (0.99f - size), size, size);
        }

        /// <summary>Compound leaf (tomato, potato): stalk + leaflet pairs + a terminal leaflet. ~1 unit long.</summary>
        public static Mesh CompoundLeaf(SpeciesProfile p)
        {
            var rng = new System.Random(p.seed);
            var d = new MeshData();
            d.AddStalk(Matrix4x4.identity, 0.86f, 0.018f, 0.008f, 0.1f);

            int pairs = Mathf.Clamp(p.leafletPairs, 1, 5);
            for (int k = 0; k < pairs; k++)
            {
                float t = Mathf.Lerp(0.22f, 0.70f, pairs == 1 ? 0.5f : k / (float)(pairs - 1));
                float size = Mathf.Lerp(0.30f, 0.36f, Mathf.Sin(t * Mathf.PI));
                float angle = Mathf.Lerp(62f, 46f, t);
                for (int side = -1; side <= 1; side += 2)
                {
                    float jitter = (float)rng.NextDouble() * 8f - 4f;
                    var m = Matrix4x4.TRS(new Vector3(0f, -t * t * 0.1f, t * 0.86f),
                        Quaternion.Euler(8f + jitter * 0.5f, side * (angle + jitter), side * -12f), Vector3.one * size);
                    d.AddBlade(m, 10, 6, p.leafWidth, 0.08f, p.leafDroop, MaskWindow(rng), p.serration, p.teeth, side < 0);
                }
            }
            var terminal = Matrix4x4.TRS(new Vector3(0f, -0.075f, 0.84f), Quaternion.Euler(6f, 0f, 0f), Vector3.one * 0.37f);
            d.AddBlade(terminal, 12, 6, p.leafWidth * 1.04f, 0.08f, p.leafDroop * 1.2f, MaskWindow(rng), p.serration, p.teeth);
            return d.ToMesh($"{p.speciesId}_CompoundLeaf");
        }

        /// <summary>Simple leaf (pepper, apple): a short stalk and one blade. ~1 unit long.</summary>
        public static Mesh SimpleLeaf(SpeciesProfile p)
        {
            var rng = new System.Random(p.seed + 1);
            var d = new MeshData();
            d.AddStalk(Matrix4x4.identity, 0.16f, 0.022f, 0.016f, 0.01f);
            AddSimpleBlade(d, Matrix4x4.TRS(new Vector3(0f, -0.004f, 0.14f), Quaternion.identity, Vector3.one * 0.86f), p, rng);
            return d.ToMesh($"{p.speciesId}_SimpleLeaf");
        }

        /// <summary>Adds one simple blade (used per leaf in tree canopies, all into one mesh).</summary>
        public static void AddSimpleBlade(MeshData d, Matrix4x4 m, SpeciesProfile p, System.Random rng) =>
            d.AddBlade(m, 12, 6, p.leafWidth, 0.06f, p.leafDroop, MaskWindow(rng), p.serration, p.teeth, false, 0.62f);

        // ------------------------------------------------------------------
        // Stems, pot, soil
        // ------------------------------------------------------------------

        /// <summary>
        /// A herbaceous stem: flared at the collar, thickening slightly at each node, and zig-zagging a little
        /// from node to node as tomato and pepper stems do, with a slight overall lean.
        /// </summary>
        public static Mesh Stem(float height, float baseRadius, float tipRadius, float lean, int nodes = 9)
        {
            var pts = new List<Vector3>();
            var radii = new List<float>();
            const int segments = 24;
            nodes = Mathf.Max(2, nodes);
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float nodePhase = t * nodes;
                float nearest = Mathf.Abs(nodePhase - Mathf.Round(nodePhase));          // 0 at a node
                float swell = 1f + 0.16f * Mathf.Exp(-nearest * nearest / 0.012f);
                float zig = Mathf.Sin(nodePhase * Mathf.PI) * 0.0045f * (1f - t * 0.5f);
                float collar = 1f + 0.9f * Mathf.Exp(-t * 22f);
                pts.Add(new Vector3(Mathf.Pow(t, 2f) * lean + zig, t * height, 0f));
                radii.Add(Mathf.Lerp(baseRadius, tipRadius, Mathf.Pow(t, 0.8f)) * swell * collar);
            }
            var d = new MeshData();
            d.AddTube(pts, radii, 10, 7f);
            return d.ToMesh("Stem");
        }

        /// <summary>Terracotta pot: tapered wall, rolled rim, inner wall (0.194 m tall).</summary>
        public static Mesh Pot(int radialSegments = 40)
        {
            var profile = new[]
            {
                new Vector2(0.000f, 0.000f), new Vector2(0.080f, 0.000f), new Vector2(0.086f, 0.006f),
                new Vector2(0.108f, 0.150f), new Vector2(0.118f, 0.152f), new Vector2(0.122f, 0.160f),
                new Vector2(0.122f, 0.188f), new Vector2(0.117f, 0.194f), new Vector2(0.108f, 0.194f),
                new Vector2(0.104f, 0.186f), new Vector2(0.096f, 0.120f),
            };
            var d = new MeshData();
            float total = 0f;
            var lengths = new float[profile.Length];
            for (int i = 1; i < profile.Length; i++) { total += Vector2.Distance(profile[i - 1], profile[i]); lengths[i] = total; }
            for (int i = 0; i < profile.Length; i++)
                for (int j = 0; j <= radialSegments; j++)
                {
                    float a = j / (float)radialSegments * Mathf.PI * 2f;
                    d.Vertices.Add(new Vector3(Mathf.Cos(a) * profile[i].x, profile[i].y, Mathf.Sin(a) * profile[i].x));
                    d.Uv0.Add(new Vector2(j / (float)radialSegments, lengths[i] / total));
                    d.Uv1.Add(Vector2.zero);
                }
            int row = radialSegments + 1;
            for (int i = 0; i < profile.Length - 1; i++)
                for (int j = 0; j < radialSegments; j++)
                {
                    // Outward normals on the outer wall (Unity front face = cross(b - a, c - a)).
                    int a = i * row + j, b = a + 1, c = a + row, e = c + 1;
                    d.Triangles.Add(a); d.Triangles.Add(c); d.Triangles.Add(b);
                    d.Triangles.Add(b); d.Triangles.Add(c); d.Triangles.Add(e);
                }
            return d.ToMesh("Pot");
        }

        /// <summary>A slightly domed, lumpy soil disc (radius in metres).</summary>
        public static Mesh Soil(float radius = 0.103f, int rings = 6, int radialSegments = 36, int seed = 5)
        {
            var d = new MeshData();
            d.Vertices.Add(new Vector3(0f, 0.012f, 0f));
            d.Uv0.Add(new Vector2(0.5f, 0.5f));
            d.Uv1.Add(Vector2.zero);
            for (int r = 1; r <= rings; r++)
            {
                float t = r / (float)rings;
                for (int j = 0; j < radialSegments; j++)
                {
                    float a = j / (float)radialSegments * Mathf.PI * 2f;
                    float lump = (Mathf.PerlinNoise(seed + Mathf.Cos(a) * 3f * t, seed + Mathf.Sin(a) * 3f * t) - 0.5f) * 0.008f;
                    d.Vertices.Add(new Vector3(Mathf.Cos(a) * radius * t, 0.012f * (1f - t * t) + lump, Mathf.Sin(a) * radius * t));
                    d.Uv0.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f * t, 0.5f + Mathf.Sin(a) * 0.5f * t));
                    d.Uv1.Add(Vector2.zero);
                }
            }
            for (int j = 0; j < radialSegments; j++)
            {
                d.Triangles.Add(0); d.Triangles.Add(1 + (j + 1) % radialSegments); d.Triangles.Add(1 + j);
            }
            for (int r = 1; r < rings; r++)
            {
                int inner = 1 + (r - 1) * radialSegments, outer = 1 + r * radialSegments;
                for (int j = 0; j < radialSegments; j++)
                {
                    int j1 = (j + 1) % radialSegments;
                    d.Triangles.Add(inner + j); d.Triangles.Add(outer + j1); d.Triangles.Add(outer + j);
                    d.Triangles.Add(inner + j); d.Triangles.Add(inner + j1); d.Triangles.Add(outer + j1);
                }
            }
            return d.ToMesh("Soil");
        }

        /// <summary>A flat unit quad on the XZ plane (rings, shadows, receivers). UV 0..1.</summary>
        public static Mesh GroundQuad()
        {
            var d = new MeshData();
            d.Vertices.AddRange(new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f) });
            d.Uv0.AddRange(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
            d.Uv1.AddRange(new[] { Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero });
            d.Triangles.AddRange(new[] { 0, 2, 1, 1, 2, 3 });
            return d.ToMesh("GroundQuad");
        }

        // ------------------------------------------------------------------
        // Fruit
        // ------------------------------------------------------------------

        /// <summary>
        /// Fruit of unit radius: a flattened round tomato, a three-lobed bell pepper hanging
        /// along -Y, or an apple with dimples at stalk and calyx.
        /// </summary>
        public static Mesh Fruit(FruitForm form, int segments = 24, int rings = 16) => WithMaskUv(FruitBody(form, segments, rings), form);

        /// <summary>
        /// Gives a fruit mesh mask coordinates (UV1) equal to its own UVs, so a fruit, head or root disease can be drawn on it: u runs
        /// round the fruit, v from its far end (v = 0, the blossom end where blossom end rot starts) to the stalk. Lathed fruit
        /// (cucumber, eggplant, pods, banana fingers, carrot) run their UVs the other way, so they are flipped.
        /// </summary>
        internal static Mesh WithMaskUv(Mesh mesh, FruitForm form)
        {
            var uv = mesh.uv;
            bool lathed = form == FruitForm.Squash || form == FruitForm.CornEar || form == FruitForm.Pod || form == FruitForm.BeanPod
                          || form == FruitForm.Cucumber || form == FruitForm.Eggplant || form == FruitForm.BananaBunch
                          || form == FruitForm.CarrotRoot || form == FruitForm.Rhizome || form == FruitForm.Panicle || form == FruitForm.Spike
                          || form == FruitForm.Mango || form == FruitForm.Chilli;
            var uv1 = new Vector2[uv.Length];
            for (int i = 0; i < uv.Length; i++)
                uv1[i] = new Vector2(Mathf.Clamp(uv[i].x, 0.001f, 0.999f), Mathf.Clamp(lathed ? 1f - uv[i].y : uv[i].y, 0.001f, 0.999f));
            mesh.uv2 = uv1;
            return mesh;
        }

        static Mesh FruitBody(FruitForm form, int segments, int rings)
        {
            if (FruitMeshes.Handles(form)) return FruitMeshes.Body(form);
            if (CropMeshes.Handles(form)) return CropMeshes.Body(form);
            var d = new MeshData();
            for (int i = 0; i <= rings; i++)
            {
                float phi = i / (float)rings * Mathf.PI;          // 0 top .. pi bottom
                for (int j = 0; j <= segments; j++)
                {
                    float theta = j / (float)segments * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    switch (form)
                    {
                        case FruitForm.Round:
                        {
                            p.y *= 0.84f;
                            p *= 1f + 0.035f * Mathf.Cos(5f * theta) * Mathf.Sin(phi);   // faint ribs
                            p.x *= 1f + 0.05f * Mathf.Sin(phi) * Mathf.Cos(phi);          // broad shoulders
                            p.z *= 1f + 0.05f * Mathf.Sin(phi) * Mathf.Cos(phi);
                            p.y -= 0.09f * Mathf.Exp(-phi * phi / 0.07f);                  // the stalk end is dished
                            break;
                        }
                        case FruitForm.Bell:
                        {
                            float lobe = 1f + 0.13f * Mathf.Cos(3f * theta) * Mathf.Sin(phi);
                            float taper = Mathf.Lerp(1f, 0.72f, Mathf.InverseLerp(0.5f, 1f, i / (float)rings));
                            p.x *= lobe * taper; p.z *= lobe * taper;
                            p.y *= 1.3f;
                            break;
                        }
                        case FruitForm.Apple:
                        {
                            float dimple = Mathf.Exp(-phi * phi / 0.08f) * 0.22f + Mathf.Exp(-(Mathf.PI - phi) * (Mathf.PI - phi) / 0.12f) * 0.12f;
                            p.y = p.y * 0.9f - Mathf.Sign(p.y) * dimple;
                            break;
                        }
                    }
                    d.Vertices.Add(p);
                    d.Uv0.Add(new Vector2(j / (float)segments, 1f - i / (float)rings));
                    d.Uv1.Add(Vector2.zero);
                }
            }
            int row = segments + 1;
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < segments; j++)
                {
                    int a = i * row + j, b = a + 1, c = a + row, e = c + 1;
                    d.Triangles.Add(a); d.Triangles.Add(b); d.Triangles.Add(c);
                    d.Triangles.Add(b); d.Triangles.Add(e); d.Triangles.Add(c);
                }
            return d.ToMesh($"Fruit_{form}");
        }

        /// <summary>The green parts of a fruit, in the fruit's own unit-radius space: a tomato's five sepals, a pepper's cap and stem, an apple's stalk.</summary>
        public static Mesh Calyx(FruitForm form)
        {
            if (FruitMeshes.Handles(form)) return FruitMeshes.Calyx(form);
            if (CropMeshes.Handles(form)) return CropMeshes.Calyx(form);
            var d = new MeshData();
            switch (form)
            {
                case FruitForm.Round:
                    for (int k = 0; k < 5; k++)
                    {
                        float theta = k / 5f * Mathf.PI * 2f + 0.3f;
                        int start = d.Vertices.Count;
                        const int rows = 5;
                        for (int i = 0; i <= rows; i++)
                        {
                            float t = i / (float)rows;
                            float phi = Mathf.Lerp(0.05f, 0.82f, t);
                            float halfWidth = Mathf.Lerp(0.20f, 0.025f, Mathf.Pow(t, 0.8f));
                            for (int j = -1; j <= 1; j += 2)
                            {
                                float th = theta + j * halfWidth;
                                var p = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi) * 0.84f, Mathf.Sin(phi) * Mathf.Sin(th));
                                p *= 1.035f + 0.10f * t * t;   // the tips lift away from the skin
                                d.Vertices.Add(p); d.Uv0.Add(new Vector2(j < 0 ? 0.2f : 0.8f, t)); d.Uv1.Add(Vector2.zero);
                            }
                        }
                        for (int i = 0; i < rows; i++)
                        {
                            int a2 = start + i * 2;
                            d.Triangles.Add(a2); d.Triangles.Add(a2 + 2); d.Triangles.Add(a2 + 1);
                            d.Triangles.Add(a2 + 1); d.Triangles.Add(a2 + 2); d.Triangles.Add(a2 + 3);
                            d.Triangles.Add(a2); d.Triangles.Add(a2 + 1); d.Triangles.Add(a2 + 2);
                            d.Triangles.Add(a2 + 1); d.Triangles.Add(a2 + 3); d.Triangles.Add(a2 + 2);
                        }
                    }
                    d.AddTube(new[] { new Vector3(0f, 0.80f, 0f), new Vector3(0.02f, 0.95f, 0f), new Vector3(0.05f, 1.08f, 0f) }, new[] { 0.07f, 0.06f, 0.05f }, 6);
                    break;
                case FruitForm.Bell:
                {
                    const int rings = 5, seg = 18;
                    int start = d.Vertices.Count;
                    for (int i = 0; i <= rings; i++)
                    {
                        float phi = Mathf.Lerp(0.02f, 0.62f, i / (float)rings);
                        for (int j = 0; j <= seg; j++)
                        {
                            float th = j / (float)seg * Mathf.PI * 2f;
                            float lobe = 1f + 0.13f * Mathf.Cos(3f * th) * Mathf.Sin(phi);
                            var p = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th) * lobe, Mathf.Cos(phi) * 1.3f, Mathf.Sin(phi) * Mathf.Sin(th) * lobe);
                            d.Vertices.Add(p * 1.02f); d.Uv0.Add(new Vector2(j / (float)seg, 0.3f)); d.Uv1.Add(Vector2.zero);
                        }
                    }
                    d.Grid(start, rings, seg, true);
                    d.AddTube(new[] { new Vector3(0f, 1.25f, 0f), new Vector3(0.05f, 1.55f, 0f), new Vector3(0.18f, 1.78f, 0f) }, new[] { 0.13f, 0.10f, 0.08f }, 7);
                    break;
                }
                case FruitForm.Apple:
                    d.AddTube(new[] { new Vector3(0f, 0.55f, 0f), new Vector3(0.03f, 0.9f, 0f), new Vector3(0.13f, 1.25f, 0f) }, new[] { 0.05f, 0.04f, 0.035f }, 6);
                    break;
            }
            return d.ToMesh($"Calyx_{form}");
        }
    }
}
