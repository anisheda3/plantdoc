#if UNITY_EDITOR
using PlantDoctor.Core;
using PlantDoctor.Diseases;
using PlantDoctor.Plants;
using PlantDoctor.Treatment;
using UnityEditor;
using UnityEngine;
using static PlantDoctor.EditorTools.DemoDataBuilder;

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// Species profiles (how each twin looks) and the potato, pepper and apple disease data:
    /// descriptions, treatment plans and prevention plans. Content mirrors the knowledge base
    /// in the AI model memo; product names stay TODO[DATA] until checked locally.
    /// </summary>
    public static class SpeciesDataBuilder
    {
        const string Root = "Assets/Data";
        const string Masks = "Assets/Textures/Generated";

        // ------------------------------------------------------------------
        // Profiles
        // ------------------------------------------------------------------

        public static SpeciesProfile TomatoProfile()
        {
            var p = Profile("tomato", "Tomato", TwinArchetype.Herb, 11);
            p.height = 1.0f;
            Roots(p, 10, 0.3f, 0.0095f, 0.09f, 0.036f);
            p.firstLeafHeight = 0.26f;
            p.mainStems = 1; p.stemSplay = 0f; p.sideBranches = 3; p.stemRadius = 0.017f;
            p.stemColor = new Color(0.78f, 0.86f, 0.62f);
            p.leafForm = LeafForm.Compound; p.leafCount = 32; p.leafSize = new Vector2(0.24f, 0.14f);   // real proportions: a tomato leaf is up to ~20% of the plant's height
            p.leafletPairs = 3; p.leafWidth = 0.5f; p.serration = 0.07f; p.teeth = 8; p.leafDroop = 0.1f;
            p.interjectedLeaflets = 1;   // small leaflets between the pairs, as on a real tomato leaf
            p.leafTint = new Color(0.96f, 0.98f, 0.94f);
            Photos(p, "tomato", 0.06f);
            p.fruit = FruitForm.Round; p.fruitCount = 3; p.fruitRadius = 0.03f;   // ~5 cm fruit next to 20 cm leaves
            p.fruitCluster = 3;   // tomatoes come in trusses: several fruit on one stalk from the stem between the leaves
            // Flowers: yellow, 1-2 cm across, 5-6 narrow petals bent back round a cone of fused anthers, nodding in trusses of several
            // (an indeterminate tomato flowers and fruits at once).
            p.flowerCount = 3; p.flowerCluster = 5; p.flowerRadius = 0.009f; p.flowerForm = PlantDoctor.Plants.FlowerForm.Star; p.flowerPetals = 6;
            p.flowerColor = new Color(0.98f, 0.86f, 0.20f); p.flowerThroat = new Color(0.94f, 0.74f, 0.10f); p.flowerCentre = new Color(0.93f, 0.80f, 0.22f);
            p.flowerCone = 0.9f; p.flowerNod = 140f;
            p.fruitColors = new[] { new Color(0.86f, 0.17f, 0.10f), new Color(0.86f, 0.17f, 0.10f), new Color(0.42f, 0.62f, 0.22f) };
            return Save(p);
        }

        static SpeciesProfile PotatoProfile()
        {
            var p = Profile("potato", "Potato", TwinArchetype.Herb, 23);
            p.height = 0.62f;                              // a low, bushy clump of stems
            Roots(p, 10, 0.26f, 0.007f, 0.1f, 0.04f);
            p.firstLeafHeight = 0.16f;
            p.mainStems = 5; p.stemSplay = 13f; p.sideBranches = 0; p.stemRadius = 0.009f; p.stemLean = 0.05f;   // a clump about as wide as tall
            p.stemColor = new Color(0.62f, 0.75f, 0.50f);
            p.leafForm = LeafForm.Compound; p.leafCount = 52; p.leafSize = new Vector2(0.22f, 0.16f);   // blade 8-22 cm + petiole 2-6 cm (Solanaceae Source)   // potato's ratio is assumed like tomato's (not found in the sources)
            p.leafletPairs = 3; p.leafWidth = 0.62f; p.serration = 0f; p.teeth = 0; p.leafDroop = 0.08f;
            p.interjectedLeaflets = 2;   // potato leaves carry many small leaflets between the main pairs
            p.leafTint = new Color(0.92f, 0.96f, 0.90f);   // darker, slightly blue-green
            Photos(p, "potato", 0.05f);
            p.fruit = FruitForm.None; p.fruitCount = 0;    // tubers are underground
            // Flowers: 2.5-3.5 cm across, a flat five-angled white wheel with a yellow-green star in the throat and a cone of orange-yellow
            // anthers, in cymes at the top of the stems.
            p.flowerCount = 4; p.flowerCluster = 6; p.flowerRadius = 0.014f; p.flowerForm = PlantDoctor.Plants.FlowerForm.Rotate; p.flowerPetals = 5;
            p.flowerColor = new Color(0.97f, 0.96f, 0.98f); p.flowerThroat = new Color(0.90f, 0.92f, 0.62f); p.flowerCentre = new Color(0.98f, 0.74f, 0.14f);
            p.flowerCone = 0.65f; p.flowerNod = 55f;
            return Save(p);
        }

        static SpeciesProfile PepperProfile()
        {
            var p = Profile("pepper", "Bell pepper", TwinArchetype.Herb, 37);
            p.height = 0.75f;
            Roots(p, 9, 0.24f, 0.007f, 0.09f, 0.036f);
            p.firstLeafHeight = 0.24f;
            p.mainStems = 1; p.stemSplay = 0f; p.sideBranches = 4; p.stemRadius = 0.014f;
            p.stemColor = new Color(0.55f, 0.72f, 0.42f);
            p.leafForm = LeafForm.Simple; p.leafCount = 40; p.leafSize = new Vector2(0.13f, 0.08f);   // blade 4-13 cm on a 20-80 cm plant (Flora of China)
            p.leafWidth = 0.62f; p.serration = 0f; p.teeth = 0; p.leafDroop = 0.06f;
            p.leafTint = new Color(0.97f, 1.0f, 0.95f);
            Photos(p, "pepper", 0.07f);
            p.fruit = FruitForm.Bell; p.fruitCount = 3; p.fruitRadius = 0.04f;
            // Flowers: single, white, about 1-1.5 cm across, 5-6 pointed petals, bluish anthers, nodding from the leaf joints.
            p.flowerCount = 4; p.flowerCluster = 1; p.flowerRadius = 0.007f; p.flowerForm = PlantDoctor.Plants.FlowerForm.Star; p.flowerPetals = 6;
            p.flowerColor = new Color(0.97f, 0.97f, 0.94f); p.flowerThroat = new Color(0.88f, 0.91f, 0.78f); p.flowerCentre = new Color(0.52f, 0.46f, 0.68f);
            p.flowerCone = 0.45f; p.flowerNod = 150f;
            p.fruitColors = new[] { new Color(0.22f, 0.55f, 0.16f), new Color(0.80f, 0.12f, 0.08f), new Color(0.25f, 0.58f, 0.18f) };
            return Save(p);
        }

        static SpeciesProfile AppleProfile()
        {
            var p = Profile("apple", "Apple", TwinArchetype.Tree, 53);
            p.height = 1.2f;
            Roots(p, 9, 0.6f, 0.026f, 0.17f, 0.06f);
            p.rootTint = new Color(0.78f, 0.72f, 0.66f);
            p.leafForm = LeafForm.Simple; p.leafSize = new Vector2(0.040f, 0.030f);   // 7-10 cm leaves on a 2-4 m tree
            p.leafWidth = 0.55f; p.serration = 0.03f; p.teeth = 14; p.leafDroop = 0.04f;
            p.leafTint = new Color(0.95f, 1.0f, 0.92f);
            Photos(p, "apple", 0.08f);
            p.trunkHeight = 0.55f; p.trunkRadius = 0.04f; p.branchLevels = 2; p.branchesPerLevel = 4;
            p.canopyRadius = 0.42f; p.leavesPerTip = 70;
            p.canopyLeafScale = 1.7f;   // exception for a lush crown (user request); leafSize stays the real proportion
            p.barkColor = new Color(0.72f, 0.62f, 0.54f);
            p.fruit = FruitForm.Apple; p.fruitCount = 12; p.fruitRadius = 0.017f;
            p.fruitDisplayScale = p.canopyLeafScale;   // drawn at the leaves' display scale so fruit and leaves keep their real proportion   // an apple is about as wide as its leaf is long
            p.fruitColors = new[] { new Color(0.75f, 0.12f, 0.10f), new Color(0.85f, 0.26f, 0.12f) };
            return Save(p);
        }

        /// <summary>Root system and the soil heap it grows from (plants stand on these, not in a pot).</summary>
        internal static void Roots(SpeciesProfile p, int count, float spread, float radius, float moundRadius, float moundHeight)
        {
            p.inPot = false;
            p.rootCount = count; p.rootSpread = spread; p.rootRadius = radius;
            p.rootTint = Color.white;
            p.moundRadius = moundRadius; p.moundHeight = moundHeight;
        }

        [System.Serializable] class AtlasLeaf { public float x, y, w, h, aspect; }
        [System.Serializable] class AtlasInfo { public AtlasLeaf[] leaves; }

        /// <summary>Leaf photographs of the crop (Assets/Art/Plants/crop/atlas.png, made by tools/assets/make_plant_textures.py).</summary>
        internal static void Photos(SpeciesProfile p, string crop, float fold)
        {
            string folder = $"Assets/Art/Plants/{crop}";
            p.leafAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/atlas.png");
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>($"{folder}/atlas.json");
            if (p.leafAtlas == null || json == null)
            {
                Debug.LogWarning($"[AI Plant Doctor] No leaf photographs for {crop} in {folder}; run tools/assets/make_plant_textures.py. The procedural leaf is used.");
                p.leafAtlas = null; p.leafRects = null; p.leafAspects = null;
                return;
            }
            var info = JsonUtility.FromJson<AtlasInfo>(json.text);
            p.leafRects = new Rect[info.leaves.Length];
            p.leafAspects = new float[info.leaves.Length];
            for (int i = 0; i < info.leaves.Length; i++)
            {
                var l = info.leaves[i];
                p.leafRects[i] = new Rect(l.x, l.y, l.w, l.h);
                p.leafAspects[i] = l.aspect;
            }
            p.leafFold = fold;
        }

        internal static SpeciesProfile Profile(string id, string name, TwinArchetype archetype, int seed)
        {
            EnsureFolder($"{Root}/Species");
            var p = CreateAsset<SpeciesProfile>($"{Root}/Species/Species_{name.Replace(" ", "")}.asset");
            p.speciesId = id; p.displayName = name; p.archetype = archetype; p.seed = seed;
            return p;
        }

        internal static SpeciesProfile Save(SpeciesProfile p) { EditorUtility.SetDirty(p); return p; }

        // ------------------------------------------------------------------
        // Plants and their diseases
        // ------------------------------------------------------------------

        public static PlantData[] BuildMoreSpecies(FeatureData[] features)
        {
            return new[] { BuildPotato(features), BuildPepper(features), BuildApple(features) };
        }

        static PlantData BuildPotato(FeatureData[] features)
        {
            var earlyBlight = Disease("Potato", "early_blight", "Early Blight",
                new[] { "Potato___Early_blight", "potato_early_blight" }, PathogenType.Fungal,
                "A common fungal leaf disease of potato caused by Alternaria solani.",
                "Warm, humid weather with long leaf wetness; stressed or under-fed plants; inoculum on crop debris.",
                new[] { "Dark spots with concentric target rings", "Yellowing around spots", "Older, lower leaves first" },
                "Lesions destroy leaf area and weaken the plant late in the season.",
                "Early defoliation reduces tuber size and yield.",
                Plan("Potato_EarlyBlight", "potato_early_blight_treatment", "Potato — Early Blight", 0.15f, 0.10f,
                    CreateStep("pot_eb_remove", "Remove infected lower leaves", "Pick off the most spotted lower leaves and bag them.",
                        "Less infected tissue means fewer spores to reach healthy leaves.", TreatmentInteractionMode.TapRegion, 2,
                        new StateDelta { diseaseSeverity = -0.10f, lesionDensity = -0.22f, yellowing = -0.12f, leafDamage = 0.04f }),
                    CreateStep("pot_eb_feed", "Feed and water evenly", "Keep soil evenly moist and give a balanced feed.",
                        "Well-fed, unstressed plants resist early blight far better.", TreatmentInteractionMode.AdjustSetting, 3,
                        new StateDelta { diseaseSeverity = -0.08f, healthyGrowth = 0.08f, yellowing = -0.06f }),
                    CreateStepWithChemical("pot_eb_treat", "Apply a protectant fungicide", "Spray a registered product, following the label.",
                        "Protects new leaves while infected ones are removed.", 3,
                        new StateDelta { diseaseSeverity = -0.20f, lesionDensity = -0.18f, healthyGrowth = 0.06f })),
                Prevention("Potato_EarlyBlight", "potato_early_blight_prevention",
                    Measure(PreventionCategory.CropManagement, "Rotate for three years", "Don't plant potato or tomato in the same ground for three seasons."),
                    Measure(PreventionCategory.CropHygiene, "Clear crop debris", "The fungus survives on old haulm and leaves; remove them after harvest."),
                    Measure(PreventionCategory.CropManagement, "Use certified seed and tolerant varieties", "Start clean and give the plant a head start."),
                    Measure(PreventionCategory.Monitoring, "Scout lower leaves weekly", "Early spots are far easier to manage.")),
                SimProfile("Potato_EarlyBlight", 8f, $"{Masks}/T_LesionMask_EarlyBlight.png", null));

            var lateBlight = Disease("Potato", "late_blight", "Late Blight",
                new[] { "Potato___Late_blight", "potato_late_blight", "phytophthora" }, PathogenType.Fungal,
                "A destructive water-mould disease (Phytophthora infestans) of potato and tomato.",
                "Cool (10–25 °C), wet weather; airborne spores from nearby infected plants or cull piles.",
                new[] { "Large water-soaked, dark patches", "White mould on leaf undersides when humid", "Brown rot in tubers" },
                "Destroys foliage within days and can rot tubers in the ground and in store.",
                "Whole crops can be lost in a wet week.",
                Plan("Potato_LateBlight", "potato_late_blight_treatment", "Potato — Late Blight", 0.20f, 0.15f,
                    CreateStep("pot_lb_remove", "Remove and destroy affected foliage", "Cut out blighted leaves and stems; bag them, don't compost.",
                        "Removing sporulating tissue is the first priority.", TreatmentInteractionMode.TapRegion, 1,
                        new StateDelta { diseaseSeverity = -0.14f, lesionDensity = -0.28f, yellowing = -0.08f }),
                    CreateStepWithChemical("pot_lb_treat", "Apply a protectant fungicide", "Spray a registered product suited to late blight, per the label.",
                        "Protects the remaining healthy foliage in risky weather.", 3,
                        new StateDelta { diseaseSeverity = -0.18f, lesionDensity = -0.16f, healthyGrowth = 0.06f }),
                    CreateStep("pot_lb_haulm", "Protect the tubers", "Hill soil over the tubers; kill and remove haulm 2–3 weeks before harvest.",
                        "Spores washed into soil infect tubers; soil cover and an early haulm kill stop them.", TreatmentInteractionMode.AdjustSetting, 2,
                        new StateDelta { diseaseSeverity = -0.08f, healthyGrowth = 0.04f })),
                Prevention("Potato_LateBlight", "potato_late_blight_prevention",
                    Measure(PreventionCategory.CropHygiene, "Destroy cull piles and volunteers", "Late blight survives on leftover tubers and self-sown plants."),
                    Measure(PreventionCategory.CropManagement, "Plant certified seed tubers", "Infected seed is a common way blight arrives."),
                    Measure(PreventionCategory.Monitoring, "Watch blight alerts", "Risk rises sharply in cool, wet spells; act before symptoms show.")),
                SimProfile("Potato_LateBlight", 6f, $"{Masks}/T_LesionMask_LateBlight.png",
                    new[] { new Color(0.20f, 0.18f, 0.12f), new Color(0.55f, 0.62f, 0.30f), new Color(0.80f, 0.76f, 0.30f) }));

            return Plant("potato", "Potato", "Solanum tuberosum.", features, PotatoProfile(), earlyBlight, lateBlight);
        }

        static PlantData BuildPepper(FeatureData[] features)
        {
            var bacterialSpot = Disease("Pepper", "bacterial_spot", "Bacterial Spot",
                new[] { "Pepper___Bacterial_spot", "Pepper,_bell___Bacterial_spot", "pepper_bacterial_spot" }, PathogenType.Bacterial,
                "A bacterial disease (Xanthomonas) of pepper and tomato leaves and fruit.",
                "Warm, wet weather; rain or overhead-water splash; infected seed or transplants.",
                new[] { "Small dark, greasy spots", "Yellow halo around spots", "Raised scabby spots on fruit" },
                "Spotted leaves yellow and drop; fruit is blemished.",
                "Heavy leaf drop exposes fruit to sunscald and cuts yield.",
                Plan("Pepper_BacterialSpot", "pepper_bacterial_spot_treatment", "Pepper — Bacterial Spot", 0.18f, 0.12f,
                    CreateStep("pep_bs_remove", "Remove spotted leaves", "Pick off the worst leaves when the plants are dry.",
                        "Reduces the bacteria available to splash onto new growth.", TreatmentInteractionMode.TapRegion, 2,
                        new StateDelta { diseaseSeverity = -0.10f, lesionDensity = -0.20f, yellowing = -0.10f, leafDamage = 0.03f }),
                    CreateStep("pep_bs_water", "Stop overhead watering", "Water at the base and avoid handling wet plants.",
                        "Bacteria spread in water droplets and on wet hands.", TreatmentInteractionMode.AdjustSetting, 2,
                        new StateDelta { diseaseSeverity = -0.08f, healthyGrowth = 0.05f }),
                    CreateStepWithChemical("pep_bs_copper", "Apply a copper spray", "Spray a registered copper product, following the label.",
                        "Copper slows the spread to new leaves; it cannot cure spotted ones.", 3,
                        new StateDelta { diseaseSeverity = -0.15f, lesionDensity = -0.15f, healthyGrowth = 0.05f })),
                Prevention("Pepper_BacterialSpot", "pepper_bacterial_spot_prevention",
                    Measure(PreventionCategory.CropManagement, "Use clean seed and transplants", "Most outbreaks start from infected seed or seedlings."),
                    Measure(PreventionCategory.WaterManagement, "Water with drip, not sprinklers", "Dry leaves stop the bacteria spreading."),
                    Measure(PreventionCategory.CropHygiene, "Disinfect tools and stakes", "Bacteria survive on surfaces between seasons.")),
                SimProfile("Pepper_BacterialSpot", 7f, $"{Masks}/T_LesionMask_EarlyBlight.png",
                    new[] { new Color(0.12f, 0.09f, 0.05f), new Color(0.90f, 0.82f, 0.30f), new Color(0.86f, 0.80f, 0.30f) }));

            var powderyMildew = Disease("Pepper", "powdery_mildew", "Powdery Mildew",
                new[] { "Pepper___Powdery_mildew", "pepper_powdery_mildew" }, PathogenType.Fungal,
                "A fungal disease (Leveillula taurica) that grows inside and under pepper leaves.",
                "Warm, dry days with humid nights; crowded plants.",
                new[] { "Yellow patches on the upper leaf", "White powdery growth underneath", "Leaves curl and drop" },
                "Reduces leaf area and causes leaf drop.",
                "Severe leaf loss leaves fruit sunburnt and small.",
                Plan("Pepper_PowderyMildew", "pepper_powdery_mildew_treatment", "Pepper — Powdery Mildew", 0.12f, 0.06f,
                    CreateStep("pep_pm_remove", "Remove the worst leaves", "Take off heavily coated leaves and bag them.",
                        "Each coated leaf releases huge numbers of spores.", TreatmentInteractionMode.TapRegion, 1,
                        new StateDelta { diseaseSeverity = -0.10f, lesionDensity = -0.20f, yellowing = -0.08f }),
                    CreateStep("pep_pm_air", "Open up the plants", "Space and prune lightly so air moves through.",
                        "Moving air lowers the humidity the fungus needs.", TreatmentInteractionMode.AdjustSetting, 2,
                        new StateDelta { diseaseSeverity = -0.08f, healthyGrowth = 0.05f }),
                    CreateStepWithChemical("pep_pm_spray", "Apply sulphur or potassium bicarbonate", "Spray a registered product, following the label.",
                        "Stops new infections on clean leaves.", 3,
                        new StateDelta { diseaseSeverity = -0.16f, lesionDensity = -0.18f, healthyGrowth = 0.06f })),
                Prevention("Pepper_PowderyMildew", "pepper_powdery_mildew_prevention",
                    Measure(PreventionCategory.AirCirculation, "Give plants space", "Crowded, still air is where mildew thrives."),
                    Measure(PreventionCategory.CropManagement, "Avoid excess nitrogen", "Soft, lush growth is the most susceptible."),
                    Measure(PreventionCategory.Monitoring, "Check leaf undersides weekly", "The white growth starts underneath.")),
                SimProfile("Pepper_PowderyMildew", 7f, $"{Masks}/T_LesionMask_LateBlight.png",
                    new[] { new Color(0.86f, 0.88f, 0.84f), new Color(0.80f, 0.84f, 0.62f), new Color(0.85f, 0.82f, 0.45f) }));

            return Plant("pepper", "Bell pepper", "Capsicum annuum (bell and chilli peppers).", features, PepperProfile(), bacterialSpot, powderyMildew);
        }

        static PlantData BuildApple(FeatureData[] features)
        {
            var scab = Disease("Apple", "apple_scab", "Apple Scab",
                new[] { "Apple___Apple_scab", "Apple___Scab", "apple_scab", "scab" }, PathogenType.Fungal,
                "A fungal disease (Venturia inaequalis) of apple leaves and fruit.",
                "Cool, wet spring weather; the fungus overwinters in fallen leaves.",
                new[] { "Olive-green to brown velvety spots", "Spots merge and leaves yellow", "Cracked, scabby fruit" },
                "Infected leaves drop early; fruit is blemished and cracks.",
                "Repeated years of leaf loss weaken the tree and cut cropping.",
                Plan("Apple_Scab", "apple_scab_treatment", "Apple — Scab", 0.15f, 0.10f,
                    CreateStep("app_sc_leaves", "Rake up fallen leaves", "Collect and destroy fallen leaves under the tree.",
                        "Next spring's spores come from last year's leaves.", TreatmentInteractionMode.TapRegion, 2,
                        new StateDelta { diseaseSeverity = -0.10f, lesionDensity = -0.15f }),
                    CreateStep("app_sc_prune", "Prune for airflow", "Thin crowded branches so the canopy dries quickly.",
                        "Shorter leaf wetness means fewer infections.", TreatmentInteractionMode.AdjustSetting, 3,
                        new StateDelta { diseaseSeverity = -0.08f, healthyGrowth = 0.06f }),
                    CreateStepWithChemical("app_sc_spray", "Protective fungicide from bud break", "Spray a registered product on the label's schedule, especially before rain.",
                        "Protects new leaves and fruit during the infection season.", 4,
                        new StateDelta { diseaseSeverity = -0.20f, lesionDensity = -0.20f, healthyGrowth = 0.06f })),
                Prevention("Apple_Scab", "apple_scab_prevention",
                    Measure(PreventionCategory.CropManagement, "Choose scab-resistant varieties", "The most reliable long-term control."),
                    Measure(PreventionCategory.CropHygiene, "Clear leaves every autumn", "Removing leaf litter breaks the cycle."),
                    Measure(PreventionCategory.Monitoring, "Watch wet spring spells", "Infections happen during long wet periods; time protection before them.")),
                SimProfile("Apple_Scab", 8f, $"{Masks}/T_LesionMask_EarlyBlight.png",
                    new[] { new Color(0.25f, 0.24f, 0.12f), new Color(0.42f, 0.45f, 0.20f), new Color(0.80f, 0.76f, 0.32f) }));

            var rust = Disease("Apple", "cedar_apple_rust", "Cedar Apple Rust",
                new[] { "Apple___Cedar_apple_rust", "Apple___Rust", "apple_rust", "rust" }, PathogenType.Fungal,
                "A rust fungus that alternates between apple trees and junipers (cedars).",
                "Spores blow in from galls on nearby junipers during wet spring weather.",
                new[] { "Bright orange-yellow spots", "Tiny black dots in the spots", "Tube-like growths under the leaf" },
                "Spotted leaves drop early; fruit can be spotted and deformed.",
                "Repeated defoliation weakens the tree.",
                Plan("Apple_Rust", "apple_rust_treatment", "Apple — Cedar Apple Rust", 0.15f, 0.08f,
                    CreateStep("app_ru_leaves", "Remove heavily spotted leaves", "Pick off the worst leaves within reach and bag them.",
                        "Reduces the visible damage and the stress on the tree.", TreatmentInteractionMode.TapRegion, 2,
                        new StateDelta { diseaseSeverity = -0.08f, lesionDensity = -0.15f, yellowing = -0.06f }),
                    CreateStep("app_ru_juniper", "Deal with nearby junipers", "Remove orange galls from nearby junipers where possible.",
                        "The fungus cannot complete its cycle without junipers.", TreatmentInteractionMode.AdjustSetting, 3,
                        new StateDelta { diseaseSeverity = -0.10f }),
                    CreateStepWithChemical("app_ru_spray", "Protective fungicide in spring", "Spray a registered product on the label's schedule.",
                        "Protects new leaves while spores are flying.", 3,
                        new StateDelta { diseaseSeverity = -0.18f, lesionDensity = -0.18f, healthyGrowth = 0.06f })),
                Prevention("Apple_Rust", "apple_rust_prevention",
                    Measure(PreventionCategory.CropManagement, "Plant rust-resistant varieties", "Many varieties barely show rust."),
                    Measure(PreventionCategory.EnvironmentalManagement, "Avoid junipers nearby", "Spores travel far, but distance still helps."),
                    Measure(PreventionCategory.Monitoring, "Scout after wet spring weather", "Orange spots appear one to two weeks after infection.")),
                SimProfile("Apple_Rust", 7f, $"{Masks}/T_LesionMask_EarlyBlight.png",
                    new[] { new Color(0.93f, 0.52f, 0.10f), new Color(0.98f, 0.85f, 0.30f), new Color(0.86f, 0.78f, 0.28f) }));

            return Plant("apple", "Apple", "Malus domestica.", features, AppleProfile(), scab, rust, PlantVillageDataBuilder.AppleBlackRot());
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        internal static PlantData Plant(string id, string name, string description, FeatureData[] features, SpeciesProfile profile, params DiseaseData[] diseases)
        {
            var plant = CreateAsset<PlantData>($"{Root}/Plants/Plant_{name.Replace(" ", "")}.asset");
            plant.plantId = id;
            plant.plantName = name;
            plant.description = description;
            plant.features = features;
            plant.diseases = diseases;
            plant.species = profile;
            EditorUtility.SetDirty(plant);
            return plant;
        }

        internal static DiseaseData Disease(string plant, string id, string name, string[] aliases, PathogenType type,
            string description, string causes, string[] symptoms, string effect, string untreated,
            TreatmentPlan plan, PreventionPlan prevention, SimulationProfile profile)
        {
            var d = CreateAsset<DiseaseData>($"{Root}/Diseases/Disease_{plant}{name.Replace(" ", "")}.asset");
            d.diseaseId = id;
            d.diseaseName = name;
            d.aiLabelAliases = aliases;
            d.pathogenType = type;
            d.description = description;
            d.causes = causes;
            d.symptoms = symptoms;
            d.effectOnPlant = effect;
            d.untreatedOutcome = untreated;
            d.supportingFeatureIds = new[] { "leaf_spots", "yellowing", "leaf_color" };
            d.treatmentPlan = plan;
            d.preventionPlan = prevention;
            d.simulationProfile = profile;
            EditorUtility.SetDirty(d);
            return d;
        }

        internal static TreatmentPlan Plan(string key, string id, string name, float severityFloor, float damageFloor, params TreatmentStep[] steps)
        {
            var p = CreateAsset<TreatmentPlan>($"{Root}/Treatments/TreatmentPlan_{key}.asset");
            p.treatmentId = id;
            p.planName = name;
            p.steps = steps;
            p.residualSeverityFloor = severityFloor;
            p.residualDamageFloor = damageFloor;
            EditorUtility.SetDirty(p);
            return p;
        }

        internal static PreventionPlan Prevention(string key, string id, params PreventionMeasure[] measures)
        {
            var p = CreateAsset<PreventionPlan>($"{Root}/Treatments/PreventionPlan_{key}.asset");
            p.preventionId = id;
            p.measures = measures;
            EditorUtility.SetDirty(p);
            return p;
        }

        /// <param name="colors">lesion, halo, chlorosis; null keeps the material's colours.</param>
        internal static SimulationProfile SimProfile(string key, float seconds, string lesionMaskPath, Color[] colors)
        {
            var p = CreateAsset<SimulationProfile>($"{Root}/Diseases/SimProfile_{key}.asset");
            p.progressionDurationSeconds = seconds;
            p.lesionMask = AssetDatabase.LoadAssetAtPath<Texture2D>(lesionMaskPath);
            p.yellowingMask = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Masks}/T_YellowingMask.png");
            p.overrideColors = colors != null && colors.Length == 3;
            if (p.overrideColors)
            {
                p.lesionColor = colors[0];
                p.haloColor = colors[1];
                p.yellowColor = colors[2];
            }
            EditorUtility.SetDirty(p);
            return p;
        }
    }
}
#endif
