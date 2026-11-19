using PlantDoctor.AI;
using PlantDoctor.Diseases;
using PlantDoctor.Plants;

namespace PlantDoctor.Core
{
    /// <summary>Per-run data shared by all states/managers. No scene references.</summary>
    public class SessionContext
    {
        public PlantAnalysisResult Analysis;
        public PlantData Plant;
        public DiseaseData Disease;
        public ConfidenceBand Confidence = ConfidenceBand.Uncertain;

        /// <summary>The spots traced on the scan photo (null in Demo Mode or when nothing could be traced). Owned by this context.</summary>
        public TraceResult ScanTrace;

        public PlantHealthState InitialState = new PlantHealthState();
        public PlantHealthState CurrentState = new PlantHealthState();

        public int CompletedTreatmentSteps;
        public int TotalTreatmentSteps;
        public int SimulatedDay;
        public int PreventionMeasuresReviewed;
        public bool IsDemoMode;

        /// <summary>The AI found no disease. Healthy plants skip simulation and treatment.</summary>
        public bool IsHealthy => Analysis != null && Analysis.isHealthy;

        /// <summary>The recovery time-lapse has been shown for this plant; going back to it must not run it again.</summary>
        public bool RecoveryPlayed;

        public void Reset()
        {
            RecoveryPlayed = false;
            ScanTrace?.Dispose();
            ScanTrace = null;
            Analysis = null; Plant = null; Disease = null;
            Confidence = ConfidenceBand.Uncertain;
            InitialState = new PlantHealthState();
            CurrentState = new PlantHealthState();
            CompletedTreatmentSteps = TotalTreatmentSteps = SimulatedDay = PreventionMeasuresReviewed = 0;
        }

        public HealthReport BuildReport() => new HealthReport
        {
            PlantName = Plant != null ? Plant.plantName : (Analysis?.plantName ?? "Unknown"),
            DiseaseName = Disease != null ? Disease.diseaseName
                        : IsHealthy ? "No disease detected"
                        : (Analysis?.diseaseName ?? "Unknown"),
            IsHealthy = IsHealthy,
            Confidence = Analysis?.confidence ?? 0f,
            ConfidenceBandLabel = Confidence.ToString(),
            InitialSeverity = InitialState.diseaseSeverity,
            FinalSeverity = CurrentState.diseaseSeverity,
            TreatmentStepsCompleted = CompletedTreatmentSteps,
            TreatmentStepsTotal = TotalTreatmentSteps,
            PreventionMeasuresReviewed = PreventionMeasuresReviewed,
            SimulatedAiResult = Analysis?.IsSimulatedResult ?? true
        };
    }
}
