#if UNITY_EDITOR
using System;
using System.IO;
using PlantDoctor.Diseases;
using PlantDoctor.Plants;
using PlantDoctor.Treatment;
using UnityEditor;
using UnityEngine;

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// Applies the content database (its app snapshot, Assets/Resources/Content/plant_doctor_content.json, published by
    /// tools/data/make_content_db.py) to the disease assets at project setup, so an edit made in the database reaches every screen:
    /// names, descriptions, causes, symptoms, lifecycle, conditions, treatment step texts and prevention texts. Diseases the database
    /// does not know keep their built-in content.
    /// </summary>
    public static class ContentDatabaseImporter
    {
        const string Snapshot = "Assets/Resources/Content/plant_doctor_content.json";

        public static void Apply()
        {
            if (!File.Exists(Snapshot)) { Debug.LogWarning($"[AI Plant Doctor] {Snapshot} missing: run tools/data/make_content_db.py. Built-in content kept."); return; }
            if (!ContentDatabase.Parse(File.ReadAllText(Snapshot))) { Debug.LogError($"[AI Plant Doctor] {Snapshot} could not be read."); return; }
            var db = AssetDatabase.LoadAssetAtPath<PlantDatabase>("Assets/Data/Plants/PlantDatabase.asset");
            if (db == null) return;
            int applied = 0, missing = 0;
            foreach (var plant in db.plants)
            {
                if (plant?.diseases == null) continue;
                foreach (var d in plant.diseases)
                {
                    if (d == null) continue;
                    var c = ContentDatabase.Find(plant.plantId, d.diseaseId);
                    if (c == null) { missing++; continue; }
                    d.diseaseName = c.name;
                    d.pathogen = c.pathogen;
                    d.description = c.description;
                    d.causes = c.causes;
                    d.effectOnPlant = c.effect;
                    d.untreatedOutcome = c.untreated;
                    if (c.symptoms != null && c.symptoms.Length > 0) d.symptoms = c.symptoms;
                    if (c.lifecycle != null && c.lifecycle.Length > 0) d.lifecycleStages = c.lifecycle;
                    if (c.conditions != null && c.conditions.Length > 0) d.conditionFactors = c.conditions;
                    if (Enum.TryParse<AffectedPart>(c.affectedPart, out var part)) d.affectedPart = part;
                    if (d.treatmentPlan?.steps != null && c.steps != null)
                        foreach (var step in d.treatmentPlan.steps)
                        {
                            if (step == null) continue;
                            var s = Array.Find(c.steps, x => x.stepId == step.stepId);
                            if (s == null) continue;
                            step.title = s.title; step.instruction = s.instruction; step.rationale = s.rationale;
                            EditorUtility.SetDirty(step);
                        }
                    if (d.preventionPlan?.measures != null && c.prevention != null && c.prevention.Length == d.preventionPlan.measures.Length)
                    {
                        for (int i = 0; i < c.prevention.Length; i++)
                        {
                            var m = d.preventionPlan.measures[i];
                            m.title = c.prevention[i].title; m.detail = c.prevention[i].detail;
                            if (Enum.TryParse<PreventionCategory>(c.prevention[i].category, out var cat)) m.category = cat;
                        }
                        EditorUtility.SetDirty(d.preventionPlan);
                    }
                    EditorUtility.SetDirty(d);
                    applied++;
                }
            }
            Debug.Log($"[AI Plant Doctor] Content database v{ContentDatabase.Version} applied to {applied} diseases ({missing} not in the database).");
        }
    }
}
#endif
