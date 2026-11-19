#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// Generates the mask textures the disease shader needs, so no binary art assets
    /// have to ship with the project.
    ///
    /// The shader reveals lesions by thresholding the mask's red channel against
    /// _LesionDensity, so the mask must encode *where lesions appear first*:
    /// high values = appears early. Early blight starts as scattered spots with
    /// concentric rings, so the mask is built from seeded radial blobs.
    /// </summary>
    public static class DiseaseTextureBuilder
    {
        const string Folder = "Assets/Textures/Generated";
        const int Size = 512;

        [MenuItem("Tools/AI Plant Doctor/Build Plant Assets")]
        public static void BuildAll()
        {
            EnsureFolder("Assets/Textures");
            EnsureFolder(Folder);

            SaveTexture(BuildLesionMask(seed: 1337, spotCount: 34), "T_LesionMask_EarlyBlight", isData: true);
            SaveTexture(BuildBlightMask(seed: 99), "T_LesionMask_LateBlight", isData: true);
            SaveTexture(BuildYellowingMask(seed: 7), "T_YellowingMask", isData: true);
            SaveTexture(BuildLeafAlbedo(), "T_LeafAlbedo", isData: false);

            // Patterns of the diseases added with the full PlantVillage set.
            SaveTexture(BuildSpots(seed: 211, count: 150, rMin: 0.010f, rMax: 0.028f, rings: false), "T_LesionMask_SmallSpots", isData: true);
            SaveTexture(BuildStreaks(seed: 23, count: 80, lenMin: 0.03f, lenMax: 0.07f, widthMin: 0.007f, widthMax: 0.014f), "T_LesionMask_Streak", isData: true);
            SaveTexture(BuildStreaks(seed: 61, count: 9, lenMin: 0.12f, lenMax: 0.28f, widthMin: 0.025f, widthMax: 0.045f), "T_LesionMask_Cigar", isData: true);
            SaveTexture(BuildSpots(seed: 405, count: 700, rMin: 0.0035f, rMax: 0.0075f, rings: false), "T_LesionMask_Pustule", isData: true);
            SaveTexture(BuildColonies(seed: 83), "T_LesionMask_Colony", isData: true);
            SaveTexture(BuildInterveinal(seed: 17), "T_LesionMask_Interveinal", isData: true);
            SaveTexture(BuildMottle(seed: 5), "T_YellowMask_Mottle", isData: true);

            // Patterns of the PlantSeg diseases (10 Nov 2026).
            SaveTexture(BuildAngular(seed: 31, cells: 30, cover: 0.42f), "T_LesionMask_Angular", isData: true);
            SaveTexture(BuildAngular(seed: 47, cells: 13, cover: 0.62f), "T_YellowMask_Angular", isData: true);
            SaveTexture(BuildMargin(seed: 3, depth: 0.42f), "T_LesionMask_MarginScorch", isData: true);
            SaveTexture(BuildMargin(seed: 9, depth: 0.62f), "T_YellowMask_Margin", isData: true);
            SaveTexture(BuildVShapes(seed: 12), "T_LesionMask_VShape", isData: true);
            SaveTexture(BuildSpots(seed: 77, count: 9, rMin: 0.045f, rMax: 0.075f, rings: false), "T_LesionMask_TarSpot", isData: true);
            SaveTexture(BuildStripeRows(seed: 19), "T_LesionMask_StripeRows", isData: true);
            SaveTexture(BuildBlisters(seed: 23), "T_LesionMask_Blister", isData: true);
            SaveTexture(BuildFruitRot(), "T_LesionMask_FruitRot", isData: true);
            SaveTexture(BuildEndRot(seed: 8), "T_LesionMask_EndRot", isData: true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AI Plant Doctor] Generated 11 textures in {Folder}");
        }

        /// <summary>
        /// Early blight: discrete dark spots with concentric rings ("target spots"),
        /// concentrated toward the leaf base/edges where infection starts.
        /// </summary>
        static Texture2D BuildLesionMask(int seed, int spotCount)
        {
            var rng = new System.Random(seed);
            var tex = NewTexture();
            var pixels = new Color[Size * Size];

            // Each spot: centre, radius, and the order it should appear.
            var centers = new Vector2[spotCount];
            var radii = new float[spotCount];
            var priority = new float[spotCount];

            for (int i = 0; i < spotCount; i++)
            {
                float u = (float)rng.NextDouble();
                float v = (float)rng.NextDouble();
                centers[i] = new Vector2(u, v);
                radii[i] = Mathf.Lerp(0.035f, 0.095f, (float)rng.NextDouble());

                // Lower leaf (v small) and edges get infected first.
                float edgeBias = Mathf.Abs(u - 0.5f) * 2f;
                priority[i] = Mathf.Clamp01(0.85f - v * 0.45f + edgeBias * 0.2f
                                            + (float)rng.NextDouble() * 0.3f);
            }

            // The shader reveals a pixel once _LesionDensity > 1 - value, so the mask must use
            // the whole 0..1 range: early spots' centres near 1 (appear at low severity), their
            // rims and late spots lower (appear as the disease advances, so spots *grow*).
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2(x / (float)Size, y / (float)Size);
                    float value = 0f;

                    for (int i = 0; i < spotCount; i++)
                    {
                        float dist = Vector2.Distance(p, centers[i]);
                        if (dist > radii[i]) continue;

                        float n = dist / radii[i];                 // 0 centre .. 1 edge
                        float core = 1f - n * n;                   // smooth dome
                        // Concentric rings - the diagnostic feature of early blight.
                        float rings = 0.9f + 0.1f * Mathf.Cos(n * Mathf.PI * 6f);
                        float reveal = Mathf.Lerp(0.25f, 1f, priority[i]);

                        value = Mathf.Max(value, core * rings * reveal);
                    }

                    // Faint noise so lesion edges are irregular, never perfect circles.
                    value = Mathf.Clamp01(value + (Noise(x, y, 0.08f) - 0.5f) * 0.08f);
                    pixels[y * Size + x] = new Color(value, value, value, 1f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Late blight: large, irregular, spreading blotches rather than discrete spots.
        /// </summary>
        static Texture2D BuildBlightMask(int seed)
        {
            var rng = new System.Random(seed);
            var tex = NewTexture();
            var pixels = new Color[Size * Size];

            float ox = (float)rng.NextDouble() * 100f;
            float oy = (float)rng.NextDouble() * 100f;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = x / (float)Size;
                    float v = y / (float)Size;

                    // Layered fractal noise produces organic blotch edges.
                    float n = 0f, amp = 1f, freq = 3.5f, total = 0f;
                    for (int octave = 0; octave < 4; octave++)
                    {
                        n += Mathf.PerlinNoise(ox + u * freq, oy + v * freq) * amp;
                        total += amp;
                        amp *= 0.5f;
                        freq *= 2.1f;
                    }
                    n /= total;

                    // Bias infection toward the leaf tip and margins.
                    float bias = Mathf.Lerp(0.15f, 0.9f, v) * 0.5f
                               + Mathf.Abs(u - 0.5f) * 0.5f;
                    float value = Mathf.Clamp01(Mathf.SmoothStep(0.42f, 0.78f, n) * (0.55f + bias));

                    pixels[y * Size + x] = new Color(value, value, value, 1f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Chlorosis: broad soft gradients, strongest between the veins.</summary>
        static Texture2D BuildYellowingMask(int seed)
        {
            var rng = new System.Random(seed);
            var tex = NewTexture();
            var pixels = new Color[Size * Size];
            float ox = (float)rng.NextDouble() * 100f;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = x / (float)Size;
                    float v = y / (float)Size;

                    float blobs = Mathf.PerlinNoise(ox + u * 2.5f, ox + v * 2.5f);

                    // Veins stay green longest -> lower mask value along vein lines.
                    float veinDistance = Mathf.Abs(Mathf.Sin((u - 0.5f) * Mathf.PI * 6f));
                    float interVein = Mathf.SmoothStep(0.15f, 0.85f, veinDistance);

                    // Margins yellow before the centre.
                    float margin = Mathf.SmoothStep(0.25f, 1f, Mathf.Abs(u - 0.5f) * 2f);

                    // Mostly blotchy patches with only a hint of the vein pattern; strong
                    // stripes read as an artificial texture rather than chlorosis.
                    float value = Mathf.Clamp01(blobs * 0.6f + interVein * 0.12f + margin * 0.35f);
                    pixels[y * Size + x] = new Color(value, value, value, 1f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Many discrete spots of one size range (septoria, bacterial spot, leaf scorch, black rot; tiny ones are rust pustules and
        /// mite stippling). Like the early blight mask, each spot's centre is highest so spots appear first and then grow.
        /// </summary>
        static Texture2D BuildSpots(int seed, int count, float rMin, float rMax, bool rings)
        {
            var rng = new System.Random(seed);
            var centres = new Vector2[count];
            var radii = new float[count];
            var reveal = new float[count];
            for (int i = 0; i < count; i++)
            {
                centres[i] = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                radii[i] = Mathf.Lerp(rMin, rMax, (float)rng.NextDouble());
                reveal[i] = Mathf.Lerp(0.3f, 1f, (float)rng.NextDouble());
            }
            return Paint((u, v, x, y) =>
            {
                float value = 0f;
                for (int i = 0; i < count; i++)
                {
                    float dx = u - centres[i].x, dy = v - centres[i].y;
                    if (Mathf.Abs(dx) > radii[i] || Mathf.Abs(dy) > radii[i]) continue;
                    float n = Mathf.Sqrt(dx * dx + dy * dy) / radii[i];
                    if (n > 1f) continue;
                    float core = 1f - n * n;
                    if (rings) core *= 0.9f + 0.1f * Mathf.Cos(n * Mathf.PI * 6f);
                    value = Mathf.Max(value, core * reveal[i]);
                }
                return Mathf.Clamp01(value + (Noise(x, y, 0.09f) - 0.5f) * 0.06f);
            });
        }

        /// <summary>
        /// Lesions stretched along the leaf's parallel veins (the mask's V axis runs along the leaf): short narrow rectangles for corn
        /// gray leaf spot, long cigar shapes for northern corn leaf blight.
        /// </summary>
        static Texture2D BuildStreaks(int seed, int count, float lenMin, float lenMax, float widthMin, float widthMax)
        {
            var rng = new System.Random(seed);
            var c = new Vector2[count];
            var len = new float[count];
            var wid = new float[count];
            var reveal = new float[count];
            for (int i = 0; i < count; i++)
            {
                // Gray leaf spot is bounded by veins: centres snap to vein lanes across the leaf.
                float lane = Mathf.Round((float)rng.NextDouble() * 40f) / 40f;
                c[i] = new Vector2(Mathf.Lerp(lane, (float)rng.NextDouble(), widthMax > 0.02f ? 1f : 0.15f), (float)rng.NextDouble());
                len[i] = Mathf.Lerp(lenMin, lenMax, (float)rng.NextDouble());
                wid[i] = Mathf.Lerp(widthMin, widthMax, (float)rng.NextDouble());
                reveal[i] = Mathf.Lerp(0.35f, 1f, (float)rng.NextDouble());
            }
            bool cigar = widthMax > 0.02f;
            return Paint((u, v, x, y) =>
            {
                float value = 0f;
                for (int i = 0; i < count; i++)
                {
                    float dx = Mathf.Abs(u - c[i].x) / wid[i], dy = Mathf.Abs(v - c[i].y) / len[i];
                    if (dx > 1f || dy > 1f) continue;
                    // Cigars are ellipses; gray leaf spots are near-rectangles with soft corners.
                    float n = cigar ? Mathf.Sqrt(dx * dx + dy * dy) : Mathf.Max(Mathf.Pow(dx, 4f), Mathf.Pow(dy, 6f));
                    if (n > 1f) continue;
                    value = Mathf.Max(value, (1f - n * n) * reveal[i]);
                }
                return Mathf.Clamp01(value + (Noise(x, y, 0.07f) - 0.5f) * 0.06f);
            });
        }

        /// <summary>Powdery mildew: soft, round, felt-like colonies that grow and merge.</summary>
        static Texture2D BuildColonies(int seed)
        {
            var rng = new System.Random(seed);
            const int count = 28;
            var c = new Vector2[count];
            var r = new float[count];
            for (int i = 0; i < count; i++)
            {
                c[i] = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                r[i] = Mathf.Lerp(0.04f, 0.11f, (float)rng.NextDouble());
            }
            return Paint((u, v, x, y) =>
            {
                float value = 0f;
                for (int i = 0; i < count; i++)
                {
                    float d = Vector2.Distance(new Vector2(u, v), c[i]) / r[i];
                    value = Mathf.Max(value, Mathf.Clamp01(1f - d) * (0.6f + 0.4f * (i % 3) / 2f));
                }
                float felt = Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.25f;   // ragged, felted edges
                return Mathf.Clamp01(value * 1.15f + felt - 0.1f);
            });
        }

        /// <summary>Grape esca ("tiger stripes"): tissue dies in bands between the main veins, starting at the margins.</summary>
        static Texture2D BuildInterveinal(int seed)
        {
            var rng = new System.Random(seed);
            float o = (float)rng.NextDouble() * 50f;
            return Paint((u, v, x, y) =>
            {
                float cx = u - 0.5f;
                // Main veins fan out from the base: their angle from the midrib.
                float angle = Mathf.Atan2(Mathf.Abs(cx), v + 0.05f);
                float between = Mathf.Abs(Mathf.Sin(angle * 9f));          // 1 midway between veins
                float margin = Mathf.Clamp01(Mathf.Sqrt(cx * cx * 4f + (v - 0.5f) * (v - 0.5f) * 2f));
                float patch = Mathf.PerlinNoise(o + u * 4f, o + v * 4f);
                // Narrow bands midway between the main veins; green tissue stays along each vein.
                return Mathf.Clamp01(Mathf.SmoothStep(0.72f, 1f, between) * (0.3f + 0.7f * margin) * (0.45f + 0.6f * patch));
            });
        }

        /// <summary>Virus mosaic and citrus greening: light and dark green islands with sharp-ish edges, not symmetric about the midrib.</summary>
        static Texture2D BuildMottle(int seed)
        {
            var rng = new System.Random(seed);
            float o = (float)rng.NextDouble() * 50f;
            return Paint((u, v, x, y) =>
            {
                float n = Mathf.PerlinNoise(o + u * 5f, o + v * 5f) * 0.65f + Mathf.PerlinNoise(o * 2f + u * 13f, v * 13f) * 0.35f;
                return Mathf.Clamp01(Mathf.SmoothStep(0.38f, 0.62f, n));
            });
        }

        /// <summary>
        /// Angular, vein-bounded spots or patches (bacterial angular leaf spot, downy mildews): the leaf is split into small cells by a
        /// slanted lattice of veins and whole cells fill in, in clusters, so the spots have straight edges along the veins.
        /// </summary>
        static Texture2D BuildAngular(int seed, int cells, float cover)
        {
            var rng = new System.Random(seed);
            float o = (float)rng.NextDouble() * 50f;
            var jitter = new float[4096];
            var reveal = new float[4096];
            for (int i = 0; i < jitter.Length; i++) { jitter[i] = (float)rng.NextDouble() * 0.6f + 0.2f; reveal[i] = (float)rng.NextDouble(); }
            return Paint((u, v, x, y) =>
            {
                // Veins run at an angle off the midrib; work in that slanted grid.
                float su = (u - 0.5f) * cells, sv = (v + Mathf.Abs(u - 0.5f) * 0.6f) * cells;
                int cu = Mathf.FloorToInt(su), cv = Mathf.FloorToInt(sv);
                float fu = su - cu, fv = sv - cv;
                int k = Mathf.Abs((cu + cells) * 7919 + cv * 104729) % reveal.Length;
                float edge = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv) * jitter[k] * 2f);
                float cluster = Mathf.PerlinNoise(o + u * 4f, o + v * 4f);                        // spots come in groups
                float r = reveal[k] * 0.45f + cluster * 0.75f;
                if (r < 1f - cover) return 0f;
                return Mathf.Clamp01(Smooth(0f, 0.12f, edge) * Mathf.Clamp01((r - (1f - cover)) / cover * 1.6f + 0.25f));
            });
        }

        /// <summary>
        /// Scorch or yellowing from the leaf margin inward (blueberry scorch, wilts, Panama disease): highest at the edge of the leaf's
        /// outline (an ellipse filling the mask) and creeping in, deeper between the veins.
        /// </summary>
        static Texture2D BuildMargin(int seed, float depth)
        {
            var rng = new System.Random(seed);
            float o = (float)rng.NextDouble() * 50f;
            return Paint((u, v, x, y) =>
            {
                float dx = (u - 0.5f) / 0.45f, dy = (v - 0.52f) / 0.47f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);                                            // 1 at the outline
                float angle = Mathf.Atan2(Mathf.Abs(u - 0.5f), v + 0.05f);
                float between = Mathf.Abs(Mathf.Sin(angle * 8f));                                   // tongues between the main veins
                float ragged = Mathf.PerlinNoise(o + u * 6f, o + v * 6f) * 0.3f;
                return Mathf.Clamp01((r - (1f - depth)) / depth + between * 0.25f + ragged - 0.15f);
            });
        }

        /// <summary>Black rot of brassicas: V-shaped wedges from the leaf margin pointing at the midrib.</summary>
        static Texture2D BuildVShapes(int seed)
        {
            var rng = new System.Random(seed);
            const int count = 6;
            var at = new float[count];
            var width = new float[count];
            var reveal = new float[count];
            for (int i = 0; i < count; i++)
            {
                at[i] = Mathf.Lerp(-2.6f, 2.6f, (i + (float)rng.NextDouble() * 0.6f) / count);   // angle round the leaf, 0 = tip
                width[i] = Mathf.Lerp(0.22f, 0.36f, (float)rng.NextDouble());
                reveal[i] = Mathf.Lerp(0.45f, 1f, (float)rng.NextDouble());
            }
            return Paint((u, v, x, y) =>
            {
                float dx = (u - 0.5f) / 0.45f, dy = (v - 0.5f) / 0.47f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Atan2(dx, dy);
                float value = 0f;
                for (int i = 0; i < count; i++)
                {
                    float da = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, at[i] * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    float half = width[i] * Mathf.Clamp01(r);                                     // narrows toward the midrib: a V
                    if (da > half || r < 0.25f) continue;
                    value = Mathf.Max(value, (1f - da / half) * 0.5f + 0.5f * Mathf.Clamp01((r - 0.25f) / 0.75f) * reveal[i]);
                }
                return Mathf.Clamp01(value);
            });
        }

        /// <summary>Stripe rust: rows of tiny pustules lined up between the parallel veins, in stripes of different lengths.</summary>
        static Texture2D BuildStripeRows(int seed)
        {
            var rng = new System.Random(seed);
            const int stripes = 70;
            var lane = new float[stripes];
            var start = new float[stripes];
            var len = new float[stripes];
            var reveal = new float[stripes];
            for (int i = 0; i < stripes; i++)
            {
                lane[i] = Mathf.Round((float)rng.NextDouble() * 44f) / 44f;
                start[i] = (float)rng.NextDouble() * 0.8f;
                len[i] = Mathf.Lerp(0.12f, 0.45f, (float)rng.NextDouble());
                reveal[i] = Mathf.Lerp(0.4f, 1f, (float)rng.NextDouble());
            }
            return Paint((u, v, x, y) =>
            {
                float value = 0f;
                for (int i = 0; i < stripes; i++)
                {
                    float off = Mathf.Abs(u - lane[i]);
                    if (v < start[i] || v > start[i] + len[i] || off > 0.016f) continue;
                    float dot = Mathf.Abs(Mathf.Sin((v - start[i]) * 260f));                       // separate pustules along the stripe
                    float grow = 1f - (v - start[i]) / len[i] * 0.5f;                              // the stripe grows from one end
                    value = Mathf.Max(value, Smooth(0.2f, 0.8f, dot) * reveal[i] * grow * (1f - off / 0.016f));
                }
                return Mathf.Clamp01(value);
            });
        }

        /// <summary>Peach leaf curl and plum pockets: large, soft, swollen areas that merge.</summary>
        static Texture2D BuildBlisters(int seed)
        {
            var rng = new System.Random(seed);
            float o = (float)rng.NextDouble() * 50f;
            return Paint((u, v, x, y) =>
            {
                float n = Mathf.PerlinNoise(o + u * 3.2f, o + v * 3.2f) * 0.7f + Mathf.PerlinNoise(o + u * 9f, o + v * 9f) * 0.3f;
                return Smooth(0.32f, 0.75f, n);
            });
        }

        /// <summary>
        /// Fruit and head rots on the organ's own UVs (u round it, v along it): a few large round lesions with concentric rings of
        /// spores, spread round the fruit so one always faces the viewer, growing from their centres.
        /// </summary>
        static Texture2D BuildFruitRot()
        {
            var c = new[] { new Vector2(0.72f, 0.5f), new Vector2(0.24f, 0.42f), new Vector2(0.52f, 0.66f), new Vector2(0.95f, 0.34f) };
            var r = new[] { 0.16f, 0.12f, 0.09f, 0.1f };
            var reveal = new[] { 1f, 0.8f, 0.65f, 0.55f };
            return Paint((u, v, x, y) =>
            {
                float value = 0f;
                for (int i = 0; i < c.Length; i++)
                {
                    float du = Mathf.Abs(u - c[i].x); du = Mathf.Min(du, 1f - du);                  // u wraps round the fruit
                    float d = Mathf.Sqrt(du * du * 2.2f + (v - c[i].y) * (v - c[i].y)) / r[i];
                    if (d > 1f) continue;
                    float core = (1f - d * d) * (0.92f + 0.08f * Mathf.Cos(d * Mathf.PI * 7f));   // rings of spore masses
                    value = Mathf.Max(value, core * reveal[i]);
                }
                return Mathf.Clamp01(value + (Noise(x, y, 0.08f) - 0.5f) * 0.05f);
            });
        }

        /// <summary>Blossom end rot and cigar end rot: a patch round the far end of the fruit (v = 0), widening as it spreads.</summary>
        static Texture2D BuildEndRot(int seed)
        {
            var rng = new System.Random(seed);
            float o = (float)rng.NextDouble() * 50f;
            return Paint((u, v, x, y) =>
            {
                float ragged = Mathf.PerlinNoise(o + Mathf.Cos(u * Mathf.PI * 2f) * 2f, o + Mathf.Sin(u * Mathf.PI * 2f) * 2f) * 0.12f;
                return Mathf.Clamp01(1f - v / (0.34f + ragged));
            });
        }

        /// <summary>GLSL-style smoothstep: 0 below e0, 1 above e1. (Unity's Mathf.SmoothStep(a, b, t) blends from a to b instead.)</summary>
        static float Smooth(float e0, float e1, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(e0, e1, x));

        static Texture2D Paint(System.Func<float, float, int, int, float> value)
        {
            var tex = NewTexture();
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float f = value(x / (float)Size, y / (float)Size, x, y);
                    pixels[y * Size + x] = new Color(f, f, f, 1f);
                }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Base leaf colour with subtle vein structure.</summary>
        static Texture2D BuildLeafAlbedo()
        {
            var tex = NewTexture();
            var pixels = new Color[Size * Size];

            var deep = new Color(0.16f, 0.38f, 0.14f);
            var light = new Color(0.32f, 0.60f, 0.24f);
            var vein = new Color(0.44f, 0.68f, 0.33f);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = x / (float)Size;
                    float v = y / (float)Size;

                    float mottle = Mathf.PerlinNoise(u * 8f, v * 8f) * 0.18f;
                    var c = Color.Lerp(deep, light, v * 0.6f + mottle);

                    // Midrib
                    float midrib = 1f - Mathf.SmoothStep(0f, 0.035f, Mathf.Abs(u - 0.5f));
                    // Lateral veins fanning from the midrib
                    float lateral = Mathf.Abs(Mathf.Sin((v * 9f - Mathf.Abs(u - 0.5f) * 7f) * Mathf.PI));
                    lateral = 1f - Mathf.SmoothStep(0.86f, 1f, lateral);

                    c = Color.Lerp(c, vein, Mathf.Clamp01(midrib * 0.85f + lateral * 0.25f));
                    pixels[y * Size + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // ---- helpers ----

        static float Noise(int x, int y, float scale) =>
            Mathf.PerlinNoise(x * scale, y * scale);

        static Texture2D NewTexture() =>
            new Texture2D(Size, Size, TextureFormat.RGB24, true);

        /// <param name="isData">Masks are data, not colour: import them linear so the shader's
        /// thresholds see the values that were written (sRGB import would darken them).</param>
        static void SaveTexture(Texture2D tex, string name, bool isData)
        {
            string path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.sRGBTexture = !isData;
                importer.isReadable = isData;   // masks are sampled on the CPU to place annotations on real lesions
                importer.SaveAndReimport();
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
