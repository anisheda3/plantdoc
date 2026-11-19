using System.Collections;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using PlantDoctor.Utilities;
using UnityEngine;

namespace PlantDoctor.Simulation
{
    /// <summary>
    /// Time-lapse driver. Interpolates PlantHealthState across simulated days so the
    /// user SEES recovery; the plant model is never swapped for a healthy one.
    /// </summary>
    public class RecoveryManager : MonoBehaviour
    {
        [SerializeField] PlantVisualBinder visualBinder;
        [SerializeField] float secondsPerSimulatedDay = 0.6f;
        [SerializeField] float naturalRecoveryPerDay = 0.02f;

        Coroutine _routine;
        public bool IsPlaying => _routine != null;

        /// <summary>Residual floors sourced from the active treatment plan, so recovery
        /// asymptotes toward a realistic outcome rather than a perfect cure.</summary>
        public float SeverityFloor { get; set; }
        public float DamageFloor { get; set; }

        /// <summary>
        /// The state the plant would be in after the given number of days of natural recovery,
        /// never better than the treatment plan's residual floors. Used by the recovery timeline.
        /// </summary>
        public PlantHealthState Project(PlantHealthState start, int days)
        {
            var end = start.Clone();
            float recovery = naturalRecoveryPerDay * days;
            end.Apply(new StateDelta
            {
                diseaseSeverity = -recovery,
                lesionDensity = -recovery * 0.9f,
                yellowing = -recovery * 0.8f,
                wilting = -recovery * 0.7f,
                leafDamage = -recovery * 0.3f,
                healthyGrowth = recovery * 1.2f
            });
            end.ApplyFloors(SeverityFloor, DamageFloor);
            return end;
        }

        /// <summary>Short lapse played right after a treatment step.</summary>
        public void PlayStepTimeLapse(SessionContext context, int days)
        {
            if (context == null || days <= 0) return;
            Stop();
            if (!isActiveAndEnabled) return;   // StartCoroutine throws on an inactive object
            _routine = StartCoroutine(LapseRoutine(context, days));
        }

        /// <summary>Longer consolidated recovery shown at the RECOVERY_TIMELINE stage.</summary>
        public void PlayFullRecovery(SessionContext context, int days = 10)
        {
            if (context == null) return;
            Stop();
            if (!isActiveAndEnabled) return;   // StartCoroutine throws on an inactive object
            _routine = StartCoroutine(LapseRoutine(context, days, raiseCompletion: true));
        }

        IEnumerator LapseRoutine(SessionContext context, int days, bool raiseCompletion = false)
        {
            var start = context.CurrentState.Clone();
            var end = start.Clone();

            float recovery = naturalRecoveryPerDay * days;
            end.Apply(new StateDelta
            {
                diseaseSeverity = -recovery,
                lesionDensity = -recovery * 0.9f,
                yellowing = -recovery * 0.8f,
                wilting = -recovery * 0.7f,
                leafDamage = -recovery * 0.3f,   // physical damage heals slowest
                healthyGrowth = recovery * 1.2f
            });
            end.ApplyFloors(SeverityFloor, DamageFloor);

            float duration = Mathf.Max(0.05f, days * secondsPerSimulatedDay);
            float elapsed = 0f;
            int lastReportedDay = -1;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                context.CurrentState = PlantHealthState.Lerp(start, end, t);
                visualBinder?.SetTargetState(context.CurrentState);

                int day = Mathf.FloorToInt(t * days);
                if (day != lastReportedDay)
                {
                    lastReportedDay = day;
                    GameEvents.RaiseRecoveryTick(context.SimulatedDay + day, context.CurrentState);
                }
                yield return null;
            }

            context.CurrentState = end;
            context.SimulatedDay += days;
            visualBinder?.SetTargetState(end);
            Log.Info(Log.Channel.Sim, $"Time-lapse finished at day {context.SimulatedDay}, severity {end.diseaseSeverity:0.00}");

            _routine = null;
            if (raiseCompletion) GameEvents.RaiseRecoveryCompleted();
        }

        public void Stop()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
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
