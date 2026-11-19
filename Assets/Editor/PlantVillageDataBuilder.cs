#if UNITY_EDITOR
using System.Collections.Generic;
using PlantDoctor.Core;
using PlantDoctor.Diseases;
using PlantDoctor.Plants;
using PlantDoctor.Treatment;
using UnityEditor;
using UnityEngine;
using static PlantDoctor.EditorTools.DemoDataBuilder;
using static PlantDoctor.EditorTools.SpeciesDataBuilder;

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// The rest of the PlantVillage dataset (38 classes): ten more crops - blueberry, cherry, corn, grape, orange, peach, raspberry,
    /// soybean, squash, strawberry - and the diseases PlantVillage has for them and for tomato and apple. Blueberry, raspberry and
    /// soybean have only a "healthy" class in PlantVillage, so they have no diseases here; orange and squash have no healthy class.
    ///
    /// Twin sizes follow the published proportions in docs/PLANT_MORPHOLOGY_SOURCES.md (leaf length against plant height, fruit
    /// against leaf, petiole against blade). Disease copy is general horticultural knowledge; product names stay TODO[DATA] and
    /// nothing gives a dose. Every disease's PlantVillage label is one of its aliases so a model trained on PlantVillage resolves.
    /// </summary>
    public static class PlantVillageDataBuilder
    {
        const string Masks = "Assets/Textures/Generated";
        const string Photos = "Assets/Art/Photos";

        static string Mask(string name) => $"{Masks}/{name}.png";

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        static StateDelta Remove => new StateDelta { diseaseSeverity = -0.10f, lesionDensity = -0.22f, yellowing = -0.08f, leafDamage = 0.04f };
        static StateDelta Cultural => new StateDelta { diseaseSeverity = -0.08f, yellowing = -0.04f, healthyGrowth = 0.06f };
        static StateDelta Protect => new StateDelta { diseaseSeverity = -0.17f, lesionDensity = -0.17f, healthyGrowth = 0.06f };
        static StateDelta Small => new StateDelta { diseaseSeverity = -0.05f, healthyGrowth = 0.03f };

        /// <summary>How the disease looks on the twin: its lesion pattern, chlorosis pattern, colours and how far each symptom goes.</summary>
        static SimulationProfile Look(string key, float seconds, string lesionMask, string yellowMask, Color lesion, Color halo, Color yellow,
            float lesions = 0.9f, float yellowing = 0.8f, float wilting = 0.5f, float damage = 0.7f)
        {
            var p = CreateAsset<SimulationProfile>($"Assets/Data/Diseases/SimProfile_{key}.asset");
            p.progressionDurationSeconds = seconds;
            p.lesionMask = AssetDatabase.LoadAssetAtPath<Texture2D>(Mask(lesionMask));
            p.yellowingMask = AssetDatabase.LoadAssetAtPath<Texture2D>(Mask(yellowMask));
            p.overrideColors = true;
            p.lesionColor = lesion; p.haloColor = halo; p.yellowColor = yellow;
            p.fullExpression = new PlantHealthState
            {
                diseaseSeverity = 1f, lesionDensity = lesions, yellowing = yellowing, wilting = wilting, leafDamage = damage, healthyGrowth = 0.05f
            };
            EditorUtility.SetDirty(p);
            return p;
        }

        static DiseaseData Make(string plant, string id, string name, string label, PathogenType type,
            string description, string causes, string[] symptoms, string effect, string untreated,
            float severityFloor, float damageFloor, TreatmentStep[] steps, PreventionMeasure[] measures, SimulationProfile look)
        {
            string key = $"{plant}_{name.Replace(" ", "")}";
            var plan = Plan(key, $"{plant.ToLowerInvariant()}_{id}_treatment", $"{plant} — {name}", severityFloor, damageFloor, steps);
            var prevention = Prevention(key, $"{plant.ToLowerInvariant()}_{id}_prevention", measures);
            return Disease(plant, id, name, new[] { label, $"{plant.ToLowerInvariant()}_{id}" }, type,
                description, causes, symptoms, effect, untreated, plan, prevention, look);
        }

        // ------------------------------------------------------------------
        // Tomato: the seven further PlantVillage diseases
        // ------------------------------------------------------------------

        public static DiseaseData[] MoreTomatoDiseases() => new[]
        {
            Make("Tomato", "bacterial_spot", "Bacterial Spot", "Tomato___Bacterial_spot", PathogenType.Bacterial,
                "A bacterial disease (Xanthomonas species) of tomato and pepper leaves, stems and fruit.",
                "Warm, wet weather; rain or overhead-water splash; infected seed or transplants.",
                new[] { "Small dark, greasy spots on leaves", "Yellow halo around spots", "Raised scabby spots on green fruit" },
                "Spotted leaves yellow and drop; fruit is blemished.",
                "Heavy leaf drop exposes fruit to sunscald and cuts yield.",
                0.18f, 0.12f, new[]
                {
                    CreateStep("tom_bs_remove", "Remove spotted leaves", "Pick off the worst leaves when the plants are dry and bag them.",
                        "Fewer bacteria are left to splash onto new growth.", TreatmentInteractionMode.TapRegion, 2, Remove),
                    CreateStep("tom_bs_water", "Water at the base", "Stop overhead watering and do not handle wet plants.",
                        "The bacteria spread in water droplets and on wet hands and tools.", TreatmentInteractionMode.AdjustSetting, 2, Cultural),
                    CreateStepWithChemical("tom_bs_copper", "Apply a copper spray", "Spray a registered copper product, following the label.",
                        "Copper slows the spread to new leaves; it cannot cure spotted ones.", 3, Protect)
                },
                new[]
                {
                    Measure(PreventionCategory.CropManagement, "Use clean seed and transplants", "Most outbreaks start from infected seed or seedlings."),
                    Measure(PreventionCategory.WaterManagement, "Water with drip, not sprinklers", "Dry leaves stop the bacteria spreading."),
                    Measure(PreventionCategory.CropHygiene, "Disinfect tools and stakes", "Bacteria survive on surfaces between seasons."),
                    Measure(PreventionCategory.CropManagement, "Rotate away from tomato and pepper", "Leave two seasons before planting either in the same ground.")
                },
                Look("Tomato_BacterialSpot", 7f, "T_LesionMask_SmallSpots", "T_YellowingMask",
                    new Color(0.13f, 0.10f, 0.06f), new Color(0.86f, 0.80f, 0.32f), new Color(0.82f, 0.76f, 0.32f))),

            Make("Tomato", "leaf_mold", "Leaf Mold", "Tomato___Leaf_Mold", PathogenType.Fungal,
                "A fungal disease (Passalora fulva) of tomato leaves, mostly in greenhouses and tunnels.",
                "Very humid air (above about 85%) and leaves that stay wet; poor ventilation; spores survive on debris and structures.",
                new[] { "Pale green to yellow patches on the upper leaf", "Olive-green to brown velvety mould underneath", "Leaves curl, wither and drop" },
                "Infected leaves lose their green tissue and drop, starting low on the plant.",
                "Heavy leaf loss in a humid house cuts fruit size and yield.",
                0.15f, 0.10f, new[]
                {
                    CreateStep("tom_lm_vent", "Lower the humidity", "Ventilate, space the plants and avoid wetting the leaves; heat a little at night under cover if needed.",
                        "The fungus needs very humid air; below about 85% it barely spreads.", TreatmentInteractionMode.AdjustSetting, 2, Cultural),
                    CreateStep("tom_lm_remove", "Remove the worst leaves", "Take off heavily mouldy lower leaves and bag them.",
                        "Each mouldy leaf releases spores into the house.", TreatmentInteractionMode.TapRegion, 2, Remove),
                    CreateStepWithChemical("tom_lm_spray", "Apply a protectant fungicide", "Spray a registered product for leaf mould, following the label.",
                        "Protects new leaves while conditions are being corrected.", 3, Protect)
                },
                new[]
                {
                    Measure(PreventionCategory.AirCirculation, "Ventilate tunnels and greenhouses", "Keep the air moving and the humidity down, especially at night."),
                    Measure(PreventionCategory.CropManagement, "Grow resistant varieties", "Many varieties carry resistance to leaf mould."),
                    Measure(PreventionCategory.CropHygiene, "Clean the house between crops", "Spores survive on debris, stakes and the structure itself.")
                },
                Look("Tomato_LeafMold", 8f, "T_LesionMask_Colony", "T_YellowingMask",
                    new Color(0.42f, 0.40f, 0.18f), new Color(0.82f, 0.80f, 0.36f), new Color(0.84f, 0.80f, 0.36f), lesions: 0.8f, yellowing: 0.9f)),

            Make("Tomato", "septoria_leaf_spot", "Septoria Leaf Spot", "Tomato___Septoria_leaf_spot", PathogenType.Fungal,
                "A fungal disease (Septoria lycopersici) that covers tomato leaves in many small spots.",
                "Warm, wet weather; spores splash up from infected debris and soil onto the lower leaves.",
                new[] { "Many small round spots with grey-tan centres", "Dark borders round the spots", "Tiny black dots in the spot centres", "Lower leaves first" },
                "Spotted leaves yellow and drop from the bottom of the plant upward.",
                "Severe defoliation weakens the plant and leaves fruit exposed to sunscald.",
                0.15f, 0.10f, new[]
                {
                    CreateStep("tom_sl_remove", "Remove spotted lower leaves", "Pick off spotted lower leaves and bag them.",
                        "Removes the spores that splash up to healthy leaves.", TreatmentInteractionMode.TapRegion, 2, Remove),
                    CreateStep("tom_sl_mulch", "Mulch and water at the base", "Mulch the soil and water at the base, not over the leaves.",
                        "Mulch stops spores splashing up from the soil.", TreatmentInteractionMode.AdjustSetting, 2, Cultural),
                    CreateStepWithChemical("tom_sl_spray", "Apply a protectant fungicide", "Spray a registered product, following the label.",
                        "Protects new leaves from infection.", 3, Protect)
                },
                new[]
                {
                    Measure(PreventionCategory.CropHygiene, "Clear tomato debris after harvest", "The fungus survives on old plant remains."),
                    Measure(PreventionCategory.CropManagement, "Rotate for at least a year", "Keep tomatoes out of infected ground for a season or more."),
                    Measure(PreventionCategory.WaterManagement, "Keep leaves dry", "Water at the base early in the day.")
                },
                Look("Tomato_SeptoriaLeafSpot", 7f, "T_LesionMask_SmallSpots", "T_YellowingMask",
                    new Color(0.56f, 0.50f, 0.40f), new Color(0.28f, 0.17f, 0.10f), new Color(0.82f, 0.76f, 0.34f))),

            Make("Tomato", "spider_mites", "Spider Mites", "Tomato___Spider_mites Two-spotted_spider_mite", PathogenType.Pest,
                "Two-spotted spider mites (Tetranychus urticae): tiny sap-sucking mites, not a disease.",
                "Hot, dry weather and dusty, water-stressed plants; mites multiply very fast in heat.",
                new[] { "Fine pale stippling (tiny dots) on the leaves", "Leaves turn yellow-bronze and dry", "Fine webbing under leaves and at the tips" },
                "Feeding empties leaf cells; leaves bronze, dry out and drop.",
                "Heavy infestations can strip and kill plants in hot weather.",
                0.12f, 0.08f, new[]
                {
                    CreateStep("tom_sm_rinse", "Hose the mites off", "Spray the undersides of the leaves with a strong jet of water.",
                        "Knocks mites and webbing off and raises the humidity they dislike.", TreatmentInteractionMode.AdjustSetting, 2, Cultural),
                    CreateStep("tom_sm_remove", "Remove the worst leaves", "Take off heavily webbed leaves and bag them.",
                        "Removes many mites and eggs at once.", TreatmentInteractionMode.TapRegion, 1, Remove),
                    CreateStepWithChemical("tom_sm_treat", "Use a registered miticide or soap", "Apply insecticidal soap, oil or a registered miticide, following the label; spare predatory mites.",
                        "Reduces the population while natural enemies recover.", 3, Protect)
                },
                new[]
                {
                    Measure(PreventionCategory.WaterManagement, "Avoid drought stress", "Water-stressed plants suffer most."),
                    Measure(PreventionCategory.Monitoring, "Check leaf undersides in hot weather", "Look for stippling and webbing every few days."),
                    Measure(PreventionCategory.EnvironmentalManagement, "Protect natural enemies", "Avoid broad-spectrum sprays that kill predatory mites.")
                },
                Look("Tomato_SpiderMites", 6f, "T_LesionMask_Pustule", "T_YellowMask_Mottle",
                    new Color(0.86f, 0.84f, 0.62f), new Color(0.78f, 0.70f, 0.40f), new Color(0.80f, 0.66f, 0.30f), lesions: 0.95f, yellowing: 0.85f, wilting: 0.6f, damage: 0.5f)),

            Make("Tomato", "target_spot", "Target Spot", "Tomato___Target_Spot", PathogenType.Fungal,
                "A fungal disease (Corynespora cassiicola) of tomato leaves, stems and fruit.",
                "Warm, humid weather with long leaf wetness; dense canopies; spores survive on debris.",
                new[] { "Brown spots with concentric rings", "Light brown centres that may crack", "Yellow halo round the spots" },
                "Spots merge and leaves drop, starting in the lower canopy.",
                "Defoliation and fruit spots reduce yield and quality.",
                0.15f, 0.10f, new[]
                {
                    CreateStep("tom_ts_remove", "Remove spotted leaves", "Remove infected lower leaves and bag them.",
                        "Reduces the spores in the canopy.", TreatmentInteractionMode.TapRegion, 2, Remove),
                    CreateStep("tom_ts_air", "Open up the canopy", "Prune lower leaves and side shoots so air moves through.",
                        "Leaves dry faster, so fewer spores infect.", TreatmentInteractionMode.AdjustSetting, 2, Cultural),
                    CreateStepWithChemical("tom_ts_spray", "Apply a protectant fungicide", "Spray a registered product, following the label.",
                        "Protects new growth.", 3, Protect)
                },
                new[]
                {
                    Measure(PreventionCategory.CropHygiene, "Remove crop debris", "The fungus survives on old leaves and stems."),
                    Measure(PreventionCategory.AirCirculation, "Space and prune", "A dry, open canopy is the best defence."),
                    Measure(PreventionCategory.CropManagement, "Rotate crops", "Avoid following tomato with tomato.")
                },
                Look("Tomato_TargetSpot", 8f, "T_LesionMask_EarlyBlight", "T_YellowingMask",
                    new Color(0.40f, 0.27f, 0.16f), new Color(0.80f, 0.74f, 0.34f), new Color(0.80f, 0.72f, 0.33f))),

            Make("Tomato", "yellow_leaf_curl_virus", "Yellow Leaf Curl Virus", "Tomato___Tomato_Yellow_Leaf_Curl_Virus", PathogenType.Viral,
                "A virus (TYLCV, a begomovirus) spread by the silverleaf whitefly (Bemisia tabaci).",
                "Whiteflies that fed on infected plants carry the virus to healthy ones; warm weather favours them.",
                new[] { "Leaves curl upward and cup", "Yellow leaf edges", "Small, crumpled new leaves; stunted plants", "Flowers drop, few fruit" },
                "Infected plants stop growing and set little or no fruit.",
                "There is no cure: infected plants stay infected and are a source for the whiteflies.",
                0.45f, 0.30f, new[]
                {
                    CreateStep("tom_ylcv_remove", "Remove infected plants", "Pull out clearly infected plants and bag them right away.",
                        "They cannot be cured and they feed the whiteflies that spread the virus.", TreatmentInteractionMode.GrabAndRemove, 1, Small),
                    CreateStep("tom_ylcv_whitefly", "Control the whiteflies", "Use yellow sticky traps and insect-proof mesh; treat whiteflies with a registered product if needed.",
                        "Stopping the carrier is the only way to protect the remaining plants.", TreatmentInteractionMode.AdjustSetting, 3, Small),
                    CreateStep("tom_ylcv_weeds", "Clear weeds and volunteers", "Remove weeds and self-sown tomatoes around the crop.",
                        "They can hold both the virus and the whiteflies.", TreatmentInteractionMode.TapRegion, 2, Small)
                },
                new[]
                {
                    Measure(PreventionCategory.CropManagement, "Plant resistant varieties", "TYLCV-resistant varieties are the most reliable protection."),
                    Measure(PreventionCategory.EnvironmentalManagement, "Use insect-proof mesh on seedlings", "Keep whiteflies off young plants."),
                    Measure(PreventionCategory.Monitoring, "Watch for whiteflies", "Check leaf undersides and sticky traps weekly.")
                },
                Look("Tomato_YellowLeafCurl", 9f, "T_LesionMask_Colony", "T_YellowingMask",
                    new Color(0.84f, 0.80f, 0.34f), new Color(0.70f, 0.74f, 0.30f), new Color(0.90f, 0.84f, 0.30f), lesions: 0.35f, yellowing: 1f, wilting: 0.8f, damage: 0.3f)),

            Make("Tomato", "mosaic_virus", "Mosaic Virus", "Tomato___Tomato_mosaic_virus", PathogenType.Viral,
                "Tomato mosaic virus (ToMV), a very stable virus spread on hands, tools and seed.",
                "Handling infected then healthy plants; infected seed; the virus survives for a long time in plant debris.",
                new[] { "Light and dark green mottling (mosaic)", "Distorted, sometimes fern-like leaves", "Uneven ripening of fruit" },
                "Infected plants grow poorly and yield less.",
                "There is no cure; the virus spreads easily to the rest of the crop.",
                0.45f, 0.30f, new[]
                {
                    CreateStep("tom_mv_remove", "Remove infected plants", "Pull out infected plants, bag them and do not compost them.",
                        "They cannot be cured and are the source for the rest.", TreatmentInteractionMode.GrabAndRemove, 1, Small),
                    CreateStep("tom_mv_hygiene", "Clean hands and tools", "Wash hands and disinfect tools before touching healthy plants.",
                        "The virus spreads by touch; this stops most new infections.", TreatmentInteractionMode.AdjustSetting, 2, Small),
                    CreateStep("tom_mv_handle", "Handle infected rows last", "Work in healthy plants first and avoid brushing past plants.",
                        "Each contact can carry the virus on.", TreatmentInteractionMode.Informational, 2, Small)
                },
                new[]
                {
                    Measure(PreventionCategory.CropManagement, "Use certified seed and resistant varieties", "Many varieties resist tomato mosaic virus."),
                    Measure(PreventionCategory.CropHygiene, "Disinfect tools and hands", "Especially when pruning and tying."),
                    Measure(PreventionCategory.CropHygiene, "Clear debris between crops", "The virus survives in old plant material.")
                },
                Look("Tomato_MosaicVirus", 9f, "T_YellowMask_Mottle", "T_YellowMask_Mottle",
                    new Color(0.60f, 0.72f, 0.30f), new Color(0.30f, 0.46f, 0.18f), new Color(0.78f, 0.82f, 0.36f), lesions: 0.6f, yellowing: 0.7f, wilting: 0.4f, damage: 0.2f)),
        };

        // ------------------------------------------------------------------
        // Apple: black rot
        // ------------------------------------------------------------------

        public static DiseaseData AppleBlackRot() =>
            Make("Apple", "black_rot", "Black Rot", "Apple___Black_rot", PathogenType.Fungal,
                "A fungal disease (Botryosphaeria obtusa) of apple leaves, fruit and branches.",
                "Spores from cankers, dead wood and mummified fruit spread in warm, wet weather.",
                new[] { "'Frog-eye' leaf spots: brown centres with purple borders", "Rotting fruit with dark rings", "Sunken cankers on branches" },
                "Leaf spots cause some leaf drop; fruit rots on the tree.",
                "Cankers weaken branches, and rotting fruit is lost.",
                0.18f, 0.12f, new[]
                {
                    CreateStep("app_br_prune", "Prune out cankers and dead wood", "Cut out cankered and dead branches in dry weather and remove them.",
                        "Cankers and dead wood are the main spore sources.", TreatmentInteractionMode.TapRegion, 2, Remove),
                    CreateStep("app_br_mummies", "Remove mummified fruit", "Pick off shrivelled fruit from the tree and the ground.",
                        "Mummies carry the fungus from one year to the next.", TreatmentInteractionMode.GrabAndRemove, 2, Cultural),
                    CreateStepWithChemical("app_br_spray", "Protective fungicide in the season", "Spray a registered product on the label's schedule.",
                        "Protects leaves and fruit while spores are flying.", 3, Protect)
                },
                new[]
                {
                    Measure(PreventionCategory.CropHygiene, "Remove dead wood and fruit mummies", "Do it every winter."),
                    Measure(PreventionCategory.CropManagement, "Avoid wounds and stress", "Healthy, unwounded trees resist infection."),
                    Measure(PreventionCategory.Monitoring, "Check for cankers when pruning", "Cut well below any canker.")
                },
                Look("Apple_BlackRot", 8f, "T_LesionMask_SmallSpots", "T_YellowingMask",
                    new Color(0.46f, 0.32f, 0.22f), new Color(0.42f, 0.16f, 0.26f), new Color(0.80f, 0.74f, 0.34f), yellowing: 0.5f));

        // ------------------------------------------------------------------
        // The ten new crops
        // ------------------------------------------------------------------

        public static PlantData[] BuildNewCrops(FeatureData[] features) => new[]
        {
            Plant("blueberry", "Blueberry", "Vaccinium corymbosum (highbush blueberry).", features, BlueberryProfile()),
            Plant("cherry", "Cherry", "Prunus avium (sweet cherry).", features, CherryProfile(), CherryPowderyMildew()),
            Plant("corn", "Corn", "Zea mays (maize).", features, CornProfile(), CornGrayLeafSpot(), CornCommonRust(), CornNorthernLeafBlight()),
            Plant("grape", "Grape", "Vitis vinifera (grapevine).", features, GrapeProfile(), GrapeBlackRot(), GrapeEsca(), GrapeLeafBlight()),
            Plant("orange", "Orange", "Citrus x sinensis (sweet orange).", features, OrangeProfile(), OrangeGreening()),
            Plant("peach", "Peach", "Prunus persica.", features, PeachProfile(), PeachBacterialSpot()),
            Plant("raspberry", "Raspberry", "Rubus idaeus.", features, RaspberryProfile()),
            Plant("soybean", "Soybean", "Glycine max.", features, SoybeanProfile()),
            Plant("squash", "Squash", "Cucurbita pepo (zucchini and other summer squash).", features, SquashProfile(), SquashPowderyMildew()),
            Plant("strawberry", "Strawberry", "Fragaria x ananassa.", features, StrawberryProfile(), StrawberryLeafScorch()),
        };

        // ---- species profiles: sizes from docs/PLANT_MORPHOLOGY_SOURCES.md ----

        static SpeciesProfile BlueberryProfile()
        {
            // Highbush blueberry: 1-5 m shrub, blades 1.5-7 cm (FNA). Twin = a 1.8 m bush at 0.61 scale.
            var p = Profile("blueberry", "Blueberry", TwinArchetype.Tree, 71);
            p.height = 1.1f;
            Roots(p, 12, 0.32f, 0.007f, 0.12f, 0.04f);
            p.rootHairs = false;                       // blueberry roots have no root hairs (they rely on mycorrhizal fungi)
            p.rootTint = new Color(0.86f, 0.74f, 0.70f);
            p.trunkCount = 7; p.trunkSplay = 14f; p.trunkHeight = 0.72f; p.trunkRadius = 0.012f;   // stems tall enough that the bush reaches its 1.1 m
            p.branchLevels = 2; p.branchesPerLevel = 3; p.canopyRadius = 0.42f; p.leavesPerTip = 18;
            p.barkColor = new Color(0.62f, 0.46f, 0.42f);
            p.leafForm = LeafForm.Simple; p.leafSize = new Vector2(0.034f, 0.026f); p.leafDroop = 0.04f;
            p.leafTint = new Color(0.96f, 1f, 0.95f);
            Photos(p, "blueberry", 0.06f);
            p.fruit = FruitForm.Berry; p.fruitCount = 20; p.fruitCluster = 7; p.fruitRadius = 0.0045f;
            p.fruitDisplayScale = 1.4f;   // exception for visibility (user request)
            p.fruitColors = new[] { new Color(0.24f, 0.30f, 0.50f), new Color(0.30f, 0.36f, 0.58f), new Color(0.50f, 0.58f, 0.36f) };
            return Save(p);
        }

        static SpeciesProfile CherryProfile()
        {
            // Sweet cherry: blades 3-13 cm (Flora of China, Cerasus avium); a ~4 m orchard tree shown at 0.33 scale.
            var p = Profile("cherry", "Cherry", TwinArchetype.Tree, 83);
            p.height = 1.3f;
            Roots(p, 9, 0.55f, 0.022f, 0.16f, 0.055f);
            p.rootTint = new Color(0.80f, 0.70f, 0.64f);
            p.trunkHeight = 0.52f; p.trunkRadius = 0.034f; p.branchLevels = 2; p.branchesPerLevel = 4;
            p.canopyRadius = 0.40f; p.leavesPerTip = 70;
            p.canopyLeafScale = 1.8f;   // exception for a lush crown (user request); leafSize stays the real proportion
            p.barkColor = new Color(0.62f, 0.44f, 0.40f);
            p.leafForm = LeafForm.Simple; p.leafSize = new Vector2(0.030f, 0.022f); p.leafDroop = 0.06f;
            p.leafTint = new Color(0.96f, 1f, 0.94f);
            Photos(p, "cherry", 0.07f);
            p.fruit = FruitForm.Cherry; p.fruitCount = 24; p.fruitCluster = 3; p.fruitRadius = 0.0034f;
            p.fruitDisplayScale = p.canopyLeafScale;   // drawn at the leaves' display scale so fruit and leaves keep their real proportion
            p.fruitColors = new[] { new Color(0.82f, 0.07f, 0.10f), new Color(0.70f, 0.05f, 0.09f), new Color(0.90f, 0.16f, 0.12f) };   // bright enough to read against the leaves
            return Save(p);
        }

        static SpeciesProfile CornProfile()
        {
            // Maize: culms 1-4 m, blades 50-90 x 3-12 cm (Flora of China). A 2.5 m plant at 0.64 scale.
            var p = Profile("corn", "Corn", TwinArchetype.Grass, 97);
            p.height = 1.6f;
            Roots(p, 14, 0.30f, 0.004f, 0.08f, 0.03f);
            p.braceRoots = 10;                         // brace (prop) roots from the lowest nodes
            p.grassTassel = true;                      // the male tassel on top of the stalk
            p.stemRadius = 0.009f; p.stemColor = new Color(0.62f, 0.72f, 0.40f); p.firstLeafHeight = 0.06f;
            p.leafForm = LeafForm.Strap; p.leafCount = 13; p.leafSize = new Vector2(0.44f, 0.30f); p.leafFold = 0.04f;
            p.leafTint = new Color(0.98f, 1f, 0.96f);
            Photos(p, "corn", 0.04f);
            p.fruit = FruitForm.CornEar; p.fruitCount = 2; p.fruitRadius = 0.048f;   // one or two ears per stalk
            p.fruitColors = new[] { new Color(0.95f, 0.80f, 0.30f) };   // kernels; the husk round them is the green calyx
            return Save(p);
        }

        static SpeciesProfile GrapeProfile()
        {
            // Grapevine: blades 7-18 x 6-16 cm, petiole 4-9 cm (Flora of China); a trained vine of ~1.9 m at 0.68 scale.
            var p = Profile("grape", "Grape", TwinArchetype.Vine, 101);
            p.height = 1.3f;
            Roots(p, 9, 0.45f, 0.016f, 0.14f, 0.05f);
            p.trunkHeight = 0.75f; p.trunkRadius = 0.022f; p.canopyRadius = 0.45f;
            p.barkColor = new Color(0.50f, 0.40f, 0.34f);
            p.stemRadius = 0.007f; p.stemColor = new Color(0.60f, 0.62f, 0.38f);
            p.leafForm = LeafForm.Simple; p.leafCount = 64; p.petioleLength = 0.52f; p.leafSize = new Vector2(0.13f, 0.10f); p.petioleInset = 0.08f;
            p.leafDroop = 0.08f; p.leafTint = new Color(0.98f, 1f, 0.96f);
            Photos(p, "grape", 0.06f);
            p.fruit = FruitForm.GrapeBunch; p.fruitCount = 6; p.fruitRadius = 0.04f;
            p.fruitColors = new[] { new Color(0.22f, 0.10f, 0.22f), new Color(0.30f, 0.14f, 0.30f) };
            return Save(p);
        }

        static SpeciesProfile OrangeProfile()
        {
            // Sweet orange: leaves 7.5-11 cm, fruit 6-9 cm across (Flora of Pakistan); a ~3.5 m tree at 0.34 scale.
            var p = Profile("orange", "Orange", TwinArchetype.Tree, 113);
            p.height = 1.2f;
            Roots(p, 10, 0.50f, 0.020f, 0.15f, 0.05f);
            p.rootTint = new Color(0.86f, 0.76f, 0.66f);
            p.trunkHeight = 0.32f; p.trunkRadius = 0.03f; p.branchLevels = 2; p.branchesPerLevel = 5;
            p.canopyRadius = 0.48f; p.leavesPerTip = 80;
            p.canopyLeafScale = 1.7f;   // exception for a lush crown (user request); leafSize stays the real proportion
            p.barkColor = new Color(0.56f, 0.53f, 0.46f);
            p.leafForm = LeafForm.Simple; p.leafSize = new Vector2(0.031f, 0.025f); p.leafDroop = 0.03f;
            p.leafTint = new Color(0.94f, 0.98f, 0.92f);
            Photos(p, "orange", 0.05f);
            p.fruit = FruitForm.Citrus; p.fruitCount = 22; p.fruitRadius = 0.013f;
            p.fruitDisplayScale = p.canopyLeafScale;   // drawn at the leaves' display scale so fruit and leaves keep their real proportion
            p.fruitColors = new[] { new Color(0.95f, 0.52f, 0.08f), new Color(0.92f, 0.58f, 0.10f), new Color(0.55f, 0.62f, 0.18f) };
            return Save(p);
        }

        static SpeciesProfile PeachProfile()
        {
            // Peach: 3-8 m trees, blades 7-15 x 2-3.5 cm, petiole 1-2 cm (Flora of China); a ~4 m tree at 0.31 scale.
            var p = Profile("peach", "Peach", TwinArchetype.Tree, 127);
            p.height = 1.25f;
            Roots(p, 9, 0.55f, 0.022f, 0.16f, 0.055f);
            p.rootTint = new Color(0.80f, 0.70f, 0.64f);
            p.trunkHeight = 0.42f; p.trunkRadius = 0.034f; p.branchLevels = 2; p.branchesPerLevel = 4;
            p.canopyRadius = 0.48f; p.leavesPerTip = 105;   // narrow lanceolate leaves: many more of them fill the crown
            p.canopyLeafScale = 1.7f;   // exception for a lush crown (user request); leafSize stays the real proportion
            p.barkColor = new Color(0.50f, 0.37f, 0.34f);
            p.leafForm = LeafForm.Simple; p.leafSize = new Vector2(0.034f, 0.026f); p.leafDroop = 0.18f;
            p.leafTint = new Color(0.96f, 1f, 0.94f);
            Photos(p, "peach", 0.1f);
            p.fruit = FruitForm.Peach; p.fruitCount = 18; p.fruitRadius = 0.0095f;
            p.fruitDisplayScale = p.canopyLeafScale;   // drawn at the leaves' display scale so fruit and leaves keep their real proportion
            p.fruitColors = new[] { new Color(0.95f, 0.55f, 0.26f), new Color(0.86f, 0.32f, 0.20f) };
            return Save(p);
        }

        static SpeciesProfile RaspberryProfile()
        {
            // Red raspberry: 1-2 m canes, 3-7 leaflets, terminal leaflet 3-15 cm (Flora of China / FNA).
            var p = Profile("raspberry", "Raspberry", TwinArchetype.Herb, 131);
            p.height = 1.3f;
            Roots(p, 10, 0.30f, 0.006f, 0.10f, 0.035f);
            p.mainStems = 6; p.stemSplay = 9f; p.sideBranches = 0; p.stemRadius = 0.008f;   // canes rise from the crown nearly upright...
            p.woodyStems = true; p.barkColor = new Color(0.66f, 0.46f, 0.40f); p.firstLeafHeight = 0.18f; p.stemLean = 0.26f;   // ...and arch over at the top
            p.leafForm = LeafForm.Compound; p.leafletPairs = 2; p.leafCount = 66; p.leafSize = new Vector2(0.20f, 0.14f);
            p.leafDroop = 0.08f; p.leafTint = new Color(0.96f, 1f, 0.95f);
            Photos(p, "raspberry", 0.06f);
            p.fruit = FruitForm.Raspberry; p.fruitCount = 18; p.fruitCluster = 3; p.fruitRadius = 0.0075f;
            p.fruitDisplayScale = 1.4f;   // exception for visibility (user request)
            p.fruitColors = new[] { new Color(0.74f, 0.08f, 0.16f), new Color(0.84f, 0.22f, 0.28f) };
            return Save(p);
        }

        static SpeciesProfile SoybeanProfile()
        {
            // Soybean: 0.3-2 m (Flora of Pakistan), pinnately trifoliolate, petiole 2-20 cm, terminal leaflet 5-12 cm (Flora of China).
            var p = Profile("soybean", "Soybean", TwinArchetype.Herb, 137);
            p.height = 0.8f;
            Roots(p, 9, 0.22f, 0.005f, 0.08f, 0.03f);
            p.rootNodules = 45;                        // nitrogen-fixing nodules (legume)
            p.mainStems = 1; p.sideBranches = 5; p.stemRadius = 0.006f; p.stemColor = new Color(0.56f, 0.60f, 0.36f); p.stemLean = 0.06f;
            p.leafForm = LeafForm.Trifoliate; p.leafCount = 24; p.petioleLength = 1.1f; p.leafSize = new Vector2(0.18f, 0.12f);
            p.trifoliateRachis = 0.12f;   // pinnately trifoliolate: the middle leaflet on its own short stalk
            p.leafDroop = 0.06f; p.leafTint = new Color(0.96f, 1f, 0.95f);
            Photos(p, "soybean", 0.05f);
            p.fruit = FruitForm.Pod; p.fruitCount = 8; p.fruitCluster = 3; p.fruitRadius = 0.012f;
            p.fruitColors = new[] { new Color(0.60f, 0.62f, 0.36f), new Color(0.55f, 0.52f, 0.32f) };
            return Save(p);
        }

        static SpeciesProfile SquashProfile()
        {
            // Bush zucchini (Cucurbita pepo): large, ascending to erect, dark green leaves; petioles 200-370 mm, hollow; blade roundish,
            // 110-340 x 120-390 mm, palmately 5-7-lobed, cordate; stems ridged, to 25 mm across; flowers yellow-orange, 120-180 mm across,
            // staminate ones on 150-330 mm stalks (Prigge & Gibson, UCLA); plant about 75 cm tall in a bush-zucchini trial (FAO AGRIS).
            var p = Profile("squash", "Squash", TwinArchetype.Rosette, 149);
            p.height = 0.7f;
            Roots(p, 10, 0.40f, 0.008f, 0.11f, 0.04f);
            p.stemRadius = 0.0125f; p.stemColor = new Color(0.56f, 0.66f, 0.40f);
            p.crownHeight = 0.28f;   // the upright stem of a bush zucchini; leaves and (axillary) fruit grow along it
            p.petioleRise = 0.62f;   // stalks angled out at about 45 degrees: a plant wider than tall (about 0.75 m high, 1.2 m across)
            p.leafForm = LeafForm.Simple; p.leafCount = 16; p.petioleLength = 1.25f; p.leafSize = new Vector2(0.60f, 0.45f);
            p.leafDroop = 0.06f; p.leafTint = new Color(0.78f, 0.88f, 0.74f); p.petioleInset = 0.10f;   // real courgette leaf photos (Commons); the stalk joins at the notch
            Photos(p, "squash", 0.06f);
            p.flowerCount = 4; p.flowerRadius = 0.075f; p.flowerColor = new Color(0.98f, 0.68f, 0.10f);
            p.flowerForm = PlantDoctor.Plants.FlowerForm.Trumpet; p.flowerPetals = 5; p.flowerNod = 20f;
            p.flowerThroat = new Color(0.94f, 0.50f, 0.06f); p.flowerCentre = new Color(0.98f, 0.80f, 0.18f);
            p.fruit = FruitForm.Squash; p.fruitCount = 3; p.fruitRadius = 0.025f;
            p.fruitColors = new[] { new Color(0.22f, 0.40f, 0.15f), new Color(0.30f, 0.48f, 0.18f) };
            return Save(p);
        }

        static SpeciesProfile StrawberryProfile()
        {
            // Strawberry: herb 10-40 cm, petiole 2-10 cm, three leaflets 3-7 x 2-6 cm (Flora of China).
            var p = Profile("strawberry", "Strawberry", TwinArchetype.Rosette, 151);
            p.height = 0.15f;   // the leafy clump; the 10-40 cm in the flora includes the flower stalks
            Roots(p, 16, 0.14f, 0.003f, 0.06f, 0.025f);
            p.stemRadius = 0.01f; p.stemColor = new Color(0.62f, 0.56f, 0.34f);
            // Largest leaf at the top of the flora's ranges (petiole 10 cm, leaflet 7 cm), so the clump stands up instead of lying flat.
            p.leafForm = LeafForm.Trifoliate; p.leafCount = 13; p.petioleLength = 1.43f; p.leafSize = new Vector2(0.17f, 0.12f);
            p.leafDroop = 0.06f; p.leafTint = new Color(0.98f, 1f, 0.96f);
            Photos(p, "strawberry", 0.05f);
            p.fruit = FruitForm.Strawberry; p.fruitCount = 4; p.fruitRadius = 0.015f;
            // Flowers: 2-3 cm across, five round white petals round a domed yellow centre, on stalks among the leaves.
            p.flowerCount = 3; p.flowerRadius = 0.012f; p.flowerForm = PlantDoctor.Plants.FlowerForm.Open; p.flowerPetals = 5; p.flowerNod = 35f;
            p.flowerColor = new Color(0.98f, 0.98f, 0.96f); p.flowerThroat = new Color(0.95f, 0.96f, 0.86f); p.flowerCentre = new Color(0.95f, 0.80f, 0.22f);
            p.fruitColors = new[] { new Color(0.84f, 0.07f, 0.10f), new Color(0.90f, 0.20f, 0.16f), new Color(0.94f, 0.88f, 0.72f) };
            return Save(p);
        }

        // ---- diseases of the new crops ----

        static DiseaseData CherryPowderyMildew() =>
            Make("Cherry", "powdery_mildew", "Powdery Mildew", "Cherry___Powdery_mildew", PathogenType.Fungal,
                "A fungal disease (Podosphaera clandestina) of cherry leaves, shoots and fruit.",
                "Warm, humid weather with dry leaves; spores overwinter on the tree and in fallen leaves.",
                new[] { "White powdery patches on young leaves", "Leaves curl and pucker", "White patches on fruit near harvest" },
                "Infected leaves photosynthesise less and may drop; fruit is blemished.",
                "Repeated years weaken young trees and spoil fruit.",
                0.12f, 0.06f, new[]
                {
                    CreateStep("che_pm_prune", "Prune out infected shoots", "Cut off white, distorted shoot tips and bag them.",
                        "Removes the most spore-laden growth.", TreatmentInteractionMode.TapRegion, 2, Remove),
                    CreateStep("che_pm_air", "Open the canopy", "Thin crowded branches so air and light get in.",
                        "Moving air lowers the humidity the fungus needs.", TreatmentInteractionMode.AdjustSetting, 2, Cultural),
                    CreateStepWithChemical("che_pm_spray", "Apply sulphur or another registered fungicide", "Spray following the label.",
                        "Stops new infections on clean leaves.", 3, Protect)
                },
                new[]
                {
                    Measure(PreventionCategory.AirCirculation, "Prune for airflow", "An open canopy dries quickly."),
                    Measure(PreventionCategory.CropHygiene, "Clear fallen leaves", "The fungus survives on leaf litter."),
                    Measure(PreventionCategory.Monitoring, "Check young shoots in spring", "Early white patches are easy to cut out.")
                },
                Look("Cherry_PowderyMildew", 7f, "T_LesionMask_Colony", "T_YellowingMask",
                    new Color(0.88f, 0.89f, 0.86f), new Color(0.80f, 0.84f, 0.66f), new Color(0.84f, 0.82f, 0.48f), yellowing: 0.5f, damage: 0.4f));

        static DiseaseData CornGrayLeafSpot() =>
            Make("Corn", "gray_leaf_spot", "Gray Leaf Spot", "Corn___Cercospora_leaf_spot Gray_leaf_spot", PathogenType.Fungal,
                "A fungal disease (Cercospora zeae-maydis) of maize leaves.",
                "Warm, very humid weather with long dew periods; the fungus survives on maize residue left on the surface.",
                new[] { "Narrow, rectangular grey-tan lesions", "Lesions run between and stop at the veins", "Lesions join and whole leaves die" },
                "Dead leaf area cuts the sugar supply to the ear.",
                "Severe epidemics cause early death of leaves, stalk rot and yield loss.",
                0.18f, 0.12f, new[]
                {
                    CreateStep("corn_gls_scout", "Check the leaves below the ear", "Look for grey rectangular lesions on the lower leaves before tasselling.",
                        "Decisions depend on how early and how high the disease is.", TreatmentInteractionMode.TapRegion, 1, Small),
                    CreateStepWithChemical("corn_gls_spray", "Apply a registered fungicide if lesions climb", "Spray around tasselling if the disease reaches the upper leaves, following the label.",
                        "Protects the leaves that feed the ear.", 4, Protect),
                    CreateStep("corn_gls_residue", "Manage residue after harvest", "Bury or break down infected residue.",
                        "Residue is where the fungus survives.", TreatmentInteractionMode.AdjustSetting, 3, Cultural)
                },
                new[]
                {
                    Measure(PreventionCategory.CropManagement, "Choose tolerant hybrids", "Resistance varies widely between hybrids."),
                    Measure(PreventionCategory.CropManagement, "Rotate away from maize", "One year without maize reduces the residue."),
                    Measure(PreventionCategory.Monitoring, "Scout in humid summers", "The disease builds up in long humid spells.")
                },
                Look("Corn_GrayLeafSpot", 8f, "T_LesionMask_Streak", "T_YellowingMask",
                    new Color(0.62f, 0.58f, 0.48f), new Color(0.70f, 0.66f, 0.42f), new Color(0.78f, 0.74f, 0.40f), yellowing: 0.4f));

        static DiseaseData CornCommonRust() =>
            Make("Corn", "common_rust", "Common Rust", "Corn___Common_rust", PathogenType.Fungal,
                "A rust fungus (Puccinia sorghi) of maize leaves.",
                "Spores blow in on the wind; cool to moderate temperatures and heavy dew favour infection.",
                new[] { "Small cinnamon-brown powdery pustules", "Pustules on both sides of the leaf", "Pustules turn dark brown-black late in the season" },
                "Heavy rusting reduces the green leaf area.",
                "Severe early rust on susceptible maize can reduce yield.",
                0.15f, 0.10f, new[]
                {
                    CreateStep("corn_cr_scout", "Gauge how heavy the rust is", "Count pustules on the leaves around the ear.",
                        "Light rust on most field maize needs no action.", TreatmentInteractionMode.TapRegion, 1, Small),
                    CreateStepWithChemical("corn_cr_spray", "Spray only if rust is heavy early", "On susceptible sweet corn or seed crops, apply a registered fungicide following the label.",
                        "Protects the upper leaves while the ear fills.", 3, Protect),
                    CreateStep("corn_cr_note", "Note the variety for next year", "Record how this variety coped.",
                        "Resistant varieties are the main control.", TreatmentInteractionMode.Informational, 2, Cultural)
                },
                new[]
                {
                    Measure(PreventionCategory.CropManagement, "Grow resistant hybrids", "Most field maize is tolerant; many sweet corn varieties are not."),
                    Measure(PreventionCategory.Monitoring, "Watch from early summer", "Rust spores arrive on the wind from warmer regions.")
                },
                Look("Corn_CommonRust", 7f, "T_LesionMask_Pustule", "T_YellowingMask",
                    new Color(0.58f, 0.27f, 0.10f), new Color(0.82f, 0.66f, 0.30f), new Color(0.82f, 0.72f, 0.34f), yellowing: 0.4f, damage: 0.4f));

        static DiseaseData CornNorthernLeafBlight() =>
            Make("Corn", "northern_leaf_blight", "Northern Leaf Blight", "Corn___Northern_Leaf_Blight", PathogenType.Fungal,
                "A fungal disease (Exserohilum turcicum) of maize leaves.",
                "Moderate temperatures with long dew or rain; the fungus survives on maize residue.",
                new[] { "Long, cigar-shaped grey-green to tan lesions (several cm)", "Lesions start on the lower leaves", "Dark spores in the lesions in damp weather" },
                "Large lesions kill big parts of the leaves.",
                "Heavy infection before or soon after tasselling can cut yield substantially.",
                0.18f, 0.12f, new[]
                {
                    CreateStep("corn_nlb_scout", "Check the lower leaves", "Look for long grey lesions before tasselling.",
                        "Early, rising infection is the trigger to act.", TreatmentInteractionMode.TapRegion, 1, Small),
                    CreateStepWithChemical("corn_nlb_spray", "Apply a registered fungicide if needed", "Spray around tasselling following the label when lesions are on the leaves near the ear.",
                        "Protects the leaves that fill the ear.", 4, Protect),
                    CreateStep("corn_nlb_residue", "Manage residue", "Break down or bury infected residue after harvest.",
                        "Residue carries the fungus to the next crop.", TreatmentInteractionMode.AdjustSetting, 3, Cultural)
                },
                new[]
                {
                    Measure(PreventionCategory.CropManagement, "Grow resistant hybrids", "Resistance is widely available."),
                    Measure(PreventionCategory.CropManagement, "Rotate crops", "A year away from maize reduces the inoculum."),
                    Measure(PreventionCategory.Monitoring, "Scout in wet, mild summers", "That is when the disease spreads.")
                },
                Look("Corn_NorthernLeafBlight", 8f, "T_LesionMask_Cigar", "T_YellowingMask",
                    new Color(0.64f, 0.60f, 0.48f), new Color(0.44f, 0.36f, 0.24f), new Color(0.78f, 0.74f, 0.40f), yellowing: 0.35f));

        static DiseaseData GrapeBlackRot() =>
            Make("Grape", "black_rot", "Black Rot", "Grape___Black_rot", PathogenType.Fungal,
                "A fungal disease (Guignardia bidwellii) of grape leaves, shoots and berries.",
                "Warm, wet spring and early summer weather; spores come from mummified berries and old canes.",
                new[] { "Small round tan spots with dark brown borders", "Tiny black dots in the spots", "Berries rot, then shrivel into hard black mummies" },
                "Leaf spots are minor; infected berries are lost.",
                "Whole bunches can be destroyed in a wet year.",
                0.18f, 0.12f, new[]
                {
                    CreateStep("grp_br_mummies", "Remove mummified berries", "Pick off mummies from the vine and the ground and destroy them.",
                        "Mummies are the main source of next year's spores.", TreatmentInteractionMode.GrabAndRemove, 2, Remove),
                    CreateStep("grp_br_air", "Open the canopy", "Remove crowded shoots and leaves round the bunches.",
                        "Bunches and leaves dry faster.", TreatmentInteractionMode.AdjustSetting, 2, Cultural),
                    CreateStepWithChemical("grp_br_spray", "Protective fungicide from bud break to fruit set", "Spray a registered product on the label's schedule.",
                        "Berries are most at risk from flowering until a few weeks after.", 3, Protect)
                },
                new[]
                {
                    Measure(PreventionCategory.CropHygiene, "Clean up mummies every winter", "Break the cycle at its source."),
                    Measure(PreventionCategory.AirCirculation, "Train for an open canopy", "Good airflow keeps leaves and fruit dry."),
                    Measure(PreventionCategory.Monitoring, "Watch after spring rain", "Infections happen during wet spells.")
                },
                Look("Grape_BlackRot", 7f, "T_LesionMask_SmallSpots", "T_YellowingMask",
                    new Color(0.56f, 0.44f, 0.30f), new Color(0.26f, 0.16f, 0.08f), new Color(0.80f, 0.74f, 0.36f), yellowing: 0.4f));

        static DiseaseData GrapeEsca() =>
            Make("Grape", "esca", "Esca (Black Measles)", "Grape___Esca_(Black_Measles)", PathogenType.Fungal,
                "A trunk disease of grapevines caused by wood-rotting fungi (Phaeomoniella chlamydospora, Phaeoacremonium species and others).",
                "The fungi enter through pruning wounds and grow in the wood over years; leaf symptoms appear in summer.",
                new[] { "'Tiger stripes': yellow or red bands between the veins that turn brown", "Small dark spots on berries (measles)", "Sudden wilting of a whole vine in hot weather" },
                "The wood is slowly destroyed; leaves scorch and fruit is spotted.",
                "Vines decline over years and can die suddenly; there is no cure.",
                0.40f, 0.30f, new[]
                {
                    CreateStep("grp_esca_mark", "Mark the affected vines", "Tag vines showing tiger stripes this summer.",
                        "Symptoms come and go between years; marking shows the real extent.", TreatmentInteractionMode.TapRegion, 1, Small),
                    CreateStep("grp_esca_cut", "Cut back to clean wood", "In dry weather, cut affected arms or the trunk back to wood without dark streaks; retrain a new shoot.",
                        "Removing infected wood can extend the vine's life.", TreatmentInteractionMode.AdjustSetting, 3, Small),
                    CreateStep("grp_esca_wounds", "Protect pruning wounds", "Prune in dry weather, late in the dormant season, and protect large cuts with a registered wound protectant.",
                        "Wounds are how the fungi get in.", TreatmentInteractionMode.SprayApply, 3, Cultural)
                },
                new[]
                {
                    Measure(PreventionCategory.CropHygiene, "Remove and burn dead vines and prunings", "They carry the fungi."),
                    Measure(PreventionCategory.CropManagement, "Prune late and in dry weather", "Wounds heal faster and are less exposed to spores."),
                    Measure(PreventionCategory.CropManagement, "Start with clean planting material", "Infected nursery vines are a common source.")
                },
                Look("Grape_Esca", 9f, "T_LesionMask_Interveinal", "T_LesionMask_Interveinal",
                    new Color(0.52f, 0.30f, 0.16f), new Color(0.82f, 0.62f, 0.26f), new Color(0.86f, 0.74f, 0.30f), lesions: 0.55f, yellowing: 0.75f, wilting: 0.6f));

        static DiseaseData GrapeLeafBlight() =>
            Make("Grape", "leaf_blight", "Leaf Blight (Isariopsis Leaf Spot)", "Grape___Leaf_blight_(Isariopsis_Leaf_Spot)", PathogenType.Fungal,
                "A fungal leaf spot of grapevine (Pseudocercospora vitis, also called Isariopsis leaf spot).",
                "Warm, humid weather late in the season; spores survive on fallen leaves.",
                new[] { "Irregular dark red-brown spots", "Spots turn brown and dry, with a darker border", "Older leaves yellow and drop" },
                "Leaf area is lost late in the season.",
                "Early leaf drop weakens the vine and ripening.",
                0.15f, 0.10f, new[]
                {
                    CreateStep("grp_lb_remove", "Remove spotted leaves", "Take off the worst leaves and clear fallen ones.",
                        "Reduces the spores in and under the canopy.", TreatmentInteractionMode.TapRegion, 2, Remove),
                    CreateStep("grp_lb_air", "Improve airflow", "Thin crowded shoots so leaves dry quickly.",
                        "Leaf wetness drives infection.", TreatmentInteractionMode.AdjustSetting, 2, Cultural),
                    CreateStepWithChemical("grp_lb_spray", "Apply a registered fungicide", "Spray following the label if the disease is spreading.",
                        "Protects the remaining leaves.", 3, Protect)
                },
                new[]
                {
                    Measure(PreventionCategory.CropHygiene, "Clear fallen leaves", "The fungus survives on them."),
                    Measure(PreventionCategory.AirCirculation, "Keep the canopy open", "Fast-drying leaves resist infection."),
                    Measure(PreventionCategory.Monitoring, "Check older leaves late in summer", "That is when the spots start.")
                },
                Look("Grape_LeafBlight", 8f, "T_LesionMask_LateBlight", "T_YellowingMask",
                    new Color(0.36f, 0.20f, 0.12f), new Color(0.58f, 0.40f, 0.20f), new Color(0.80f, 0.72f, 0.34f)));

        static DiseaseData OrangeGreening() =>
            Make("Orange", "citrus_greening", "Citrus Greening (Huanglongbing)", "Orange___Haunglongbing_(Citrus_greening)", PathogenType.Bacterial,
                "A bacterial disease (Candidatus Liberibacter asiaticus) of citrus, spread by the Asian citrus psyllid.",
                "Psyllids that fed on an infected tree carry the bacteria to healthy ones; infected budwood also spreads it.",
                new[] { "Blotchy yellow mottling, different on each side of the midrib", "Yellow shoots", "Small, lopsided, bitter fruit that stays partly green", "Twig dieback" },
                "The bacteria block the tree's food-conducting tissue; roots and canopy decline.",
                "There is no cure: infected trees decline and become unproductive.",
                0.50f, 0.35f, new[]
                {
                    CreateStep("org_hlb_confirm", "Get the diagnosis confirmed", "Report suspected trees to the local plant-health service; a lab test confirms it.",
                        "Greening is a regulated disease in many areas, and other problems look similar.", TreatmentInteractionMode.Informational, 1, Small),
                    CreateStep("org_hlb_psyllid", "Control the psyllids", "Treat the psyllids with registered products as advised locally, and check new flushes.",
                        "Stopping the carrier protects the trees that are still healthy.", TreatmentInteractionMode.SprayApply, 3, Small),
                    CreateStep("org_hlb_remove", "Remove confirmed trees", "Remove infected trees as advised.",
                        "They cannot recover and are a source for the psyllids.", TreatmentInteractionMode.GrabAndRemove, 2, Small)
                },
                new[]
                {
                    Measure(PreventionCategory.CropManagement, "Plant certified clean trees", "Buy from registered nurseries only."),
                    Measure(PreventionCategory.Monitoring, "Check new shoots for psyllids", "Especially during flushes of new growth."),
                    Measure(PreventionCategory.CropHygiene, "Do not move citrus plants or budwood", "It is how the disease reaches new areas.")
                },
                Look("Orange_CitrusGreening", 10f, "T_YellowMask_Mottle", "T_YellowMask_Mottle",
                    new Color(0.70f, 0.74f, 0.32f), new Color(0.62f, 0.70f, 0.26f), new Color(0.86f, 0.82f, 0.34f), lesions: 0.1f, yellowing: 0.6f, wilting: 0.4f, damage: 0.1f));   // a blotchy mottle, no lesions

        static DiseaseData PeachBacterialSpot() =>
            Make("Peach", "bacterial_spot", "Bacterial Spot", "Peach___Bacterial_spot", PathogenType.Bacterial,
                "A bacterial disease (Xanthomonas arboricola pv. pruni) of peach and other stone fruit.",
                "Warm, wet, windy weather; bacteria overwinter in twig cankers and spread by rain splash.",
                new[] { "Small angular purple-brown spots on leaves", "Spot centres drop out, leaving 'shot holes'", "Yellow leaves that drop early", "Pitted, cracked fruit" },
                "Leaves drop and fruit is blemished.",
                "Repeated defoliation weakens trees and makes fruit unmarketable.",
                0.20f, 0.14f, new[]
                {
                    CreateStep("pch_bs_prune", "Prune out cankered twigs", "In dry weather, cut out twigs with dark cankers.",
                        "Cankers are where the bacteria spend the winter.", TreatmentInteractionMode.TapRegion, 2, Remove),
                    CreateStep("pch_bs_feed", "Keep the tree vigorous", "Water and feed evenly; avoid excess nitrogen.",
                        "Stressed trees lose more leaves.", TreatmentInteractionMode.AdjustSetting, 2, Cultural),
                    CreateStepWithChemical("pch_bs_copper", "Copper or another registered bactericide", "Apply following the label, usually from leaf fall to early spring.",
                        "Reduces the bacteria before new leaves appear.", 3, Protect)
                },
                new[]
                {
                    Measure(PreventionCategory.CropManagement, "Plant resistant varieties", "Peach varieties differ greatly in susceptibility."),
                    Measure(PreventionCategory.EnvironmentalManagement, "Shelter from wind", "Wind-driven rain spreads the bacteria."),
                    Measure(PreventionCategory.Monitoring, "Check leaves after storms", "New spots show a week or two later.")
                },
                Look("Peach_BacterialSpot", 7f, "T_LesionMask_SmallSpots", "T_YellowingMask",
                    new Color(0.34f, 0.17f, 0.20f), new Color(0.84f, 0.78f, 0.34f), new Color(0.84f, 0.78f, 0.34f), yellowing: 0.7f));

        static DiseaseData SquashPowderyMildew() =>
            Make("Squash", "powdery_mildew", "Powdery Mildew", "Squash___Powdery_mildew", PathogenType.Fungal,
                "A fungal disease (Podosphaera xanthii and others) of squash, pumpkin, cucumber and melon.",
                "Warm days, dense plants and shade; spores blow in on the wind and do not need wet leaves.",
                new[] { "White powdery spots that spread over the leaf", "Older leaves yellow and dry", "Stems and petioles also turn white" },
                "Leaves die early, which cuts yield and fruit quality.",
                "Plants can lose most of their leaves late in the season.",
                0.12f, 0.08f, new[]
                {
                    CreateStep("sq_pm_remove", "Remove the worst leaves", "Cut off heavily coated older leaves and bag them.",
                        "Each coated leaf releases huge numbers of spores.", TreatmentInteractionMode.TapRegion, 1, Remove),
                    CreateStep("sq_pm_space", "Give plants space and sun", "Thin crowded plants and remove weeds round them.",
                        "Shade and still air favour the fungus.", TreatmentInteractionMode.AdjustSetting, 2, Cultural),
                    CreateStepWithChemical("sq_pm_spray", "Apply sulphur, potassium bicarbonate or another registered product", "Spray following the label, covering both sides of the leaves.",
                        "Stops new infections on clean leaves.", 3, Protect)
                },
                new[]
                {
                    Measure(PreventionCategory.CropManagement, "Grow resistant varieties", "Many squash and cucumber varieties tolerate mildew."),
                    Measure(PreventionCategory.AirCirculation, "Space plants generously", "Air and light keep mildew down."),
                    Measure(PreventionCategory.Monitoring, "Check older leaves from midsummer", "Early spots are easy to deal with.")
                },
                Look("Squash_PowderyMildew", 7f, "T_LesionMask_Colony", "T_YellowingMask",
                    new Color(0.90f, 0.91f, 0.88f), new Color(0.80f, 0.84f, 0.64f), new Color(0.84f, 0.80f, 0.46f), yellowing: 0.6f, damage: 0.5f));

        static DiseaseData StrawberryLeafScorch() =>
            Make("Strawberry", "leaf_scorch", "Leaf Scorch", "Strawberry___Leaf_scorch", PathogenType.Fungal,
                "A fungal disease (Diplocarpon earlianum) of strawberry leaves, stalks and runners.",
                "Wet weather and long leaf wetness; the fungus survives on old infected leaves.",
                new[] { "Many small irregular purple spots", "Spot centres stay dark purple (no pale centre)", "Spots merge and the leaf looks scorched and dries" },
                "Leaves die back, weakening the plant.",
                "Heavily infected plants yield less and survive the winter poorly.",
                0.15f, 0.10f, new[]
                {
                    CreateStep("str_ls_remove", "Remove scorched leaves", "Cut off badly spotted leaves and clear old leaves after harvest.",
                        "Old leaves carry the fungus.", TreatmentInteractionMode.TapRegion, 2, Remove),
                    CreateStep("str_ls_water", "Water at the base", "Use drip or water the soil early in the day.",
                        "Leaves that dry quickly are hard to infect.", TreatmentInteractionMode.AdjustSetting, 2, Cultural),
                    CreateStepWithChemical("str_ls_spray", "Apply a registered fungicide", "Spray following the label if the disease is spreading.",
                        "Protects new leaves.", 3, Protect)
                },
                new[]
                {
                    Measure(PreventionCategory.CropManagement, "Use resistant varieties and clean plants", "Start beds with certified plants."),
                    Measure(PreventionCategory.CropManagement, "Renew beds every few years", "Old beds build up disease."),
                    Measure(PreventionCategory.AirCirculation, "Keep rows narrow and weed-free", "Good airflow dries the leaves.")
                },
                Look("Strawberry_LeafScorch", 7f, "T_LesionMask_SmallSpots", "T_YellowingMask",
                    new Color(0.36f, 0.10f, 0.20f), new Color(0.56f, 0.20f, 0.26f), new Color(0.76f, 0.56f, 0.30f), yellowing: 0.5f));

        // ------------------------------------------------------------------
        // Educational copy for the new diseases (lifecycle, conditions, the zoomed leaf's labels, reference photos)
        // ------------------------------------------------------------------

        class Copy
        {
            public string Asset, Lesion, Halo, Area;
            public string[] Lifecycle, Conditions, Photos;
        }

        static readonly Copy[] Content =
        {
            new Copy { Asset = "Disease_TomatoBacterialSpot", Lesion = "Greasy dark spot", Halo = "Yellow halo", Area = "Spotted area", Photos = new[] { "tomato_bacterial_spot_1" },
                Lifecycle = new[] { "The bacteria survive on seed, debris and volunteer plants.", "Rain splash and wet hands carry them onto leaves.", "Small water-soaked spots turn dark with yellow halos.", "Spots join, leaves drop, and fruit gets scabby spots." },
                Conditions = new[] { "Warm, wet weather (about 24-30 °C).", "Rain or overhead watering.", "Handling wet plants.", "Infected seed or transplants." } },
            new Copy { Asset = "Disease_TomatoLeafMold", Lesion = "Mould patch", Halo = "Pale patch", Area = "Yellowing leaf", Photos = new[] { "tomato_leaf_mold_1" },
                Lifecycle = new[] { "Spores survive on debris and greenhouse surfaces.", "In very humid air they infect the leaf undersides.", "Pale patches show on top, olive mould underneath.", "Leaves wither and the mould releases more spores." },
                Conditions = new[] { "Humidity above about 85%.", "Mild to warm temperatures.", "Poor ventilation under cover.", "Dense, unpruned plants." } },
            new Copy { Asset = "Disease_TomatoSeptoriaLeafSpot", Lesion = "Grey-centred spot", Halo = "Dark border", Area = "Spotted leaf", Photos = new[] { "tomato_septoria_leaf_spot_1" },
                Lifecycle = new[] { "The fungus survives on old tomato debris and some weeds.", "Rain splashes spores onto the lowest leaves.", "Many small spots with grey centres and black dots appear.", "Leaves yellow and drop from the bottom up." },
                Conditions = new[] { "Warm, wet weather.", "Splashing rain or irrigation.", "Debris left in the bed.", "Lower leaves touching the soil." } },
            new Copy { Asset = "Disease_TomatoSpiderMites", Lesion = "Stippling", Halo = "Bronzed area", Area = "Webbed leaf", Photos = new[] { "tomato_spider_mites_1" },
                Lifecycle = new[] { "Mites overwinter on weeds and sheltered plants.", "In hot, dry weather they move onto crops.", "They feed under the leaves, leaving pale dots.", "Numbers explode in days; leaves bronze and webbing appears." },
                Conditions = new[] { "Hot, dry weather.", "Dusty, water-stressed plants.", "Sprays that killed natural predators.", "Sheltered spots under cover." } },
            new Copy { Asset = "Disease_TomatoTargetSpot", Lesion = "Ringed spot", Halo = "Yellow halo", Area = "Affected leaf", Photos = new[] { "tomato_target_spot_1" },
                Lifecycle = new[] { "The fungus survives on crop debris.", "Spores land on leaves wet for many hours.", "Brown spots with concentric rings appear.", "Spots merge and leaves drop." },
                Conditions = new[] { "Warm, humid weather.", "Long leaf wetness.", "A dense canopy.", "Debris from the last crop." } },
            new Copy { Asset = "Disease_TomatoYellowLeafCurlVirus", Lesion = "Yellow edge", Halo = "Curled margin", Area = "Stunted growth", Photos = new[] { "tomato_yellow_leaf_curl_1" },
                Lifecycle = new[] { "Whiteflies pick up the virus from infected plants.", "They carry it to healthy plants as they feed.", "New leaves come out small, curled and yellow-edged.", "The plant stops growing and sets little fruit." },
                Conditions = new[] { "Warm weather that favours whiteflies.", "Infected plants or weeds nearby.", "No insect-proof cover on young plants.", "Susceptible varieties." } },
            new Copy { Asset = "Disease_TomatoMosaicVirus", Lesion = "Light green patch", Halo = "Dark green patch", Area = "Mottled leaf", Photos = new[] { "tomato_mosaic_virus_1" },
                Lifecycle = new[] { "The virus survives in seed and plant debris for a long time.", "It spreads on hands, tools and clothing.", "Leaves show a light and dark green mosaic and may distort.", "Every handling can carry it to the next plant." },
                Conditions = new[] { "Handling plants without washing.", "Infected seed.", "Debris from earlier crops.", "Susceptible varieties." } },
            new Copy { Asset = "Disease_AppleBlackRot", Lesion = "Frog-eye spot", Halo = "Purple border", Area = "Affected area", Photos = new[] { "apple_black_rot_1" },
                Lifecycle = new[] { "The fungus lives in cankers, dead wood and mummified fruit.", "Spores are released in warm, wet weather.", "Leaves get brown spots with purple borders.", "Fruit rots and mummifies, carrying the fungus on." },
                Conditions = new[] { "Warm, wet weather.", "Dead wood and cankers in the tree.", "Mummified fruit left on or under the tree.", "Wounded or stressed trees." } },
            new Copy { Asset = "Disease_CherryPowderyMildew", Lesion = "White patch", Halo = "Puckered area", Area = "Powdery area", Photos = new[] { "cherry_powdery_mildew_1" },
                Lifecycle = new[] { "The fungus overwinters on the tree and in leaf litter.", "Spores infect young leaves in warm, humid weather.", "White powdery patches spread and leaves curl.", "New spores spread through the canopy." },
                Conditions = new[] { "Warm, humid weather.", "Dry leaves are enough.", "Dense, shaded canopies.", "Young, soft growth." } },
            new Copy { Asset = "Disease_CornGrayLeafSpot", Lesion = "Rectangular lesion", Halo = "Vein-bounded edge", Area = "Dead leaf area", Photos = new[] { "corn_gray_leaf_spot_1" },
                Lifecycle = new[] { "The fungus survives on maize residue on the surface.", "Spores splash and blow onto lower leaves in humid weather.", "Narrow rectangular grey lesions form between the veins.", "Lesions join and the infection climbs the plant." },
                Conditions = new[] { "Warm, very humid weather.", "Long dew periods.", "Maize residue on the surface.", "Susceptible hybrids." } },
            new Copy { Asset = "Disease_CornCommonRust", Lesion = "Rust pustule", Halo = "Pale ring", Area = "Rusted leaf", Photos = new[] { "corn_common_rust_1" },
                Lifecycle = new[] { "Spores blow in on the wind each year.", "They infect leaves wet from dew.", "Cinnamon-brown pustules break through both leaf surfaces.", "Pustules release new spores that spread up the plant." },
                Conditions = new[] { "Cool to moderate temperatures.", "Heavy dew.", "Susceptible varieties (often sweet corn).", "Early infection before tasselling." } },
            new Copy { Asset = "Disease_CornNorthernLeafBlight", Lesion = "Cigar lesion", Halo = "Dark edge", Area = "Blighted leaf", Photos = new[] { "corn_northern_leaf_blight_1" },
                Lifecycle = new[] { "The fungus survives on maize residue.", "Spores reach the lower leaves in wet, mild weather.", "Long grey-green lesions form and turn tan.", "Spores from the lesions spread to upper leaves." },
                Conditions = new[] { "Moderate temperatures (about 18-27 °C).", "Long dew or rain.", "Residue from the last maize crop.", "Susceptible hybrids." } },
            new Copy { Asset = "Disease_GrapeBlackRot", Lesion = "Tan spot", Halo = "Dark border", Area = "Spotted leaf", Photos = new[] { "grape_black_rot_1" },
                Lifecycle = new[] { "The fungus overwinters in mummified berries and old canes.", "Spring rain releases spores onto young leaves and shoots.", "Tan spots with dark borders and black dots appear.", "Berries rot and become mummies for next year." },
                Conditions = new[] { "Warm, wet spring weather.", "Mummies left in the vineyard.", "A dense canopy.", "Flowering to young-berry stage." } },
            new Copy { Asset = "Disease_GrapeEsca(BlackMeasles)", Lesion = "Dead stripe", Halo = "Yellow band", Area = "Tiger-striped leaf", Photos = new[] { "grape_esca_1" },
                Lifecycle = new[] { "Fungal spores enter through pruning wounds.", "The fungi grow in the wood for years, unseen.", "In summer, leaves show tiger stripes and berries get dark spots.", "The vine declines or suddenly collapses." },
                Conditions = new[] { "Pruning wounds made in wet weather.", "Older vines.", "Infected planting material.", "Hot weather (sudden collapse)." } },
            new Copy { Asset = "Disease_GrapeLeafBlight(IsariopsisLeafSpot)", Lesion = "Dark spot", Halo = "Brown edge", Area = "Yellowing leaf", Photos = new[] { "grape_leaf_blight_1" },
                Lifecycle = new[] { "The fungus survives on fallen leaves.", "Spores infect leaves in warm, humid weather.", "Irregular dark spots appear and dry out.", "Older leaves yellow and drop." },
                Conditions = new[] { "Warm, humid weather.", "Late in the season.", "Fallen leaves left under the vines.", "A crowded canopy." } },
            new Copy { Asset = "Disease_OrangeCitrusGreening(Huanglongbing)", Lesion = "Yellow blotch", Halo = "Green island", Area = "Mottled leaf", Photos = new[] { "orange_citrus_greening_1" },
                Lifecycle = new[] { "Psyllids pick up the bacteria feeding on infected trees.", "They carry them to new shoots of healthy trees.", "The bacteria spread through the tree's food-conducting tissue.", "Leaves mottle, fruit is poor and the tree declines." },
                Conditions = new[] { "Asian citrus psyllids present.", "Infected trees nearby.", "New flushes of growth (psyllids feed on them).", "Uncertified nursery trees." } },
            new Copy { Asset = "Disease_PeachBacterialSpot", Lesion = "Angular spot", Halo = "Shot hole", Area = "Yellowing leaf", Photos = new[] { "peach_bacterial_spot_1" },
                Lifecycle = new[] { "The bacteria overwinter in twig cankers.", "Spring rain splashes them onto young leaves.", "Small purple-brown spots form, then drop out as shot holes.", "Leaves yellow and drop; fruit is pitted." },
                Conditions = new[] { "Warm, wet, windy weather.", "Sandy soils and stressed trees.", "Susceptible varieties.", "Storms during leaf-out." } },
            new Copy { Asset = "Disease_SquashPowderyMildew", Lesion = "White spot", Halo = "Pale patch", Area = "Powdery area", Photos = new[] { "squash_powdery_mildew_1" },
                Lifecycle = new[] { "Spores arrive on the wind from other cucurbits.", "They infect leaves without needing them to be wet.", "White powdery spots spread over the leaf.", "Leaves yellow and die, and spores spread on." },
                Conditions = new[] { "Warm days and dense plants.", "Shade.", "Dry leaves are enough.", "Older leaves later in the season." } },
            new Copy { Asset = "Disease_StrawberryLeafScorch", Lesion = "Purple spot", Halo = "Red ring", Area = "Scorched leaf", Photos = new[] { "strawberry_leaf_scorch_1" },
                Lifecycle = new[] { "The fungus survives on old infected leaves.", "Spores splash onto new leaves in wet weather.", "Small dark purple spots multiply.", "Spots join and the leaf looks scorched." },
                Conditions = new[] { "Wet weather.", "Long leaf wetness.", "Old, dense beds.", "Susceptible varieties." } },
        };

        static readonly (string asset, string name)[] Species =
        {
            ("Plant_Blueberry", "Vaccinium corymbosum"), ("Plant_Cherry", "Prunus avium"), ("Plant_Corn", "Zea mays"),
            ("Plant_Grape", "Vitis vinifera"), ("Plant_Orange", "Citrus × sinensis"), ("Plant_Peach", "Prunus persica"),
            ("Plant_Raspberry", "Rubus idaeus"), ("Plant_Soybean", "Glycine max"), ("Plant_Squash", "Cucurbita pepo"),
            ("Plant_Strawberry", "Fragaria × ananassa"),
        };

        public static void ApplyContent()
        {
            foreach (var (asset, name) in Species)
            {
                var plant = AssetDatabase.LoadAssetAtPath<PlantData>($"Assets/Data/Plants/{asset}.asset");
                if (plant == null) { Debug.LogWarning($"[AI Plant Doctor] {asset} not found for its scientific name."); continue; }
                plant.scientificName = name;
                EditorUtility.SetDirty(plant);
            }
            foreach (var c in Content)
            {
                var d = AssetDatabase.LoadAssetAtPath<DiseaseData>($"Assets/Data/Diseases/{c.Asset}.asset");
                if (d == null) { Debug.LogWarning($"[AI Plant Doctor] {c.Asset} not found for its content."); continue; }
                d.lifecycleStages = c.Lifecycle;
                d.conditionFactors = c.Conditions;
                d.lesionLabel = c.Lesion; d.haloLabel = c.Halo; d.areaLabel = c.Area;
                var photos = new List<Texture2D>();
                foreach (var photo in c.Photos)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Photos}/diseases/{photo}.jpg");
                    if (tex != null) photos.Add(tex); else Debug.LogWarning($"[AI Plant Doctor] Missing reference photo {photo}.");
                }
                d.referencePhotos = photos.ToArray();
                if (d.treatmentPlan != null && d.treatmentPlan.steps != null)
                    foreach (var step in d.treatmentPlan.steps)
                    {
                        if (step == null) continue;
                        step.illustration = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Photos}/treatment/{ContentBuilder.IllustrationFor(step)}.jpg");
                        EditorUtility.SetDirty(step);
                    }
                EditorUtility.SetDirty(d);
            }
        }
    }
}
#endif
