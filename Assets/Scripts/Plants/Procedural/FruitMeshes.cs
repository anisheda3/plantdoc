using System.Collections.Generic;
using UnityEngine;

namespace PlantDoctor.Plants
{
    /// <summary>
    /// Fruit of the crops added with the full PlantVillage set, in the same unit space as ProceduralMeshes.Fruit (radius 1, the stalk
    /// end up at +Y, hanging fruit below it): blueberries, cherries, peaches, oranges, grape bunches, strawberries, raspberries,
    /// squash, corn ears and soybean pods. <see cref="Calyx"/> holds the green or woody parts that sit on each fruit.
    /// </summary>
    public static class FruitMeshes
    {
        /// <summary>A corn ear is about four times as long as it is wide; the mesh is 2.4 units long.</summary>
        const float EarWidth = 0.4f;

        public static bool Handles(FruitForm form) =>
            form >= FruitForm.Berry && form <= FruitForm.Pod;   // the PlantSeg forms are CropMeshes

        delegate Vector3 Shape(Vector3 unit, float phi, float theta);

        /// <summary>A deformed sphere: phi 0 at the top (+Y) to pi at the bottom.</summary>
        static void AddSphere(ProceduralMeshes.MeshData d, Vector3 centre, float radius, int segments, int rings, Shape shape = null)
        {
            int start = d.Vertices.Count;
            for (int i = 0; i <= rings; i++)
            {
                float phi = i / (float)rings * Mathf.PI;
                for (int j = 0; j <= segments; j++)
                {
                    float theta = j / (float)segments * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    var p = shape != null ? shape(n, phi, theta) : n;
                    d.Vertices.Add(centre + p * radius);
                    d.Uv0.Add(new Vector2(j / (float)segments, 1f - i / (float)rings));
                    d.Uv1.Add(Vector2.zero);
                }
            }
            int row = segments + 1;
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < segments; j++)
                {
                    int a = start + i * row + j, b = a + 1, c = a + row, e = c + 1;
                    d.Triangles.Add(a); d.Triangles.Add(b); d.Triangles.Add(c);
                    d.Triangles.Add(b); d.Triangles.Add(e); d.Triangles.Add(c);
                }
        }

        static float Hash(float x, float y) => Mathf.Repeat(Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f, 1f);

