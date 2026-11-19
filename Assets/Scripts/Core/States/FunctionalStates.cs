using PlantDoctor.Diseases;
using PlantDoctor.Plants;
using PlantDoctor.Simulation;
using PlantDoctor.Treatment;
using PlantDoctor.Utilities;

namespace PlantDoctor.Core.States
{
    public class FeatureAnalysisState : StateBase
    {
        readonly FeatureAnalysisManager _features;
        public FeatureAnalysisState(FeatureAnalysisManager features) => _features = features;
        public override SimState Id => SimState.FeatureAnalysis;

        public override void Enter()
        {
            var rows = _features != null ? _features.Build(Context) : null;
            Log.Info(Log.Channel.Sim, $"Feature analysis built ({rows?.Count ?? 0} parameters)");
        }
    }

    public class DiagnosisState : StateBase
    {
        public override SimState Id => SimState.Diagnosis;
        public override void Enter()
        {
            if (Context.IsHealthy)
            {
                Log.Info(Log.Channel.Sim, "No disease detected; diagnosis reports a healthy plant.");
                return;
            }
            if (Context.Disease == null)
            {
                GameEvents.RaiseError("MISSING_SIMULATION_DATA",
                    $"The AI identified '{Context.Analysis?.diseaseName}', but no simulation is available for it yet.");
                Machine.Transition(SimState.Error);
                return;
            }
            GameEvents.RaiseDiseaseDetected(Context.Disease, Context.Confidence);
        }
    }

    public class DiseaseSimulationState : StateBase
    {
        readonly DiseaseSimulationManager _sim;
        public DiseaseSimulationState(DiseaseSimulationManager sim) => _sim = sim;
        public override SimState Id => SimState.DiseaseSimulation;
        public override void Enter() => _sim?.PlayProgression(Context);
        public override void Exit() => _sim?.Stop();
    }

    public class TreatmentIntroState : StateBase
    {
        readonly TreatmentManager _treatment;
        public TreatmentIntroState(TreatmentManager treatment) => _treatment = treatment;
        public override SimState Id => SimState.TreatmentIntro;

        public override void Enter()
        {
            if (_treatment == null || !_treatment.Begin(Context))
            {
                Machine.Transition(SimState.Error);
                return;
            }
            GameEvents.RaiseStageContentChanged();   // the page was shown before the plan was ready
        }
    }

    public class TreatmentStepState : StateBase
    {
        public override SimState Id => SimState.TreatmentStep;
        public override void Enter() => Log.Info(Log.Channel.Treatment, "Treatment step stage");
    }

    public class RecoveryTimelineState : StateBase
    {
        readonly RecoveryManager _recovery;
        readonly int _days;
        readonly bool _playOnEnter;
        public RecoveryTimelineState(RecoveryManager recovery, int days = 10, bool playOnEnter = false)
        {
            _recovery = recovery; _days = days; _playOnEnter = playOnEnter;
        }
        public override SimState Id => SimState.RecoveryTimeline;
        // The classic interface plays the recovery time-lapse here; the dashboard drives its own timeline,
        // so this stage only owns the exit cleanup.
        public override void Enter()
        {
            if (_playOnEnter && !Context.RecoveryPlayed)
            {
                Context.RecoveryPlayed = true;   // coming back to this step later shows the result, it does not run another ten days
                _recovery?.PlayFullRecovery(Context, _days);
            }
            else _recovery?.Stop();
        }
        public override void Exit() => _recovery?.Stop();
    }

    public class PreventionState : StateBase
    {
        readonly PreventionManager _prevention;
        public PreventionState(PreventionManager prevention) => _prevention = prevention;
        public override SimState Id => SimState.Prevention;
        public override void Enter()
        {
            _prevention?.Begin(Context);
            GameEvents.RaiseStageContentChanged();   // the page was shown before the plan was ready
        }
    }

    public class FinalReportState : StateBase
    {
        public override SimState Id => SimState.FinalReport;
        public override void Enter()
        {
            var report = Context.BuildReport();
            Log.Info(Log.Channel.Sim,
                $"Report: {report.PlantName}/{report.DiseaseName} recovery {report.RecoveryPercent:P0}");
            GameEvents.RaiseReportReady(report);
        }
    }
}
