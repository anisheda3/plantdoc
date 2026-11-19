using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PlantDoctor.Plants
{
    /// <summary>
    /// Builds the plant twin at runtime from a <see cref="SpeciesProfile"/>: one generator for
    /// soft-stemmed plants (tomato, potato, pepper) and one for trees (apple). Called again
    /// whenever the AI identifies a different species; the disease look is then projected
    /// onto the new body by <see cref="PlantVisualBinder"/> exactly as before.
    ///
    /// The plant stands on a small heap of soil with its root system spreading over the ground
    /// around it (no pot), and its leaves are cut-outs of real leaf photographs (PlantVillage,
    /// see tools/assets/make_plant_textures.py).
    ///
    /// Lives on the twin's PlantRoot. Generated parts go under a child named "Generated",
    /// which is replaced on every rebuild; meshes are cached per species.
    /// </summary>
    public partial class PlantTwinBuilder : MonoBehaviour
    {
        [Header("Materials")]
        [Tooltip("PlantDisease material for leaves (diseases show here); alpha clip on, each renderer brings its species' leaf atlas.")]
        [SerializeField] Material leafMaterial;
        [Tooltip("PlantDisease material for stems, branches and bark (dulls with stress, never spots).")]
        [SerializeField] Material stemMaterial;
        [Tooltip("Lit material for the heap of soil at the plant's crown.")]
        [SerializeField] Material soilMaterial;
        [Tooltip("Lit material for the roots (tinted per species).")]
        [SerializeField] Material rootMaterial;
        [Tooltip("A neutral lit material; each fruit's colour comes from its species profile.")]
        [SerializeField] Material fruitMaterial;
        [Tooltip("Fruit, heads and roots when the disease is on them (the disease shader).")]
        [SerializeField] Material diseasedFruitMaterial;
        [Tooltip("Tiling bark texture for tree trunks and branches.")]
        [SerializeField] Texture2D barkTexture;

        [Header("Wiring")]
        [SerializeField] PlantVisualBinder visualBinder;
        [Tooltip("Ground pieces scaled with the species' footprint (base ring, contact shadow).")]
        [SerializeField] Transform[] footprintPieces;
        [Tooltip("Floating name tag, moved to just above the plant.")]
        [SerializeField] Transform label;
        [Tooltip("Layer for everything generated (the viewer twin lives on its own layer, so only the viewer camera sees it). Empty = leave on the default layer.")]
        [SerializeField] string layerName = "";

        [Header("Species")]
        [SerializeField] SpeciesProfile defaultProfile;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        const float StemLean = 0.03f;
        float _lean = StemLean;   // the current species' stem lean (raspberry canes arch over)
        /// <summary>Length of a photographic compound leaf in mesh units (rachis + terminal leaflet), so leafSize stays in metres.</summary>
        const float CompoundSpan = 1.3f;
        const int LeafVariants = 3;

        class CachedMeshes
        {
            public Mesh Stem, Canopy, Wood, Fruit, Calyx, Pedicels, Mound, Roots, RootSoil, Extra, Flower, CalyxBatch, Flowers, FlowerStalks;
            public Texture2D FlowerColours;
            public Mesh[] FruitBatches;   // all of a plant's fruit of one colour in one mesh (dozens of berries = one draw call)
            public FoliageMeshes.LeafSet[] Leaves;
        }

        readonly Dictionary<SpeciesProfile, CachedMeshes> _cache = new Dictionary<SpeciesProfile, CachedMeshes>();
        readonly Dictionary<SpeciesProfile, List<(Vector3, Vector3)>> _treeTips = new Dictionary<SpeciesProfile, List<(Vector3, Vector3)>>();
        Vector3[] _footprintBase;
        Transform _generated;
        MaterialPropertyBlock _block;

        public SpeciesProfile Current { get; private set; }
        public Material LeafMaterial => leafMaterial;
        /// <summary>Renderers that show disease (leaves, stems, bark) after the last build.</summary>
        public IReadOnlyList<Renderer> DiseaseRenderers => _disease;
        readonly List<Renderer> _disease = new List<Renderer>();
        /// <summary>Fruit, heads and roots after the last build (they carry fruit, head and root diseases).</summary>
        readonly List<Renderer> _organs = new List<Renderer>();
        Diseases.AffectedPart _part = Diseases.AffectedPart.Leaf;

        /// <summary>
        /// Where the current disease shows: on the leaves (and the whole plant for wilts), or on the fruit, head or root. Organ diseases
        /// switch the organs to the disease material and leave the leaves healthy.
        /// </summary>
        public void ShowDiseaseOn(Diseases.AffectedPart part)
        {
            _part = part;
            RegisterDisease();
        }

        bool OrganDisease => (_part == Diseases.AffectedPart.Fruit || _part == Diseases.AffectedPart.Head || _part == Diseases.AffectedPart.Root)
                             && _organs.Count > 0 && diseasedFruitMaterial != null;

        void RegisterDisease()
        {
            bool organ = OrganDisease;
            foreach (var r in _organs) if (r != null) r.sharedMaterial = organ ? diseasedFruitMaterial : fruitMaterial;
            if (organ)
            {
                // The leaves keep their healthy look: clear whatever an earlier leaf disease left on them.
                _block ??= new MaterialPropertyBlock();
                foreach (var r in _disease)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(_block);
                    _block.SetFloat("_LesionDensity", 0f); _block.SetFloat("_Yellowing", 0f); _block.SetFloat("_Wilt", 0f); _block.SetFloat("_LeafDamage", 0f);
                    r.SetPropertyBlock(_block);
                }
            }
            visualBinder?.SetRenderers(this, organ ? _organs.ToArray() : _disease.ToArray());
        }

        /// <summary>Raised after every rebuild.</summary>
        public event Action<SpeciesProfile> Rebuilt;

        void Awake()
        {
            // Build before any Start(), so the binder and presenter find a plant on frame one.
            if (Current == null && defaultProfile != null) Build(defaultProfile);
        }

        void OnDestroy()
        {
            foreach (var c in _cache.Values)
            {
                foreach (var m in new[] { c.Stem, c.Canopy, c.Wood, c.Fruit, c.Calyx, c.Pedicels, c.Mound, c.Roots, c.RootSoil, c.Extra, c.Flower, c.CalyxBatch })
                    if (m != null) DestroyObject(m);
                if (c.FruitBatches != null)
                    foreach (var m in c.FruitBatches)
                        if (m != null) DestroyObject(m);
                if (c.Leaves != null)
                    foreach (var set in c.Leaves) { DestroyObject(set.Blades); DestroyObject(set.Stalks); }
            }
            _cache.Clear();
        }

        /// <summary>Replaces the twin's body with the given species. Cheap: meshes are cached.</summary>
        public void Build(SpeciesProfile profile)
        {
            if (profile == null) return;
            _block ??= new MaterialPropertyBlock();
            if (_footprintBase == null && footprintPieces != null)
            {
                _footprintBase = new Vector3[footprintPieces.Length];
                for (int i = 0; i < footprintPieces.Length; i++)
                    if (footprintPieces[i] != null) _footprintBase[i] = footprintPieces[i].localScale;
            }

            if (_generated != null) DestroyObject(_generated.gameObject);
            _generated = new GameObject("Generated").transform;
            _generated.SetParent(transform, false);
            _disease.Clear();
            _organs.Clear();

            if (!_cache.TryGetValue(profile, out var meshes)) _cache[profile] = meshes = new CachedMeshes();
            var rng = new System.Random(profile.seed);

            switch (profile.archetype)
            {
                case TwinArchetype.Tree: BuildTree(profile, meshes, rng); break;
                case TwinArchetype.Rosette: BuildRosette(profile, meshes, rng); break;
                case TwinArchetype.Grass: BuildGrass(profile, meshes, rng); break;
                case TwinArchetype.Vine: BuildVine(profile, meshes, rng); break;
                case TwinArchetype.Banana: BuildBanana(profile, meshes, rng); break;
                case TwinArchetype.Shrub: BuildShrub(profile, meshes, rng); break;
                case TwinArchetype.Climber: BuildClimber(profile, meshes, rng); break;
                default: BuildHerb(profile, meshes, rng); break;
            }

            Current = profile;
            ApplyFootprint(profile);
            if (label != null) label.localPosition = new Vector3(0f, profile.height + 0.1f, 0f);
            AddMarkers(profile);
            ApplyLayer();
            RegisterDisease();
            Rebuilt?.Invoke(profile);
        }

        // ------------------------------------------------------------------
        // Ground: a heap of soil at the crown and the roots lying on the surface
        // ------------------------------------------------------------------

        /// <summary>Builds the mound and roots; returns the height of the soil where the stem comes out.</summary>
        float BuildGround(SpeciesProfile p, CachedMeshes meshes, float crownRadius)
        {
            meshes.Mound ??= RootMeshes.Mound(p.moundRadius, p.moundHeight, p.seed * 3 + 1);
            if (meshes.Roots == null)
            {
                var system = RootMeshes.Roots(p, crownRadius, new System.Random(p.seed * 13 + 5));
                meshes.Roots = system.Roots;
                meshes.RootSoil = system.Soil;
            }
            var mound = Part("SoilMound", meshes.Mound, soilMaterial, Vector3.zero, Quaternion.identity, Vector3.one, false);
            mound.receiveShadows = true;
            var roots = Part("Roots", meshes.Roots, rootMaterial, Vector3.zero, Quaternion.identity, Vector3.one, false);
            roots.shadowCastingMode = ShadowCastingMode.Off;
            Paint(roots, p.rootTint, null);
            var crumbs = Part("RootSoil", meshes.RootSoil, soilMaterial, Vector3.zero, Quaternion.identity, Vector3.one, false);
            crumbs.shadowCastingMode = ShadowCastingMode.Off;
            return p.moundHeight * 0.85f;
        }

        // ------------------------------------------------------------------
        // Soft-stemmed plants
        // ------------------------------------------------------------------

        FoliageMeshes.LeafSet[] LeafSets(SpeciesProfile p, CachedMeshes meshes)
        {
            if (meshes.Leaves != null) return meshes.Leaves;
            var sets = new FoliageMeshes.LeafSet[LeafVariants];
            for (int v = 0; v < LeafVariants; v++)
            {
                float rise = p.archetype == TwinArchetype.Rosette ? p.petioleRise : 0f;   // rosette leaves stand up on their stalks
                if (FoliageMeshes.HasPhotos(p))
                    sets[v] = p.leafForm switch
                    {
                        LeafForm.Compound => FoliageMeshes.CompoundLeaf(p, v, rise),
                        LeafForm.Trifoliate => FoliageMeshes.TrifoliateLeaf(p, v, rise),
                        LeafForm.Strap => FoliageMeshes.StrapLeaf(p, v),
                        _ => FoliageMeshes.SimpleLeaf(p, v, rise)
                    };
                else
                    sets[v] = new FoliageMeshes.LeafSet { Blades = p.leafForm == LeafForm.Compound ? ProceduralMeshes.CompoundLeaf(p) : ProceduralMeshes.SimpleLeaf(p) };
            }
            return meshes.Leaves = sets;
        }

        /// <summary>One leaf: the photographic blades (leaf material) and their stalks (stem material).</summary>
        MeshRenderer AddLeaf(string name, SpeciesProfile p, FoliageMeshes.LeafSet set, Vector3 pos, Quaternion rot, float size)
        {
            float span = p.leafForm == LeafForm.Compound && FoliageMeshes.HasPhotos(p) ? CompoundSpan : 1f;
            var scale = Vector3.one * (size / span);
            var blades = Part(name, set.Blades, leafMaterial, pos, rot, scale, true);
            Paint(blades, p.leafTint, p.leafAtlas);
            if (set.Stalks != null)
                Paint(Part(name + "_Stalk", set.Stalks, stemMaterial, pos, rot, scale, true), p.stemColor, null);
            return blades;
        }

        void BuildHerb(SpeciesProfile p, CachedMeshes meshes, System.Random rng)
        {
            float crown = p.stemRadius * 1.6f;
            float soilTop = BuildGround(p, meshes, crown);
            float stemHeight = Mathf.Max(0.2f, p.height - soilTop - 0.05f);
            _lean = p.stemLean;
            meshes.Stem ??= ProceduralMeshes.Stem(stemHeight, p.stemRadius, p.stemRadius * 0.35f, _lean);
            var leafSets = LeafSets(p, meshes);

            int stems = Mathf.Max(1, p.mainStems);
            var stemRotations = new Quaternion[stems];
            var basePos = new Vector3(0f, soilTop - 0.012f, 0f);
            for (int s = 0; s < stems; s++)
            {
                float tilt = stems > 1 ? p.stemSplay : 0f;
                stemRotations[s] = Quaternion.Euler(0f, s * 360f / stems + 20f, -tilt);   // leaning toward +X, the way the stem bends
                PaintStem(Part($"Stem_{s}", meshes.Stem, stemMaterial, basePos, stemRotations[s], Vector3.one, true), p);
            }

            // Leaves spiral up each stem (golden angle), larger and droopier low down.
            int perStem = Mathf.Max(2, p.leafCount / stems);
            for (int s = 0; s < stems; s++)
            {
                for (int i = 0; i < perStem; i++)
                {
                    // Alternate leaves spiral up the stem (golden angle); opposite leaves come in pairs, each pair at right angles.
                    float t = p.oppositeLeaves ? (i / 2) / (float)Mathf.Max(1, (perStem - 1) / 2) : i / (float)(perStem - 1);
                    float yaw = p.oppositeLeaves ? (i / 2) * 90f + (i % 2) * 180f + 15f + s * 61f : i * 137.5f + 15f + s * 61f;
                    float size = Mathf.Lerp(p.leafSize.x, p.leafSize.y, Mathf.Pow(t, 0.85f)) * (0.94f + (float)rng.NextDouble() * 0.12f);
                    float pitch = Mathf.Lerp(18f, -18f, t) + ((float)rng.NextDouble() - 0.5f) * 10f;
                    float radius = Mathf.Lerp(0.014f, 0.006f, t);
                    var pos = basePos + StemPoint(stemRotations[s], Mathf.Lerp(p.firstLeafHeight, 0.95f, t), stemHeight)
                              + new Vector3(Mathf.Cos(yaw * Mathf.Deg2Rad) * radius, 0f, Mathf.Sin(yaw * Mathf.Deg2Rad) * radius);
                    var rot = Quaternion.Euler(pitch, 90f - yaw, (i % 2 == 0 ? 1f : -1f) * 8f);
                    AddLeaf($"Leaf_{s}_{i:00}", p, leafSets[(i + s) % LeafVariants], pos, rot, size);
                }
            }

            // Each stem ends in a tuft of young leaves, so no bare stem tip sticks out above the plant.
            for (int s = 0; s < stems; s++)
            {
                var tip = basePos + StemPoint(stemRotations[s], 1f, stemHeight);
                var below = basePos + StemPoint(stemRotations[s], 0.93f, stemHeight);
                AddTipLeaves($"Leaf_tip{s}", p, leafSets, tip, (tip - below).normalized, p.leafSize.y, 3, rng);
            }

            // Side shoots end in leaves, like real laterals (no bare sticks).
            for (int b = 0; b < p.sideBranches; b++)
            {
                float frac = p.sideBranches == 1 ? 0.45f : Mathf.Lerp(0.3f, 0.62f, b / (float)(p.sideBranches - 1));
                float yaw = b * 137.5f + 25f;
                var rot = Quaternion.Euler(0f, yaw, b % 2 == 0 ? 34f : -34f);
                var from = basePos + new Vector3(0f, frac * stemHeight, 0f);
                var scale = new Vector3(0.5f, 0.36f, 0.5f);
                PaintStem(Part($"Branch_{b}", meshes.Stem, stemMaterial, from, rot, scale, true), p);
                var tip = from + rot * Vector3.Scale(new Vector3(_lean, stemHeight, 0f), scale);
                var dir = rot * Vector3.up;
                var leafRot = Quaternion.LookRotation(new Vector3(dir.x, -0.15f, dir.z).normalized, Vector3.up);
                AddLeaf($"Branch_{b}_Leaf", p, leafSets[(b + 1) % LeafVariants], tip, leafRot, p.leafSize.y * 1.3f);
                AddTipLeaves($"Branch_{b}_Tip", p, leafSets, tip, dir, p.leafSize.y * 0.8f, 2, rng);
            }

            BuildHerbFruit(p, meshes, basePos, stemRotations, stemHeight);
            BuildHerbFlowers(p, meshes, basePos, stemRotations, stemHeight);
        }

        /// <summary>
        /// Flowers of a leafy-stemmed plant in clusters on short stalks from the upper stem: tomato trusses (yellow, nodding), potato
        /// cymes at the top (white or purple, facing out), single pepper flowers (white, nodding).
        /// </summary>
        void BuildHerbFlowers(SpeciesProfile p, CachedMeshes meshes, Vector3 basePos, Quaternion[] stemRotations, float stemHeight)
        {
            if (p.flowerCount <= 0 || fruitMaterial == null) return;
            if (meshes.Flowers == null)
            {
                var rng = new System.Random(p.seed * 17 + 9);
                var placed = new List<Matrix4x4>();
                var stalks = new ProceduralMeshes.MeshData();
                for (int k = 0; k < p.flowerCount; k++)
                {
                    int s = k % stemRotations.Length;
                    float along = Mathf.Lerp(p.flowerZone.x, p.flowerZone.y, p.flowerCount > 1 ? (k * 0.61f) % 1f : 0.8f);
                    var onStem = basePos + StemPoint(stemRotations[s], along, stemHeight);
                    float yaw = k * 137.5f + 40f;
                    var outward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                    // The cluster's own stalk (peduncle) leaves the stem between the leaves and arches out.
                    float reach = p.flowerRadius * (p.flowerCluster > 1 ? 4.5f : 2.2f) + 0.02f;
                    var end = onStem + outward * reach + Vector3.up * (reach * 0.35f);
                    FoliageMeshes.AddStalk(stalks, onStem, end, 0.0028f, 0.0022f, -reach * 0.15f, 5);
                    for (int c = 0; c < p.flowerCluster; c++)
                    {
                        float ca = c * 2.4f + k;
                        // A spray spreads the flowers round the end of the stalk; a spike stacks them in whorls up a short upright stalk.
                        var spread = p.flowerSpike
                            ? new Vector3(Mathf.Cos(ca) * p.flowerRadius * 1.3f, c * p.flowerRadius * 1.6f, Mathf.Sin(ca) * p.flowerRadius * 1.3f)
                            : new Vector3(Mathf.Cos(ca), 0.15f, Mathf.Sin(ca)) * (p.flowerRadius * 1.9f * Mathf.Sqrt(c));
                        var at = end + spread + outward * (p.flowerRadius * 0.6f);
                        if (c > 0) FoliageMeshes.AddStalk(stalks, end, at, 0.0018f, 0.0015f, 0f, 4);
                        // Facing: out from the plant, turned down by the species' nod, with a little scatter.
                        float nod = p.flowerNod + ((float)rng.NextDouble() - 0.5f) * 30f;
                        var facing = Quaternion.AngleAxis(nod, Vector3.Cross(Vector3.up, outward)) * Vector3.up;
                        facing = (facing + outward * 0.3f).normalized;
                        float size = p.flowerRadius * Mathf.Lerp(0.85f, 1.1f, (float)rng.NextDouble());
                        placed.Add(Matrix4x4.TRS(at, Quaternion.FromToRotation(Vector3.up, facing) * Quaternion.Euler(0f, (float)rng.NextDouble() * 72f, 0f), Vector3.one * size));
                    }
                }
                meshes.Flowers = CombineFlowers(p, meshes, placed);
                meshes.FlowerStalks = stalks.ToMesh($"{p.speciesId}_FlowerStalks");
            }
            DrawFlowers(p, meshes);
        }

        Mesh CombineFlowers(SpeciesProfile p, CachedMeshes meshes, List<Matrix4x4> placed)
        {
            meshes.Flower ??= FlowerMeshes.Build(p.flowerForm, p.flowerPetals, p.flowerCone);
            var parts = new List<CombineInstance>();
            foreach (var m in placed) parts.Add(new CombineInstance { mesh = meshes.Flower, transform = m });
            return Combine(parts, $"{p.speciesId}_Flowers");
        }

        void DrawFlowers(SpeciesProfile p, CachedMeshes meshes)
        {
            if (meshes.Flowers == null) return;
            meshes.FlowerColours ??= FlowerMeshes.Texture(p.flowerColor, p.flowerThroat, p.flowerCentre);
            if (meshes.FlowerStalks != null)
                Paint(Part("FlowerStalks", meshes.FlowerStalks, stemMaterial, Vector3.zero, Quaternion.identity, Vector3.one, false), p.stemColor, null);
            var flowers = Part("Flowers", meshes.Flowers, fruitMaterial, Vector3.zero, Quaternion.identity, Vector3.one, false);
            flowers.shadowCastingMode = ShadowCastingMode.Off;
            Paint(flowers, Color.white, meshes.FlowerColours);
        }

        /// <summary>A point a fraction t along a stem, relative to its base (follows the stem's lean).</summary>
        Vector3 StemPoint(Quaternion stemRotation, float t, float stemHeight) =>
            stemRotation * new Vector3(t * t * _lean, t * stemHeight, 0f);

        void BuildHerbFruit(SpeciesProfile p, CachedMeshes meshes, Vector3 basePos, Quaternion[] stemRotations, float stemHeight)
        {
            if (p.fruit == FruitForm.None || p.fruitCount <= 0 || fruitMaterial == null) return;
            meshes.Fruit ??= ProceduralMeshes.Fruit(p.fruit);
            meshes.Calyx ??= ProceduralMeshes.Calyx(p.fruit);
            bool makePedicels = meshes.Pedicels == null;
            var pedicels = makePedicels ? new ProceduralMeshes.MeshData() : null;
            _batch = new List<(Matrix4x4, int)>();
            for (int k = 0; k < p.fruitCount; k++)
            {
                float ang = k * 2.4f + 0.6f;
                float r = 0.035f + 0.02f * (k % 2) + p.fruitRadius;   // hanging just off its own stem
                float tAlong = 0.32f + 0.5f * ((k * 0.37f) % 1f);     // spread along the stem
                float rad = p.fruitRadius * p.fruitDisplayScale * (k % 3 == 2 ? 0.8f : 1f);
                float drop = p.fruit == FruitForm.Bell || p.fruit == FruitForm.Chilli ? 0.06f + rad * 1.2f : rad * 1.1f;   // peppers and chillies hang below the leaves
                var onStem = basePos + StemPoint(stemRotations[k % stemRotations.Length], tAlong, stemHeight);
                var pos = onStem + new Vector3(Mathf.Cos(ang) * r, -drop, Mathf.Sin(ang) * r);
                for (int c = 0; c < Mathf.Max(1, p.fruitCluster); c++)
                {
                    // A cluster (soybean pods, raspberries) fans out a little around the first fruit.
                    float ca = c * 2.1f + k;
                    var cpos = pos + (c == 0 ? Vector3.zero : new Vector3(Mathf.Cos(ca), -0.3f, Mathf.Sin(ca)) * (rad * 1.6f));
                    AddFruit(p, meshes, k * 12 + c, cpos, Quaternion.Euler(0f, ang * 50f + c * 40f, c == 0 ? 0f : 12f), rad);
                    if (makePedicels)
                        FoliageMeshes.AddStalk(pedicels, onStem, cpos + Vector3.up * (rad * 1.05f), 0.0045f, 0.0045f, 0.01f, 5);
                }
            }
            if (makePedicels) meshes.Pedicels = pedicels.ToMesh($"{p.speciesId}_Pedicels");
            Paint(Part("Pedicels", meshes.Pedicels, stemMaterial, Vector3.zero, Quaternion.identity, Vector3.one, true), p.stemColor, null);
            EndFruitBatch(p, meshes);
        }

        /// <summary>Young leaves round a shoot's tip, angled up and out, so the shoot ends in foliage instead of a bare stick.</summary>
        void AddTipLeaves(string name, SpeciesProfile p, FoliageMeshes.LeafSet[] sets, Vector3 tip, Vector3 dir, float size, int count, System.Random rng)
        {
            float baseYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            for (int i = 0; i < count; i++)
            {
                float yaw = baseYaw + i * 360f / count + (float)rng.NextDouble() * 30f;
                float pitch = -Mathf.Lerp(18f, 42f, (float)rng.NextDouble());   // young leaves point up
                AddLeaf($"{name}_{i}", p, sets[(i + 1) % LeafVariants], tip, Quaternion.Euler(pitch, yaw, 0f),
                    size * Mathf.Lerp(0.6f, 0.85f, (float)rng.NextDouble()));
            }
        }

        // While a batch is open, AddFruit only records where each fruit goes; EndFruitBatch then draws all of a plant's fruit of one
        // colour as one mesh (dozens of blueberries or cherries would otherwise be dozens of draw calls).
        List<(Matrix4x4 m, int k)> _batch;

        void EndFruitBatch(SpeciesProfile p, CachedMeshes meshes)
        {
            var items = _batch;
            _batch = null;
            if (items == null || items.Count == 0) return;
            int colors = Mathf.Max(1, p.fruitColors != null ? p.fruitColors.Length : 1);
            if (meshes.FruitBatches == null)
            {
                meshes.FruitBatches = new Mesh[colors];
                for (int c = 0; c < colors; c++)
                {
                    var parts = new List<CombineInstance>();
                    foreach (var (m, k) in items)
                        if (k % colors == c) parts.Add(new CombineInstance { mesh = meshes.Fruit, transform = m });
                    meshes.FruitBatches[c] = Combine(parts, $"{p.speciesId}_Fruit{c}");
                }
                var calyx = new List<CombineInstance>();
                foreach (var (m, _) in items) calyx.Add(new CombineInstance { mesh = meshes.Calyx, transform = m });
                meshes.CalyxBatch = Combine(calyx, $"{p.speciesId}_Calyx");
            }
            for (int c = 0; c < colors; c++)
            {
                if (meshes.FruitBatches[c] == null) continue;
                var fruit = Part($"Fruit_{c}", meshes.FruitBatches[c], fruitMaterial, Vector3.zero, Quaternion.identity, Vector3.one, false);
                _organs.Add(fruit);
                if (p.fruitColors != null && p.fruitColors.Length > 0) Paint(fruit, p.fruitColors[c], CropMeshes.Texture(p.fruit));
            }
            var cal = Part("Calyx_all", meshes.CalyxBatch, fruitMaterial, Vector3.zero, Quaternion.identity, Vector3.one, false);
            cal.shadowCastingMode = ShadowCastingMode.Off;
            Paint(cal, CalyxColor(p.fruit), null);
        }

        static Mesh Combine(List<CombineInstance> parts, string name)
        {
            if (parts.Count == 0) return null;
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A fruit with its calyx (a tomato's sepals, a pepper's cap, an apple's stalk).</summary>
        void AddFruit(SpeciesProfile p, CachedMeshes meshes, int k, Vector3 pos, Quaternion rot, float radius)
        {
            var scale = Vector3.one * radius;
            if (_batch != null) { _batch.Add((Matrix4x4.TRS(pos, rot, scale), k)); return; }
            var fruit = Part($"Fruit_{k}", meshes.Fruit, fruitMaterial, pos, rot, scale, false);
            fruit.shadowCastingMode = ShadowCastingMode.On;
            _organs.Add(fruit);
            if (p.fruitColors != null && p.fruitColors.Length > 0) Paint(fruit, p.fruitColors[k % p.fruitColors.Length], null);
            var calyx = Part($"Calyx_{k}", meshes.Calyx, fruitMaterial, pos, rot, scale, false);
            calyx.shadowCastingMode = ShadowCastingMode.Off;
            Paint(calyx, CalyxColor(p.fruit), null);
        }

        internal static Color CalyxColor(FruitForm form) => form switch
        {
            FruitForm.Apple or FruitForm.Cherry or FruitForm.Peach or FruitForm.GrapeBunch => new Color(0.30f, 0.24f, 0.13f),
            FruitForm.Berry => new Color(0.22f, 0.20f, 0.28f),
            FruitForm.Pod => new Color(0.45f, 0.40f, 0.22f),
            FruitForm.CornEar => new Color(0.48f, 0.62f, 0.30f),
            _ when CropMeshes.Handles(form) => CropMeshes.CalyxColor(form),
            _ => new Color(0.24f, 0.42f, 0.14f)
        };

        void PaintStem(Renderer r, SpeciesProfile p)
        {
            if (p.woodyStems) Paint(r, p.barkColor, barkTexture != null ? barkTexture : Texture2D.whiteTexture);
            else Paint(r, p.stemColor, null);
        }

        // ------------------------------------------------------------------
        // Trees
        // ------------------------------------------------------------------

        void BuildTree(SpeciesProfile p, CachedMeshes meshes, System.Random rng)
        {
            BuildGround(p, meshes, p.trunkRadius * 1.3f);

            if (meshes.Wood == null || meshes.Canopy == null)
            {
                var wood = new ProceduralMeshes.MeshData();
                var canopy = new ProceduralMeshes.MeshData();
                var tips = new List<(Vector3 pos, Vector3 dir)>();

                // One trunk (a tree) or several stems from the base (a shrub such as blueberry), each tapered, gently curved,
                // flaring where it meets the ground, with scaffold branches round its top and a central leader.
                int trunks = Mathf.Max(1, p.trunkCount);
                for (int tr = 0; tr < trunks; tr++)
                {
                    float trunkYaw = tr * 137.5f * Mathf.Deg2Rad;
                    float lean = trunks > 1 ? (p.trunkSplay + (float)rng.NextDouble() * 8f) * Mathf.Deg2Rad : 0f;
                    float hgt = p.trunkHeight * (trunks > 1 ? Mathf.Lerp(0.75f, 1.1f, (float)rng.NextDouble()) : 1f);
                    var leanDir = new Vector3(Mathf.Cos(trunkYaw), 0f, Mathf.Sin(trunkYaw));
                    var baseOffset = trunks > 1 ? leanDir * (p.trunkRadius * 1.2f) : Vector3.zero;
                    var trunkPts = new List<Vector3>();
                    var trunkRadii = new List<float>();
                    for (int i = 0; i <= 10; i++)
                    {
                        float t = i / 10f;
                        var up = new Vector3(Mathf.Sin(t * 2.2f + tr) * 0.015f, t * hgt, 0f);
                        trunkPts.Add(baseOffset + up + leanDir * (Mathf.Sin(lean) * hgt * t * t));
                        float flare = 1f + 0.7f * Mathf.Exp(-t * 16f);
                        trunkRadii.Add(Mathf.Lerp(p.trunkRadius * 1.1f, p.trunkRadius * 0.7f, t) * flare);
                    }
                    wood.AddTube(trunkPts, trunkRadii, trunks > 1 ? 8 : 14, 5f);
                    var top = trunkPts[trunkPts.Count - 1];

                    float l1 = p.canopyRadius * (trunks > 1 ? 0.55f : 0.95f);
                    int scaffolds = trunks > 1 ? Mathf.Max(2, p.branchesPerLevel - 1) : p.branchesPerLevel;
                    for (int b = 0; b < scaffolds; b++)
                    {
                        float yaw = (b * 137.5f + 10f + tr * 47f) * Mathf.Deg2Rad;
                        float elevation = Mathf.Lerp(38f, 55f, (float)rng.NextDouble()) * Mathf.Deg2Rad;
                        var dir = new Vector3(Mathf.Sin(elevation) * Mathf.Cos(yaw), Mathf.Cos(elevation), Mathf.Sin(elevation) * Mathf.Sin(yaw));
                        if (trunks > 1) dir = (dir + leanDir * 0.6f).normalized;   // a shrub's stems open outward
                        var origin = Vector3.Lerp(trunkPts[trunks > 1 ? 5 : 7], top, b / (float)scaffolds);
                        Branch(wood, tips, origin, dir, l1, p.trunkRadius * 0.6f, 1, p, rng);
                    }
                    Branch(wood, tips, top, (Vector3.up + leanDir * Mathf.Sin(lean)).normalized, l1 * 0.8f, p.trunkRadius * 0.55f, 1, p, rng);
                }

                // Leaf clusters at every branch tip, all in ONE mesh (one draw call).
                bool photos = FoliageMeshes.HasPhotos(p);
                foreach (var (pos, dir) in tips)
                    for (int k = 0; k < p.leavesPerTip; k++)
                    {
                        // A fuller crown spreads its (display-enlarged) leaves a little wider round each twig.
                        float spread = Mathf.Lerp(0.11f, 0.17f, Mathf.Clamp01(p.canopyLeafScale - 1f));
                        var jitter = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.4f, (float)rng.NextDouble() - 0.5f) * spread;
                        var outward = (dir + jitter * 6f).normalized;
                        var rot = Quaternion.LookRotation(new Vector3(outward.x, outward.y * 0.4f - 0.15f, outward.z).normalized, Vector3.up)
                                  * Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 60f - 30f);
                        // canopyLeafScale: a declared exception to the real proportion, so fruit trees look lush (see SpeciesProfile).
                        float size = Mathf.Lerp(p.leafSize.x, p.leafSize.y, (float)rng.NextDouble()) * p.canopyLeafScale;
                        var m = Matrix4x4.TRS(pos + jitter, rot, Vector3.one * size);
                        if (photos) FoliageMeshes.AddCanopyLeaf(canopy, p, m, rng);
                        else ProceduralMeshes.AddSimpleBlade(canopy, m, p, rng);
                    }

                meshes.Wood = wood.ToMesh($"{p.speciesId}_Wood");
                meshes.Canopy = canopy.ToMesh($"{p.speciesId}_Canopy");
                _treeTips[p] = tips;
            }

            // Bark is brown and has its own texture, not the green stem one.
            var bark = Part("Wood", meshes.Wood, stemMaterial, Vector3.zero, Quaternion.identity, Vector3.one, true);
            Paint(bark, p.barkColor, barkTexture != null ? barkTexture : Texture2D.whiteTexture);
            Paint(Part("Canopy", meshes.Canopy, leafMaterial, Vector3.zero, Quaternion.identity, Vector3.one, true), p.leafTint, p.leafAtlas);

            // Fruit hangs just below a few tips, on a short stalk.
            if (p.fruit != FruitForm.None && p.fruitCount > 0 && fruitMaterial != null && _treeTips.TryGetValue(p, out var tipList) && tipList.Count > 0)
            {
                meshes.Fruit ??= ProceduralMeshes.Fruit(p.fruit);
                meshes.Calyx ??= ProceduralMeshes.Calyx(p.fruit);
                float hang = p.fruit == FruitForm.Cherry ? 3.4f : p.fruit == FruitForm.Berry ? 1.3f : 1.8f;   // cherries dangle on long stalks
                float fr = p.fruitRadius * p.fruitDisplayScale;
                float leaf = p.leafSize.x * p.canopyLeafScale;
                _batch = new List<(Matrix4x4, int)>();
                for (int k = 0; k < p.fruitCount; k++)
                {
                    // Spread over the crown, and pushed out to its surface so the fruit shows among the leaves.
                    var (pos, dir) = tipList[(k * 7 + 3) % tipList.Count];
                    var outward = new Vector3(dir.x, 0f, dir.z).normalized;
                    var at = pos + outward * (leaf * 0.7f) + Vector3.down * (fr * hang + leaf * 0.2f * (k % 3));
                    for (int c = 0; c < Mathf.Max(1, p.fruitCluster); c++)
                    {
                        float ca = c * 2.4f + k;
                        var offset = c == 0 ? Vector3.zero : new Vector3(Mathf.Cos(ca), -0.4f * c / p.fruitCluster, Mathf.Sin(ca)) * (fr * 2.1f);
                        AddFruit(p, meshes, k * 12 + c, at + offset, Quaternion.Euler(c * 9f, k * 70f + c * 50f, 0f), fr * (c % 3 == 2 ? 0.85f : 1f));
                    }
                }
                EndFruitBatch(p, meshes);
            }
        }

        void Branch(ProceduralMeshes.MeshData wood, List<(Vector3, Vector3)> tips, Vector3 origin, Vector3 dir,
            float length, float radius, int level, SpeciesProfile p, System.Random rng)
        {
            var pts = new List<Vector3>();
            var radii = new List<float>();
            for (int i = 0; i <= 6; i++)
            {
                float t = i / 6f;
                pts.Add(origin + dir * (length * t) + Vector3.up * (0.12f * length * t * t));
                radii.Add(Mathf.Lerp(radius, radius * 0.4f, t));
            }
            wood.AddTube(pts, radii, 8, 5f);
            var end = pts[pts.Count - 1];
            var endDir = (end - pts[pts.Count - 2]).normalized;

            if (level >= p.branchLevels)
            {
                tips.Add((end, endDir));
                tips.Add((Vector3.Lerp(pts[3], end, 0.5f), endDir));   // a second cluster mid-way
                return;
            }

            int children = Mathf.Max(2, p.branchesPerLevel - 1);
            for (int c = 0; c < children; c++)
            {
                float at = Mathf.Lerp(0.5f, 1f, c / (float)children);
                var from = Vector3.Lerp(origin, end, at);
                var axis = Vector3.Cross(endDir, Quaternion.AngleAxis(c * 120f + (float)rng.NextDouble() * 40f, endDir) * Vector3.right).normalized;
                if (axis.sqrMagnitude < 0.01f) axis = Vector3.forward;
                var childDir = Quaternion.AngleAxis(Mathf.Lerp(28f, 45f, (float)rng.NextDouble()), axis) * endDir;
                Branch(wood, tips, from, childDir.normalized, length * 0.6f, radius * 0.6f, level + 1, p, rng);
            }
        }

        // ------------------------------------------------------------------
        // Rosettes: strawberry, squash - leaves on long stalks straight from a crown at the soil
        // ------------------------------------------------------------------

        void BuildRosette(SpeciesProfile p, CachedMeshes meshes, System.Random rng)
        {
            float crownRadius = p.stemRadius * 1.4f;
            float soilTop = BuildGround(p, meshes, crownRadius);
            var crown = new Vector3(0f, soilTop - 0.006f, 0f);
            var leafSets = LeafSets(p, meshes);

            // The stem the leaves come out of: a short stout crown (strawberry), or for a bush squash an upright, thick stem with the
            // leaves spiralling up it (crownHeight).
            float stemHeight = p.crownHeight > 0.03f ? p.crownHeight : Mathf.Max(0.02f, p.height * 0.08f);
            meshes.Stem ??= ProceduralMeshes.Stem(stemHeight, p.stemRadius, p.stemRadius * 0.7f, 0.004f, Mathf.Max(3, p.leafCount / 2));
            PaintStem(Part("Crown", meshes.Stem, stemMaterial, crown, Quaternion.identity, Vector3.one, true), p);

            // Older leaves sit lower and lean out more; young leaves stand up at the top. On a tall stem the lean stays moderate so the
            // leaves form an open vase on their long stalks instead of a dome over the ground.
            bool tall = p.crownHeight > 0.03f;
            int n = Mathf.Max(3, p.leafCount);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);                       // 0 = oldest
                float yaw = i * 137.5f + 10f;
                float size = Mathf.Lerp(p.leafSize.x, p.leafSize.y, t) * (0.92f + (float)rng.NextDouble() * 0.16f);
                // A strawberry's stalks stand up from the crown and hold the leaflets level; only the oldest lean out a little.
                // Squash's young leaves at the top of the stem are "ascending to erect" (Prigge & Gibson), which gives the plant its height.
                float tilt = (tall ? Mathf.Lerp(10f, -30f, t) : Mathf.Lerp(10f, -14f, t)) + ((float)rng.NextDouble() - 0.5f) * 8f;
                var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                var pos = crown + dir * (crownRadius * 0.6f) + Vector3.up * (tall ? Mathf.Lerp(0.12f, 1f, t) * stemHeight : p.height * 0.06f * t);
                var rot = Quaternion.Euler(tilt, yaw, ((float)rng.NextDouble() - 0.5f) * 10f);
                AddLeaf($"Leaf_0_{i:00}", p, leafSets[i % LeafVariants], pos, rot, size);
            }
            if (IsCentral(p.fruit)) BuildCentralFruit(p, meshes, crown, tall ? stemHeight : 0f);
            else BuildGroundFruit(p, meshes, crown, stemHeight, rng);
            BuildFlowers(p, meshes, crown + Vector3.up * (tall ? stemHeight * 0.8f : 0f), rng);
        }

        /// <summary>
        /// Squash flowers: big yellow-orange trumpets on long stalks from the leaf joints, opening among the leaf stalks in the middle
        /// of the plant at about the height of the blades (staminate flowers stand on 15-33 cm stalks; the flowers are 12-18 cm across).
        /// </summary>
        void BuildFlowers(SpeciesProfile p, CachedMeshes meshes, Vector3 crown, System.Random rng)
        {
            if (p.flowerCount <= 0 || fruitMaterial == null) return;
            if (meshes.Flowers == null)
            {
                bool tall = p.crownHeight > 0.03f;
                var placed = new List<Matrix4x4>();
                var stalks = new ProceduralMeshes.MeshData();
                for (int k = 0; k < p.flowerCount; k++)
                {
                    float ang = k * 2.4f + 0.3f;
                    var outward = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    // Squash: big trumpets among the leaf stalks at about the height of the blades. Strawberry: small open flowers on
                    // slender stalks that rise among the leaves and arch out a little.
                    float reach = tall ? p.flowerRadius * Mathf.Lerp(0.6f, 1.6f, (float)rng.NextDouble()) : p.leafSize.x * Mathf.Lerp(0.15f, 0.4f, (float)rng.NextDouble());
                    float up = tall ? p.height * Mathf.Lerp(0.12f, 0.24f, (float)rng.NextDouble()) : p.height * Mathf.Lerp(0.45f, 0.75f, (float)rng.NextDouble());
                    var head = crown + outward * reach + Vector3.up * up;
                    var facing = Quaternion.AngleAxis(p.flowerNod * Mathf.Lerp(0.6f, 1.2f, (float)rng.NextDouble()), Vector3.Cross(Vector3.up, outward)) * Vector3.up;
                    placed.Add(Matrix4x4.TRS(head, Quaternion.FromToRotation(Vector3.up, facing) * Quaternion.Euler(0f, k * 47f, 0f), Vector3.one * p.flowerRadius));
                    float r = tall ? 0.0045f : 0.0015f;
                    stalks.AddTube(new[] { crown + Vector3.up * 0.01f, crown + outward * (reach * 0.4f) + Vector3.up * (up * 0.55f), head - facing * (p.flowerRadius * 0.05f) },
                                   new[] { r, r * 0.9f, r * 0.75f }, 5);
                }
                meshes.Flowers = CombineFlowers(p, meshes, placed);
                meshes.FlowerStalks = stalks.ToMesh($"{p.speciesId}_FlowerStalks");
            }
            DrawFlowers(p, meshes);
        }

        /// <summary>Fruit at the foot of a rosette: strawberries hanging over the soil on their stalks, squash lying on it.</summary>
        void BuildGroundFruit(SpeciesProfile p, CachedMeshes meshes, Vector3 crown, float stemHeight, System.Random rng)
        {
            if (p.fruit == FruitForm.None || p.fruitCount <= 0 || fruitMaterial == null) return;
            meshes.Fruit ??= ProceduralMeshes.Fruit(p.fruit);
            meshes.Calyx ??= ProceduralMeshes.Calyx(p.fruit);
            bool makePedicels = meshes.Pedicels == null;
            var pedicels = makePedicels ? new ProceduralMeshes.MeshData() : null;
            for (int k = 0; k < p.fruitCount; k++)
            {
                float ang = k * 2.4f + 0.9f;
                var outward = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                float rad = p.fruitRadius * (k % 3 == 2 ? 0.85f : 1f);
                if (p.fruit == FruitForm.Squash)
                {
                    // Squash flowers, and so the fruit, are axillary: each zucchini grows from a leaf joint on the stem and points up and
                    // out from it, the lowest resting on the soil. (The fruit mesh's stem end is at -X, the fruit along +X.)
                    float node = Mathf.Lerp(0.18f, 0.65f, p.fruitCount > 1 ? k / (float)(p.fruitCount - 1) : 0f) * stemHeight;
                    float lift = Mathf.Lerp(18f, 42f, p.fruitCount > 1 ? k / (float)(p.fruitCount - 1) : 0f) * Mathf.Deg2Rad;
                    var along = (outward * Mathf.Cos(lift) + Vector3.up * Mathf.Sin(lift)).normalized;
                    var pos = crown + Vector3.up * node + outward * (p.stemRadius * 1.1f) + along * (rad * 3.3f);
                    pos.y = Mathf.Max(pos.y, rad * 0.98f);
                    AddFruit(p, meshes, k, pos, Quaternion.FromToRotation(Vector3.right, along), rad);
                }
                else
                {
                    // A strawberry hangs from its stalk with the tip down and resting near the soil.
                    float reach = p.leafSize.x * Mathf.Lerp(0.45f, 0.75f, (float)rng.NextDouble());
                    var pos = crown + outward * reach;
                    pos.y = rad * 1.45f + 0.004f;
                    var rot = Quaternion.AngleAxis(25f, Vector3.Cross(Vector3.up, outward)) * Quaternion.Euler(0f, ang * 57f, 0f);
                    AddFruit(p, meshes, k, pos, rot, rad);
                    if (makePedicels)
                    {
                        var top = pos + rot * Vector3.up * (rad * 1.3f);
                        var arch = crown + outward * (reach * 0.45f) + Vector3.up * (p.height * 0.16f);
                        FoliageMeshes.AddStalk(pedicels, crown + Vector3.up * 0.01f, arch, 0.0022f, 0.002f, 0f, 5);
                        FoliageMeshes.AddStalk(pedicels, arch, top, 0.002f, 0.0018f, -0.004f, 5);
                    }
                }
            }
            if (makePedicels && !pedicels.IsEmpty) meshes.Pedicels = pedicels.ToMesh($"{p.speciesId}_Pedicels");
            if (meshes.Pedicels != null)
                Paint(Part("Pedicels", meshes.Pedicels, stemMaterial, Vector3.zero, Quaternion.identity, Vector3.one, true), p.stemColor, null);
        }

        // ------------------------------------------------------------------
        // Grass: corn - one jointed stalk, strap leaves in two rows, a tassel and an ear
        // ------------------------------------------------------------------

        // ------------------------------------------------------------------
        // Vine: grape - a woody trunk, two arms (cordons), green shoots with leaves, tendrils and bunches
        // ------------------------------------------------------------------

        void BuildVine(SpeciesProfile p, CachedMeshes meshes, System.Random rng)
        {
            BuildGround(p, meshes, p.trunkRadius * 1.3f);
            var leafSets = LeafSets(p, meshes);
            var shoots = new List<(Vector3 from, Vector3 dir, float len)>();
            if (meshes.Wood == null || meshes.Extra == null)
            {
                var wood = new ProceduralMeshes.MeshData();
                var green = new ProceduralMeshes.MeshData();
                // Trunk, a little twisted, to the height of the arms.
                var trunk = new List<Vector3>();
                var radii = new List<float>();
                for (int i = 0; i <= 8; i++)
                {
                    float t = i / 8f;
                    trunk.Add(new Vector3(Mathf.Sin(t * 3.1f) * 0.02f, t * p.trunkHeight, Mathf.Cos(t * 2.3f) * 0.012f - 0.012f));
                    radii.Add(p.trunkRadius * Mathf.Lerp(1.15f, 0.8f, t) * (1f + 0.5f * Mathf.Exp(-t * 14f)));
                }
                wood.AddTube(trunk, radii, 10, 5f);
                var head = trunk[trunk.Count - 1];
                // Two cordons along the row, sagging a little between spurs.
                for (int side = -1; side <= 1; side += 2)
                {
                    var arm = new List<Vector3>();
                    var armR = new List<float>();
                    for (int i = 0; i <= 10; i++)
                    {
                        float t = i / 10f;
                        arm.Add(head + new Vector3(side * p.canopyRadius * t, -0.02f * Mathf.Sin(t * Mathf.PI) + 0.03f * t * t - 0.01f * (1 - t) * (1 - t), 0.01f * Mathf.Sin(t * 5f)));
                        armR.Add(p.trunkRadius * Mathf.Lerp(0.75f, 0.35f, t));
                    }
                    wood.AddTube(arm, armR, 8, 5f);
                    // Shoots from spurs along the arm, rising and leaning out.
                    int spurs = 4;
                    for (int s2 = 0; s2 < spurs; s2++)
                    {
                        float t = (s2 + 0.6f) / spurs;
                        var from = Vector3.Lerp(arm[0], arm[arm.Count - 1], t) + Vector3.up * 0.01f;
                        var dir = new Vector3(side * 0.35f + ((float)rng.NextDouble() - 0.5f) * 0.4f, 1f, ((float)rng.NextDouble() - 0.5f) * 0.9f).normalized;
                        float len = (p.height - p.trunkHeight) * Mathf.Lerp(0.8f, 1.05f, (float)rng.NextDouble());
                        shoots.Add((from, dir, len));
                        var pts = new List<Vector3>();
                        var rr = new List<float>();
                        for (int i = 0; i <= 8; i++)
                        {
                            float u = i / 8f;
                            pts.Add(from + dir * (len * u) + new Vector3(0f, 0f, 0.03f * Mathf.Sin(u * 4f)));
                            rr.Add(Mathf.Lerp(p.stemRadius, p.stemRadius * 0.4f, u));
                        }
                        green.AddTube(pts, rr, 6);
                        // Tendrils: forked, curling threads opposite the leaves.
                        for (int k = 1; k < 4; k++)
                        {
                            var at = from + dir * (len * (0.3f + k * 0.18f));
                            float a0 = (float)rng.NextDouble() * 6.28f;
                            for (int fork = 0; fork < 2; fork++)
                            {
                                var curl = new List<Vector3>();
                                var cr = new List<float>();
                                for (int i = 0; i <= 10; i++)
                                {
                                    float u = i / 10f;
                                    float ang = a0 + fork * 1.2f + u * 9f;
                                    float rad = Mathf.Lerp(0.03f, 0.008f, u);
                                    curl.Add(at + new Vector3(Mathf.Cos(a0 + fork) * 0.04f * u, -0.01f * u, Mathf.Sin(a0 + fork) * 0.04f * u)
                                             + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang) * 0.6f, Mathf.Sin(ang)) * rad * u);
                                    cr.Add(Mathf.Lerp(0.0016f, 0.0006f, u));
                                }
                                green.AddTube(curl, cr, 3);
                            }
                        }
                    }
                }
                meshes.Wood = wood.ToMesh($"{p.speciesId}_Wood");
                meshes.Extra = green.ToMesh($"{p.speciesId}_Shoots");
                _vineShoots[p] = shoots;
            }
            else shoots = _vineShoots[p];

            Paint(Part("Wood", meshes.Wood, stemMaterial, Vector3.zero, Quaternion.identity, Vector3.one, true), p.barkColor,
                barkTexture != null ? barkTexture : Texture2D.whiteTexture);
            Paint(Part("Shoots", meshes.Extra, stemMaterial, Vector3.zero, Quaternion.identity, Vector3.one, true), p.stemColor, null);

            // Leaves alternate along each shoot, the largest low down.
            int perShoot = Mathf.Max(2, p.leafCount / Mathf.Max(1, shoots.Count));
            int li = 0;
            foreach (var (from, dir, len) in shoots)
                for (int i = 0; i < perShoot; i++, li++)
                {
                    float t = (i + 0.5f) / perShoot;
                    float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + (i % 2 == 0 ? 90f : -90f) + ((float)rng.NextDouble() - 0.5f) * 30f;
                    var pos = from + dir * (len * t);
                    float size = Mathf.Lerp(p.leafSize.x, p.leafSize.y, t) * (0.9f + (float)rng.NextDouble() * 0.2f);
                    // Vine leaves hang on their stalks, blades facing out from the row.
                    float hang = 50f + ((float)rng.NextDouble() - 0.5f) * 30f;
                    AddLeaf($"Leaf_0_{li:00}", p, leafSets[li % LeafVariants], pos, Quaternion.Euler(hang, yaw, 0f), size);
                }

            // Shoots end in young leaves, not bare tips.
            int ti = 0;
            foreach (var (from, dir, len) in shoots)
                AddTipLeaves($"Leaf_tip{ti++}", p, leafSets, from + dir * len, dir, p.leafSize.y * 0.8f, 2, rng);

            // Bunches hang below the arms from the base of the shoots.
            if (p.fruit != FruitForm.None && p.fruitCount > 0 && fruitMaterial != null)
            {
                meshes.Fruit ??= ProceduralMeshes.Fruit(p.fruit);
                meshes.Calyx ??= ProceduralMeshes.Calyx(p.fruit);
                for (int k = 0; k < p.fruitCount && shoots.Count > 0; k++)
                {
                    var (from, dir, len) = shoots[(k * 3 + 1) % shoots.Count];
                    var pos = from + dir * (len * 0.12f) + Vector3.down * (p.fruitRadius * 0.6f) + new Vector3(0f, 0f, (k % 2 == 0 ? 1 : -1) * 0.03f);
                    AddFruit(p, meshes, k, pos, Quaternion.Euler(0f, k * 70f, (k % 2 == 0 ? 1 : -1) * 6f), p.fruitRadius);
                }
            }
        }

        readonly Dictionary<SpeciesProfile, List<(Vector3, Vector3, float)>> _vineShoots = new Dictionary<SpeciesProfile, List<(Vector3, Vector3, float)>>();

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>Up to three points on the plant that show the disease; empty transforms under the plant.</summary>
        public IReadOnlyList<Transform> Markers => _markers;
        readonly List<Transform> _markers = new List<Transform>();

        void AddMarkers(SpeciesProfile p)
        {
            _markers.Clear();
            var spots = new List<Vector3>();
            if (p.archetype == TwinArchetype.Tree)
            {
                if (_treeTips.TryGetValue(p, out var tips))
                    foreach (int i in new[] { tips.Count / 2, tips.Count / 5, tips.Count * 4 / 5 })
                        if (i >= 0 && i < tips.Count) spots.Add(tips[i].Item1);
            }
            else
            {
                var leaves = new List<Renderer>();
                foreach (var r in _disease) if (r != null && r.name.StartsWith("Leaf_") && !r.name.EndsWith("_Stalk")) leaves.Add(r);
                foreach (float f in new[] { 0.3f, 0.2f, 0.7f })
                    if (leaves.Count > 0)
                    {
                        var leaf = leaves[Mathf.Clamp(Mathf.RoundToInt(f * (leaves.Count - 1)), 0, leaves.Count - 1)].transform;
                        spots.Add(leaf.localPosition + leaf.localRotation * (Vector3.forward * 0.45f * leaf.localScale.z * CompoundSpan));
                    }
            }
            for (int i = 0; i < spots.Count; i++)
            {
                var m = new GameObject("Marker_" + i).transform;
                m.SetParent(_generated, false);
                m.localPosition = spots[i];
                _markers.Add(m);
            }
        }

        void ApplyLayer()
        {
            if (string.IsNullOrEmpty(layerName)) return;
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0) return;
            foreach (var t in _generated.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        void ApplyFootprint(SpeciesProfile p)
        {
            if (footprintPieces == null || _footprintBase == null) return;
            float k = p.FootprintRadius / 0.3f;
            for (int i = 0; i < footprintPieces.Length; i++)
                if (footprintPieces[i] != null) footprintPieces[i].localScale = _footprintBase[i] * k;
        }

        MeshRenderer Part(string name, Mesh mesh, Material material, Vector3 pos, Quaternion rot, Vector3 scale, bool diseased)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_generated, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On;
            if (diseased) _disease.Add(r);
            return r;
        }

        /// <summary>Per-renderer colour and (optionally) base texture, without making a material copy.</summary>
        void Paint(Renderer r, Color c, Texture texture)
        {
            if (r == null) return;
            r.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, c);
            if (texture != null) _block.SetTexture(BaseMapId, texture);
            r.SetPropertyBlock(_block);
        }

        static void DestroyObject(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