        public static Mesh Body(FruitForm form)
        {
            var d = new ProceduralMeshes.MeshData();
            var rng = new System.Random((int)form * 97 + 5);
            switch (form)
            {
                case FruitForm.Berry:       // blueberry: a little flattened, with a crown at the blossom end (built in Calyx)
                    AddSphere(d, Vector3.zero, 1f, 16, 10, (n, phi, th) => new Vector3(n.x, n.y * 0.84f, n.z));
                    break;
                case FruitForm.Cherry:      // round with a dip at the stalk
                    AddSphere(d, Vector3.zero, 1f, 20, 14, (n, phi, th) =>
                    {
                        var p = new Vector3(n.x * 1.02f, n.y * 0.92f, n.z);
                        p.y -= 0.16f * Mathf.Exp(-phi * phi / 0.06f);
                        return p;
                    });
                    break;
                case FruitForm.Peach:       // round, a groove (suture) down one side and a small point at the bottom
                    AddSphere(d, Vector3.zero, 1f, 28, 18, (n, phi, th) =>
                    {
                        float groove = 0.07f * Mathf.Exp(-Mathf.Pow(Mathf.DeltaAngle(th * Mathf.Rad2Deg, 0f) / 14f, 2f)) * Mathf.Sin(phi);
                        var p = n * (1f - groove);
                        p.y -= 0.1f * Mathf.Exp(-phi * phi / 0.08f);                      // stalk cavity
                        p.y -= 0.06f * Mathf.Exp(-(Mathf.PI - phi) * (Mathf.PI - phi) / 0.03f); // tip
                        return p;
                    });
                    break;
                case FruitForm.Citrus:      // orange: oil-gland pits all over the peel, a navel at the bottom
                    AddSphere(d, Vector3.zero, 1f, 32, 22, (n, phi, th) =>
                    {
                        float pits = (Hash(Mathf.Round(th * 18f), Mathf.Round(phi * 12f)) - 0.5f) * 0.015f;
                        var p = n * (1f + pits);
                        p.y *= 0.96f;
                        p.y += 0.05f * Mathf.Exp(-(Mathf.PI - phi) * (Mathf.PI - phi) / 0.05f);
                        return p;
                    });
                    break;
                case FruitForm.GrapeBunch:  // a tapering cone of berries hanging from the stalk, about two units long
                {
                    const int berries = 46;
                    for (int k = 0; k < berries; k++)
                    {
                        float t = (k + 0.5f) / berries;
                        float y = -0.1f - t * 1.8f;
                        float r = Mathf.Lerp(0.62f, 0.12f, Mathf.Pow(t, 0.9f));
                        float ang = k * 2.3999f;
                        float jitter = (float)rng.NextDouble() * 0.12f;
                        var c = new Vector3(Mathf.Cos(ang) * (r + jitter), y, Mathf.Sin(ang) * (r + jitter));
                        AddSphere(d, c, 0.2f + (float)rng.NextDouble() * 0.03f, 8, 6, (n, phi, th) => new Vector3(n.x, n.y * 1.08f, n.z));
                    }
                    break;
                }
                case FruitForm.Strawberry:  // a cone, broad at the top, with the seeds sitting in little pits
                    AddSphere(d, Vector3.zero, 1f, 28, 20, (n, phi, th) =>
                    {
                        float t = phi / Mathf.PI;
                        float width = Mathf.Lerp(1f, 0.08f, Mathf.Pow(t, 1.35f)) * (1f + 0.2f * Mathf.Sin(t * Mathf.PI));
                        float seeds = Hash(Mathf.Round(th * 9f + (Mathf.Round(phi * 9f) % 2) * 0.5f), Mathf.Round(phi * 9f)) > 0.45f ? -0.03f : 0f;
                        return new Vector3(Mathf.Cos(th) * width * (1f + seeds), Mathf.Lerp(0.55f, -1.45f, t), Mathf.Sin(th) * width * (1f + seeds));
                    });
                    break;
                case FruitForm.Raspberry:   // an aggregate of small drupelets over a dome
                {
                    for (int i = 0; i < 9; i++)
                    {
                        float phi = Mathf.Lerp(0.35f, Mathf.PI * 0.92f, i / 8f);
                        int around = Mathf.Max(5, Mathf.RoundToInt(Mathf.Sin(phi) * 13f));
                        for (int j = 0; j < around; j++)
                        {
                            float th = (j + (i % 2) * 0.5f) / around * Mathf.PI * 2f;
                            var c = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi) * 1.15f, Mathf.Sin(phi) * Mathf.Sin(th)) * 0.86f;
                            AddSphere(d, c, 0.23f, 7, 5);
                        }
                    }
                    break;
                }
                case FruitForm.Squash:      // a zucchini lying along X: long, slightly club-shaped, with faint ribs
                {
                    const int seg = 20, rings = 26;
                    int start = d.Vertices.Count;
                    for (int i = 0; i <= rings; i++)
                    {
                        float t = i / (float)rings;
                        float x = Mathf.Lerp(-3.2f, 3.2f, t);
                        float r = Mathf.Lerp(0.85f, 1.12f, t) * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(2f * t - 1f, 8f)));
                        for (int j = 0; j <= seg; j++)
                        {
                            float th = j / (float)seg * Mathf.PI * 2f;
                            float rib = 1f + 0.04f * Mathf.Cos(th * 5f);
                            d.Vertices.Add(new Vector3(x, Mathf.Cos(th) * r * rib + 0.15f * t * t, Mathf.Sin(th) * r * rib));
                            d.Uv0.Add(new Vector2(j / (float)seg, t));
                            d.Uv1.Add(Vector2.zero);
                        }
                    }
                    d.Grid(start, rings, seg, true);
                    break;
                }
                case FruitForm.CornEar:     // an ear: a tapering cylinder of kernel rows, about 2.4 units long, tip up
                {
                    const int seg = 16, rings = 24;
                    int start = d.Vertices.Count;
                    for (int i = 0; i <= rings; i++)
                    {
                        float t = i / (float)rings;
                        float r = Mathf.Lerp(1f, 0.55f, t * t) * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(2f * t - 1f, 10f)));
                        for (int j = 0; j <= seg; j++)
                        {
                            float th = j / (float)seg * Mathf.PI * 2f;
                            float kernel = 1f + 0.05f * Mathf.Abs(Mathf.Sin(th * 7f)) + 0.03f * Mathf.Abs(Mathf.Sin(t * 70f));
                            d.Vertices.Add(new Vector3(Mathf.Cos(th) * r * kernel * EarWidth, Mathf.Lerp(-1.2f, 1.2f, t), Mathf.Sin(th) * r * kernel * EarWidth));
                            d.Uv0.Add(new Vector2(j / (float)seg, t));
                            d.Uv1.Add(Vector2.zero);
                        }
                    }
                    d.Grid(start, rings, seg, true);
                    break;
                }
                case FruitForm.Pod:         // a soybean pod: flat, hanging, with two or three seeds bulging
                {
                    const int seg = 12, rings = 18;
                    int start = d.Vertices.Count;
                    for (int i = 0; i <= rings; i++)
                    {
                        float t = i / (float)rings;
                        float y = Mathf.Lerp(0f, -3.2f, t);
                        float bulge = 0.75f + 0.25f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f));
                        float r = Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI))) * bulge;
                        for (int j = 0; j <= seg; j++)
                        {
                            float th = j / (float)seg * Mathf.PI * 2f;
                            d.Vertices.Add(new Vector3(Mathf.Cos(th) * r * 0.42f + 0.25f * t * t, y, Mathf.Sin(th) * r));
                            d.Uv0.Add(new Vector2(j / (float)seg, t));
                            d.Uv1.Add(Vector2.zero);
                        }
                    }
                    d.Grid(start, rings, seg, true);
                    break;
                }
            }
            return d.ToMesh($"Fruit_{form}");
        }

        /// <summary>The stalk, sepals, husk or crown that goes with each fruit, in the same unit space.</summary>
        public static Mesh Calyx(FruitForm form)
        {
            var d = new ProceduralMeshes.MeshData();
            switch (form)
            {
                case FruitForm.Berry:       // the five-pointed crown left by the flower, at the bottom of the berry
                    for (int k = 0; k < 5; k++)
                    {
                        float th = k / 5f * Mathf.PI * 2f;
                        var a = new Vector3(Mathf.Cos(th) * 0.18f, -0.8f, Mathf.Sin(th) * 0.18f);
                        var b = new Vector3(Mathf.Cos(th) * 0.32f, -0.98f, Mathf.Sin(th) * 0.32f);
                        d.AddTube(new[] { a, b }, new[] { 0.07f, 0.03f }, 4);
                    }
                    d.AddTube(new[] { new Vector3(0f, 0.8f, 0f), new Vector3(0.05f, 1.4f, 0f) }, new[] { 0.06f, 0.05f }, 5);
                    break;
                case FruitForm.Cherry:      // a long slender stalk
                    d.AddTube(new[] { new Vector3(0f, 0.7f, 0f), new Vector3(0.1f, 2.2f, 0f), new Vector3(0.35f, 3.6f, 0f) }, new[] { 0.07f, 0.06f, 0.05f }, 6);
                    break;
                case FruitForm.Peach:
                case FruitForm.Citrus:
                    d.AddTube(new[] { new Vector3(0f, 0.85f, 0f), new Vector3(0.03f, 1.15f, 0f) }, new[] { 0.09f, 0.07f }, 6);
                    if (form == FruitForm.Citrus)   // the green button under the stalk
                        FruitButton(d, 0.9f, 0.16f);
                    break;
                case FruitForm.GrapeBunch:  // the bunch's woody stalk and its side branches
                    d.AddTube(new[] { new Vector3(0f, 0.6f, 0f), new Vector3(0f, -0.1f, 0f), new Vector3(0.02f, -1.4f, 0f) }, new[] { 0.06f, 0.05f, 0.025f }, 5);
                    for (int k = 0; k < 6; k++)
                    {
                        float y = -0.2f - k * 0.22f, ang = k * 2.1f;
                        d.AddTube(new[] { new Vector3(0f, y, 0f), new Vector3(Mathf.Cos(ang) * 0.4f, y - 0.12f, Mathf.Sin(ang) * 0.4f) }, new[] { 0.03f, 0.02f }, 4);
                    }
                    break;
                case FruitForm.Strawberry:  // the green leafy cap
                    for (int k = 0; k < 9; k++)
                    {
                        float th = k / 9f * Mathf.PI * 2f + 0.2f;
                        var a = new Vector3(0f, 0.6f, 0f);
                        var b = new Vector3(Mathf.Cos(th) * 0.55f, 0.66f, Mathf.Sin(th) * 0.55f);
                        var c = new Vector3(Mathf.Cos(th) * 0.95f, 0.45f, Mathf.Sin(th) * 0.95f);
                        d.AddTube(new[] { a, b, c }, new[] { 0.13f, 0.11f, 0.02f }, 4);
                    }
                    d.AddTube(new[] { new Vector3(0f, 0.6f, 0f), new Vector3(0.06f, 1.4f, 0f) }, new[] { 0.07f, 0.06f }, 5);
                    break;
                case FruitForm.Raspberry:
                    for (int k = 0; k < 5; k++)
                    {
                        float th = k / 5f * Mathf.PI * 2f;
                        d.AddTube(new[] { new Vector3(0f, 1.05f, 0f), new Vector3(Mathf.Cos(th) * 0.55f, 1.05f, Mathf.Sin(th) * 0.55f) }, new[] { 0.09f, 0.02f }, 4);
                    }
                    d.AddTube(new[] { new Vector3(0f, 1f, 0f), new Vector3(0.05f, 1.7f, 0f) }, new[] { 0.07f, 0.06f }, 5);
                    break;
                case FruitForm.Squash:      // the ridged stem at one end
                    d.AddTube(new[] { new Vector3(-3.1f, 0.1f, 0f), new Vector3(-3.6f, 0.25f, 0f), new Vector3(-3.9f, 0.5f, 0f) }, new[] { 0.32f, 0.28f, 0.24f }, 5);
                    break;
                case FruitForm.CornEar:     // husk leaves wrapping the lower ear, a shank below, silk at the tip
                    for (int k = 0; k < 4; k++)
                    {
                        float th = k / 4f * Mathf.PI * 2f;
                        var pts = new List<Vector3>();
                        var radii = new List<float>();
                        for (int i = 0; i <= 6; i++)
                        {
                            float t = i / 6f;
                            float r = (Mathf.Lerp(0.9f, 1.05f, Mathf.Sin(t * Mathf.PI)) + 0.04f) * EarWidth;
                            pts.Add(new Vector3(Mathf.Cos(th) * r, Mathf.Lerp(-1.3f, 0.9f + 0.2f * k % 2, t), Mathf.Sin(th) * r));
                            radii.Add(Mathf.Lerp(0.38f, 0.1f, t) * EarWidth);
                        }
                        d.AddTube(pts, radii, 5);
                    }
                    d.AddTube(new[] { new Vector3(0f, -1.2f, 0f), new Vector3(0f, -2f, 0f) }, new[] { 0.35f * EarWidth, 0.3f * EarWidth }, 6);
                    for (int k = 0; k < 7; k++)
                    {
                        float th = k / 7f * Mathf.PI * 2f;
                        d.AddTube(new[] { new Vector3(0f, 1.15f, 0f), new Vector3(Mathf.Cos(th) * 0.35f, 1.9f, Mathf.Sin(th) * 0.35f) }, new[] { 0.035f, 0.01f }, 3);
                    }
                    break;
                case FruitForm.Pod:
                    d.AddTube(new[] { new Vector3(0f, 0.3f, 0f), new Vector3(0f, -0.05f, 0f) }, new[] { 0.08f, 0.07f }, 5);
                    break;
            }
            return d.ToMesh($"Calyx_{form}");
        }

        /// <summary>
        /// A squash flower, rim radius 1, opening up (+Y): a bell-shaped corolla whose five broad lobes spread at the rim, seen from
        /// inside and outside (both faces), with the stamen column in the middle.
        /// </summary>
        public static Mesh Flower()
        {
            var d = new ProceduralMeshes.MeshData();
            const int rings = 10, seg = 40;
            // Two copies of the surface with their own vertices, wound opposite ways, so each side gets its own lighting
            // (sharing vertices would average the normals to nothing and blotch the shading).
            for (int side = 0; side < 2; side++)
            {
                int start = d.Vertices.Count;
                for (int i = 0; i <= rings; i++)
                {
                    float t = i / (float)rings;
                    for (int j = 0; j <= seg; j++)
                    {
                        float th = j / (float)seg * Mathf.PI * 2f;
                        float lobe = 1f + 0.22f * Mathf.Pow(Mathf.Abs(Mathf.Cos(th * 2.5f)), 3f) * Mathf.Pow(t, 4f);   // five pointed lobes
                        float r = Mathf.Lerp(0.14f, 1f, Mathf.Pow(t, 1.7f)) * lobe;
                        float y = 0.78f * t - 0.18f * Mathf.Pow(t, 7f);            // the lobes spread outward at the rim
                        d.Vertices.Add(new Vector3(Mathf.Cos(th) * r, y, Mathf.Sin(th) * r));
                        d.Uv0.Add(new Vector2(j / (float)seg, t));
                        d.Uv1.Add(Vector2.zero);
                    }
                }
                d.Grid(start, rings, seg, side == 1);
            }
            d.AddTube(new[] { new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0.42f, 0f) }, new[] { 0.09f, 0.06f }, 6);
            return d.ToMesh("SquashFlower");
        }

        static void FruitButton(ProceduralMeshes.MeshData d, float y, float radius)
        {
            for (int k = 0; k < 5; k++)
            {
                float th = k / 5f * Mathf.PI * 2f;
                d.AddTube(new[] { new Vector3(0f, y + 0.04f, 0f), new Vector3(Mathf.Cos(th) * radius, y, Mathf.Sin(th) * radius) }, new[] { 0.06f, 0.03f }, 4);
            }
        }
    }
}
