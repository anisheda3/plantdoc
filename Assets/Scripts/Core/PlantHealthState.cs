using System;
using UnityEngine;

namespace PlantDoctor.Core
{
    /// <summary>
    /// Single source of truth for how the plant currently looks and how sick it is.
    /// Drives shaders, blendshapes, UI and the final assessment. All values 0..1.
    /// </summary>
    [Serializable]
    public class PlantHealthState
    {
        [Range(0f, 1f)] public float diseaseSeverity;
        [Range(0f, 1f)] public float lesionDensity;
        [Range(0f, 1f)] public float yellowing;
        [Range(0f, 1f)] public float wilting;
        [Range(0f, 1f)] public float leafDamage;
        [Range(0f, 1f)] public float healthyGrowth;

        public PlantHealthState Clone() => (PlantHealthState)MemberwiseClone();

        /// <summary>
        /// Clamps the state so a simulation can never depict a guaranteed complete cure.
        /// Severity and physical leaf damage are held at realistic residual floors.
        /// </summary>
        public void ApplyFloors(float severityFloor, float damageFloor)
        {
            diseaseSeverity = Mathf.Max(diseaseSeverity, severityFloor);
            leafDamage = Mathf.Max(leafDamage, damageFloor);
        }

        public void Apply(StateDelta delta, float scale = 1f)
        {
            if (delta == null) return;
            diseaseSeverity = Clamp(diseaseSeverity + delta.diseaseSeverity * scale);
            lesionDensity   = Clamp(lesionDensity   + delta.lesionDensity   * scale);
            yellowing       = Clamp(yellowing       + delta.yellowing       * scale);
            wilting         = Clamp(wilting         + delta.wilting         * scale);
            leafDamage      = Clamp(leafDamage      + delta.leafDamage      * scale);
            healthyGrowth   = Clamp(healthyGrowth   + delta.healthyGrowth   * scale);
        }

        public static PlantHealthState Lerp(PlantHealthState a, PlantHealthState b, float t)
        {
            t = Mathf.Clamp01(t);
            return new PlantHealthState
            {
                diseaseSeverity = Mathf.Lerp(a.diseaseSeverity, b.diseaseSeverity, t),
                lesionDensity   = Mathf.Lerp(a.lesionDensity,   b.lesionDensity,   t),
                yellowing       = Mathf.Lerp(a.yellowing,       b.yellowing,       t),
                wilting         = Mathf.Lerp(a.wilting,         b.wilting,         t),
                leafDamage      = Mathf.Lerp(a.leafDamage,      b.leafDamage,      t),
                healthyGrowth   = Mathf.Lerp(a.healthyGrowth,   b.healthyGrowth,   t)
            };
        }

        /// <summary>Overall vitality 0..1 used by the final report.</summary>
        public float Vitality =>
            Clamp(1f - (diseaseSeverity * 0.4f + lesionDensity * 0.2f + yellowing * 0.15f
                        + wilting * 0.15f + leafDamage * 0.1f) + healthyGrowth * 0.2f);

        static float Clamp(float v) => Mathf.Clamp01(v);
    }

    /// <summary>Change applied to a PlantHealthState by a treatment step or a progression tick.</summary>
    [Serializable]
    public class StateDelta
    {
        [Range(-1f, 1f)] public float diseaseSeverity;
        [Range(-1f, 1f)] public float lesionDensity;
        [Range(-1f, 1f)] public float yellowing;
        [Range(-1f, 1f)] public float wilting;
        [Range(-1f, 1f)] public float leafDamage;
        [Range(-1f, 1f)] public float healthyGrowth;
    }
}
