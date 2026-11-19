using UnityEngine;

namespace PlantDoctor.Plants
{
    /// <summary>
    /// Fruits, heads and storage organs of the plants added from the PlantSeg dataset, in the same unit space as the other fruit
    /// (one unit = the profile's fruitRadius). Hanging fruit (bean pod, cucumber, eggplant, banana bunch, rice panicle) hang from their
    /// stalk at the origin down -Y; heads (broccoli, cauliflower, cabbage, wheat spike) stand on the origin up +Y; organs at the soil
    /// (carrot shoulder, garlic bulb, ginger rhizome) sit with their top at the origin. <see cref="Calyx"/> holds each one's second
    /// colour (sepals, husk, wrapper leaves, the banana's purple bud).
    /// </summary>
    public static class CropMeshes
    {
        public static bool Handles(FruitForm form) => form >= FruitForm.BeanPod;

        static float Hash(float x, float y) => Mathf.Repeat(Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f, 1f);

        /// <summary>A tube along a centre line with a radius per ring and a radial bump function (ribs, tubercles, florets).</summary>
        static void Lathe(ProceduralMeshes.MeshData d, int rings, int seg, System.Func<float, Vector3> centre, System.Func<float, float> radius,
            System.Func<float, float, float> bump = null, Vector3? side = null)
        {
            int start = d.Vertices.Count;
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings;
                var c = centre(t);
                var ahead = centre(Mathf.Min(1f, t + 0.01f)) - centre(Mathf.Max(0f, t - 0.01f));
                var axis = ahead.sqrMagnitude > 1e-8f ? ahead.normalized : Vector3.down;
                var n1 = Vector3.Cross(axis, side ?? Vector3.forward);
                if (n1.sqrMagnitude < 1e-6f) n1 = Vector3.Cross(axis, Vector3.right);
                n1.Normalize();
                var n2 = Vector3.Cross(axis, n1);
                float r = radius(t);
                for (int j = 0; j <= seg; j++)
                {
                    float th = j / (float)seg * Mathf.PI * 2f;
                    float k = bump != null ? bump(t, th) : 1f;
                    d.Vertices.Add(c + (Mathf.Cos(th) * n1 + Mathf.Sin(th) * n2) * r * k);
                    d.Uv0.Add(new Vector2(j / (float)seg, t));
                    d.Uv1.Add(Vector2.zero);
                }
            }
            d.Grid(start, rings, seg, true);
        }

        static void Ball(ProceduralMeshes.MeshData d, Vector3 c, Vector3 size, int seg = 10, int rings = 7, System.Func<Vector3, float> bump = null)
        {
            int start = d.Vertices.Count;
            for (int i = 0; i <= rings; i++)
            {
                float phi = i / (float)rings * Mathf.PI;
                for (int j = 0; j <= seg; j++)
                {
                    float th = j / (float)seg * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                    float k = bump != null ? bump(n) : 1f;
                    d.Vertices.Add(c + Vector3.Scale(n, size) * k);
                    d.Uv0.Add(new Vector2(j / (float)seg, 1f - i / (float)rings));
                    d.Uv1.Add(Vector2.zero);
                }
            }
            d.Grid(start, rings, seg, false);
        }

