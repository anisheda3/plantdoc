#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// The PlantSeg dataset's plants and diseases (Wei et al. 2024: 34 host plants, 115 disease classes, CC BY 4.0), plus the labels
    /// of PlantDoc (Singh et al. 2020; every one of its 17 disease classes is already a PlantVillage disease).
    ///
    /// The disease content is DATA: one JSON file per plant in Assets/Editor/Data/PlantSeg (written by hand from extension services,
    /// APS and floras; each entry lists its sources). Diseases of plants the app already has are added to those plants; the other
    /// hosts become new plants with species profiles from <see cref="PlantSegSpecies"/>. Every disease keeps the dataset's own label
    /// as an alias, so a model trained on PlantSeg or PlantDoc resolves to it.
    /// </summary>
    public static class PlantSegDataBuilder
    {
        public const string DataFolder = "Assets/Editor/Data/PlantSeg";
        const string Masks = "Assets/Textures/Generated";
        const string PhotoFolder = "Assets/Art/Photos/diseases";

        // ---- the JSON layout ----
        [Serializable] class PlantFile
        {
            public string plantId;            // the app's plant id (existing or new)
            public string plantName;          // "Banana"
            public string scientificName;     // "Musa acuminata (AAA group)"
            public string description;        // one line for a new plant
            public DiseaseEntry[] diseases = new DiseaseEntry[0];
            public AliasEntry[] aliases = new AliasEntry[0];   // dataset labels for diseases the plant already has
            public string[] healthyLabels = new string[0];      // e.g. "Tomato leaf" (PlantDoc), kept for the label map
        }

        [Serializable] class AliasEntry { public string disease; public string[] labels = new string[0]; }

        [Serializable] class DiseaseEntry
        {
            public string id, name, type = "Fungal", part = "Leaf", pathogen;
            public string[] labels = new string[0];
            public string description, causes, effect, untreated;
            public string[] symptoms = new string[0];
            public float severityFloor = 0.15f, damageFloor = 0.10f;
            public StepEntry[] steps = new StepEntry[0];
            public MeasureEntry[] prevention = new MeasureEntry[0];
            public LookEntry look = new LookEntry();
            public string lesionLabel = "Lesion", haloLabel = "Edge", areaLabel = "Affected area";
            public string[] lifecycle = new string[0], conditions = new string[0], photos = new string[0], sources = new string[0];
        }

        [Serializable] class StepEntry
        {
            public string id, title, instruction, rationale, mode = "Informational", effect = "Cultural";
            public int days = 2;
            public bool chemical;
        }

        [Serializable] class MeasureEntry { public string category = "CropHygiene", title, detail; }

        [Serializable] class LookEntry
        {
            public string mask = "T_LesionMask_SmallSpots", yellowMask = "T_YellowingMask";
            public float[] lesion = { 0.35f, 0.24f, 0.14f }, halo = { 0.80f, 0.74f, 0.34f }, yellow = { 0.80f, 0.74f, 0.34f };
            public float lesions = 0.9f, yellowing = 0.7f, wilting = 0.4f, damage = 0.6f, seconds = 7f;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Adds the PlantSeg diseases to the plants in <paramref name="plants"/> (by plant id) and returns the new plants it created.
        /// </summary>
        public static List<PlantData> Build(FeatureData[] features, List<PlantData> plants)
        {
            var created = new List<PlantData>();
            if (!Directory.Exists(DataFolder)) { Debug.LogWarning($"[AI Plant Doctor] {DataFolder} missing: no PlantSeg diseases."); return created; }
            var byId = plants.Where(p => p != null).ToDictionary(p => p.plantId);

            foreach (var path in Directory.GetFiles(DataFolder, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                PlantFile file;
                try { file = JsonUtility.FromJson<PlantFile>(File.ReadAllText(path)); }
                catch (Exception e) { Debug.LogError($"[AI Plant Doctor] {path}: {e.Message}"); continue; }
                if (file == null || string.IsNullOrEmpty(file.plantId)) { Debug.LogError($"[AI Plant Doctor] {path}: no plantId."); continue; }

                if (!byId.TryGetValue(file.plantId, out var plant))
                {
                    var profile = PlantSegSpecies.For(file.plantId);
                    if (profile == null) { Debug.LogError($"[AI Plant Doctor] {path}: no species profile for {file.plantId}."); continue; }
                    plant = Plant(file.plantId, file.plantName, file.description, features, profile);
                    plant.scientificName = file.scientificName;
                    byId[file.plantId] = plant;
                    created.Add(plant);
                }

                var diseases = new List<DiseaseData>(plant.diseases ?? new DiseaseData[0]);
                // The file's own diseases first, so aliases can name them too (e.g. CropScan class names of PlantSeg diseases).
                foreach (var e in file.diseases)
                {
                    if (diseases.Any(x => x != null && x.diseaseId == e.id)) { Debug.LogError($"[AI Plant Doctor] {path}: duplicate disease {e.id}."); continue; }
                    diseases.Add(Make(file.plantName, file.plantId, e));
                }
                foreach (var a in file.aliases)
                {
                    var d = diseases.FirstOrDefault(x => x != null && x.diseaseId == a.disease);
                    if (d == null) { Debug.LogError($"[AI Plant Doctor] {path}: {file.plantId} has no disease {a.disease} for aliases."); continue; }
                    d.aiLabelAliases = (d.aiLabelAliases ?? new string[0]).Concat(a.labels).Distinct().ToArray();
                    EditorUtility.SetDirty(d);
                }
                plant.diseases = diseases.ToArray();
                EditorUtility.SetDirty(plant);
            }
            return created;
        }

        static DiseaseData Make(string plantName, string plantId, DiseaseEntry e)
        {
            string key = $"{plantName.Replace(" ", "")}_{Pascal(e.id)}";
            var steps = e.steps.Select((s, i) => Step(plantId, e.id, s, i)).ToArray();
            var plan = Plan(key, $"{plantId}_{e.id}_treatment", $"{plantName} — {e.name}", e.severityFloor, e.damageFloor, steps);
            var prevention = Prevention(key, $"{plantId}_{e.id}_prevention",
                e.prevention.Select(m => Measure(Parse<PreventionCategory>(m.category), m.title, m.detail)).ToArray());
            var aliases = new List<string> { $"{plantId}_{e.id}" };
            aliases.AddRange(e.labels);
            var d = Disease(plantName.Replace(" ", ""), e.id, e.name, aliases.Distinct().ToArray(), Parse<PathogenType>(e.type),
                e.description, e.causes, e.symptoms, e.effect, e.untreated, plan, prevention, Look(key, e.look));
            d.pathogen = e.pathogen;
            d.affectedPart = Parse<AffectedPart>(e.part);
            d.sources = e.sources;
            d.lifecycleStages = e.lifecycle;
            d.conditionFactors = e.conditions;
            d.lesionLabel = e.lesionLabel; d.haloLabel = e.haloLabel; d.areaLabel = e.areaLabel;
            d.supportingFeatureIds = Features(d.affectedPart, e.look);
            var photos = new List<Texture2D>();
            foreach (var photo in e.photos)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PhotoFolder}/{photo}.jpg");
                if (tex != null) photos.Add(tex); else Debug.LogWarning($"[AI Plant Doctor] Missing reference photo {photo}.");
            }
            d.referencePhotos = photos.ToArray();
            EditorUtility.SetDirty(d);
            return d;
        }

        static string[] Features(AffectedPart part, LookEntry look)
        {
            var f = new List<string> { "leaf_spots", "leaf_color" };
            if (look.yellowing >= 0.5f) f.Add("yellowing");
            if (look.wilting >= 0.5f || part == AffectedPart.WholePlant) f.Add("wilting");
            return f.ToArray();
        }

        static TreatmentStep Step(string plantId, string diseaseId, StepEntry s, int index)
        {
            string id = string.IsNullOrEmpty(s.id) ? $"{plantId}_{diseaseId}_{index}" : s.id;
            var effect = Effect(s.effect);
            if (s.chemical) return CreateStepWithChemical(id, s.title, s.instruction, s.rationale, s.days, effect);
            var step = CreateStep(id, s.title, s.instruction, s.rationale, Parse<TreatmentInteractionMode>(s.mode), s.days, effect);
            ContentBuilderLink(step);
            return step;
        }

        static void ContentBuilderLink(TreatmentStep step)
        {
            // The step illustration is chosen from the step's wording, as for the PlantVillage diseases.
            step.illustration = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Art/Photos/treatment/{ContentBuilder.IllustrationFor(step)}.jpg");
            EditorUtility.SetDirty(step);
        }

        /// <summary>How much a step moves the plant's state in the simulation (the same four kinds as the PlantVillage diseases).</summary>
        static StateDelta Effect(string kind) => kind switch
        {
            "Remove" => new StateDelta { diseaseSeverity = -0.10f, lesionDensity = -0.22f, yellowing = -0.08f, leafDamage = 0.04f },
            "Protect" => new StateDelta { diseaseSeverity = -0.17f, lesionDensity = -0.17f, healthyGrowth = 0.06f },
            "Small" => new StateDelta { diseaseSeverity = -0.05f, healthyGrowth = 0.03f },
            "Rogue" => new StateDelta { diseaseSeverity = -0.20f, lesionDensity = -0.10f, wilting = -0.10f },   // removing a sick plant protects the rest
            _ => new StateDelta { diseaseSeverity = -0.08f, yellowing = -0.04f, healthyGrowth = 0.06f },
        };

        static SimulationProfile Look(string key, LookEntry l)
        {
            var p = CreateAsset<SimulationProfile>($"Assets/Data/Diseases/SimProfile_{key}.asset");
            p.progressionDurationSeconds = l.seconds;
            p.lesionMask = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Masks}/{l.mask}.png");
            p.yellowingMask = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Masks}/{l.yellowMask}.png");
            if (p.lesionMask == null) Debug.LogError($"[AI Plant Doctor] {key}: mask {l.mask} not found.");
            if (p.yellowingMask == null) Debug.LogError($"[AI Plant Doctor] {key}: mask {l.yellowMask} not found.");
            p.overrideColors = true;
            p.lesionColor = Rgb(l.lesion); p.haloColor = Rgb(l.halo); p.yellowColor = Rgb(l.yellow);
            p.fullExpression = new PlantHealthState
            {
                diseaseSeverity = 1f, lesionDensity = l.lesions, yellowing = l.yellowing, wilting = l.wilting, leafDamage = l.damage, healthyGrowth = 0.05f
            };
            EditorUtility.SetDirty(p);
            return p;
        }

        static Color Rgb(float[] c) => c != null && c.Length >= 3 ? new Color(c[0], c[1], c[2]) : Color.gray;

        static T Parse<T>(string value) where T : struct
        {
            if (Enum.TryParse<T>(value, true, out var v)) return v;
            Debug.LogError($"[AI Plant Doctor] '{value}' is not a {typeof(T).Name}.");
            return default;
        }

        static string Pascal(string id) => string.Concat(id.Split('_').Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w.Substring(1)));
    }
}
#endif
