using UnityEngine;

namespace PlantDoctor.Plants
{
    /// <summary>The body plan the twin generator builds.</summary>
    public enum TwinArchetype
    {
        /// <summary>Plants with leafy stems: tomato, potato, pepper, soybean; with woody stems, raspberry canes.</summary>
        Herb,
        /// <summary>Woody plant: trunk(s), branches, a leafy canopy (apple, cherry, peach, orange; blueberry with several stems).</summary>
        Tree,
        /// <summary>Leaves on long stalks straight from a crown at the soil: strawberry, squash.</summary>
        Rosette,
        /// <summary>A grass: one jointed stalk with long strap leaves in two rows (corn).</summary>
        Grass,
        /// <summary>A trained vine: woody trunk, two arms (cordons), green shoots with tendrils (grape).</summary>
        Vine,
        /// <summary>A giant herb: a pseudostem of leaf sheaths, huge arching leaves from its top, a hanging bunch (banana).</summary>
        Banana,
        /// <summary>An upright stem with pairs of horizontal side branches in tiers, leaves and fruit along them (coffee).</summary>
        Shrub,
        /// <summary>A stem climbing a stake by its tendrils, leaves on long stalks, fruit hanging from the nodes (cucumber).</summary>
        Climber
    }

    public enum LeafForm
    {
        /// <summary>Leaflets along a stalk (tomato, potato, raspberry).</summary>
        Compound,
        /// <summary>One blade per leaf on a stalk (pepper, grape, squash, apple).</summary>
        Simple,
        /// <summary>Three leaflets at the tip of the stalk (strawberry, soybean).</summary>
        Trifoliate,
        /// <summary>A long strap that grows from a sheath round the stalk (corn).</summary>
        Strap
    }

    public enum FruitForm
    {
        None, Round, Bell, Apple, Berry, Cherry, Peach, Citrus, GrapeBunch, Strawberry, Raspberry, Squash, CornEar, Pod,
        // PlantSeg plants (CropMeshes); keep this order, the assets store the numbers.
        BeanPod, Cucumber, Eggplant, BananaBunch, BroccoliHead, CauliflowerCurd, CabbageHead, CarrotRoot, Bulb, Rhizome, Panicle, Spike,
        // added 11 Nov 2026
        Mango, Chilli,
        // added 19 Nov 2026
        CottonBoll
    }

    /// <summary>
    /// Everything that makes the twin look like one species rather than another.
    ///
    /// Instead of a hand-made 3D model per species, a handful of body plans read these numbers.
    /// Adding a species is a data entry, not modelling work, and every disease look (spots, rings,
    /// yellowing, scorch, coatings) is projected onto whichever species is built. Sizes follow the
    /// published proportions in docs/PLANT_MORPHOLOGY_SOURCES.md.
    /// </summary>
    [CreateAssetMenu(menuName = "PlantDoctor/Species Profile", fileName = "Species_")]
    public class SpeciesProfile : ScriptableObject
    {
        [Header("Identity")]
        public string speciesId = "tomato";
        public string displayName = "Tomato";
        public TwinArchetype archetype = TwinArchetype.Herb;
        [Tooltip("Seed for the generator, so a species always looks the same.")]
        public int seed = 11;

        [Header("Overall")]
        [Tooltip("Twin height in metres, from the ground to the top of the plant. The viewer camera frames to this.")]
        [Min(0.1f)] public float height = 1f;
        [Tooltip("Obsolete: plants now stand on their own roots and a small heap of soil instead of in a pot.")]
        public bool inPot = false;
        public Color potColor = new Color(0.72f, 0.38f, 0.24f);

        [Header("Roots and ground (replace the pot)")]
        [Range(3, 28)] public int rootCount = 12;
        [Tooltip("How far the main roots reach along the ground, in metres.")]
        public float rootSpread = 0.2f;
        [Tooltip("Radius of a main root at the crown, in metres.")]
        public float rootRadius = 0.007f;
        public Color rootTint = Color.white;
        public float moundRadius = 0.085f;
        public float moundHeight = 0.034f;
        [Tooltip("Fine root hairs on the thin roots. Blueberry roots have none (they rely on fungi instead).")]
        public bool rootHairs = true;
        [Tooltip("Nitrogen-fixing nodules on the roots (legumes such as soybean).")]
        [Range(0, 120)] public int rootNodules = 0;
        [Tooltip("Brace (prop) roots from the lowest stalk nodes into the ground, as on corn.")]
        [Range(0, 24)] public int braceRoots = 0;