        static float Cap(float t, float p = 6f) => Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(2f * t - 1f), p)));   // Abs: a fractional power of a negative number is NaN

        public static Mesh Body(FruitForm form)
        {
            var d = new ProceduralMeshes.MeshData();
            var rng = new System.Random((int)form * 131 + 7);
            switch (form)
            {
                case FruitForm.BeanPod:      // slender, a little curved, seeds faintly bulging, a beak at the tip: 14 units long, 1 across
                    Lathe(d, 30, 10, t => new Vector3(0.9f * t * t, -14f * t, 0f),
                        t => Mathf.Lerp(0.9f, 0.55f, t * t) * Cap(t, 4f) * (0.92f + 0.08f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 6f))),
                        (t, th) => 1f + 0.25f * Mathf.Abs(Mathf.Cos(th)) - 0.25f);   // flattened side to side
                    break;
                case FruitForm.Cucumber:     // cylindrical, blunt-ended, with scattered small tubercles: 6 units long
                    Lathe(d, 34, 18, t => new Vector3(0.25f * t * t, -6f * t, 0f),
                        t => Mathf.Lerp(0.82f, 1f, Mathf.Sin(t * Mathf.PI * 0.8f)) * Cap(t, 10f),
                        (t, th) => 1f + (Hash(Mathf.Round(t * 22f), Mathf.Round(th * 5f)) > 0.72f ? 0.06f : 0f) + 0.025f * Mathf.Cos(th * 3f));
                    break;
                case FruitForm.Eggplant:     // glossy teardrop: narrow under the calyx, widest near the bottom; 4 units long
                    Lathe(d, 30, 22, t => new Vector3(0.2f * t * t, -4f * t, 0f),
                        t => Mathf.Lerp(0.55f, 1.15f, Mathf.SmoothStep(0f, 1f, t * 1.15f)) * Cap(t, 3.2f));
                    break;
                case FruitForm.BananaBunch:  // a hanging stalk with hands of curved fingers that turn up toward the light
                {
                    Lathe(d, 12, 8, t => new Vector3(0f, -3.6f * t, 0f), t => 0.13f, null);     // the rachis
                    for (int hand = 0; hand < 6; hand++)
                    {
                        float y = -0.35f - hand * 0.48f;
                        int fingers = 7 - hand / 2;
                        for (int f = 0; f < fingers; f++)
                        {
                            float a = (f / (float)fingers + hand * 0.13f) * Mathf.PI * 2f;
                            var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                            float len = Mathf.Lerp(1.5f, 1.15f, hand / 5f);
                            var root = new Vector3(0f, y, 0f) + outward * 0.12f;
                            Lathe(d, 10, 7, t => root + outward * (0.35f * t + 0.25f * len * Mathf.Sin(t * 1.2f)) + Vector3.up * (len * t * 0.95f - 0.25f * t),
                                t => 0.17f * Cap(t, 2.5f) * (1f + 0.15f * t), (t, th) => 1f + 0.06f * Mathf.Abs(Mathf.Cos(th * 2.5f)));
                        }
                    }
                    break;
                }
                case FruitForm.BroccoliHead: // a dome of tight floret clusters on short stalks: 1 unit radius, 0.75 high
                {
                    for (int k = 0; k < 70; k++)
                    {
                        float u = (k + 0.5f) / 70f;
                        float phi = Mathf.Acos(1f - u * 0.95f);                       // over the upper hemisphere
                        float th = k * 2.39996f;
                        var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                        var c = new Vector3(n.x * 0.82f, 0.08f + n.y * 0.62f, n.z * 0.82f);
                        float r = 0.17f + (float)rng.NextDouble() * 0.05f;
                        Ball(d, c, new Vector3(r, r * 0.8f, r), 9, 6, q => 1f + 0.12f * Hash(Mathf.Round(q.x * 6f), Mathf.Round(q.z * 6f + q.y * 3f)));
                    }
                    Lathe(d, 4, 10, t => new Vector3(0f, Mathf.Lerp(-0.6f, 0.1f, t), 0f), t => Mathf.Lerp(0.32f, 0.5f, t));  // the thick stalk under the head
                    break;
                }
                case FruitForm.CauliflowerCurd: // a white dome of tightly packed curd lobes
                    Ball(d, new Vector3(0f, 0.05f, 0f), new Vector3(1f, 0.72f, 1f), 36, 18, n =>
                        n.y < -0.1f ? 0.6f : 1f + 0.05f * Mathf.Sin(n.x * 18f) * Mathf.Sin(n.z * 18f) + 0.035f * Hash(Mathf.Round(n.x * 9f), Mathf.Round(n.z * 9f)));
                    break;
                case FruitForm.CabbageHead:  // a firm, slightly flattened ball of wrapped leaves; the leaf edges show as shallow ridges
                    Ball(d, new Vector3(0f, 0.85f, 0f), new Vector3(1f, 0.88f, 1f), 36, 20, n =>
                        1f + 0.025f * Mathf.Sin(Mathf.Atan2(n.z, n.x) * 3f + n.y * 4f) + 0.02f * Mathf.Sin(Mathf.Atan2(n.z, n.x) * 5f - n.y * 6f));
                    break;
                case FruitForm.CarrotRoot:   // a long tapering orange root; only the shoulder shows above the soil
                    Lathe(d, 30, 14, t => new Vector3(0.1f * Mathf.Sin(t * 3f), 0.35f - 6.5f * t, 0f),
                        t => Mathf.Lerp(1f, 0.06f, Mathf.Pow(t, 1.2f)) * (t < 0.05f ? Mathf.Sqrt(t / 0.05f) * 0.9f + 0.1f : 1f),
                        (t, th) => 1f - 0.03f * Mathf.Abs(Mathf.Sin(t * 60f)));            // the fine horizontal rings
                    break;
                case FruitForm.Bulb:         // a garlic bulb: cloves bulging round a flat-bottomed globe, half in the soil
                    Ball(d, new Vector3(0f, 0f, 0f), new Vector3(1f, 0.82f, 1f), 30, 16, n =>
                        1f + 0.07f * Mathf.Abs(Mathf.Cos(Mathf.Atan2(n.z, n.x) * 5f)) * (1f - Mathf.Abs(n.y)) - (n.y < -0.6f ? 0.15f : 0f));
                    Lathe(d, 6, 10, t => new Vector3(0f, 0.6f + 0.6f * t, 0f), t => Mathf.Lerp(0.38f, 0.26f, t));   // the neck
                    break;
                case FruitForm.Rhizome:      // ginger: knobbly fingers branching along the surface
                {
                    var at = Vector3.zero;
                    for (int k = 0; k < 6; k++)
                    {
                        float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                        var dir = new Vector3(Mathf.Cos(a), -0.1f, Mathf.Sin(a)).normalized;
                        var start = k == 0 ? Vector3.zero : at;
                        float len = 1.2f + (float)rng.NextDouble() * 0.9f;
                        Lathe(d, 10, 10, t => start + dir * (len * t), t => 0.42f * Cap(t, 2.2f) * (0.85f + 0.15f * Mathf.Sin(t * 9f)));
                        at = start + dir * (len * 0.6f);
                    }
                    break;
                }
                case FruitForm.Panicle:      // rice: a drooping panicle of grains on fine branches, about 4 units long
                {
                    for (int b = 0; b < 9; b++)
                    {
                        float t0 = b / 9f;
                        var origin = new Vector3(0.9f * t0 * t0, -3.6f * t0, 0f);
                        float a = b * 2.4f;
                        var dir = (new Vector3(Mathf.Cos(a) * 0.35f + 0.5f, -1f, Mathf.Sin(a) * 0.35f)).normalized;
                        float len = Mathf.Lerp(1.4f, 0.6f, t0);
                        Lathe(d, 4, 4, t => origin + dir * (len * t), t => 0.035f);
                        for (int g = 0; g < 7; g++)
                        {
                            var c = origin + dir * (len * (g + 1) / 7.5f) + new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.08f;
                            Ball(d, c, new Vector3(0.09f, 0.2f, 0.09f), 6, 5);
                        }
                    }
                    Lathe(d, 8, 5, t => new Vector3(0.9f * t * t, -3.6f * t, 0f), t => Mathf.Lerp(0.07f, 0.03f, t));   // the main axis
                    break;
                }
                case FruitForm.Spike:        // wheat: an upright ear of overlapping spikelets in two rows, about 2.6 units long, with awns
                {
                    for (int s = 0; s < 18; s++)
                    {
                        float t = s / 17f;
                        float side = s % 2 == 0 ? 1f : -1f;
                        var c = new Vector3(side * 0.22f, 0.2f + t * 2.3f, 0f);
                        float w = Mathf.Lerp(0.36f, 0.24f, t * t);
                        Ball(d, c, new Vector3(w, 0.28f, w * 0.75f), 8, 6);
                        Lathe(d, 2, 3, u => c + new Vector3(side * 0.12f * u, 0.25f + 1.6f * u, 0f), u => 0.012f);   // the awn
                    }
                    Lathe(d, 6, 6, t => new Vector3(0f, -0.4f + 2.9f * t, 0f), t => 0.07f);
                    break;
                }
                case FruitForm.Mango:        // an oblong-oval drupe, fuller on one side with a small beak near the tip: 2.6 units long
                    Lathe(d, 26, 22, t => new Vector3(0.28f * Mathf.Sin(t * Mathf.PI) + 0.12f * t * t, -2.6f * t, 0f),
                        t => Cap(t, 2.4f) * (0.95f + 0.1f * Mathf.Sin(t * Mathf.PI * 0.9f)),
                        (t, th) => 1f + 0.08f * Mathf.Cos(th) * Mathf.Sin(t * Mathf.PI) - 0.05f * Mathf.Exp(-Mathf.Pow((t - 0.8f) / 0.08f, 2f)) * Mathf.Max(0f, Mathf.Cos(th)));
                    break;
                case FruitForm.Chilli:       // a slender, slightly curved, tapering pod hanging from its cap: 8 units long, 1 across
                    Lathe(d, 30, 12, t => new Vector3(0.9f * t * t, -8f * t, 0.3f * Mathf.Sin(t * 2.5f)),
                        t => Mathf.Lerp(1f, 0.08f, Mathf.Pow(t, 1.3f)) * Mathf.Sqrt(Mathf.Clamp01(t / 0.04f)),
                        (t, th) => 1f + 0.05f * Mathf.Abs(Mathf.Sin(th * 1.5f)));
                    break;
                case FruitForm.CottonBoll:   // an open boll: four fluffy locks of lint bulging out of the split burr, 2.4 units deep
                {
                    System.Func<Vector3, float> fluff = n => 1f + 0.07f * Mathf.Sin(9f * n.x + 4f * n.y) * Mathf.Sin(7f * n.z + 3f * n.y)
                                                              + 0.05f * Mathf.Sin(15f * n.y + 6f * n.x + 2f * n.z);
                    for (int k = 0; k < 4; k++)
                    {
                        float a = (k + 0.5f) / 4f * Mathf.PI * 2f;
                        var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        Ball(d, dir * 0.55f + Vector3.down * (1.1f + 0.08f * (k % 2)), new Vector3(0.72f, 0.92f, 0.72f), 12, 9, fluff);
                    }
                    Ball(d, Vector3.down * 1.35f, new Vector3(0.6f, 1.0f, 0.6f), 10, 8, fluff);   // the lint drooping at the centre
                    break;
                }
            }
            return d.ToMesh($"Fruit_{form}");
        }

        /// <summary>One banana finger, finer than in the bunch: what the zoom step shows for a banana fruit disease.</summary>
        public static Mesh BananaFinger()
        {
            var d = new ProceduralMeshes.MeshData();
            Lathe(d, 28, 14, t => new Vector3(0f, -2.4f * t, 0.55f * Mathf.Sin(t * 2.2f)),
                t => 0.42f * Mathf.Sqrt(Mathf.Max(0.02f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 0.98f + 0.01f)))) * (1f + 0.12f * t),
                (t, th) => 1f + 0.07f * Mathf.Abs(Mathf.Cos(th * 2.5f)));
            return d.ToMesh("Fruit_BananaFinger");
        }

        /// <summary>The second-colour parts (see the class summary); an empty mesh when the form has none.</summary>
        public static Mesh Calyx(FruitForm form)
        {
            var d = new ProceduralMeshes.MeshData();
            switch (form)
            {
                case FruitForm.Chilli:       // the green cap (calyx) and its curved stalk
                    Lathe(d, 6, 12, t => new Vector3(0f, 0.25f - 0.35f * t, 0f), t => Mathf.Lerp(0.55f, 1.12f, t));
                    Lathe(d, 6, 6, t => new Vector3(0.4f * t * t, 0.25f + 1.6f * t, 0f), t => 0.22f);
                    break;
                case FruitForm.CottonBoll:   // the dry burr: four pointed carpel walls curling back between the locks, three bracts, the stalk
                    for (int k = 0; k < 4; k++)
                    {
                        float a = k / 4f * Mathf.PI * 2f;
                        var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        Lathe(d, 8, 6, t => dir * (0.18f + 0.75f * Mathf.Sin(t * 1.9f)) + Vector3.down * (0.1f + 1.35f * t - 0.45f * t * t),
                            t => 0.3f * (1f - t * 0.92f), (t, th) => 1f + 0.55f * Mathf.Abs(Mathf.Cos(th)) - 0.55f, Vector3.Cross(Vector3.up, dir));
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        float a = (k + 0.3f) / 3f * Mathf.PI * 2f;
                        var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        Lathe(d, 5, 6, t => dir * (0.15f + 0.4f * t) + Vector3.down * (0.05f + 0.25f * t), t => 0.26f * (1f - 0.7f * t),
                            (t, th) => 1f + 0.6f * Mathf.Abs(Mathf.Cos(th)) - 0.6f, Vector3.Cross(Vector3.up, dir));
                    }
                    Lathe(d, 4, 6, t => new Vector3(0f, 0.05f + 0.9f * t, 0.05f * t), t => 0.11f);
                    break;
                case FruitForm.Mango:        // the long fruit stalk
                    Lathe(d, 6, 6, t => new Vector3(0f, 0.1f + 3.2f * t, 0.15f * t * t), t => 0.07f);
                    break;
                case FruitForm.BeanPod:
                case FruitForm.Cucumber:
                    Lathe(d, 4, 6, t => new Vector3(0f, 0.6f - 0.7f * t, 0f), t => form == FruitForm.Cucumber ? 0.18f : 0.12f);   // the stalk
                    break;
                case FruitForm.Eggplant:     // a big, spiny green calyx clasping the top of the fruit
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k / 6f * Mathf.PI * 2f;
                        var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        Lathe(d, 6, 6, t => dir * (0.3f + 0.45f * t) + Vector3.down * (0.15f + 0.9f * t * t), t => 0.22f * (1f - t * 0.85f),
                            (t, th) => 1f + 0.4f * Mathf.Abs(Mathf.Cos(th)) - 0.4f);
                    }
                    Lathe(d, 6, 10, t => new Vector3(0f, 0.75f - 0.95f * t, 0f), t => Mathf.Lerp(0.16f, 0.5f, t * t));
                    break;
                case FruitForm.BananaBunch:  // the purple male bud at the end of the rachis
                    Lathe(d, 12, 12, t => new Vector3(0f, -3.6f - 1.1f * t, 0.1f * t), t => 0.42f * Mathf.Sin(Mathf.Min(1f, t * 1.3f) * Mathf.PI * 0.9f + 0.25f));
                    break;
                case FruitForm.CauliflowerCurd:  // inner leaves cupping up round the curd
                    for (int k = 0; k < 7; k++)
                    {
                        float a = k / 7f * Mathf.PI * 2f;
                        var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        var tangent = Vector3.Cross(Vector3.up, dir);
                        int start = d.Vertices.Count;
                        for (int i = 0; i <= 6; i++)
                            for (int j = 0; j <= 6; j++)
                            {
                                float v = i / 6f, u = j / 6f - 0.5f;
                                float w = 0.7f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, v * 1.1f + 0.05f));
                                var p = dir * (0.95f + 0.25f * v) + Vector3.up * (-0.5f + 1.25f * v) + tangent * (u * w) - dir * (u * u * 0.35f);
                                d.Vertices.Add(p); d.Uv0.Add(new Vector2(u + 0.5f, v)); d.Uv1.Add(Vector2.zero);
                            }
                        d.Grid(start, 6, 6, false);
                        start = d.Vertices.Count;
                        for (int i = 0; i <= 6; i++)
                            for (int j = 0; j <= 6; j++)
                            {
                                float v = i / 6f, u = j / 6f - 0.5f;
                                float w = 0.7f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, v * 1.1f + 0.05f));
                                var p = dir * (0.95f + 0.25f * v) + Vector3.up * (-0.5f + 1.25f * v) + tangent * (u * w) - dir * (u * u * 0.35f);
                                d.Vertices.Add(p); d.Uv0.Add(new Vector2(u + 0.5f, v)); d.Uv1.Add(Vector2.zero);
                            }
                        d.Grid(start, 6, 6, true);
                    }
                    break;
                case FruitForm.CarrotRoot:   // the base of the leaf stalks on the shoulder
                    Lathe(d, 4, 10, t => new Vector3(0f, 0.3f + 0.5f * t, 0f), t => Mathf.Lerp(0.45f, 0.3f, t));
                    break;
                case FruitForm.Bulb:         // the papery outer skin streaked purple at the base: a thin shell over the lower half
                    Ball(d, new Vector3(0f, -0.04f, 0f), new Vector3(1.03f, 0.8f, 1.03f), 30, 10, n => n.y > 0.1f ? 0.2f : 1f);
                    break;
                default:
                    Lathe(d, 2, 5, t => new Vector3(0f, 0.25f - 0.3f * t, 0f), t => 0.08f);   // a short stalk
                    break;
            }
            return d.ToMesh($"Calyx_{form}");
        }

        static readonly System.Collections.Generic.Dictionary<FruitForm, Texture2D> Textures = new System.Collections.Generic.Dictionary<FruitForm, Texture2D>();

        /// <summary>
        /// A surface texture for the forms whose look is more than one colour (white = the fruit colour): the cabbage head's wrapped
        /// leaves with their pale veins and overlapping edges, the cauliflower curd's grain. Null for the rest.
        /// </summary>
        public static Texture2D Texture(FruitForm form)
        {
            if (form != FruitForm.CabbageHead && form != FruitForm.CauliflowerCurd) return null;
            if (Textures.TryGetValue(form, out var cached) && cached != null) return cached;
            const int w = 256, h = 256;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    float c;
                    if (form == FruitForm.CabbageHead)
                    {
                        // Leaves wrap round the head: their edges cross it as wavy arcs, each leaf a little paler toward its edge;
                        // veins fan up from the base.
                        float shade = 0f, edge = 0f;
                        for (int k = 0; k < 4; k++)
                        {
                            float line = 0.32f + k * 0.16f + 0.08f * Mathf.Sin(u * Mathf.PI * 2f * (2 + k % 2) + k * 1.7f);
                            float d = v - line;
                            if (d < 0f) shade = Mathf.Max(shade, Mathf.Exp(d * 14f) * 0.12f);
                            edge = Mathf.Max(edge, Mathf.Exp(-d * d * 2600f));
                        }
                        float vein = Mathf.Pow(Mathf.Abs(Mathf.Sin(u * Mathf.PI * 22f + v * 3f)), 40f) * (1f - v) * 0.5f;
                        c = 0.86f + shade + vein * 0.6f - edge * 0.16f + 0.14f * v;
                    }
                    else
                    {
                        float grain = Mathf.PerlinNoise(u * 40f, v * 40f) * 0.6f + Mathf.PerlinNoise(u * 120f, v * 120f) * 0.4f;
                        c = 0.84f + grain * 0.2f;
                    }
                    px[y * w + x] = new Color(c, c, c * 0.98f, 1f);
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = $"{form}_Surface", wrapMode = TextureWrapMode.Repeat };
            tex.SetPixels(px);
            tex.Apply(true, false);
            Textures[form] = tex;
            return tex;
        }

        /// <summary>Colour of each form's second part.</summary>
        public static Color CalyxColor(FruitForm form) => form switch
        {
            FruitForm.BananaBunch => new Color(0.42f, 0.12f, 0.20f),
            FruitForm.CauliflowerCurd => new Color(0.36f, 0.50f, 0.30f),
            FruitForm.Bulb => new Color(0.82f, 0.70f, 0.74f),
            FruitForm.CarrotRoot => new Color(0.45f, 0.58f, 0.30f),
            FruitForm.Eggplant => new Color(0.36f, 0.48f, 0.22f),
            FruitForm.CottonBoll => new Color(0.38f, 0.27f, 0.17f),   // the dry brown burr
            _ => new Color(0.40f, 0.52f, 0.26f)
        };
    }
}
