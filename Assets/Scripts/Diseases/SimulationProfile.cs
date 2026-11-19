using PlantDoctor.Core;
using UnityEngine;

namespace PlantDoctor.Diseases
{
    /// <summary>
    /// Describes how a disease looks and evolves. Drives shader params over time
    /// instead of swapping the whole plant model.
    /// </summary>
    [CreateAssetMenu(menuName = "PlantDoctor/Simulation Profile", fileName = "SimProfile_")]
    public class SimulationProfile : ScriptableObject
    {
        [Header("Progression (healthy -> full expression)")]
        public float progressionDurationSeconds = 8f;
        public AnimationCurve lesionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve yellowingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve wiltingCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Recovery")]
        public float recoveryDurationSeconds = 4f;
        public AnimationCurve recoveryCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Visuals — TODO[ASSET]: assign authored masks/VFX")]
        public Texture2D lesionMask;
        public Texture2D yellowingMask;
        public GameObject sporeVfxPrefab;

        [Header("Colours on the twin (e.g. rust is orange, powdery mildew is white)")]
        public bool overrideColors;
        public Color lesionColor = new Color(0.22f, 0.13f, 0.06f);
        public Color haloColor = new Color(0.76f, 0.70f, 0.34f);
        public Color yellowColor = new Color(0.78f, 0.70f, 0.33f);

        [Header("Fully-expressed disease state at severity 1.0")]
        public PlantHealthState fullExpression = new PlantHealthState
        {
            diseaseSeverity = 1f, lesionDensity = 0.9f, yellowing = 0.8f,
            wilting = 0.6f, leafDamage = 0.7f, healthyGrowth = 0.05f
        };

        /// <summary>Interpolated state for a given severity and progression t (0..1).</summary>
        public PlantHealthState Evaluate(float severity, float t)
        {
            severity = Mathf.Clamp01(severity);
            return new PlantHealthState
            {
                diseaseSeverity = severity * t,
                lesionDensity = fullExpression.lesionDensity * severity * lesionCurve.Evaluate(t),
                yellowing     = fullExpression.yellowing     * severity * yellowingCurve.Evaluate(t),
                wilting       = fullExpression.wilting       * severity * wiltingCurve.Evaluate(t),
                leafDamage    = fullExpression.leafDamage    * severity * lesionCurve.Evaluate(t),
                healthyGrowth = Mathf.Lerp(0.6f, fullExpression.healthyGrowth, severity * t)
            };
        }
    }
}
