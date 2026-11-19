using System;
using PlantDoctor.AI;
using PlantDoctor.Diseases;
using PlantDoctor.Plants;
using PlantDoctor.Treatment;

namespace PlantDoctor.Core
{
    /// <summary>
    /// Loose coupling hub. Managers raise, UI listens. UI never calls managers' internals.
    /// </summary>
    public static class GameEvents
    {
        public static event Action OnScanRequested;
        public static event Action<ScanQualityReport> OnScanQualityFailed;
        public static event Action OnScanCompleted;

        public static event Action OnAIAnalysisStarted;
        public static event Action<float> OnAIAnalysisProgress;
        public static event Action<PlantAnalysisResult> OnAIAnalysisCompleted;
        public static event Action<string> OnAIAnalysisFailed;

        public static event Action<PlantData> OnPlantIdentified;
        public static event Action<DiseaseData, ConfidenceBand> OnDiseaseDetected;
        public static event Action<string> OnFeatureSelected;

        public static event Action<TreatmentPlan> OnTreatmentStarted;
        public static event Action<TreatmentStep, int, int> OnTreatmentStepCompleted;
        public static event Action OnTreatmentCompleted;

        public static event Action<int, PlantHealthState> OnRecoveryTick;   // simulated day, state
        public static event Action OnRecoveryCompleted;

        public static event Action OnPreventionCompleted;
        public static event Action<HealthReport> OnReportReady;

        public static event Action<SimState, SimState> OnStateChanged;
        /// <summary>The stage is unchanged but what it shows moved on (next treatment step, next prevention card, step performed).</summary>
        public static event Action OnStageContentChanged;
        public static event Action<string, string> OnErrorRaised;          // code, message

        public static void RaiseScanRequested() => OnScanRequested?.Invoke();
        public static void RaiseScanQualityFailed(ScanQualityReport r) => OnScanQualityFailed?.Invoke(r);
        public static void RaiseScanCompleted() => OnScanCompleted?.Invoke();
        public static void RaiseAIAnalysisStarted() => OnAIAnalysisStarted?.Invoke();
        public static void RaiseAIAnalysisProgress(float p) => OnAIAnalysisProgress?.Invoke(p);
        public static void RaiseAIAnalysisCompleted(PlantAnalysisResult r) => OnAIAnalysisCompleted?.Invoke(r);
        public static void RaiseAIAnalysisFailed(string m) => OnAIAnalysisFailed?.Invoke(m);
        public static void RaisePlantIdentified(PlantData p) => OnPlantIdentified?.Invoke(p);
        public static void RaiseDiseaseDetected(DiseaseData d, ConfidenceBand b) => OnDiseaseDetected?.Invoke(d, b);
        public static void RaiseFeatureSelected(string id) => OnFeatureSelected?.Invoke(id);
        public static void RaiseTreatmentStarted(TreatmentPlan p) => OnTreatmentStarted?.Invoke(p);
        public static void RaiseTreatmentStepCompleted(TreatmentStep s, int i, int total) => OnTreatmentStepCompleted?.Invoke(s, i, total);
        public static void RaiseTreatmentCompleted() => OnTreatmentCompleted?.Invoke();
        public static void RaiseRecoveryTick(int day, PlantHealthState s) => OnRecoveryTick?.Invoke(day, s);
        public static void RaiseRecoveryCompleted() => OnRecoveryCompleted?.Invoke();
        public static void RaisePreventionCompleted() => OnPreventionCompleted?.Invoke();
        public static void RaiseReportReady(HealthReport r) => OnReportReady?.Invoke(r);
        public static void RaiseStateChanged(SimState from, SimState to) => OnStateChanged?.Invoke(from, to);
        public static void RaiseStageContentChanged() => OnStageContentChanged?.Invoke();
        public static void RaiseError(string code, string message) => OnErrorRaised?.Invoke(code, message);
    }
}
