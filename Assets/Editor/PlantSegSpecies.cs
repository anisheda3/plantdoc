#if UNITY_EDITOR
using PlantDoctor.Plants;
using UnityEngine;
using static PlantDoctor.EditorTools.SpeciesDataBuilder;

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// Species profiles of the plants that came with the PlantSeg dataset. Sizes follow the measurements in
    /// docs/PLANT_MORPHOLOGY_SOURCES.md ("The PlantSeg plants"); tall plants are drawn at a stated scale with their leaf-to-plant
    /// proportions kept. Leaf photographs: Assets/Art/Plants/&lt;id&gt; (tools/assets/make_commons_leaves.py).
    /// </summary>
    public static class PlantSegSpecies
    {
        public static readonly string[] Ids =
        {
            "banana", "basil", "bean", "broccoli", "cabbage", "carrot", "cauliflower", "celery", "coffee", "cucumber", "eggplant",
            "garlic", "ginger", "lettuce", "maple", "plum", "rice", "tobacco", "wheat", "mango", "sugarcane", "chilli", "cotton"
        };

        public static SpeciesProfile For(string id) => id switch
        {
            "banana" => Banana(), "basil" => Basil(), "bean" => Bean(), "broccoli" => Broccoli(), "cabbage" => Cabbage(),
            "carrot" => Carrot(), "cauliflower" => Cauliflower(), "celery" => Celery(), "coffee" => Coffee(), "cucumber" => Cucumber(),
            "eggplant" => Eggplant(), "garlic" => Garlic(), "ginger" => Ginger(), "lettuce" => Lettuce(), "maple" => Maple(),
            "plum" => Plum(), "rice" => Rice(), "tobacco" => Tobacco(), "wheat" => Wheat(),
            "mango" => Mango(), "sugarcane" => Sugarcane(), "chilli" => Chilli(), "cotton" => Cotton(),
            _ => null
        };

        static Color C(float r, float g, float b) => new Color(r, g, b);

        static SpeciesProfile Banana()
        {
            // Banana (Cavendish type): pseudostem ~2.2 m x 20-25 cm, blades ~2 x 0.6 m on ~0.6 m petioles; a 3.5-3.8 m plant at 0.46 scale.
            var p = Profile("banana", "Banana", TwinArchetype.Banana, 211);
            p.height = 1.75f;
            Roots(p, 18, 0.5f, 0.012f, 0.2f, 0.06f);
            p.trunkHeight = 1.0f; p.trunkRadius = 0.05f;
            p.stemRadius = 0.012f; p.stemColor = C(0.52f, 0.62f, 0.34f);
            p.leafForm = LeafForm.Simple; p.leafCount = 9; p.leafSize = new Vector2(1.2f, 0.95f); p.petioleLength = 0.35f;   // petiole ~0.8 m on a ~2 m blade
            p.leafFold = 0.04f; p.leafDroop = 0.3f; p.leafTint = C(0.96f, 1f, 0.94f);
            Photos(p, "banana", 0.04f);
            p.fruit = FruitForm.BananaBunch; p.fruitCount = 1; p.fruitRadius = 0.075f;
            p.fruitColors = new[] { C(0.48f, 0.64f, 0.20f) };
            return Save(p);
        }

        static SpeciesProfile Basil()
        {
            // Basil: 20-80 cm herb, opposite leaves, blade 2.5-5 cm on 1-2 cm petioles, square stems, spikes of small white flowers.
            var p = Profile("basil", "Basil", TwinArchetype.Herb, 223);
            p.height = 0.45f;
            Roots(p, 10, 0.14f, 0.004f, 0.07f, 0.03f);
            // Bushy: branched from low down, so a plant about as wide as tall.
            p.mainStems = 3; p.stemSplay = 22f; p.sideBranches = 5; p.stemRadius = 0.005f; p.stemColor = C(0.50f, 0.62f, 0.32f); p.firstLeafHeight = 0.05f; p.stemLean = 0.04f;
            p.oppositeLeaves = true;
            p.leafForm = LeafForm.Simple; p.leafCount = 80; p.leafSize = new Vector2(0.085f, 0.055f); p.petioleLength = 0.3f;
            p.leafDroop = 0.08f; p.leafTint = C(0.86f, 1f, 0.80f);   // basil's bright, glossy green
            Photos(p, "basil", 0.08f);
            p.flowerCount = 4; p.flowerCluster = 10; p.flowerSpike = true; p.flowerZone = new Vector2(0.96f, 1f);
            p.flowerRadius = 0.004f; p.flowerForm = FlowerForm.Open; p.flowerPetals = 5; p.flowerNod = 70f;
            p.flowerColor = C(0.97f, 0.96f, 0.95f); p.flowerThroat = C(0.94f, 0.92f, 0.90f); p.flowerCentre = C(0.92f, 0.90f, 0.78f);
            p.fruit = FruitForm.None; p.fruitCount = 0;
            return Save(p);
        }

        static SpeciesProfile Bean()
        {
            // Common bean (bush type): trifoliate, leaflets 6-15 cm, terminal leaflet on a longer stalk, petiole to 15 cm, pods to 20 cm.
            // Bush height ~50 cm is assumed (not in the floras found).
            var p = Profile("bean", "Bean", TwinArchetype.Herb, 227);
            p.height = 0.5f;
            Roots(p, 10, 0.18f, 0.005f, 0.08f, 0.03f);
            p.rootNodules = 30;                        // a legume: nitrogen-fixing nodules
            p.mainStems = 1; p.sideBranches = 4; p.stemRadius = 0.006f; p.stemColor = C(0.52f, 0.64f, 0.32f); p.stemLean = 0.05f;
            p.leafForm = LeafForm.Trifoliate; p.leafCount = 16; p.petioleLength = 1.0f; p.leafSize = new Vector2(0.2f, 0.14f);
            p.trifoliateRachis = 0.15f; p.leafDroop = 0.06f; p.leafTint = C(0.97f, 1f, 0.95f);
            Photos(p, "bean", 0.05f);
            p.flowerCount = 3; p.flowerCluster = 3; p.flowerRadius = 0.006f; p.flowerForm = FlowerForm.Open; p.flowerNod = 90f;
            p.flowerColor = C(0.96f, 0.94f, 0.97f); p.flowerThroat = C(0.90f, 0.86f, 0.92f); p.flowerCentre = C(0.86f, 0.88f, 0.70f);
            p.fruit = FruitForm.BeanPod; p.fruitCount = 6; p.fruitCluster = 3; p.fruitRadius = 0.0105f;
            p.fruitColors = new[] { C(0.42f, 0.62f, 0.24f), C(0.48f, 0.66f, 0.28f) };
            return Save(p);
        }

        static SpeciesProfile Broccoli()
        {
            // Broccoli: upright to 1 m, basal leaves with petioles to 30 cm and blades to 40 x 15 cm; head 7-20 cm across.
            var p = Profile("broccoli", "Broccoli", TwinArchetype.Rosette, 229);
            p.height = 0.75f;
            Roots(p, 12, 0.3f, 0.007f, 0.1f, 0.04f);
            p.stemRadius = 0.02f; p.stemColor = C(0.62f, 0.72f, 0.56f);
            p.crownHeight = 0.34f; p.petioleRise = 0.72f;
            p.leafForm = LeafForm.Simple; p.leafCount = 14; p.leafSize = new Vector2(0.6f, 0.38f); p.petioleLength = 0.5f;
            p.leafDroop = 0.1f; p.leafTint = C(0.86f, 0.96f, 0.98f);   // blue-green, waxy
            Photos(p, "broccoli", 0.06f);
            p.fruit = FruitForm.BroccoliHead; p.fruitCount = 1; p.fruitRadius = 0.085f;
            p.fruitColors = new[] { C(0.20f, 0.36f, 0.17f) };
            return Save(p);
        }

        static SpeciesProfile Cabbage()
        {
            // Cabbage: a firm head of 10-25 cm on a short stem, ringed by spreading outer leaves; about 40 cm tall, 60 cm across (assumed).
            var p = Profile("cabbage", "Cabbage", TwinArchetype.Rosette, 233);
            p.height = 0.38f;
            Roots(p, 12, 0.3f, 0.007f, 0.1f, 0.04f);
            p.stemRadius = 0.025f; p.stemColor = C(0.66f, 0.76f, 0.58f);
            p.crownHeight = 0.07f; p.petioleRise = 0.35f;
            p.leafForm = LeafForm.Simple; p.leafCount = 12; p.leafSize = new Vector2(0.42f, 0.3f); p.petioleLength = 0.25f;
            p.leafDroop = 0.14f; p.leafTint = C(0.86f, 0.96f, 0.96f);
            Photos(p, "cabbage", 0.06f);
            p.fruit = FruitForm.CabbageHead; p.fruitCount = 1; p.fruitRadius = 0.1f;
            p.fruitColors = new[] { C(0.68f, 0.82f, 0.56f) };
            return Save(p);
        }

        static SpeciesProfile Cauliflower()
        {
            // Cauliflower: a white curd 12-20 cm across (assumed) wrapped by upright inner leaves, on a stout stem.
            var p = Profile("cauliflower", "Cauliflower", TwinArchetype.Rosette, 239);
            p.height = 0.6f;
            Roots(p, 12, 0.3f, 0.007f, 0.1f, 0.04f);
            p.stemRadius = 0.022f; p.stemColor = C(0.64f, 0.74f, 0.58f);
            p.crownHeight = 0.2f; p.petioleRise = 0.78f;
            p.leafForm = LeafForm.Simple; p.leafCount = 14; p.leafSize = new Vector2(0.55f, 0.42f); p.petioleLength = 0.35f;
            p.leafDroop = 0.1f; p.leafTint = C(0.86f, 0.96f, 0.96f);
            Photos(p, "cauliflower", 0.06f);
            p.fruit = FruitForm.CauliflowerCurd; p.fruitCount = 1; p.fruitRadius = 0.08f;
            p.fruitColors = new[] { C(0.95f, 0.93f, 0.84f) };
            return Save(p);
        }

        static SpeciesProfile Carrot()
        {
            // Carrot: a rosette of finely divided leaves on long stalks, 30-60 cm, over the orange root (assumed values).
            var p = Profile("carrot", "Carrot", TwinArchetype.Rosette, 241);
            p.height = 0.42f;
            Roots(p, 10, 0.12f, 0.003f, 0.06f, 0.025f);
            p.stemRadius = 0.008f; p.stemColor = C(0.48f, 0.62f, 0.30f);
            p.petioleRise = 0.86f;
            p.leafForm = LeafForm.Simple; p.leafCount = 12; p.leafSize = new Vector2(0.42f, 0.3f); p.petioleLength = 0.9f; p.bladeLift = 55f;
            p.leafDroop = 0.12f; p.leafTint = C(0.97f, 1f, 0.95f);
            Photos(p, "carrot", 0.03f);
            p.fruit = FruitForm.CarrotRoot; p.fruitCount = 1; p.fruitRadius = 0.018f;
            p.fruitColors = new[] { C(0.93f, 0.48f, 0.10f) };
            return Save(p);
        }

        static SpeciesProfile Celery()
        {
            // Celery: thick, ribbed, upright leaf stalks with pinnate fronds (leaflets 3-6 cm) on top.
            var p = Profile("celery", "Celery", TwinArchetype.Rosette, 251);
            p.height = 0.55f;
            Roots(p, 12, 0.14f, 0.004f, 0.08f, 0.03f);
            p.stemRadius = 0.02f; p.stemColor = C(0.62f, 0.78f, 0.46f);
            p.petioleRise = 0.93f;
            p.leafForm = LeafForm.Compound; p.leafletPairs = 2; p.leafCount = 14; p.leafSize = new Vector2(0.52f, 0.4f); p.petioleLength = 1.4f;
            p.leafDroop = 0.08f; p.leafTint = C(0.98f, 1f, 0.95f);
            Photos(p, "celery", 0.04f);
            p.fruit = FruitForm.None; p.fruitCount = 0;
            return Save(p);
        }

        static SpeciesProfile Coffee()
        {
            // Arabica coffee as pruned on farms (1.2-1.8 m): opposite elliptic leaves to 10-12 cm on side branches; cherries 1-2 cm, red.
            var p = Profile("coffee", "Coffee", TwinArchetype.Shrub, 257);
            p.height = 1.4f;
            Roots(p, 10, 0.4f, 0.012f, 0.13f, 0.045f);
            p.trunkRadius = 0.022f; p.sideBranches = 14; p.canopyRadius = 0.42f; p.barkColor = C(0.56f, 0.50f, 0.42f);
            p.oppositeLeaves = true;
            p.leafForm = LeafForm.Simple; p.leafSize = new Vector2(0.14f, 0.11f); p.leafDroop = 0.08f; p.leafTint = C(0.44f, 0.60f, 0.44f);   // the photo shows the pale underside; the upper face is dark, glossy green
            Photos(p, "coffee", 0.07f);
            p.fruit = FruitForm.Cherry; p.fruitCount = 18; p.fruitCluster = 5; p.fruitRadius = 0.0075f;
            p.fruitColors = new[] { C(0.70f, 0.08f, 0.08f), C(0.52f, 0.62f, 0.18f), C(0.84f, 0.24f, 0.10f) };
            return Save(p);
        }

        static SpeciesProfile Cucumber()
        {
            // Cucumber on a stake: leaves 7-20 cm, 3-7-lobed, cordate, petiole 10-16 cm; simple tendrils; fruit 5-50 cm, tuberculate.
            var p = Profile("cucumber", "Cucumber", TwinArchetype.Climber, 263);
            p.height = 1.5f;
            Roots(p, 10, 0.2f, 0.004f, 0.08f, 0.03f);
            p.stemRadius = 0.006f; p.stemColor = C(0.48f, 0.62f, 0.30f); p.barkColor = C(0.72f, 0.62f, 0.42f);   // a bamboo stake
            p.leafForm = LeafForm.Simple; p.leafCount = 22; p.leafSize = new Vector2(0.34f, 0.22f); p.petioleLength = 0.75f; p.petioleInset = 0.08f;
            p.leafDroop = 0.08f; p.leafTint = C(0.95f, 1f, 0.94f);
            Photos(p, "cucumber", 0.05f);
            p.flowerCount = 3; p.flowerRadius = 0.013f; p.flowerForm = FlowerForm.Star; p.flowerPetals = 5; p.flowerCone = 0f; p.flowerNod = 70f;
            p.flowerColor = C(0.98f, 0.86f, 0.20f); p.flowerThroat = C(0.94f, 0.72f, 0.10f); p.flowerCentre = C(0.95f, 0.80f, 0.20f);
            p.fruit = FruitForm.Cucumber; p.fruitCount = 3; p.fruitRadius = 0.025f;
            p.fruitColors = new[] { C(0.20f, 0.38f, 0.14f), C(0.26f, 0.44f, 0.16f) };
            return Save(p);
        }

        static SpeciesProfile Eggplant()
        {
            // Eggplant: 0.5-1.5 m; blades ovate 3-25 cm, petioles to 10 cm; purple flowers 3-5 cm across; fruit 5-30 cm.
            var p = Profile("eggplant", "Eggplant", TwinArchetype.Herb, 269);
            p.height = 0.9f;
            Roots(p, 10, 0.26f, 0.008f, 0.1f, 0.04f);
            p.firstLeafHeight = 0.2f;
            p.mainStems = 1; p.sideBranches = 4; p.stemRadius = 0.014f; p.stemColor = C(0.42f, 0.46f, 0.32f); p.stemLean = 0.04f;
            p.leafForm = LeafForm.Simple; p.leafCount = 26; p.leafSize = new Vector2(0.28f, 0.16f); p.petioleLength = 0.4f;
            p.leafDroop = 0.1f; p.leafTint = C(0.95f, 0.98f, 0.95f);
            Photos(p, "eggplant", 0.06f);
            p.flowerCount = 3; p.flowerCluster = 2; p.flowerRadius = 0.02f; p.flowerForm = FlowerForm.Rotate; p.flowerNod = 100f; p.flowerCone = 0.6f;
            p.flowerColor = C(0.64f, 0.48f, 0.80f); p.flowerThroat = C(0.50f, 0.36f, 0.66f); p.flowerCentre = C(0.96f, 0.82f, 0.20f);
            p.fruit = FruitForm.Eggplant; p.fruitCount = 3; p.fruitRadius = 0.045f;
            p.fruitColors = new[] { C(0.24f, 0.07f, 0.21f), C(0.30f, 0.10f, 0.27f) };
            return Save(p);
        }

        static SpeciesProfile Garlic()
        {
            // Garlic: 4-10 flat leaves to 50 x 2.5 cm from a pseudostem of their sheaths; bulb to 7 cm.
            var p = Profile("garlic", "Garlic", TwinArchetype.Grass, 271);
            p.height = 0.55f;
            Roots(p, 18, 0.1f, 0.0025f, 0.06f, 0.025f);
            p.mainStems = 1; p.stalkShare = 0.42f; p.stemRadius = 0.007f; p.stemColor = C(0.62f, 0.72f, 0.48f); p.firstLeafHeight = 0.35f;
            p.leafForm = LeafForm.Strap; p.leafCount = 8; p.leafSize = new Vector2(0.42f, 0.32f); p.leafFold = 0.05f; p.leafTint = C(0.92f, 0.98f, 0.96f);
            Photos(p, "garlic", 0.05f);
            p.fruit = FruitForm.Bulb; p.fruitCount = 1; p.fruitRadius = 0.03f;
            p.fruitColors = new[] { C(0.93f, 0.90f, 0.86f) };
            return Save(p);
        }

        static SpeciesProfile Ginger()
        {
            // Ginger: leafy pseudostems 30-100 cm from the rhizome, 8-12 two-ranked leaves each, 5-30 cm x 8-20 mm.
            var p = Profile("ginger", "Ginger", TwinArchetype.Grass, 277);
            p.height = 0.8f;
            Roots(p, 14, 0.14f, 0.003f, 0.08f, 0.03f);
            p.mainStems = 4; p.stemSplay = 9f; p.stemLean = 0.04f; p.stalkShare = 0.92f; p.stemRadius = 0.006f;
            p.stemColor = C(0.48f, 0.60f, 0.30f); p.firstLeafHeight = 0.28f;
            p.leafForm = LeafForm.Strap; p.leafCount = 40; p.leafSize = new Vector2(0.26f, 0.2f); p.leafFold = 0.03f; p.strapSpread = 42f; p.strapArch = 0.72f; p.leafTint = C(0.95f, 1f, 0.94f);
            Photos(p, "ginger", 0.04f);
            p.fruit = FruitForm.Rhizome; p.fruitCount = 1; p.fruitRadius = 0.022f;
            p.fruitColors = new[] { C(0.82f, 0.70f, 0.50f) };
            return Save(p);
        }

        static SpeciesProfile Lettuce()
        {
            // Lettuce: a basal rosette of broad leaves to about 15-20 cm, the inner ones standing up into a loose head.
            var p = Profile("lettuce", "Lettuce", TwinArchetype.Rosette, 281);
            p.height = 0.2f;
            Roots(p, 12, 0.1f, 0.003f, 0.06f, 0.025f);
            p.stemRadius = 0.012f; p.stemColor = C(0.80f, 0.86f, 0.64f);
            p.crownHeight = 0.04f; p.petioleRise = 0.85f;   // a very short stem; the inner leaves stand up and cup into a loose head
            p.leafForm = LeafForm.Simple; p.leafCount = 24; p.leafSize = new Vector2(0.2f, 0.12f); p.petioleLength = 0.08f; p.bladeLift = 25f;
            p.leafDroop = 0.1f; p.leafTint = C(1f, 1f, 0.96f);
            Photos(p, "lettuce", 0.07f);
            p.fruit = FruitForm.None; p.fruitCount = 0;
            return Save(p);
        }

        static SpeciesProfile Maple()
        {
            // Sycamore maple (tar spot's main host) as a young tree: leaves 5-lobed, 10-25 cm, petiole 5-15 cm; a 3 m sapling at 0.47 scale.
            var p = Profile("maple", "Maple", TwinArchetype.Tree, 283);
            p.height = 1.4f;
            Roots(p, 9, 0.55f, 0.022f, 0.16f, 0.055f);
            p.rootTint = C(0.80f, 0.72f, 0.66f);
            p.trunkHeight = 0.6f; p.trunkRadius = 0.03f; p.branchLevels = 2; p.branchesPerLevel = 4;
            p.canopyRadius = 0.48f; p.leavesPerTip = 40; p.barkColor = C(0.58f, 0.56f, 0.52f);
            p.leafForm = LeafForm.Simple; p.leafSize = new Vector2(0.075f, 0.055f); p.leafDroop = 0.05f; p.leafTint = C(0.95f, 1f, 0.94f);
            Photos(p, "maple", 0.06f);
            p.fruit = FruitForm.None; p.fruitCount = 0;
            return Save(p);
        }

        static SpeciesProfile Plum()
        {
            // European plum: tree 6-10 m, leaves elliptic 4-10 cm, drupe 1-3.5 cm (to 6 cultivated); a 5 m tree at 0.26 scale.
            var p = Profile("plum", "Plum", TwinArchetype.Tree, 293);
            p.height = 1.3f;
            Roots(p, 9, 0.55f, 0.022f, 0.16f, 0.055f);
            p.rootTint = C(0.80f, 0.70f, 0.64f);
            p.trunkHeight = 0.5f; p.trunkRadius = 0.033f; p.branchLevels = 2; p.branchesPerLevel = 4;
            p.canopyRadius = 0.44f; p.leavesPerTip = 75;
            p.canopyLeafScale = 1.8f;   // exception for a lush crown (as the other fruit trees); leafSize stays the real proportion
            p.barkColor = C(0.42f, 0.34f, 0.32f);
            p.leafForm = LeafForm.Simple; p.leafSize = new Vector2(0.021f, 0.016f); p.leafDroop = 0.06f; p.leafTint = C(0.95f, 1f, 0.94f);
            Photos(p, "plum", 0.07f);
            p.fruit = FruitForm.Peach; p.fruitCount = 20; p.fruitRadius = 0.006f;
            p.fruitDisplayScale = p.canopyLeafScale;
            p.fruitColors = new[] { C(0.34f, 0.15f, 0.36f), C(0.44f, 0.20f, 0.42f) };
            return Save(p);
        }

        static SpeciesProfile Rice()
        {
            // Rice: tillers 0.5-1.5 m, blades 25-60 x 0.5-2 cm, a drooping panicle to 30 cm on each tiller.
            var p = Profile("rice", "Rice", TwinArchetype.Grass, 307);
            p.height = 0.95f;
            Roots(p, 22, 0.12f, 0.0025f, 0.07f, 0.03f);
            p.mainStems = 10; p.stemSplay = 15f; p.stemLean = 0.05f; p.stalkShare = 0.84f; p.stemRadius = 0.0035f;
            p.stemColor = C(0.56f, 0.66f, 0.34f); p.firstLeafHeight = 0.08f;
            p.leafForm = LeafForm.Strap; p.leafCount = 40; p.leafSize = new Vector2(0.45f, 0.32f); p.leafFold = 0.02f; p.leafTint = C(0.95f, 1f, 0.94f);
            Photos(p, "rice", 0.03f);
            p.fruit = FruitForm.Panicle; p.fruitCount = 10; p.fruitRadius = 0.065f;
            p.fruitColors = new[] { C(0.78f, 0.74f, 0.42f) };
            return Save(p);
        }

        static SpeciesProfile Tobacco()
        {
            // Tobacco: 0.5-2 m, leaves 20-50 x 3-15 cm, nearly stalkless; pink funnel flowers 3.5-5 cm long in a cluster at the top.
            var p = Profile("tobacco", "Tobacco", TwinArchetype.Herb, 311);
            p.height = 1.5f;
            Roots(p, 12, 0.3f, 0.009f, 0.1f, 0.04f);
            p.firstLeafHeight = 0.05f;
            p.mainStems = 1; p.sideBranches = 0; p.stemRadius = 0.016f; p.stemColor = C(0.56f, 0.66f, 0.40f); p.stemLean = 0.02f;
            p.leafForm = LeafForm.Simple; p.leafCount = 20; p.leafSize = new Vector2(0.52f, 0.3f); p.petioleLength = 0.05f;   // blades to 50 cm (floras)
            p.leafDroop = 0.18f; p.leafTint = C(0.96f, 1f, 0.94f);
            Photos(p, "tobacco", 0.06f);
            p.flowerCount = 2; p.flowerCluster = 12; p.flowerZone = new Vector2(0.97f, 1f); p.flowerRadius = 0.008f;
            p.flowerForm = FlowerForm.Trumpet; p.flowerNod = 50f;
            p.flowerColor = C(0.95f, 0.60f, 0.72f); p.flowerThroat = C(0.96f, 0.90f, 0.80f); p.flowerCentre = C(0.90f, 0.86f, 0.62f);
            p.fruit = FruitForm.None; p.fruitCount = 0;
            return Save(p);
        }

        static SpeciesProfile Mango()
        {
            // Mango: tree 5-40 m; leaves oblong-lanceolate 10-30 x 3.5-7 cm, leathery, petiole 2-6 cm; drupe 5-20 cm, green to
            // yellow or red. A young grafted tree of ~4 m at 0.33 scale, with the fruit trees' lush-crown exception.
            var p = Profile("mango", "Mango", TwinArchetype.Tree, 331);
            p.height = 1.35f;
            Roots(p, 10, 0.6f, 0.024f, 0.17f, 0.06f);
            p.rootTint = C(0.80f, 0.70f, 0.62f);
            p.trunkHeight = 0.45f; p.trunkRadius = 0.04f; p.branchLevels = 2; p.branchesPerLevel = 5;
            p.canopyRadius = 0.55f; p.leavesPerTip = 48;
            p.canopyLeafScale = 1.6f;   // exception for a lush crown (as the other fruit trees); leafSize stays the real proportion
            p.barkColor = C(0.40f, 0.34f, 0.30f);
            p.leafForm = LeafForm.Simple; p.leafSize = new Vector2(0.08f, 0.06f); p.leafDroop = 0.12f; p.leafTint = C(0.62f, 0.80f, 0.56f);
            Photos(p, "mango", 0.06f);
            p.fruit = FruitForm.Mango; p.fruitCount = 14; p.fruitRadius = 0.0115f;
            p.fruitDisplayScale = p.canopyLeafScale;
            p.fruitColors = new[] { C(0.52f, 0.64f, 0.20f), C(0.90f, 0.62f, 0.18f), C(0.78f, 0.34f, 0.16f) };
            return Save(p);
        }

        static SpeciesProfile Sugarcane()
        {
            // Sugarcane: culms 3-6 m, 2-5 cm thick, clumped, jointed; blades 70-150 x 2-6 cm, crowded toward the top as the lower ones
            // dry and fall. A ~3.5 m clump at 0.5 scale.
            var p = Profile("sugarcane", "Sugarcane", TwinArchetype.Grass, 337);
            p.height = 1.8f;
            Roots(p, 22, 0.16f, 0.004f, 0.1f, 0.04f);
            p.mainStems = 6; p.stemSplay = 7f; p.stemLean = 0.05f; p.stalkShare = 0.8f; p.stemRadius = 0.0085f;
            p.stemColor = C(0.70f, 0.66f, 0.40f); p.firstLeafHeight = 0.45f;
            p.leafForm = LeafForm.Strap; p.leafCount = 48; p.leafSize = new Vector2(0.62f, 0.5f); p.leafFold = 0.03f; p.leafTint = C(0.92f, 1f, 0.9f);
            Photos(p, "sugarcane", 0.03f);
            p.fruit = FruitForm.None; p.fruitCount = 0;
            return Save(p);
        }

        static SpeciesProfile Chilli()
        {
            // Chilli (hot pepper, Capsicum annuum): 0.3-1.2 m; glossy ovate leaves to 7.5-12 cm; slender fruit 7-12 cm, green ripening red;
            // white flowers nodding from the leaf joints. The same species as bell pepper (whose leaf photos it uses, narrowed).
            var p = Profile("chilli", "Chilli", TwinArchetype.Herb, 347);
            p.height = 0.7f;
            Roots(p, 9, 0.22f, 0.006f, 0.08f, 0.034f);
            p.firstLeafHeight = 0.18f;
            p.mainStems = 1; p.sideBranches = 5; p.stemRadius = 0.009f; p.stemColor = C(0.50f, 0.66f, 0.36f); p.stemLean = 0.03f;
            p.leafForm = LeafForm.Simple; p.leafCount = 46; p.leafSize = new Vector2(0.1f, 0.06f); p.petioleLength = 0.35f;
            p.leafDroop = 0.08f; p.leafTint = C(0.95f, 1f, 0.94f);
            Photos(p, "chilli", 0.06f);
            p.flowerCount = 5; p.flowerRadius = 0.006f; p.flowerForm = FlowerForm.Star; p.flowerPetals = 5; p.flowerCone = 0.4f; p.flowerNod = 150f;
            p.flowerColor = C(0.97f, 0.97f, 0.94f); p.flowerThroat = C(0.88f, 0.91f, 0.78f); p.flowerCentre = C(0.55f, 0.48f, 0.70f);
            p.fruit = FruitForm.Chilli; p.fruitCount = 10; p.fruitRadius = 0.0075f;
            p.fruitColors = new[] { C(0.80f, 0.08f, 0.06f), C(0.26f, 0.55f, 0.16f), C(0.86f, 0.14f, 0.08f) };
            return Save(p);
        }

        static SpeciesProfile Cotton()
        {
            // Upland cotton (Gossypium hirsutum) as grown: an annual shrub 1-1.5 m, one main stem with fruiting branches; leaves
            // palmately 3-5-lobed, about as broad as long, 7.5-15 cm, on petioles about as long as the blade; flowers 5-9 cm across,
            // cream on the first day and pink-red the next; bolls 3-5 cm, splitting into 3-5 locks of white lint at maturity.
            var p = Profile("cotton", "Cotton", TwinArchetype.Herb, 353);
            p.height = 1.05f;
            Roots(p, 9, 0.3f, 0.008f, 0.1f, 0.04f);   // a deep tap root with laterals
            p.firstLeafHeight = 0.16f;
            // Dense and leafy: the leaves sit on long petioles along the main stem; the short fruiting branches stay inside that
            // column, so no long side shoots (the builder's side branches are long laterals with leaves only at the tip).
            p.mainStems = 1; p.sideBranches = 0; p.stemRadius = 0.009f; p.stemColor = C(0.62f, 0.52f, 0.36f); p.stemLean = 0.03f;   // the stem reddens with age
            p.leafForm = LeafForm.Simple; p.leafCount = 92; p.leafSize = new Vector2(0.16f, 0.12f); p.petioleLength = 0.85f; p.petioleInset = 0.06f;
            p.leafDroop = 0.1f; p.leafTint = C(0.92f, 1f, 0.92f);
            Photos(p, "cotton", 0.06f);
            p.flowerCount = 5; p.flowerRadius = 0.03f; p.flowerForm = FlowerForm.Open; p.flowerPetals = 5; p.flowerCone = 0.35f; p.flowerNod = 20f;
            p.flowerColor = C(0.98f, 0.95f, 0.80f); p.flowerThroat = C(0.94f, 0.88f, 0.66f); p.flowerCentre = C(0.96f, 0.90f, 0.56f);
            p.fruit = FruitForm.CottonBoll; p.fruitCount = 12; p.fruitRadius = 0.017f;
            p.fruitColors = new[] { C(0.97f, 0.97f, 0.95f), C(0.95f, 0.94f, 0.90f), C(0.98f, 0.97f, 0.93f) };
            return Save(p);
        }

        static SpeciesProfile Wheat()
        {
            // Wheat: culms 40-170 cm, blades 15-40 x 1-2 cm, a spike of 4-18 cm on each tiller.
            var p = Profile("wheat", "Wheat", TwinArchetype.Grass, 313);
            p.height = 0.9f;
            Roots(p, 20, 0.12f, 0.0025f, 0.07f, 0.03f);
            p.mainStems = 5; p.stemSplay = 8f; p.stemLean = 0.03f; p.stalkShare = 0.88f; p.stemRadius = 0.003f;
            p.stemColor = C(0.58f, 0.68f, 0.34f); p.firstLeafHeight = 0.1f;
            p.leafForm = LeafForm.Strap; p.leafCount = 20; p.leafSize = new Vector2(0.3f, 0.2f); p.leafFold = 0.02f; p.leafTint = C(0.95f, 1f, 0.94f);
            Photos(p, "wheat", 0.03f);
            p.fruit = FruitForm.Spike; p.fruitCount = 5; p.fruitRadius = 0.04f;
            p.fruitColors = new[] { C(0.66f, 0.72f, 0.36f) };
            return Save(p);
        }
    }
}
#endif
