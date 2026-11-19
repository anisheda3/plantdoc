#if UNITY_EDITOR
using PlantDoctor.Diseases;
using PlantDoctor.Plants;
using PlantDoctor.Treatment;
using UnityEditor;
using UnityEngine;

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// The educational copy and licensed reference photos the new screens show for each disease:
    /// scientific names, how the disease develops and spreads, what favours it, the zoomed-leaf
    /// annotation labels, and a photo for each treatment step. Everything here is general
    /// horticultural knowledge, never dosages or product advice.
    /// </summary>
    public static class ContentBuilder
    {
        const string Root = "Assets/Data";
        const string Photos = "Assets/Art/Photos";

        class Entry
        {
            public string AssetName;
            public string[] Lifecycle, Conditions, Photos;
            public string Lesion = "Dark spot", Halo = "Yellow halo", Area = "Affected area";
        }

        static readonly Entry[] Diseases =
        {
            new Entry
            {
                AssetName = "Disease_TomatoEarlyBlight",
                Lifecycle = EarlyBlightLifecycle, Conditions = EarlyBlightConditions,
                Photos = new[] { "tomato_early_blight_1", "tomato_early_blight_2", "tomato_early_blight_3" },
                Lesion = "Ringed lesion", Halo = "Yellow halo", Area = "Yellowing leaf"
            },
            new Entry
            {
                AssetName = "Disease_PotatoEarlyBlight",
                Lifecycle = EarlyBlightLifecycle, Conditions = EarlyBlightConditions,
                Photos = new[] { "potato_early_blight_1" },
                Lesion = "Ringed lesion", Halo = "Yellow halo", Area = "Yellowing leaf"
            },
            new Entry
            {
                AssetName = "Disease_TomatoLateBlight",
                Lifecycle = LateBlightLifecycle, Conditions = LateBlightConditions,
                Photos = new[] { "tomato_late_blight_1", "tomato_late_blight_2" },
                Lesion = "Blighted patch", Halo = "Pale edge", Area = "Collapsing tissue"
            },
            new Entry
            {
                AssetName = "Disease_PotatoLateBlight",
                Lifecycle = LateBlightLifecycle, Conditions = LateBlightConditions,
                Photos = new[] { "potato_late_blight_1", "potato_late_blight_2" },
                Lesion = "Blighted patch", Halo = "Pale edge", Area = "Collapsing tissue"
            },
            new Entry
            {
                AssetName = "Disease_PepperBacterialSpot",
                Lifecycle = new[]
                {
                    "The bacteria survive on seed, plant debris and some weeds between crops.",
                    "Rain splash and handling wet plants carry them onto leaves, where they enter through tiny openings.",
                    "Small water-soaked spots turn brown with yellow edges, often angular between the veins.",
                    "Spots join, leaves yellow and drop, and the bacteria can reach the fruit."
                },
                Conditions = new[]
                {
                    "Warm, wet weather, roughly 24–30 °C.",
                    "Rain or overhead watering that splashes leaves.",
                    "Working among or picking from wet plants.",
                    "Infected seed or transplants."
                },
                Photos = new[] { "pepper_bacterial_spot_1", "pepper_bacterial_spot_2" },
                Lesion = "Small dark spot", Halo = "Yellow edge", Area = "Spotted area"
            },
            new Entry
            {
                AssetName = "Disease_PepperPowderyMildew",
                Lifecycle = new[]
                {
                    "The fungus survives on crop remains and on nearby host plants.",
                    "Wind-blown spores land on leaves; unlike many fungi they do not need wet leaves.",
                    "Pale yellow patches show on the upper side, with white powdery growth underneath.",
                    "Leaves curl and drop, which can expose the fruit to sunburn."
                },
                Conditions = new[]
                {
                    "Warm days and cooler nights with moderate humidity.",
                    "Dry leaves are enough: rain is not required.",
                    "Crowded or shaded plants.",
                    "Older leaves later in the season."
                },
                Photos = new string[0],
                Lesion = "Pale patch", Halo = "Yellow patch", Area = "Powdery area"
            },
            new Entry
            {
                AssetName = "Disease_AppleAppleScab",
                Lifecycle = new[]
                {
                    "The fungus spends winter in fallen, infected leaves.",
                    "In spring, rain releases spores that land on young leaves and fruit.",
                    "Olive-green to brown velvety spots appear and later turn dark and scabby.",
                    "Infected leaves and fruit drop, and the fungus returns to the leaf litter."
                },
                Conditions = new[]
                {
                    "Cool, wet spring weather.",
                    "Leaves staying wet for several hours.",
                    "Fallen leaves left under the tree.",
                    "A crowded canopy that dries slowly."
                },
                Photos = new[] { "apple_scab_1", "apple_scab_2" },
                Lesion = "Scab lesion", Halo = "Olive edge", Area = "Affected area"
            },
            new Entry
            {
                AssetName = "Disease_AppleCedarAppleRust",
                Lifecycle = new[]
                {
                    "The fungus lives on nearby junipers or cedars, forming brown galls.",
                    "In spring rain the galls swell into orange, jelly-like horns that release spores.",
                    "Bright yellow-orange spots form on apple leaves in early summer.",
                    "Late-summer spores return to the junipers: the cycle needs both plants."
                },
                Conditions = new[]
                {
                    "Juniper or cedar trees growing nearby.",
                    "Warm, wet spring weather.",
                    "Leaves wet during the infection window.",
                    "Susceptible apple varieties."
                },
                Photos = new[] { "apple_rust_1", "apple_rust_2" },
                Lesion = "Orange spot", Halo = "Yellow ring", Area = "Affected area"
            },
        };

        static string[] EarlyBlightLifecycle => new[]
        {
            "Spores survive on crop debris and in the soil between seasons.",
            "Wind and splashing water carry them to the lower, older leaves.",
            "Dark spots with rings and yellow halos appear within days in warm, damp weather.",
            "Spots join, leaves yellow and drop, and new spores spread to nearby plants."
        };

        static string[] EarlyBlightConditions => new[]
        {
            "Warm days, roughly 24–29 °C, with humid nights.",
            "Leaves staying wet from dew, rain or overhead watering.",
            "Older or stressed plants, especially those short of nitrogen.",
            "Plants close together, or infected debris left on the ground."
        };

        static string[] LateBlightLifecycle => new[]
        {
            "Spores arrive on the wind or from infected tubers and volunteer plants.",
            "In cool, wet weather they land on leaves and infect within hours.",
            "Greasy grey-green blotches appear and may grow white fuzz underneath.",
            "The tissue collapses and releases more spores, so it can spread through a crop in days."
        };

        static string[] LateBlightConditions => new[]
        {
            "Cool nights, roughly 10–16 °C, with mild days.",
            "Leaves wet for many hours: fog, rain or heavy dew.",
            "Infected plants, cull piles or volunteers nearby.",
            "A dense canopy that stays humid."
        };

        static readonly (string asset, string name)[] Species =
        {
            ("Plant_Tomato", "Solanum lycopersicum"),
            ("Plant_Potato", "Solanum tuberosum"),
            ("Plant_Bellpepper", "Capsicum annuum"),
            ("Plant_Apple", "Malus domestica"),
        };

        public static void Apply()
        {
            foreach (var (asset, name) in Species)
            {
                var plant = AssetDatabase.LoadAssetAtPath<PlantData>($"{Root}/Plants/{asset}.asset");
                if (plant == null) { Debug.LogWarning($"[AI Plant Doctor] {asset} not found for scientific name."); continue; }
                plant.scientificName = name;
                EditorUtility.SetDirty(plant);
            }

            foreach (var e in Diseases)
            {
                var d = AssetDatabase.LoadAssetAtPath<DiseaseData>($"{Root}/Diseases/{e.AssetName}.asset");
                if (d == null) { Debug.LogWarning($"[AI Plant Doctor] {e.AssetName} not found for content."); continue; }
                d.lifecycleStages = e.Lifecycle;
                d.conditionFactors = e.Conditions;
                d.lesionLabel = e.Lesion; d.haloLabel = e.Halo; d.areaLabel = e.Area;

                var list = new System.Collections.Generic.List<Texture2D>();
                foreach (var photo in e.Photos)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Photos}/diseases/{photo}.jpg");
                    if (tex != null) list.Add(tex); else Debug.LogWarning($"[AI Plant Doctor] Missing reference photo {photo}.");
                }
                d.referencePhotos = list.ToArray();

                var plan = d.treatmentPlan;
                if (plan != null && plan.steps != null)
                    foreach (var step in plan.steps)
                    {
                        if (step == null) continue;
                        step.illustration = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Photos}/treatment/{IllustrationFor(step)}.jpg");
                        EditorUtility.SetDirty(step);
                    }
                EditorUtility.SetDirty(d);
            }
            PlantVillageDataBuilder.ApplyContent();   // the crops and diseases added with the full PlantVillage set
        }

        /// <summary>Picks the photo that matches what the step asks the grower to do.</summary>
        internal static string IllustrationFor(TreatmentStep step)
        {
            string t = (step.title + " " + step.instruction).ToLowerInvariant();
            if (t.Contains("rake")) return "rake";
            if (t.Contains("airflow") || t.Contains("canopy") || t.Contains("space") || t.Contains("open up") || t.Contains("humidity")) return "airflow";
            if (t.Contains("water") || t.Contains("irrigation")) return "water";
            if (step.chemicalInfo != null && step.chemicalInfo.involvesChemicalProduct) return "spray";
            if (t.Contains("fungicide") || t.Contains("spray") || t.Contains("copper") || t.Contains("sulphur")) return "spray";
            if (t.Contains("remove") || t.Contains("prune") || t.Contains("pick") || t.Contains("take off") || t.Contains("cut")) return "prune";
            return "inspect";
        }
    }
}
#endif
