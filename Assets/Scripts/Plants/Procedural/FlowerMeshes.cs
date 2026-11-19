using UnityEngine;

namespace PlantDoctor.Plants
{
    /// <summary>How a species' flower is shaped.</summary>
    public enum FlowerForm
    {
        /// <summary>Petals fused into a deep, flaring trumpet with pointed lobes (squash, cucumber, tobacco).</summary>
        Trumpet,
        /// <summary>A flat or reflexed five-pointed star with a cone of anthers in the middle (tomato, potato, pepper, eggplant).</summary>
        Star,
        /// <summary>Separate rounded petals round a yellow centre (strawberry, raspberry, fruit-tree blossom).</summary>
        Open,
        /// <summary>A flat, fused, five-angled wheel with a cone of anthers (potato, eggplant).</summary>
        Rotate
    }

    /// <summary>
    /// Flowers, built from a few numbers instead of one model per species: a corolla (two-sided, so both faces light correctly), the
    /// centre (a cone of anthers or a domed receptacle) and the green sepals underneath. One unit = the flower's radius; it faces +Y.
    ///
    /// A small generated texture colours it: petal colour shading to the throat, a darker midrib and fine veins on each petal, the
    /// centre colour and the sepal green. UV0.x picks the part (corolla 0-0.6 from throat to rim, centre 0.7-0.8, sepals 0.9-1),
    /// UV0.y runs across one petal.
    /// </summary>
    public static class FlowerMeshes
    {
        const float CorollaU = 0.6f, CentreU = 0.75f, SepalU = 0.95f;

        public static Mesh Build(FlowerForm form, int petals, float cone)
        {
            petals = Mathf.Clamp(petals, 3, 8);
            var d = new ProceduralMeshes.MeshData();
            Corolla(d, form, petals);
            Centre(d, form, cone);
            Sepals(d, form, petals);
            return d.ToMesh($"Flower_{form}_{petals}");
        }

        /// <summary>Outline of a petal: 1 at its tip (phi = 0), smaller toward the gap between petals (phi = +-0.5).</summary>
        static float Outline(FlowerForm form, float phi)
        {
            float a = Mathf.Abs(phi) * 2f;   // 0 at the petal's middle, 1 between petals
            switch (form)
            {
                case FlowerForm.Trumpet: return 1f - 0.22f * Mathf.Pow(a, 1.4f);                        // shallow pointed lobes
                case FlowerForm.Star: return Mathf.Lerp(1f, 0.28f, Mathf.Pow(a, 0.9f));                 // narrow pointed petals
                case FlowerForm.Rotate: return 1f - 0.27f * Mathf.Pow(a, 1.3f);                         // a pentagon with short points
                default: return 0.22f + 0.78f * Mathf.Sqrt(Mathf.Max(0f, 1f - a * a * 0.92f));          // rounded, nearly touching
            }
        }

        static void Corolla(ProceduralMeshes.MeshData d, FlowerForm form, int petals)
        {
            const int rings = 7, perPetal = 8;
            int seg = petals * perPetal;
            for (int side = 0; side < 2; side++)
            {
                int start = d.Vertices.Count;
                for (int i = 0; i <= rings; i++)
                {
                    float t = i / (float)rings;
                    for (int j = 0; j <= seg; j++)
                    {
                        float th = j / (float)seg * Mathf.PI * 2f;
                        float phi = Mathf.Repeat(j / (float)perPetal, 1f) - 0.5f;   // -0.5..0.5 across one petal, 0 = its midline
                        float outline = Outline(form, phi);
                        float mid = 1f - Mathf.Abs(phi) * 2f;
                        float r, y;
                        switch (form)
                        {
                            case FlowerForm.Trumpet:
                                r = Mathf.Lerp(0.13f, outline, Mathf.Pow(t, 1.6f));
                                y = 0.95f * t - 0.32f * Mathf.Pow(t, 6f) * (0.6f + 0.4f * mid);   // a deep tube, the lobes flaring out at the rim
                                y += 0.025f * Mathf.Sin(phi * Mathf.PI * 2f) * t;                 // pleats between the lobes
                                break;
                            case FlowerForm.Star:
                                r = Mathf.Lerp(0.06f, outline, t);
                                y = 0.10f * t - 0.36f * Mathf.Pow(t, 2.2f) * mid;                 // petals bend back from a shallow cup
                                break;
                            case FlowerForm.Rotate:
                                r = Mathf.Lerp(0.06f, outline, t);
                                y = 0.07f * t - 0.10f * t * t * mid;                               // nearly flat, the points bent back a little
                                break;
                            default:
                                r = Mathf.Lerp(0.05f, outline, t);
                                y = 0.16f * t * t * (0.7f + 0.3f * mid);                           // a shallow saucer
                                break;
                        }
                        y += 0.018f * mid * mid * Mathf.Sin(t * 7f);                                // a slight ripple along the petal
                        d.Vertices.Add(new Vector3(Mathf.Cos(th) * r, y, Mathf.Sin(th) * r));
                        d.Uv0.Add(new Vector2(t * CorollaU * 0.98f, phi + 0.5f));
                        d.Uv1.Add(Vector2.zero);
                    }
                }
                d.Grid(start, rings, seg, side == 1);
            }
        }

