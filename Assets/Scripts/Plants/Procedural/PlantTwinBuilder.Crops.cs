using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PlantDoctor.Plants
{
    /// <summary>
    /// The body plans added with the PlantSeg plants: grasses with several tillers and their heads (rice, wheat, corn), leafy
    /// pseudostems with a bulb or rhizome (garlic, ginger), the banana's pseudostem, the coffee shrub's tiers of side branches and
    /// the cucumber climbing its stake; plus the heads and roots at a rosette's centre (cabbage, broccoli, cauliflower, carrot).
    /// </summary>
    public partial class PlantTwinBuilder
    {
        // ------------------------------------------------------------------
        // Grasses and grass-like plants
        // ------------------------------------------------------------------

        static bool IsHead(FruitForm f) => f == FruitForm.Panicle || f == FruitForm.Spike;

        /// <summary>
        /// One or more stalks (tillers, pseudostems) from the base, each with two ranks of long leaves, and what the species carries:
        /// corn a tassel and ears, rice a drooping panicle and wheat an upright spike on every tiller, garlic a bulb and ginger a
        /// rhizome at the soil.
        /// </summary>
        void BuildGrass(SpeciesProfile p, CachedMeshes meshes, System.Random rng)
        {
            float crown = p.stemRadius * 1.3f;
            float soilTop = BuildGround(p, meshes, crown);
            float stalkHeight = Mathf.Max(0.08f, p.height * p.stalkShare - soilTop - 0.02f);
            _lean = p.mainStems > 1 ? Mathf.Max(0.01f, p.stemLean) : 0.01f;
            meshes.Stem ??= ProceduralMeshes.Stem(stalkHeight, p.stemRadius, p.stemRadius * 0.45f, _lean, Mathf.Max(4, p.leafCount));
            var basePos = new Vector3(0f, soilTop - 0.01f, 0f);
            var leafSets = LeafSets(p, meshes);
            int tillers = Mathf.Max(1, p.mainStems);
            int perTiller = Mathf.Max(3, p.leafCount / tillers);
            float leafTop = p.grassTassel ? 0.86f : 0.97f;
            var tops = new List<(Vector3 pos, Quaternion rot)>();

            for (int s = 0; s < tillers; s++)
            {
                // The first stalk stands straight; tillers lean out by different amounts round it.
                float tilt = tillers > 1 && s > 0 ? p.stemSplay * Mathf.Lerp(0.55f, 1.15f, (float)rng.NextDouble()) : 0f;
                float baseYaw = s * 137.5f + 20f;
                var rot = Quaternion.Euler(0f, baseYaw, -tilt);
                var from = basePos + (s > 0 ? Quaternion.Euler(0f, baseYaw, 0f) * Vector3.right * (p.stemRadius * 1.6f) : Vector3.zero);
                PaintStem(Part($"Stem_{s}", meshes.Stem, stemMaterial, from, rot, Vector3.one, true), p);
                tops.Add((from + StemPoint(rot, 1f, stalkHeight), rot));

                // Leaves alternate on opposite sides of the stalk (two ranks), one per node.
                for (int i = 0; i < perTiller; i++)
                {
                    float t = i / (float)(perTiller - 1);
                    float yaw = baseYaw + (i % 2 == 0 ? 0f : 180f) + ((float)rng.NextDouble() - 0.5f) * 30f + t * 25f;
                    float size = Mathf.Lerp(p.leafSize.x, p.leafSize.y, Mathf.Abs(t - 0.4f) / 0.6f) * (0.92f + (float)rng.NextDouble() * 0.14f);
                    var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                    var pos = from + StemPoint(rot, Mathf.Lerp(p.firstLeafHeight, leafTop, t), stalkHeight) + dir * (p.stemRadius * 0.7f);
                    AddLeaf($"Leaf_{s}_{i:00}", p, leafSets[(i + s) % LeafVariants], pos, Quaternion.Euler(0f, yaw, 0f), size);
                }
            }

            if (p.grassTassel) BuildTassel(p, meshes, tops[0].pos);

            if (p.fruit == FruitForm.None || p.fruitCount <= 0 || fruitMaterial == null) return;
            meshes.Fruit ??= ProceduralMeshes.Fruit(p.fruit);
            meshes.Calyx ??= ProceduralMeshes.Calyx(p.fruit);
            float r = p.fruitRadius * p.fruitDisplayScale;
            _batch = new List<(Matrix4x4, int)>();
            if (IsHead(p.fruit))
            {
                // A head on every tiller: wheat's spike carries on up the stalk, rice's panicle bows over from it.
                for (int k = 0; k < tops.Count && k < p.fruitCount; k++)
                {
                    var (top, rot) = tops[k];
                    var hang = p.fruit == FruitForm.Panicle ? Quaternion.Euler(0f, k * 67f, 0f) : rot;
                    AddFruit(p, meshes, k, top, hang, r);
                }
            }
            else if (p.fruit == FruitForm.CornEar)
            {
                // The ear sits in a leaf axil about half way up, angled up from the stalk.
                for (int k = 0; k < p.fruitCount; k++)
                {
                    var outward = Quaternion.Euler(0f, 90f + k * 180f, 0f) * Vector3.forward;
                    var at = basePos + StemPoint(Quaternion.identity, 0.48f - k * 0.08f, stalkHeight);
                    var tilt = Quaternion.AngleAxis(28f, Vector3.Cross(Vector3.up, outward));
                    AddFruit(p, meshes, k, at + outward * (p.stemRadius + r * 1.1f) + Vector3.up * (r * 0.9f), tilt, r);
                }
            }
            else
            {
                // A bulb or rhizome at the foot of the stalks, half in the soil.
                for (int k = 0; k < p.fruitCount; k++)
                {
                    var off = k == 0 ? Vector3.zero : Quaternion.Euler(0f, k * 140f, 0f) * Vector3.forward * (r * 1.4f);
                    AddFruit(p, meshes, k, basePos + off + Vector3.up * (p.fruit == FruitForm.Bulb ? r * 0.25f : r * 0.15f), Quaternion.Euler(0f, k * 70f, 0f), r);
                }
            }
            EndFruitBatch(p, meshes);
        }

        void BuildTassel(SpeciesProfile p, CachedMeshes meshes, Vector3 top)
        {
            if (meshes.Extra == null)
            {
                var tassel = new ProceduralMeshes.MeshData();
                float len = p.height * 0.16f;
                tassel.AddTube(new[] { top - Vector3.up * 0.02f, top + Vector3.up * len * 0.5f, top + new Vector3(0.01f, len, 0f) }, new[] { 0.004f, 0.003f, 0.0015f }, 5);
                for (int b = 0; b < 9; b++)
                {
                    float a = b * 2.4f;
                    var from = top + Vector3.up * (len * 0.1f * (b % 3));
                    var mid = from + new Vector3(Mathf.Cos(a), 0.9f, Mathf.Sin(a)) * (len * 0.45f);
                    var end = mid + new Vector3(Mathf.Cos(a), -0.6f, Mathf.Sin(a)) * (len * 0.35f);
                    tassel.AddTube(new[] { from, mid, end }, new[] { 0.002f, 0.0015f, 0.0008f }, 4);
                }
                meshes.Extra = tassel.ToMesh($"{p.speciesId}_Tassel");
            }
            Paint(Part("Tassel", meshes.Extra, stemMaterial, Vector3.zero, Quaternion.identity, Vector3.one, false), new Color(0.78f, 0.68f, 0.42f), null);
        }

        // ------------------------------------------------------------------
        // Rosette centres: heads and roots
        // ------------------------------------------------------------------

        static bool IsCentral(FruitForm f) =>
            f == FruitForm.BroccoliHead || f == FruitForm.CauliflowerCurd || f == FruitForm.CabbageHead || f == FruitForm.CarrotRoot;

        /// <summary>
        /// What sits in the middle of a rosette: broccoli's green head and cauliflower's white curd on top of the stem, ringed by the
        /// leaves; cabbage's head of wrapped leaves low on its short stem; a carrot's orange shoulder at the soil.
        /// </summary>
        void BuildCentralFruit(SpeciesProfile p, CachedMeshes meshes, Vector3 crown, float stemHeight)
        {
            if (p.fruitCount <= 0 || fruitMaterial == null) return;
            meshes.Fruit ??= ProceduralMeshes.Fruit(p.fruit);
            meshes.Calyx ??= ProceduralMeshes.Calyx(p.fruit);
            float r = p.fruitRadius * p.fruitDisplayScale;
            var at = p.fruit == FruitForm.CarrotRoot ? crown + Vector3.down * (r * 0.3f) : crown + Vector3.up * (stemHeight * 0.92f);
            _batch = new List<(Matrix4x4, int)>();
            AddFruit(p, meshes, 0, at, Quaternion.identity, r);
            EndFruitBatch(p, meshes);
        }

        // ------------------------------------------------------------------
        // Banana
        // ------------------------------------------------------------------

        /// <summary>
        /// A banana plant: the pseudostem (rolled leaf sheaths, green blotched brown), a crown of huge leaves on stout stalks (the old
        /// ones bowing low, the young ones nearly upright), the bunch hanging on its curved stalk from the top with the purple bud at its
        /// end, and two suckers coming up beside the mother plant.
        /// </summary>
        void BuildBanana(SpeciesProfile p, CachedMeshes meshes, System.Random rng)
        {
            float soilTop = BuildGround(p, meshes, p.trunkRadius * 1.2f);
            float h = p.trunkHeight;
            var top = new Vector3(0f, soilTop + h, 0f);
            if (meshes.Wood == null || meshes.Canopy == null)
            {
                var wood = new ProceduralMeshes.MeshData();
                var leaves = new ProceduralMeshes.MeshData();
                var stalks = new ProceduralMeshes.MeshData();
                AddPseudostem(wood, Vector3.up * (soilTop - 0.01f), h, p.trunkRadius);
                AddBananaLeaves(p, leaves, stalks, top, p.leafCount, 1f, rng);
                // Suckers ("followers"): young shoots from the corm that will replace the mother plant.
                for (int k = 0; k < 2; k++)
                {
                    var dir = Quaternion.Euler(0f, 60f + k * 150f, 0f) * Vector3.forward;
                    var basePos = dir * (p.trunkRadius * 2.6f) + Vector3.up * (soilTop - 0.01f);
                    float sh = h * (k == 0 ? 0.32f : 0.2f);
                    AddPseudostem(wood, basePos, sh, p.trunkRadius * 0.45f);
                    AddBananaLeaves(p, leaves, stalks, basePos + Vector3.up * sh, 4, k == 0 ? 0.42f : 0.3f, rng);
                }
                meshes.Wood = wood.ToMesh($"{p.speciesId}_Pseudostem");
                meshes.Canopy = leaves.ToMesh($"{p.speciesId}_Leaves");
                meshes.Pedicels = stalks.ToMesh($"{p.speciesId}_Petioles");
            }
            PaintStem(Part("Wood", meshes.Wood, stemMaterial, Vector3.zero, Quaternion.identity, Vector3.one, true), p);
            Paint(Part("Petioles", meshes.Pedicels, stemMaterial, Vector3.zero, Quaternion.identity, Vector3.one, true), p.stemColor, null);
            Paint(Part("Canopy", meshes.Canopy, leafMaterial, Vector3.zero, Quaternion.identity, Vector3.one, true), p.leafTint, p.leafAtlas);

            if (p.fruit == FruitForm.None || p.fruitCount <= 0 || fruitMaterial == null) return;
            // The bunch stalk leaves the crown, arches out and hangs down; the bunch hangs from its end.
            meshes.Fruit ??= ProceduralMeshes.Fruit(p.fruit);
            meshes.Calyx ??= ProceduralMeshes.Calyx(p.fruit);
            float r = p.fruitRadius * p.fruitDisplayScale;
            var outward = Quaternion.Euler(0f, 35f, 0f) * Vector3.forward;
            var bend = top + outward * (p.trunkRadius * 2.2f) + Vector3.up * 0.04f;
            var hangAt = bend + outward * (p.trunkRadius * 1.2f) - Vector3.up * 0.05f;
            if (meshes.Extra == null)
            {
                var peduncle = new ProceduralMeshes.MeshData();
                peduncle.AddTube(new[] { top - Vector3.up * 0.03f, bend, hangAt }, new[] { p.trunkRadius * 0.3f, p.trunkRadius * 0.26f, p.trunkRadius * 0.22f }, 8);
                meshes.Extra = peduncle.ToMesh($"{p.speciesId}_Peduncle");
            }
            Paint(Part("Peduncle", meshes.Extra, stemMaterial, Vector3.zero, Quaternion.identity, Vector3.one, false), p.stemColor, null);
            _batch = new List<(Matrix4x4, int)>();
            AddFruit(p, meshes, 0, hangAt, Quaternion.Euler(0f, 20f, 6f), r);
            EndFruitBatch(p, meshes);
        }

        /// <summary>A pseudostem: a slightly bulging cylinder of rolled sheaths, narrowing a little toward the crown.</summary>
        static void AddPseudostem(ProceduralMeshes.MeshData d, Vector3 basePos, float height, float radius)
        {
            var pts = new List<Vector3>();
            var radii = new List<float>();
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                pts.Add(basePos + new Vector3(0.006f * Mathf.Sin(t * 2.4f), t * height, 0f));
                radii.Add(radius * (1.12f - 0.3f * t + 0.12f * Mathf.Exp(-t * 10f)));
            }
            d.AddTube(pts, radii, 16, 3f);
        }

        /// <summary>Leaves from a banana crown: each a stout stalk and a long blade arching out; the oldest lowest and drooping.</summary>
        void AddBananaLeaves(SpeciesProfile p, ProceduralMeshes.MeshData leaves, ProceduralMeshes.MeshData stalks, Vector3 crown, int count, float scale, System.Random rng)
        {
            bool photos = FoliageMeshes.HasPhotos(p);
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 1f;                  // 0 = oldest
                float yaw = i * 137.5f + 25f;
                var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                float length = Mathf.Lerp(p.leafSize.x, p.leafSize.y, t) * scale * (0.94f + (float)rng.NextDouble() * 0.12f);
                float stalk = length * p.petioleLength / (1f + p.petioleLength), blade = length - stalk;
                float elevation = Mathf.Lerp(18f, 72f, t);                           // degrees above level where the stalk leaves the crown
                var stalkDir = Quaternion.AngleAxis(-elevation, Vector3.Cross(Vector3.up, dir)) * dir;
                var at = crown + stalkDir * stalk;
                stalks.AddTube(new[] { crown - Vector3.up * 0.02f, Vector3.Lerp(crown, at, 0.5f), at },
                               new[] { p.stemRadius * 1.4f * scale, p.stemRadius * scale, p.stemRadius * 0.8f * scale }, 6);
                // The blade carries on rising a little, then bows over: old leaves droop hard, young ones stay up.
                var rot = Quaternion.LookRotation(Quaternion.AngleAxis(-elevation * 0.45f, Vector3.Cross(Vector3.up, dir)) * dir, Vector3.up)
                          * Quaternion.Euler(0f, 0f, ((float)rng.NextDouble() - 0.5f) * 20f);
                var m = Matrix4x4.TRS(at, rot, Vector3.one * blade);
                if (photos)
                {
                    int v = rng.Next(p.leafRects.Length);
                    FoliageMeshes.AddLeafCard(leaves, m, p.leafAspects[v], p.leafRects[v], ProceduralMeshes.MaskWindow(rng), p.leafFold,
                        Mathf.Lerp(0.55f, 0.18f, t), 0.01f, 5, 16);
                }
                else ProceduralMeshes.AddSimpleBlade(leaves, m, p, rng);
            }
        }

        // ------------------------------------------------------------------
        // Coffee: tiers of side branches
        // ------------------------------------------------------------------

        /// <summary>
        /// A coffee bush as pruned on a farm: one upright stem with pairs of near-horizontal side branches at each node (each pair at
        /// right angles to the last), longest low down, so the bush is a cone; leaves in opposite pairs along the side branches; the
        /// cherries in tight clusters at the leaf joints of the older, inner part of each branch.
        /// </summary>
        void BuildShrub(SpeciesProfile p, CachedMeshes meshes, System.Random rng)
        {
            BuildGround(p, meshes, p.trunkRadius * 1.3f);
            var nodes = new List<(Vector3 pos, Vector3 dir)>();
            if (meshes.Wood == null || meshes.Canopy == null)
            {
                var wood = new ProceduralMeshes.MeshData();
                var canopy = new ProceduralMeshes.MeshData();
                float H = p.height * 0.97f;
                var stem = new List<Vector3>();
                var radii = new List<float>();
                for (int i = 0; i <= 10; i++)
                {
                    float t = i / 10f;
                    stem.Add(new Vector3(0.008f * Mathf.Sin(t * 3f), t * H, 0.006f * Mathf.Cos(t * 2f)));
                    radii.Add(Mathf.Lerp(p.trunkRadius, p.trunkRadius * 0.25f, t) * (1f + 0.5f * Mathf.Exp(-t * 14f)));
                }
                wood.AddTube(stem, radii, 10, 5f);
                bool photos = FoliageMeshes.HasPhotos(p);
                int pairs = Mathf.Max(2, p.sideBranches);
                for (int k = 0; k < pairs; k++)
                {
                    float f = k / (float)(pairs - 1);
                    float y = Mathf.Lerp(0.16f, 0.93f, f) * H;
                    float len = p.canopyRadius * Mathf.Lerp(1f, 0.22f, f);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float yaw = k * 90f + (side < 0 ? 180f : 0f) + ((float)rng.NextDouble() - 0.5f) * 14f;
                        var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                        var origin = new Vector3(0f, y, 0f);
                        var pts = new List<Vector3>();
                        var rr = new List<float>();
                        for (int i = 0; i <= 6; i++)
                        {
                            float u = i / 6f;
                            pts.Add(origin + dir * (len * u) + Vector3.up * (len * (0.06f * u - 0.16f * u * u)));   // out level, the tip bowing under its weight
                            rr.Add(Mathf.Lerp(p.trunkRadius * 0.38f, p.trunkRadius * 0.12f, u));
                        }
                        wood.AddTube(pts, rr, 6, 5f);
                        // Leaf pairs at the nodes along the branch, spreading flat to both sides.
                        int leafNodes = Mathf.Max(2, Mathf.RoundToInt(len / 0.075f));   // internodes of several centimetres
                        for (int n = 0; n < leafNodes; n++)
                        {
                            float u = (n + 1f) / (leafNodes + 0.5f);
                            var at = origin + dir * (len * u) + Vector3.up * (len * (0.06f * u - 0.16f * u * u));
                            nodes.Add((at, dir));
                            var across = Vector3.Cross(Vector3.up, dir);
                            for (int ls = -1; ls <= 1; ls += 2)
                            {
                                var leafDir = (dir * 0.3f + across * ls).normalized;           // opposite pairs, spread out to both sides
                                float size = Mathf.Lerp(p.leafSize.x, p.leafSize.y, u) * p.canopyLeafScale * (0.9f + (float)rng.NextDouble() * 0.2f);
                                var rot = Quaternion.LookRotation(new Vector3(leafDir.x, -0.35f, leafDir.z).normalized, Vector3.up)
                                          * Quaternion.Euler(0f, 0f, ls * 8f);
                                var m = Matrix4x4.TRS(at, rot, Vector3.one * size);
                                if (photos) FoliageMeshes.AddCanopyLeaf(canopy, p, m, rng);
                                else ProceduralMeshes.AddSimpleBlade(canopy, m, p, rng);
                            }
                        }
                    }
                }
                // A tuft of young leaves at the top of the stem.
                for (int i = 0; i < 4; i++)
                {
                    var rot = Quaternion.Euler(-35f, i * 90f + 45f, 0f);
                    var m = Matrix4x4.TRS(new Vector3(0f, H, 0f), rot, Vector3.one * p.leafSize.y * p.canopyLeafScale * 0.8f);
                    if (photos) FoliageMeshes.AddCanopyLeaf(canopy, p, m, rng);
                }
                meshes.Wood = wood.ToMesh($"{p.speciesId}_Wood");
                meshes.Canopy = canopy.ToMesh($"{p.speciesId}_Canopy");
                _shrubNodes[p] = nodes;
            }
            else nodes = _shrubNodes[p];

            Paint(Part("Wood", meshes.Wood, stemMaterial, Vector3.zero, Quaternion.identity, Vector3.one, true), p.barkColor,
                barkTexture != null ? barkTexture : Texture2D.whiteTexture);
            Paint(Part("Canopy", meshes.Canopy, leafMaterial, Vector3.zero, Quaternion.identity, Vector3.one, true), p.leafTint, p.leafAtlas);

            if (p.fruit == FruitForm.None || p.fruitCount <= 0 || fruitMaterial == null || nodes.Count == 0) return;
            meshes.Fruit ??= ProceduralMeshes.Fruit(p.fruit);
            meshes.Calyx ??= ProceduralMeshes.Calyx(p.fruit);
            float r = p.fruitRadius * p.fruitDisplayScale;
            _batch = new List<(Matrix4x4, int)>();
            for (int k = 0; k < p.fruitCount; k++)
            {
                var (at, dir) = nodes[(k * 11 + 2) % nodes.Count];
                for (int c = 0; c < Mathf.Max(1, p.fruitCluster); c++)
                {
                    // Cherries crowd round the branch at the node, above and below it.
                    float a = c * 2.4f + k;
                    var around = Quaternion.AngleAxis(a * Mathf.Rad2Deg, dir) * Vector3.up;
                    AddFruit(p, meshes, k * 12 + c, at + around * (r * 1.25f) + dir * (r * 0.5f * (c % 2)), Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f), r);
                }
            }
            EndFruitBatch(p, meshes);
        }

        readonly Dictionary<SpeciesProfile, List<(Vector3, Vector3)>> _shrubNodes = new Dictionary<SpeciesProfile, List<(Vector3, Vector3)>>();

        // ------------------------------------------------------------------
        // Cucumber: a climber on a stake
        // ------------------------------------------------------------------

        /// <summary>
        /// A cucumber trained up a stake: the stem winds up beside it, a leaf on a long stalk at every node turning out to the light,
        /// a coiled tendril opposite each leaf gripping the stake, yellow flowers at the upper nodes and fruit hanging from the middle
        /// ones.
        /// </summary>
        void BuildClimber(SpeciesProfile p, CachedMeshes meshes, System.Random rng)
        {
            float soilTop = BuildGround(p, meshes, p.stemRadius * 1.6f);
            var pole = new Vector3(0.045f, 0f, 0f);
            float top = p.height * 0.96f;
            Vector3 StemAt(float t) => pole + new Vector3(-0.035f * Mathf.Cos(t * 9f), Mathf.Lerp(soilTop - 0.01f, top, t), 0.035f * Mathf.Sin(t * 9f));
            int nodesCount = Mathf.Max(4, p.leafCount);
            if (meshes.Wood == null || meshes.Extra == null)
            {
                var stake = new ProceduralMeshes.MeshData();
                stake.AddTube(new[] { pole + Vector3.down * 0.05f, pole + Vector3.up * (p.height * 1.04f) }, new[] { 0.009f, 0.008f }, 8, 4f);
                var green = new ProceduralMeshes.MeshData();
                var pts = new List<Vector3>();
                var rr = new List<float>();
                for (int i = 0; i <= 40; i++)
                {
                    float t = i / 40f;
                    pts.Add(StemAt(t));
                    rr.Add(Mathf.Lerp(p.stemRadius, p.stemRadius * 0.45f, t));
                }
                green.AddTube(pts, rr, 6);
                for (int n = 0; n < nodesCount; n++)
                {
                    float t = (n + 0.6f) / nodesCount;
                    var at = StemAt(t);
                    // The tendril reaches for the stake and coils round it.
                    var toPole = new Vector3(pole.x - at.x, 0f, pole.z - at.z);
                    var coil = new List<Vector3>();
                    var cr = new List<float>();
                    for (int i = 0; i <= 14; i++)
                    {
                        float u = i / 14f;
                        float ang = u * 11f + n;
                        var reach = at + toPole * Mathf.Min(1f, u * 1.6f) + Vector3.up * (0.04f * u);
                        coil.Add(reach + new Vector3(Mathf.Cos(ang), 0.2f * u, Mathf.Sin(ang)) * (0.012f * Mathf.SmoothStep(0f, 1f, (u - 0.4f) / 0.6f)));
                        cr.Add(Mathf.Lerp(0.0016f, 0.0007f, u));
                    }
                    green.AddTube(coil, cr, 3);
                }
                meshes.Wood = stake.ToMesh($"{p.speciesId}_Stake");
                meshes.Extra = green.ToMesh($"{p.speciesId}_Vine");
            }
            Paint(Part("Stake", meshes.Wood, stemMaterial, Vector3.zero, Quaternion.identity, Vector3.one, false), p.barkColor,
                barkTexture != null ? barkTexture : Texture2D.whiteTexture);
            Paint(Part("Stem_0", meshes.Extra, stemMaterial, Vector3.zero, Quaternion.identity, Vector3.one, true), p.stemColor, null);

            // Leaves turn out from the stake to the light, larger low down.
            var leafSets = LeafSets(p, meshes);
            var nodePos = new List<(Vector3 at, Vector3 outward)>();
            for (int n = 0; n < nodesCount; n++)
            {
                float t = (n + 0.6f) / nodesCount;
                var at = StemAt(t);
                float yaw = n * 137.5f + 200f;
                var outward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                if (Vector3.Dot(outward, Vector3.left) < -0.3f) outward = Vector3.Reflect(outward, Vector3.right);   // not into the stake
                nodePos.Add((at, outward));
                float size = Mathf.Lerp(p.leafSize.x, p.leafSize.y, t) * (0.92f + (float)rng.NextDouble() * 0.16f);
                // A trained cucumber turns its leaves out to the light: the blades stand up from their stalks, faces outward.
                var rot = Quaternion.LookRotation(outward, Vector3.up) * Quaternion.Euler(Mathf.Lerp(-40f, -60f, t), 0f, ((float)rng.NextDouble() - 0.5f) * 14f);
                AddLeaf($"Leaf_0_{n:00}", p, leafSets[n % LeafVariants], at, rot, size);
            }
            AddTipLeaves("Leaf_tip0", p, leafSets, StemAt(1f), Vector3.up, p.leafSize.y, 2, rng);

            // Fruit hangs from the middle nodes on short stalks.
            if (p.fruit != FruitForm.None && p.fruitCount > 0 && fruitMaterial != null)
            {
                meshes.Fruit ??= ProceduralMeshes.Fruit(p.fruit);
                meshes.Calyx ??= ProceduralMeshes.Calyx(p.fruit);
                float r = p.fruitRadius * p.fruitDisplayScale;
                _batch = new List<(Matrix4x4, int)>();
                for (int k = 0; k < p.fruitCount; k++)
                {
                    var (at, outward) = nodePos[Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(0.2f, 0.65f, p.fruitCount > 1 ? k / (float)(p.fruitCount - 1) : 0.4f) * nodesCount), 0, nodesCount - 1)];
                    var hang = at + Quaternion.Euler(0f, 70f, 0f) * outward * (r * 1.6f) + Vector3.down * (r * 0.4f);
                    AddFruit(p, meshes, k, hang, Quaternion.Euler(0f, k * 40f, 4f), r);
                }
                EndFruitBatch(p, meshes);
            }

            // Flowers at the upper nodes, facing out.
            if (p.flowerCount > 0 && fruitMaterial != null)
            {
                if (meshes.Flowers == null)
                {
                    var placed = new List<Matrix4x4>();
                    for (int k = 0; k < p.flowerCount; k++)
                    {
                        var (at, outward) = nodePos[Mathf.Clamp(nodesCount - 2 - k * 2, 0, nodesCount - 1)];
                        var facing = (outward + Vector3.up * 0.6f).normalized;
                        placed.Add(Matrix4x4.TRS(at + outward * (p.flowerRadius * 2.2f), Quaternion.FromToRotation(Vector3.up, facing), Vector3.one * p.flowerRadius));
                    }
                    meshes.Flowers = CombineFlowers(p, meshes, placed);
                }
                DrawFlowers(p, meshes);
            }
        }
    }
}
