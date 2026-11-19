using PlantDoctor.AI;
using UnityEngine;

namespace PlantDoctor.Plants
{
    /// <summary>Definition of one health parameter and its educational copy.</summary>
    [CreateAssetMenu(menuName = "PlantDoctor/Feature Data", fileName = "Feature_")]
    public class FeatureData : ScriptableObject
    {
        public string featureId = "leaf_color";
        public string displayName = "Leaf Color";
        public MeasurementSource source = MeasurementSource.VisualAiEstimate;

        [Range(0f, 1f)] public float optimalValue = 0.95f;
        [Range(0f, 1f)] public float tolerance = 0.10f;
        [Tooltip("If true a higher observed value is healthier (e.g. leaf color).")]
        public bool higherIsBetter = true;

        [TextArea] public string meaning;
        [TextArea] public string whyItMatters;
        public Color overlayColor = Color.yellow;

        public FeatureStatus Evaluate(float observed)
        {
            float diff = higherIsBetter ? optimalValue - observed : observed - optimalValue;
            return diff > tolerance ? FeatureStatus.Abnormal : FeatureStatus.Normal;
        }
    }
}