        [Header("Stems (herb, grass)")]
        [Range(1, 12)] public int mainStems = 1;
        [Tooltip("Lean of each main stem away from vertical, degrees.")]
        [Range(0f, 45f)] public float stemSplay = 0f;
        [Range(0, 6)] public int sideBranches = 2;
        [Tooltip("Where the lowest leaf grows, as a fraction of the stem (a bare lower stem shows the roots).")]
        [Range(0.02f, 0.5f)] public float firstLeafHeight = 0.12f;
        public float stemRadius = 0.017f;
        public Color stemColor = new Color(0.78f, 0.86f, 0.62f);
        [Tooltip("How far a stem bends over by its tip, in metres (raspberry canes arch).")]
        [Range(0f, 0.6f)] public float stemLean = 0.03f;
        [Tooltip("Rosettes: height of the upright stem the leaves grow along (a bush squash); 0 = a short crown at the soil (strawberry).")]
        [Range(0f, 0.5f)] public float crownHeight = 0f;
        [Tooltip("Stems are wood or cane: bark texture and barkColor (raspberry canes).")]
        public bool woodyStems = false;
        [Tooltip("Leaves in opposite pairs, each pair at right angles to the last (basil, coffee).")]
        public bool oppositeLeaves = false;
        [Tooltip("Grass: share of the height that is stalk (corn 0.86, the tassel on top); the rest is leaves or the head above it.")]
        [Range(0.1f, 1f)] public float stalkShare = 0.86f;
        [Tooltip("Grass: a male tassel on top of the stalk (corn).")]
        public bool grassTassel = false;

        [Header("Photographic leaves (atlas built by tools/assets/make_plant_textures.py from PlantVillage photos)")]
        public Texture2D leafAtlas;
        [Tooltip("Where each leaf photo sits in the atlas, in UV space.")]
        public Rect[] leafRects;
        [Tooltip("Width / height of each leaf photo.")]
        public float[] leafAspects;
        [Range(0f, 0.2f)] public float leafFold = 0.07f;

        [Header("Leaves")]
        public LeafForm leafForm = LeafForm.Compound;
        [Range(3, 80)] public int leafCount = 13;
        [Tooltip("Leaf length in metres at the bottom (x) and the top (y) of the plant, stalk included.")]
        public Vector2 leafSize = new Vector2(0.42f, 0.22f);
        [Range(1, 5)] public int leafletPairs = 3;
        [Tooltip("Compound leaves: tiny leaflets in each gap between the pairs (tomato, potato have 1-2).")]
        [Range(0, 3)] public int interjectedLeaflets = 0;
        [Tooltip("Trifoliate leaves: stalk between the side pair and the middle leaflet, relative to the leaf (0 = all three from one point, " +
                 "as strawberry; about 0.1-0.15 on soybean and bean).")]
        [Range(0f, 0.3f)] public float trifoliateRachis = 0f;
        [Tooltip("Which leaf photo of the atlas is the zoomed leaf (and the terminal leaflet of a compound leaf).")]
        [Min(0)] public int heroLeaf = 0;
        [Tooltip("Length of the leaf stalk (petiole) relative to the blade (simple and trifoliate leaves).")]
        [Range(0.05f, 2.5f)] public float petioleLength = 0.2f;
        [Tooltip("Rosettes: how steeply the leaf stalks rise from the stem (1 = nearly upright, as strawberry; lower = angled outward, as squash).")]
        [Range(0f, 1f)] public float petioleRise = 1f;
        [Tooltip("How far the stalk reaches into the blade photo, as a share of the blade (heart-shaped leaves join their stalk inside the notch).")]
        [Range(0f, 0.4f)] public float petioleInset = 0f;
        [Tooltip("Degrees a stalked blade rises above level at the end of its stalk (carrot fronds stand up; 0 = held level).")]
        [Range(-20f, 80f)] public float bladeLift = 0f;
        [Tooltip("Strap leaves: extra angle at which the blade leaves the stalk, degrees (ginger's leaves spread out; 0 = grass-like).")]
        [Range(0f, 70f)] public float strapSpread = 0f;
        [Tooltip("Strap leaves: how far the blade bows over toward its tip, relative to a grass leaf (1).")]
        [Range(0.3f, 1.3f)] public float strapArch = 1f;
        [Tooltip("Blade width relative to its length.")]
        [Range(0.05f, 1.2f)] public float leafWidth = 0.5f;
        [Range(0f, 0.2f)] public float serration = 0.07f;
        [Range(0, 16)] public int teeth = 8;
        [Range(0f, 0.6f)] public float leafDroop = 0.1f;
        [Tooltip("Multiplies the leaf texture: species leaf colour.")]
        public Color leafTint = Color.white;

