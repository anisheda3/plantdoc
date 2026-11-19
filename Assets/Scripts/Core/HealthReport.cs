using UnityEngine;

namespace PlantDoctor.Core
{
    /// <summary>Final assessment values computed from the session, not hard-coded.</summary>
    public class HealthReport
    {
        public string PlantName = "Unknown";
        public string DiseaseName = "Unknown";
        public string ConfidenceBandLabel = "Unknown";
        public float Confidence;
        public float InitialSeverity;
        public float FinalSeverity;
        public int TreatmentStepsCompleted;
        public int TreatmentStepsTotal;
        public int PreventionMeasuresReviewed;
        public bool SimulatedAiResult;
        public bool IsHealthy;

        public float RecoveryPercent =>
            InitialSeverity <= Mathf.Epsilon ? 0f
            : Mathf.Clamp01((InitialSeverity - FinalSeverity) / InitialSeverity);

        public string PreventionScore =>
            PreventionMeasuresReviewed >= 5 ? "Excellent"
            : PreventionMeasuresReviewed >= 3 ? "Good"
            : PreventionMeasuresReviewed >= 1 ? "Fair" : "Not reviewed";
    }
}
