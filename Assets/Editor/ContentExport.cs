#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using PlantDoctor.Core;
using PlantDoctor.Diseases;
using PlantDoctor.Plants;
using PlantDoctor.Treatment;
using UnityEditor;
using UnityEngine;

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// Dumps the app's plant and disease content (everything the PlantDatabase holds) to Database/_export/content.json, from which
    /// tools/data/make_content_db.py builds the content database (Database/plant_doctor_content.sql and .db).
    ///   Unity.exe -batchmode -projectPath E:\plant -executeMethod PlantDoctor.EditorTools.ContentExport.Run
    /// </summary>
    public static class ContentExport
    {
        public const string OutFile = "Database/_export/content.json";

        [Serializable] class Step { public string id, title, instruction, rationale, mode; public int ord, days; public bool chemical; public float severityChange, lesionChange, yellowingChange, wiltingChange, damageChange, growthChange; }
        [Serializable] class Measure { public int ord; public string category, title, detail; }
        [Serializable] class Disease
        {
            public string plantId, id, name, pathogen, pathogenType, affectedPart, description, causes, effect, untreated;
            public float moderateThreshold, severeThreshold, severityFloor, damageFloor, fullLesions, fullYellowing, fullWilting, fullDamage;
            public string[] symptoms, lifecycle, conditions, aliases, sources;
            public Step[] steps; public Measure[] prevention;
        }
        [Serializable] class Plant { public string id, name, scientificName, description, archetype, fruit; public float height; }
        [Serializable] class Dump { public Plant[] plants; public Disease[] diseases; }

        [MenuItem("Tools/AI Plant Doctor/Export Content Database")]
        public static void Run()
        {
            bool ok = false;
            try { ok = Export(); }
            catch (Exception e) { Debug.LogException(e); }
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Export()
        {
            var db = AssetDatabase.LoadAssetAtPath<PlantDatabase>("Assets/Data/Plants/PlantDatabase.asset");
            if (db == null) { Debug.LogError("PlantDatabase missing; run the project setup first."); return false; }
            var plants = new List<Plant>();
            var diseases = new List<Disease>();
            foreach (var p in db.plants)
            {
                if (p == null) continue;
                plants.Add(new Plant { id = p.plantId, name = p.plantName, scientificName = p.scientificName, description = p.description,
                    archetype = p.species != null ? p.species.archetype.ToString() : "", fruit = p.species != null ? p.species.fruit.ToString() : "",
                    height = p.species != null ? p.species.height : 0f });
                if (p.diseases == null) continue;
                foreach (var d in p.diseases)
                {
                    if (d == null) continue;
                    var plan = d.treatmentPlan;
                    var steps = new List<Step>();
                    if (plan != null && plan.steps != null)
                        for (int i = 0; i < plan.steps.Length; i++)
                        {
                            var s = plan.steps[i];
                            if (s == null) continue;
                            var e = s.effect ?? new StateDelta();
                            steps.Add(new Step { id = s.stepId, ord = (i + 1) * 10, title = s.title, instruction = s.instruction, rationale = s.rationale,
                                mode = s.interactionMode.ToString(), days = s.simulatedDaysAdvanced, chemical = s.chemicalInfo != null && s.chemicalInfo.involvesChemicalProduct,
                                severityChange = e.diseaseSeverity, lesionChange = e.lesionDensity, yellowingChange = e.yellowing, wiltingChange = e.wilting,
                                damageChange = e.leafDamage, growthChange = e.healthyGrowth });
                        }
                    var measures = new List<Measure>();
                    if (d.preventionPlan != null && d.preventionPlan.measures != null)
                        for (int i = 0; i < d.preventionPlan.measures.Length; i++)
                        {
                            var m = d.preventionPlan.measures[i];
                            if (m != null) measures.Add(new Measure { ord = (i + 1) * 10, category = m.category.ToString(), title = m.title, detail = m.detail });
                        }
                    var full = d.simulationProfile != null ? d.simulationProfile.fullExpression : null;
                    diseases.Add(new Disease
                    {
                        plantId = p.plantId, id = d.diseaseId, name = d.diseaseName, pathogen = d.pathogen ?? "", pathogenType = d.pathogenType.ToString(),
                        affectedPart = d.affectedPart.ToString(), description = d.description, causes = d.causes, effect = d.effectOnPlant, untreated = d.untreatedOutcome,
                        moderateThreshold = d.moderateSeverityThreshold, severeThreshold = d.severeSeverityThreshold,
                        severityFloor = plan != null ? plan.residualSeverityFloor : 0f, damageFloor = plan != null ? plan.residualDamageFloor : 0f,
                        fullLesions = full != null ? full.lesionDensity : 0f, fullYellowing = full != null ? full.yellowing : 0f,
                        fullWilting = full != null ? full.wilting : 0f, fullDamage = full != null ? full.leafDamage : 0f,
                        symptoms = d.symptoms ?? new string[0], lifecycle = d.lifecycleStages ?? new string[0], conditions = d.conditionFactors ?? new string[0],
                        aliases = d.aiLabelAliases ?? new string[0], sources = d.sources ?? new string[0], steps = steps.ToArray(), prevention = measures.ToArray()
                    });
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(OutFile));
            File.WriteAllText(OutFile, JsonUtility.ToJson(new Dump { plants = plants.ToArray(), diseases = diseases.ToArray() }, true));
            Debug.Log($"[AI Plant Doctor] Exported {plants.Count} plants and {diseases.Count} diseases to {OutFile}");
            return true;
        }
    }
}
#endif
