using PlantDoctor.Core;
using UnityEngine;

namespace PlantDoctor.Treatment
{
    public enum TreatmentInteractionMode { Informational, TapRegion, GrabAndRemove, SprayApply, AdjustSetting }

    [CreateAssetMenu(menuName = "PlantDoctor/Treatment Step", fileName = "Step_")]
    public class TreatmentStep : ScriptableObject
    {
        public string stepId;
        public string title;
        [TextArea] public string instruction;
        [TextArea] public string rationale;

        public TreatmentInteractionMode interactionMode = TreatmentInteractionMode.Informational;
        public string targetRegionType = "lesion";

        [Tooltip("Applied to PlantHealthState when the step is performed.")]
        public StateDelta effect = new StateDelta();

        [Min(0)] public int simulatedDaysAdvanced = 2;
        public ChemicalTreatmentInfo chemicalInfo = new ChemicalTreatmentInfo();

        [Tooltip("Photo shown on the step's card.")]
        public Texture2D illustration;
        [Tooltip("Shown on the card when set (label-following and protective-equipment guidance).")]
        [TextArea] public string safetyNote;
    }
}
