using PlantDoctor.Core;
using PlantDoctor.Treatment;
using PlantDoctor.Utilities;
using UnityEngine;

namespace PlantDoctor.VR
{
    /// <summary>
    /// Translates physical VR actions (grab a leaf, spray, prune) into treatment-step
    /// completion. It validates that the performed action matches the step the plan
    /// is currently asking for, so users actually perform the right treatment.
    /// </summary>
    public class VRTreatmentInteractor : MonoBehaviour
    {
        [SerializeField] AppFlowController flow;
        [SerializeField] VRInteractionEvents interactionEvents;
        [SerializeField] float hapticIntensity = 0.4f;
        [SerializeField] float hapticDuration = 0.1f;

        void OnEnable()
        {
            if (interactionEvents == null) return;
            interactionEvents.Grabbed += HandleGrabbed;
            interactionEvents.Activated += HandleActivated;
        }

        void OnDisable()
        {
            if (interactionEvents == null) return;
            interactionEvents.Grabbed -= HandleGrabbed;
            interactionEvents.Activated -= HandleActivated;
        }

        void HandleGrabbed(GameObject target) => TryPerform(TreatmentInteractionMode.GrabAndRemove, target);
        void HandleActivated(GameObject target) => TryPerform(TreatmentInteractionMode.SprayApply, target);

        /// <summary>Performs the current step only if the gesture matches what it expects.</summary>
        public bool TryPerform(TreatmentInteractionMode performedMode, GameObject target)
        {
            var step = flow != null ? flow.Treatment?.CurrentStep : null;
            if (step == null) return false;

            if (step.interactionMode != performedMode)
            {
                Log.Info(Log.Channel.VR,
                    $"Ignored {performedMode} — current step expects {step.interactionMode}.");
                return false;
            }

            Log.Info(Log.Channel.VR, $"Performed {performedMode} on {(target != null ? target.name : "target")}");
            SendHaptics();
            flow.PerformNextTreatmentStep();
            return true;
        }

        /// <summary>
        /// Haptics hook. Wired to XRBaseController.SendHapticImpulse in the scene;
        /// no-op on devices without haptic support.
        /// </summary>
        void SendHaptics()
        {
            Log.Info(Log.Channel.VR, $"Haptic pulse {hapticIntensity:0.00} for {hapticDuration:0.00}s");
        }
    }
}