        /// <summary>The middle: a cone of fused anthers (tomato family) or a domed, many-stamened centre (strawberry, squash).</summary>
        static void Centre(ProceduralMeshes.MeshData d, FlowerForm form, float cone)
        {
            int start = d.Vertices.Count;
            if ((form == FlowerForm.Star || form == FlowerForm.Rotate) && cone > 0.01f)
                d.AddTube(new[] { new Vector3(0f, 0.02f, 0f), new Vector3(0f, cone * 0.6f, 0f), new Vector3(0f, cone, 0f) },
                          new[] { 0.14f, 0.11f, 0.03f }, 8);
            else
            {
                float h = form == FlowerForm.Trumpet ? 0.42f : 0.09f;
                float rad = form == FlowerForm.Trumpet ? 0.08f : 0.22f;
                d.AddTube(new[] { new Vector3(0f, 0.01f, 0f), new Vector3(0f, h * 0.7f, 0f), new Vector3(0f, h, 0f) },
                          new[] { rad, rad * 0.9f, rad * 0.08f }, 10);
            }
            for (int i = start; i < d.Uv0.Count; i++) d.Uv0[i] = new Vector2(CentreU, 0.5f);
        }

        /// <summary>The green sepals just under the corolla: long and narrow on the tomato family, short on the others.</summary>
        static void Sepals(ProceduralMeshes.MeshData d, FlowerForm form, int petals)
        {
            float reach = form == FlowerForm.Star || form == FlowerForm.Rotate ? 0.62f : form == FlowerForm.Open ? 0.55f : 0.32f;
            const int perSepal = 6;
            int seg = petals * perSepal;
            for (int side = 0; side < 2; side++)
            {
                int start = d.Vertices.Count;
                for (int i = 0; i <= 3; i++)
                {
                    float t = i / 3f;
                    for (int j = 0; j <= seg; j++)
                    {
                        float th = (j / (float)seg + 0.5f / petals) * Mathf.PI * 2f;   // between the petals
                        float phi = Mathf.Repeat(j / (float)perSepal, 1f) - 0.5f;
                        float outline = Mathf.Lerp(1f, 0.2f, Mathf.Abs(phi) * 2f);
                        float r = Mathf.Lerp(0.05f, reach * outline, t);
                        float y = -0.03f - 0.05f * t * t;
                        d.Vertices.Add(new Vector3(Mathf.Cos(th) * r, y, Mathf.Sin(th) * r));
                        d.Uv0.Add(new Vector2(SepalU, 0.5f));
                        d.Uv1.Add(Vector2.zero);
                    }
                }
                d.Grid(start, 3, seg, side == 1);
            }
        }

        /// <summary>The colours of one species' flower, as a small texture (see the class summary for the layout).</summary>
        public static Texture2D Texture(Color petal, Color throat, Color centre)
        {
            const int w = 128, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "FlowerColours", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            var sepal = new Color(0.30f, 0.46f, 0.18f);
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;                 // across a petal
                float across = Mathf.Abs(v - 0.5f) * 2f;   // 0 at the midrib
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    Color c;
                    if (u < CorollaU + 0.02f)
                    {
                        float t = Mathf.Clamp01(u / CorollaU);
                        c = Color.Lerp(throat, petal, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.45f, t)));
                        float midrib = Mathf.Exp(-across * across * 140f) * 0.16f;
                        float veins = Mathf.Pow(Mathf.Abs(Mathf.Sin(across * 9f + t * 1.5f)), 18f) * 0.07f * t;
                        float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, t)) * 0.07f;
                        float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, across)) * 0.08f;   // a little darker toward the petal's sides (overlap shade)
                        c = c * (1f - midrib - veins - edge) + Color.white * rim;
                    }
                    else if (u < 0.86f) c = centre * (0.92f + 0.08f * Mathf.Sin(v * 40f));
                    else c = sepal;
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply(true, false);
            return tex;
        }
    }
}
