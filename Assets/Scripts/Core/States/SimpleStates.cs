using PlantDoctor.Utilities;

namespace PlantDoctor.Core.States
{
    /// <summary>Passive stages: they only announce themselves; UI reacts to OnStateChanged.</summary>
    public class WelcomeState : StateBase
    {
        public override SimState Id => SimState.Welcome;
        public override void Enter() => Log.Info(Log.Channel.UI, "Welcome stage");
    }

    public class ScanState : StateBase
    {
        public override SimState Id => SimState.Scan;
        public override void Enter() => Log.Info(Log.Channel.UI, "Awaiting scan");
    }

    public class ScanRetryState : StateBase
    {
        public override SimState Id => SimState.ScanRetry;
        public override void Enter() => Log.Info(Log.Channel.UI, "Scan rejected by the quality gate; awaiting retry");
    }

    public class AiAnalysisState : StateBase
    {
        public override SimState Id => SimState.AiAnalysis;
        public override void Enter() => Log.Info(Log.Channel.AI, "Analyzing plant...");
    }

    public class PlantIdentifiedState : StateBase
    {
        public override SimState Id => SimState.PlantIdentified;
        public override void Enter() =>
            Log.Info(Log.Channel.Sim, $"Plant identified: {Context.Plant?.plantName ?? "unknown"}");
    }

    /// <summary>The plant twin in AR (or the plant view): the hub the zoom, spread, treatment and recovery screens open from.</summary>
    public class ArViewState : StateBase
    {
        public override SimState Id => SimState.ArView;
        public override void Enter() => Log.Info(Log.Channel.Sim, "Plant view");
    }

    /// <summary>The magnified leaf screen.</summary>
    public class FocusPartState : StateBase
    {
        public override SimState Id => SimState.FocusPart;
        public override void Enter() => Log.Info(Log.Channel.Sim, "Magnified leaf");
    }

    public class LowConfidenceState : StateBase
    {
        public override SimState Id => SimState.LowConfidence;
        public override void Enter() =>
            Log.Warn(Log.Channel.AI, "Confidence below threshold — presenting result as uncertain.");
    }

    public class ErrorState : StateBase
    {
        public override SimState Id => SimState.Error;
        public override void Enter() => Log.Warn(Log.Channel.Core, "Error stage entered");
    }
}
