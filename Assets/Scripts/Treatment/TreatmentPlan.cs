using UnityEngine;

namespace PlantDoctor.Treatment
{
    [CreateAssetMenu(menuName = "PlantDoctor/Treatment Plan", fileName = "TreatmentPlan_")]
    public class TreatmentPlan : ScriptableObject
    {
        public string treatmentId;
        public string planName;
        public TreatmentStep[] steps;

        [Header("Realism")]
        [Tooltip("Lowest severity this plan can realistically reach. Treatment is never " +
                 "modelled as a guaranteed cure: necrotic tissue does not regenerate and " +
                 "some residual disease pressure remains.")]
        [Range(0f, 1f)] public float residualSeverityFloor = 0.15f;

        [Tooltip("Damaged leaf tissue that cannot heal, only be outgrown.")]
        [Range(0f, 1f)] public float residualDamageFloor = 0.10f;

        [TextArea] public string disclaimer =
            "Educational guidance only. Treatment does not guarantee recovery. Consult a local agricultural extension service for field decisions.";

        public int StepCount => steps?.Length ?? 0;
        public TreatmentStep GetStep(int index) =>
            (steps != null && index >= 0 && index < steps.Length) ? steps[index] : null;
    }
}
