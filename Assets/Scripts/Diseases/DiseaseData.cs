using System;
using PlantDoctor.Treatment;
using UnityEngine;

namespace PlantDoctor.Diseases
{
    /// <summary>Oomycete = water moulds (downy mildews, late blight): fungus-like but not fungi. Appended last so saved assets keep their values.</summary>
    public enum PathogenType { Fungal, Bacterial, Viral, Pest, Abiotic, Unknown, Oomycete }
    public enum SeverityBand { Mild, Moderate, Severe }

    /// <summary>Where the disease shows: the zoom step shows that part, and the twin shows the symptom there.</summary>
    public enum AffectedPart
    {
        Leaf,
        /// <summary>Fruit or berries (blossom end rot, brown rot, mummy berry, anthracnose fruit rot).</summary>
        Fruit,
        /// <summary>A head, ear or flower spike (wheat head scab, corn smut, cauliflower curd rot).</summary>
        Head,
        /// <summary>The storage root, bulb or tuber below ground (carrot cavity spot).</summary>
        Root,
        /// <summary>The whole plant wilts, yellows or is stunted (bacterial wilt, Panama disease, bunchy top).</summary>
        WholePlant
    }

    [CreateAssetMenu(menuName = "PlantDoctor/Disease Data", fileName = "Disease_")]
    public class DiseaseData : ScriptableObject
    {
        [Header("Identity")]
        public string diseaseId = "early_blight";
        public string diseaseName = "Early Blight";
        [Tooltip("Extra AI label strings that resolve to this disease (model labels are DATA).")]
        public string[] aiLabelAliases;
        public PathogenType pathogenType = PathogenType.Fungal;
        [Tooltip("The causal organism's scientific name (or the cause, for a disorder).")]
        public string pathogen;
        public AffectedPart affectedPart = AffectedPart.Leaf;
        [Tooltip("Where the facts on this page come from (extension services, floras, APS).")]
        public string[] sources;

        [Header("Education")]
        [TextArea] public string description;
        [TextArea] public string causes;
        [TextArea] public string effectOnPlant;
        [TextArea] public string untreatedOutcome;
        public string[] symptoms;

        [Tooltip("Feature ids whose abnormality supports this diagnosis.")]
        public string[] supportingFeatureIds;

        [Header("Severity banding")]
        [Range(0f, 1f)] public float moderateSeverityThreshold = 0.35f;
        [Range(0f, 1f)] public float severeSeverityThreshold = 0.70f;

        [Header("Modules")]
        public TreatmentPlan treatmentPlan;
        public PreventionPlan preventionPlan;
        public SimulationProfile simulationProfile;

        [Header("Presentation (educational content shown in the app)")]
        [Tooltip("Four short stages of how the disease develops and spreads.")]
        public string[] lifecycleStages;
        [Tooltip("Four conditions that favour the disease (weather, practices).")]
        public string[] conditionFactors;
        [Tooltip("Typical symptom photos (licensed reference images, not the user's plant).")]
        public Texture2D[] referencePhotos;
        [Tooltip("Callout texts on the zoomed leaf.")]
        public string lesionLabel = "Dark spot";
        public string haloLabel = "Yellow halo";
        public string areaLabel = "Affected area";

        public bool Matches(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (string.Equals(id, diseaseId, StringComparison.OrdinalIgnoreCase)) return true;
            if (aiLabelAliases == null) return false;
            foreach (var a in aiLabelAliases)
                if (string.Equals(id, a, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public SeverityBand BandFor(float severity) =>
            severity >= severeSeverityThreshold ? SeverityBand.Severe
            : severity >= moderateSeverityThreshold ? SeverityBand.Moderate
            : SeverityBand.Mild;
    }
}
