using System;
using UnityEngine;

namespace PlantDoctor.Treatment
{
    public enum PreventionCategory { CropHygiene, WaterManagement, AirCirculation, Monitoring, EnvironmentalManagement, CropManagement }

    [Serializable]
    public class PreventionMeasure
    {
        public PreventionCategory category;
        public string title;
        [TextArea] public string detail;
    }

    [CreateAssetMenu(menuName = "PlantDoctor/Prevention Plan", fileName = "PreventionPlan_")]
    public class PreventionPlan : ScriptableObject
    {
        public string preventionId;
        [Tooltip("Disease-specific measures. Generic advice only where no specific measure exists.")]
        public PreventionMeasure[] measures;
        public int MeasureCount => measures?.Length ?? 0;
    }
}
