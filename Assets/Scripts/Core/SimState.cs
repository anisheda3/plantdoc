namespace PlantDoctor.Core
{
    public enum SimState
    {
        Welcome, Scan, ScanRetry, AiAnalysis, PlantIdentified, FeatureAnalysis,
        Diagnosis, LowConfidence, ArView, FocusPart, DiseaseSimulation, TreatmentIntro, TreatmentStep,
        RecoveryTimeline, Prevention, FinalReport, Error
    }

    /// <summary>Base class for every stage. Enter/Tick/Exit only.</summary>
    public abstract class StateBase
    {
        protected SimulationStateManager Machine { get; private set; }
        protected SessionContext Context => Machine.Context;

        public void Bind(SimulationStateManager machine) => Machine = machine;

        public abstract SimState Id { get; }
        public virtual void Enter() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void Exit() { }
    }
}
