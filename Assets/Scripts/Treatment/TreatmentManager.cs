using PlantDoctor.Core;
using PlantDoctor.Plants;
using PlantDoctor.Utilities;
using UnityEngine;

namespace PlantDoctor.Treatment
{
    /// <summary>
    /// Drives the numbered treatment workflow. Steps and their effects are data;
    /// this class contains no disease-specific logic.
    /// </summary>
    public class TreatmentManager : MonoBehaviour
    {
        [SerializeField] PlantVisualBinder visualBinder;
        [SerializeField] Simulation.RecoveryManager recoveryManager;

        SessionContext _context;
        TreatmentPlan _plan;
        int _index = -1;
        TreatmentPlan _beganPlan;
        SessionContext _beganContext;

        public TreatmentPlan Plan => _plan;
        public int CurrentIndex => _index;
        public TreatmentStep CurrentStep => _plan?.GetStep(_index);
        public bool HasPlan => _plan != null && _plan.StepCount > 0;

        /// <summary>
        /// True once the current step has been carried out. Each step applies its effect
        /// exactly once; without this, tapping PERFORM repeatedly stacked the same
        /// treatment and drove severity straight to the floor.
        /// </summary>
        public bool CurrentStepPerformed { get; private set; }

        public bool Begin(SessionContext context)
        {
            // Coming back to the treatment screen must not wipe the steps already done.
            var wanted = context?.Disease?.treatmentPlan;
            if (wanted != null && _beganPlan == wanted && _beganContext == context && _index >= 0)
            {
                _context = context; _plan = wanted;
                return true;
            }
            _context = context;
            _plan = wanted;

            if (!HasPlan)
            {
                Log.Warn(Log.Channel.Treatment, "No treatment plan available for this diagnosis.");
                GameEvents.RaiseError("MISSING_TREATMENT_DATA",
                    "No treatment plan is available for this diagnosis yet.");
                return false;
            }

            _index = 0;
            _beganPlan = _plan; _beganContext = context;
            CurrentStepPerformed = false;
            if (recoveryManager != null)
            {
                recoveryManager.SeverityFloor = _plan.residualSeverityFloor;
                recoveryManager.DamageFloor = _plan.residualDamageFloor;
            }
            context.TotalTreatmentSteps = _plan.StepCount;
            context.CompletedTreatmentSteps = 0;
            Log.Info(Log.Channel.Treatment, $"Plan '{_plan.planName}' started ({_plan.StepCount} steps)");
            GameEvents.RaiseTreatmentStarted(_plan);
            return true;
        }

        /// <summary>Called when the user actually performs the current step (tap/grab/spray).</summary>
        public void PerformCurrentStep(bool animate = true)
        {
            var step = CurrentStep;
            if (step == null || _context == null || CurrentStepPerformed) return;

            CurrentStepPerformed = true;
            Log.Info(Log.Channel.Treatment, $"Step {_index + 1} started: {step.title}");
            _context.CurrentState.Apply(step.effect);
            // Treatment is never modelled as a guaranteed cure (see TreatmentPlan floors).
            _context.CurrentState.ApplyFloors(_plan.residualSeverityFloor, _plan.residualDamageFloor);
            _context.CompletedTreatmentSteps = _index + 1;
            _context.SimulatedDay += step.simulatedDaysAdvanced;
            if (animate) visualBinder?.SetTargetState(_context.CurrentState);

            GameEvents.RaiseTreatmentStepCompleted(step, _index + 1, _plan.StepCount);
            Log.Info(Log.Channel.Treatment, $"Step {_index + 1} completed");

            // Every step is followed by a short time-lapse so the user sees the effect.
            if (animate) recoveryManager?.PlayStepTimeLapse(_context, step.simulatedDaysAdvanced);
        }

        public bool Advance()
        {
            if (_plan == null) return false;
            if (_index + 1 >= _plan.StepCount)
            {
                Log.Info(Log.Channel.Treatment, "All treatment steps completed.");
                GameEvents.RaiseTreatmentCompleted();
                return false;
            }
            _index++;
            CurrentStepPerformed = false;
            return true;
        }

        public void ResetPlan() { _plan = null; _index = -1; CurrentStepPerformed = false; _beganPlan = null; _beganContext = null; }

        /// <summary>Steps already performed (they stay done when the screen is left and reopened).</summary>
        public int CompletedCount => _context != null ? _context.CompletedTreatmentSteps : 0;
    }
}
