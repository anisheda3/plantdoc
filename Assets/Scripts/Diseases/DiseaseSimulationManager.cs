using System.Collections;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using PlantDoctor.Utilities;
using UnityEngine;

namespace PlantDoctor.Diseases
{
    /// <summary>
    /// Animates healthy -> diseased progression by driving PlantHealthState over time.
    /// Never swaps the plant model.
    /// </summary>
    public class DiseaseSimulationManager : MonoBehaviour
    {
        [SerializeField] PlantVisualBinder visualBinder;

        Coroutine _routine;
        SessionContext _context;
        // Replaying the spread after treatment has started must not undo the treatment.
        bool _altersSession = true;

        public bool IsPlaying => _routine != null;

        public void PlayProgression(SessionContext context)
        {
            if (context?.Disease?.simulationProfile == null)
            {
                Log.Warn(Log.Channel.Sim, "No simulation profile for this disease; skipping progression.");
                return;
            }
            Stop();
            if (!isActiveAndEnabled) return;   // StartCoroutine throws on an inactive object
            _context = context;
            _altersSession = context.CompletedTreatmentSteps == 0 && context.SimulatedDay == 0;
            _routine = StartCoroutine(ProgressionRoutine(context));
        }

        IEnumerator ProgressionRoutine(SessionContext context)
        {
            var profile = context.Disease.simulationProfile;
            float duration = Mathf.Max(0.1f, profile.progressionDurationSeconds);
            float severity = context.Analysis?.severity ?? 0f;
            Log.Info(Log.Channel.Sim, $"Loading {context.Plant?.plantName} / {context.Disease.diseaseName} progression ({duration}s)");

            // Replay from healthy tissue; the end state is what the scan observed.
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                var state = profile.Evaluate(severity, t);
                if (_altersSession) context.CurrentState = state;
                visualBinder?.SetTargetState(state);
                yield return null;
            }

            Finish(context);
        }

        /// <summary>
        /// Settles on the fully-progressed state. Called when the replay ends naturally or
        /// is skipped: leaving the stage early must not hand treatment a half-progressed
        /// plant (which made the first treatment step look like a near-total cure).
        /// </summary>
        void Finish(SessionContext context)
        {
            _routine = null;
            _context = null;
            var profile = context?.Disease?.simulationProfile;
            if (profile == null) return;

            var final = profile.Evaluate(context.Analysis?.severity ?? 0f, 1f);
            if (_altersSession)
            {
                context.CurrentState = final;
                context.InitialState = final.Clone();
                visualBinder?.SetTargetState(final);
            }
            else
            {
                visualBinder?.SetTargetState(context.CurrentState);   // back to where treatment left the plant
            }
            Log.Info(Log.Channel.Sim, "Progression complete.");
        }

        public void Stop()
        {
            if (_routine == null) return;
            StopCoroutine(_routine);
            Finish(_context);
        }

        /// <summary>
        /// Unity silently kills running coroutines when a GameObject is disabled
        /// or destroyed, but it does not clear our handle. Without this, _routine
        /// stays non-null forever and IsPlaying reports a false positive, so the
        /// simulation can never be restarted.
        /// </summary>
        void OnDisable() => Stop();
    }
}
