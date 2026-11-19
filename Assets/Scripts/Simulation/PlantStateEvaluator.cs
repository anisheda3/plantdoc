using PlantDoctor.AI;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using UnityEngine;

namespace PlantDoctor.Simulation
{
    /// <summary>Seeds a PlantHealthState from an AI result. Pure function, easy to unit test.</summary>
    public static class PlantStateEvaluator
    {
        public static PlantHealthState FromAnalysis(PlantAnalysisResult analysis, PlantData plant)
        {
            var state = new PlantHealthState();
            if (analysis == null) return state;

            state.diseaseSeverity = Mathf.Clamp01(analysis.severity);
            state.lesionDensity = Read(analysis, "leaf_spots", "lesion_density");
            state.yellowing = Read(analysis, "yellowing");
            state.wilting = Read(analysis, "wilting");
            state.leafDamage = Mathf.Clamp01(state.lesionDensity * 0.6f + state.wilting * 0.4f);

            var colour = analysis.GetFeature("leaf_color");
            state.healthyGrowth = colour != null
                ? Mathf.Clamp01(colour.value * (1f - state.diseaseSeverity))
                : Mathf.Clamp01(1f - state.diseaseSeverity);

            return state;
        }

        static float Read(PlantAnalysisResult analysis, params string[] ids)
        {
            foreach (var id in ids)
            {
                var f = analysis.GetFeature(id);
                if (f != null) return Mathf.Clamp01(f.value);
            }
            return 0f;
        }
    }
}
