#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using PlantDoctor.AI;
using PlantDoctor.Core;
using PlantDoctor.Diseases;
using PlantDoctor.Plants;
using PlantDoctor.Treatment;
using UnityEditor;
using UnityEngine;

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// One-click generation of the Phase 1 demo dataset so the whole loop runs
    /// immediately. Menu: Tools > AI Plant Doctor > Build Demo Data.
    /// Re-running overwrites the generated assets only.
    /// </summary>
    public static class DemoDataBuilder
    {
        const string Root = "Assets/Data";

        [MenuItem("Tools/AI Plant Doctor/Build Demo Data")]
        public static void Build()
        {
            EnsureFolder(Root);
            foreach (var sub in new[] { "Plants", "Diseases", "Treatments", "Features", "Config" })
                EnsureFolder($"{Root}/{sub}");

            // ---- Features -------------------------------------------------
            var leafColor = CreateFeature("leaf_color", "Leaf Color", 0.95f, true,
                "Overall greenness of the leaf surface.",
                "Loss of green colour reduces photosynthesis and often signals stress or infection.",
                new Color(0.35f, 0.8f, 0.35f));

            var leafSpots = CreateFeature("leaf_spots", "Leaf Spots", 0.05f, false,
                "Proportion of the leaf covered by visible spots or lesions.",
                "Persistent leaf spotting reduces photosynthetic area and may indicate fungal or bacterial infection.",
                new Color(0.75f, 0.2f, 0.15f));

            var yellowing = CreateFeature("yellowing", "Yellowing", 0.10f, false,
                "Extent of chlorosis (yellow discolouration).",
                "Yellowing can indicate infection, nutrient deficiency or water stress.",
                new Color(0.95f, 0.85f, 0.2f));

            var wilting = CreateFeature("wilting", "Wilting", 0.05f, false,
                "Degree of drooping or loss of turgor.",
                "Wilting suggests impaired water transport, root problems or advanced infection.",
                new Color(0.6f, 0.45f, 0.25f));

            var features = new[] { leafColor, leafSpots, yellowing, wilting };

            // ---- Tomato Early Blight -------------------------------------
            var ebSteps = new List<TreatmentStep>
            {
                CreateStep("eb_remove", "Remove heavily infected leaves",
                    "Identify and remove the most heavily affected lower leaves.",
                    "Reduces the amount of infected tissue and limits spread to healthy foliage.",
                    TreatmentInteractionMode.TapRegion, 2,
                    new StateDelta { diseaseSeverity = -0.12f, lesionDensity = -0.25f, leafDamage = 0.05f, yellowing = -0.15f }),

                CreateStep("eb_airflow", "Improve airflow around the plant",
                    "Prune crowded growth and increase spacing so foliage dries faster.",
                    "Lower leaf wetness duration slows fungal development.",
                    TreatmentInteractionMode.AdjustSetting, 2,
                    new StateDelta { diseaseSeverity = -0.10f, yellowing = -0.05f, healthyGrowth = 0.05f }),

                CreateStepWithChemical("eb_treat", "Apply the recommended treatment",
                    "Apply an appropriate registered fungicide following the product label.",
                    "Protects new growth from further infection while damaged tissue is shed.",
                    3, new StateDelta { diseaseSeverity = -0.22f, lesionDensity = -0.20f, healthyGrowth = 0.08f }),

                CreateStep("eb_irrigation", "Adjust irrigation practices",
                    "Water at the base of the plant, early in the day, avoiding prolonged leaf wetness.",
                    "Dry foliage is far less favourable to fungal spore germination.",
                    TreatmentInteractionMode.AdjustSetting, 3,
                    new StateDelta { diseaseSeverity = -0.10f, wilting = -0.15f, healthyGrowth = 0.10f })
            };

            var ebPlan = CreateAsset<TreatmentPlan>($"{Root}/Treatments/TreatmentPlan_TomatoEarlyBlight.asset");
            ebPlan.treatmentId = "tomato_early_blight_treatment";
            ebPlan.planName = "Tomato — Early Blight";
            ebPlan.steps = ebSteps.ToArray();
            ebPlan.residualSeverityFloor = 0.15f;   // treatment is never modelled as a full cure
            ebPlan.residualDamageFloor = 0.10f;
            EditorUtility.SetDirty(ebPlan);

            var ebPrevention = CreateAsset<PreventionPlan>($"{Root}/Treatments/PreventionPlan_TomatoEarlyBlight.asset");
            ebPrevention.preventionId = "tomato_early_blight_prevention";
            ebPrevention.measures = new[]
            {
                Measure(PreventionCategory.CropHygiene, "Remove and destroy infected debris",
                    "Early blight overwinters on crop residue and volunteer plants. Clear fallen leaves and end-of-season debris rather than leaving them in the bed."),
                Measure(PreventionCategory.CropHygiene, "Sanitise pruning tools",
                    "Clean blades between plants so cuts do not carry inoculum from an infected plant to a healthy one."),
                Measure(PreventionCategory.WaterManagement, "Avoid overhead irrigation",
                    "Water at the soil line early in the day so leaves do not stay wet for long periods."),
                Measure(PreventionCategory.AirCirculation, "Space and stake plants",
                    "Staking and adequate spacing let foliage dry quickly and reduce leaf-to-leaf contact."),
                Measure(PreventionCategory.Monitoring, "Scout lower leaves weekly",
                    "Early blight typically starts on older, lower leaves as small concentric-ringed spots. Early detection makes management far easier."),
                Measure(PreventionCategory.CropManagement, "Rotate crops and choose tolerant varieties",
                    "Avoid planting tomato or potato in the same ground in consecutive seasons, and prefer varieties with documented tolerance where available.")
            };
            EditorUtility.SetDirty(ebPrevention);

            var ebProfile = CreateAsset<SimulationProfile>($"{Root}/Diseases/SimProfile_TomatoEarlyBlight.asset");
            EditorUtility.SetDirty(ebProfile);

            var earlyBlight = CreateAsset<DiseaseData>($"{Root}/Diseases/Disease_TomatoEarlyBlight.asset");
            earlyBlight.diseaseId = "early_blight";
            earlyBlight.diseaseName = "Early Blight";
            earlyBlight.aiLabelAliases = new[] { "Tomato___Early_blight", "tomato_early_blight" };
            earlyBlight.pathogenType = PathogenType.Fungal;
            earlyBlight.description = "A common fungal leaf disease of tomato and potato caused by Alternaria species.";
            earlyBlight.causes = "Warm, humid conditions with extended leaf wetness; inoculum surviving on crop residue and soil.";
            earlyBlight.symptoms = new[] { "Dark spots with concentric rings", "Yellow halo around lesions", "Lower/older leaves affected first", "Progressive defoliation" };
            earlyBlight.effectOnPlant = "Lesions destroy photosynthetic tissue and cause leaves to yellow and drop, weakening the plant.";
            earlyBlight.untreatedOutcome = "Severe defoliation can expose fruit to sunscald and substantially reduce yield.";
            earlyBlight.supportingFeatureIds = new[] { "leaf_spots", "yellowing", "leaf_color" };
            earlyBlight.treatmentPlan = ebPlan;
            earlyBlight.preventionPlan = ebPrevention;
            earlyBlight.simulationProfile = ebProfile;
            EditorUtility.SetDirty(earlyBlight);

            // ---- Tomato Late Blight (second scenario) --------------------
            var lbPlan = CreateAsset<TreatmentPlan>($"{Root}/Treatments/TreatmentPlan_TomatoLateBlight.asset");
            lbPlan.treatmentId = "tomato_late_blight_treatment";
            lbPlan.planName = "Tomato — Late Blight";
            lbPlan.steps = new[]
            {
                CreateStep("lb_remove", "Remove and destroy affected foliage and fruit",
                    "Bag and remove affected tissue rather than composting it.",
                    "Late blight spreads explosively; removing sporulating tissue is the first priority.",
                    TreatmentInteractionMode.TapRegion, 1,
                    new StateDelta { diseaseSeverity = -0.15f, lesionDensity = -0.3f, yellowing = -0.1f }),
                CreateStep("lb_airflow", "Reduce canopy humidity",
                    "Prune for airflow and avoid working among wet plants.",
                    "The pathogen requires cool, wet conditions to sporulate.",
                    TreatmentInteractionMode.AdjustSetting, 2,
                    new StateDelta { diseaseSeverity = -0.12f, healthyGrowth = 0.05f }),
                CreateStepWithChemical("lb_treat", "Apply an appropriate protectant",
                    "Apply a registered product suited to late blight, following the label.",
                    "Protects remaining healthy tissue during high-risk weather.",
                    3, new StateDelta { diseaseSeverity = -0.2f, lesionDensity = -0.18f, healthyGrowth = 0.08f })
            };
            lbPlan.residualSeverityFloor = 0.20f;   // late blight leaves more lasting damage
            lbPlan.residualDamageFloor = 0.15f;
            EditorUtility.SetDirty(lbPlan);

            var lbProfile = CreateAsset<SimulationProfile>($"{Root}/Diseases/SimProfile_TomatoLateBlight.asset");
            lbProfile.progressionDurationSeconds = 6f;
            EditorUtility.SetDirty(lbProfile);

            var lbPrevention = CreateAsset<PreventionPlan>($"{Root}/Treatments/PreventionPlan_TomatoLateBlight.asset");
            lbPrevention.preventionId = "tomato_late_blight_prevention";
            lbPrevention.measures = new[]
            {
                Measure(PreventionCategory.CropHygiene, "Destroy cull piles and volunteers",
                    "Late blight survives on living tissue such as leftover tubers and volunteer plants."),
                Measure(PreventionCategory.Monitoring, "Watch regional blight forecasts",
                    "Risk rises sharply during cool, wet periods; regional alerts give useful early warning."),
                Measure(PreventionCategory.EnvironmentalManagement, "Manage humidity under cover",
                    "Ventilate tunnels and greenhouses to prevent condensation on foliage.")
            };
            EditorUtility.SetDirty(lbPrevention);

            var lateBlight = CreateAsset<DiseaseData>($"{Root}/Diseases/Disease_TomatoLateBlight.asset");
            lateBlight.diseaseId = "late_blight";
            lateBlight.diseaseName = "Late Blight";
            lateBlight.aiLabelAliases = new[] { "Tomato___Late_blight", "tomato_late_blight" };
            lateBlight.pathogenType = PathogenType.Fungal;
            lateBlight.description = "A destructive oomycete disease (Phytophthora infestans) of tomato and potato.";
            lateBlight.causes = "Cool, wet, humid weather; airborne sporangia from nearby infected plants or residue.";
            lateBlight.symptoms = new[] { "Large greasy grey-green blotches", "White fuzzy growth on leaf undersides in humid conditions", "Rapid collapse of foliage", "Firm brown fruit lesions" };
            lateBlight.effectOnPlant = "Destroys foliage and fruit very rapidly under favourable weather.";
            lateBlight.untreatedOutcome = "Whole plantings can be lost within days under sustained cool, wet conditions.";
            lateBlight.supportingFeatureIds = new[] { "leaf_spots", "wilting", "leaf_color" };
            lateBlight.treatmentPlan = lbPlan;
            lateBlight.preventionPlan = lbPrevention;
            lateBlight.simulationProfile = lbProfile;
            EditorUtility.SetDirty(lateBlight);

            // ---- Plant ----------------------------------------------------
            var tomato = CreateAsset<PlantData>($"{Root}/Plants/Plant_Tomato.asset");
            tomato.plantId = "tomato";
            tomato.plantName = "Tomato";
            tomato.description = "Solanum lycopersicum — the Phase 1 reference crop.";
            tomato.features = features;
            var tomatoDiseases = new List<DiseaseData> { earlyBlight, lateBlight };
            tomatoDiseases.AddRange(PlantVillageDataBuilder.MoreTomatoDiseases());
            tomato.diseases = tomatoDiseases.ToArray();
            tomato.species = SpeciesDataBuilder.TomatoProfile();
            EditorUtility.SetDirty(tomato);

            // ---- Potato, pepper, apple (one twin generator, a profile per species) ----
            var more = SpeciesDataBuilder.BuildMoreSpecies(features);

            var database = CreateAsset<PlantDatabase>($"{Root}/Plants/PlantDatabase.asset");
            var plants = new List<PlantData> { tomato };
            plants.AddRange(more);
            plants.AddRange(PlantVillageDataBuilder.BuildNewCrops(features));
            plants.AddRange(PlantSegDataBuilder.Build(features, plants));   // PlantSeg: new plants, and new diseases for the ones above
            database.plants = plants.ToArray();
            database.Rebuild();
            EditorUtility.SetDirty(database);

            // ---- General prevention (healthy plants / diseases without a specific plan) ----
            var general = CreateAsset<PreventionPlan>($"{Root}/Treatments/PreventionPlan_General.asset");
            general.preventionId = "general_prevention";
            general.measures = new[]
            {
                Measure(PreventionCategory.WaterManagement, "Water the soil, not the leaves",
                    "Water at the base early in the day so foliage dries quickly. Most leaf diseases need a film of water to infect."),
                Measure(PreventionCategory.AirCirculation, "Give plants room to breathe",
                    "Space, stake and lightly prune so air moves through the canopy and leaves dry after rain or dew."),
                Measure(PreventionCategory.Monitoring, "Check lower leaves weekly",
                    "Problems usually start low and small. Catching the first spots makes treatment far easier and often unnecessary."),
                Measure(PreventionCategory.CropHygiene, "Clear fallen leaves and debris",
                    "Many pathogens survive on dead plant material. Removing it breaks the cycle between seasons.")
            };
            EditorUtility.SetDirty(general);

            ContentBuilder.Apply();   // scientific names, lifecycle/conditions copy, reference photos, step illustrations
            ContentDatabaseImporter.Apply();   // the content database (Database/plant_doctor_content.db) has the last word on the texts

            var policy = CreateAsset<ConfidencePolicy>($"{Root}/Config/ConfidencePolicy.asset");
            EditorUtility.SetDirty(policy);

            var quality = CreateAsset<ImageQualityAnalyzer>($"{Root}/Config/ImageQualitySettings.asset");
            EditorUtility.SetDirty(quality);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AI Plant Doctor] Demo data built under Assets/Data. Assign PlantDatabase, ConfidencePolicy and ImageQualitySettings on AppFlowController/AIManager.");
        }

        // ---- helpers ------------------------------------------------------
        static FeatureData CreateFeature(string id, string name, float optimal, bool higherIsBetter,
            string meaning, string why, Color color)
        {
            var f = CreateAsset<FeatureData>($"{Root}/Features/Feature_{id}.asset");
            f.featureId = id;
            f.displayName = name;
            f.optimalValue = optimal;
            f.higherIsBetter = higherIsBetter;
            f.source = MeasurementSource.VisualAiEstimate;
            f.meaning = meaning;
            f.whyItMatters = why;
            f.overlayColor = color;
            EditorUtility.SetDirty(f);
            return f;
        }

        internal static TreatmentStep CreateStep(string id, string title, string instruction, string rationale,
            TreatmentInteractionMode mode, int days, StateDelta effect)
        {
            var s = CreateAsset<TreatmentStep>($"{Root}/Treatments/Step_{id}.asset");
            s.stepId = id; s.title = title; s.instruction = instruction; s.rationale = rationale;
            s.interactionMode = mode; s.simulatedDaysAdvanced = days; s.effect = effect;
            EditorUtility.SetDirty(s);
            return s;
        }

        internal static TreatmentStep CreateStepWithChemical(string id, string title, string instruction,
            string rationale, int days, StateDelta effect)
        {
            var s = CreateStep(id, title, instruction, rationale, TreatmentInteractionMode.SprayApply, days, effect);
            s.chemicalInfo = new ChemicalTreatmentInfo
            {
                involvesChemicalProduct = true,
                productOrActiveIngredient = "TODO[DATA]: enter a locally registered product / active ingredient",
                dosage = "TODO[DATA]: follow the product label rate",
                applicationMethod = "Foliar spray with full, even coverage including leaf undersides",
                safetyNotes = "Wear the protective equipment stated on the label. Keep people and animals out of the treated area for the stated re-entry period.",
                preHarvestInterval = "TODO[DATA]: per product label"
            };
            EditorUtility.SetDirty(s);
            return s;
        }

        internal static PreventionMeasure Measure(PreventionCategory category, string title, string detail) =>
            new PreventionMeasure { category = category, title = title, detail = detail };

        internal static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