        [Header("Flowers")]
        [Tooltip("Open flowers on the plant (squash's big yellow trumpets).")]
        [Range(0, 12)] public int flowerCount = 0;
        [Tooltip("Radius of an open flower, metres.")]
        public float flowerRadius = 0.07f;
        public Color flowerColor = new Color(0.98f, 0.70f, 0.12f);
        public FlowerForm flowerForm = FlowerForm.Trumpet;
        [Range(3, 8)] public int flowerPetals = 5;
        [Tooltip("Colour deep in the flower (the throat), shading out to flowerColor at the petal tips.")]
        public Color flowerThroat = new Color(0.95f, 0.55f, 0.08f);
        [Tooltip("The flower's centre: the anther cone (tomato family) or the stamens and receptacle.")]
        public Color flowerCentre = new Color(0.95f, 0.78f, 0.16f);
        [Tooltip("Length of the anther cone in flower radii (tomato about 0.9, potato 0.7; 0 = none).")]
        [Range(0f, 1.5f)] public float flowerCone = 0f;
        [Tooltip("Flowers in one cluster on a shared stalk (a tomato truss has 4-8, a potato cyme 5-15).")]
        [Range(1, 12)] public int flowerCluster = 1;
        [Tooltip("How far the flowers turn down from facing the sky, degrees (0 up, 90 sideways, 150 nodding down as tomato and pepper).")]
        [Range(0f, 180f)] public float flowerNod = 30f;
        [Tooltip("Where along the stems the flower clusters grow (fraction of the stem; tobacco and basil flower at the very top).")]
        public Vector2 flowerZone = new Vector2(0.62f, 0.95f);
        [Tooltip("The cluster is a spike: whorls of small flowers up a stalk (basil) instead of a spray (tomato).")]
        public bool flowerSpike = false;

        [Header("Fruit")]
        public FruitForm fruit = FruitForm.Round;
        public Color[] fruitColors = { new Color(0.86f, 0.17f, 0.10f), new Color(0.42f, 0.62f, 0.22f) };
        [Range(0, 24)] public int fruitCount = 3;
        [Tooltip("EXCEPTION for visibility: fruit drawn this many times its real size (blueberry, cherry, orange, peach, raspberry; user" +
                 " request, 2 Nov 2026). On the fruit trees it equals canopyLeafScale, so fruit and leaves keep their real proportion to each other.")]
        [Range(1f, 2.5f)] public float fruitDisplayScale = 1f;
        [Tooltip("Fruits per cluster (blueberries, cherries, soybean pods).")]
        [Range(1, 12)] public int fruitCluster = 1;
        public float fruitRadius = 0.05f;

        [Header("Tree, vine")]
        public float trunkHeight = 0.55f;
        public float trunkRadius = 0.04f;
        [Tooltip("Stems rising from the ground (1 = a tree; several = a shrub such as blueberry).")]
        [Range(1, 12)] public int trunkCount = 1;
        [Range(0f, 40f)] public float trunkSplay = 12f;
        [Range(1, 3)] public int branchLevels = 2;
        [Range(2, 6)] public int branchesPerLevel = 4;
        public float canopyRadius = 0.45f;
        [Range(2, 120)] public int leavesPerTip = 8;
        [Tooltip("EXCEPTION to the published proportions, for looks only: canopy leaves are drawn this many times their real size so a " +
                 "fruit tree reads as a full, leafy crown (user request, 1 Nov 2026). leafSize itself stays the real proportion.")]
        [Range(1f, 2.5f)] public float canopyLeafScale = 1f;
        public Color barkColor = new Color(0.36f, 0.27f, 0.20f);

        /// <summary>Radius of the twin's footprint on the ground (contact shadow, base ring).</summary>
        public float FootprintRadius => archetype == TwinArchetype.Tree || archetype == TwinArchetype.Vine
            ? Mathf.Max(0.3f, canopyRadius * 0.8f)
            : Mathf.Max(0.25f, rootSpread * 1.5f);
    }
}
