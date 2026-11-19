using UnityEngine;

namespace PlantDoctor.AI
{
    /// <summary>Maps a raw confidence into a band. Thresholds are data, not magic numbers.</summary>
    [CreateAssetMenu(menuName = "PlantDoctor/AI/Confidence Policy", fileName = "ConfidencePolicy")]
    public class ConfidencePolicy : ScriptableObject
    {
        [Range(0f, 1f)] public float highConfidenceThreshold = 0.85f;
        [Range(0f, 1f)] public float moderateConfidenceThreshold = 0.60f;

        public ConfidenceBand Evaluate(float confidence)
        {
            if (confidence >= highConfidenceThreshold) return ConfidenceBand.High;
            if (confidence >= moderateConfidenceThreshold) return ConfidenceBand.Moderate;
            return ConfidenceBand.Uncertain;
        }

        /// <summary>Uncertain results must be phrased as possibilities, never as diagnoses.</summary>
        public string Headline(ConfidenceBand band) => band switch
        {
            ConfidenceBand.High => "DISEASE DETECTED",
            ConfidenceBand.Moderate => "LIKELY DISEASE",
            _ => "POSSIBLE DISEASE"
        };
    }
}
