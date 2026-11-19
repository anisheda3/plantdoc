using System.Threading.Tasks;
using PlantDoctor.AI;
using PlantDoctor.Core.States;
using PlantDoctor.Diseases;
using PlantDoctor.Plants;
using PlantDoctor.Simulation;
using PlantDoctor.Treatment;
using PlantDoctor.Utilities;
using UnityEngine;

namespace PlantDoctor.Core
{
    /// <summary>
    /// Composition root: registers states, owns the scan->AI->database->simulation pipeline.
    /// This is the only class that knows about all the managers; they do not know each other.
    /// </summary>
    public class AppFlowController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] PlantDatabase plantDatabase;
        [SerializeField] ImageQualityAnalyzer qualitySettings;

        [Header("Managers")]
        [SerializeField] SimulationStateManager stateManager;
        [SerializeField] AIManager aiManager;
        [SerializeField] FeatureAnalysisManager featureAnalysis;
        [SerializeField] DiseaseSimulationManager diseaseSimulation;
        [SerializeField] TreatmentManager treatmentManager;
        [SerializeField] RecoveryManager recoveryManager;
        [SerializeField] PreventionManager preventionManager;

        [Header("Interface")]
        [Tooltip("Classic interface: every stage (identification, health check, diagnosis...) is its own step the user continues from. Off for the dashboard interface, which chains them itself.")]
        [SerializeField] bool guidedStages;

        [Header("Debug")]
        [SerializeField] bool verboseLogging = true;
        [SerializeField] int recoveryTimelineDays = 10;

        Texture2D _lastScan;
        TraceResult _pendingTrace;   // traced from the scan while the AI request is out, adopted by ApplyAnalysis

        /// <summary>True for the classic, step-by-step interface.</summary>
        public bool GuidedStages { get => guidedStages; set => guidedStages = value; }

        public SessionContext Context => stateManager != null ? stateManager.Context : null;
        public TreatmentManager Treatment => treatmentManager;
        public FeatureAnalysisManager Features => featureAnalysis;
        public PreventionManager Prevention => preventionManager;
        public AIManager AI => aiManager;
        public ImageQualityAnalyzer QualitySettings => qualitySettings;
        public SimState CurrentState => stateManager != null ? stateManager.Current : SimState.Welcome;

        /// <summary>The model-input crop of the most recent real scan (null in Demo Mode). Owned by this class.</summary>
        public Texture2D LastScanImage => _lastScan;

        void Awake()
        {
            Log.Enabled = verboseLogging;
            RegisterStates();
        }

        void Start() => stateManager?.Begin(SimState.Welcome);

        void OnDestroy()
        {
            ClearLastScan();
            _pendingTrace?.Dispose();
            _pendingTrace = null;
        }

        void RegisterStates()
        {
            if (stateManager == null)
            {
                Log.Error(Log.Channel.Core, "SimulationStateManager not assigned.");
                return;
            }
            stateManager.Register(new WelcomeState());
            stateManager.Register(new ScanState());
            stateManager.Register(new ScanRetryState());
            stateManager.Register(new AiAnalysisState());
            stateManager.Register(new PlantIdentifiedState());
            stateManager.Register(new FeatureAnalysisState(featureAnalysis));
            stateManager.Register(new DiagnosisState());
            stateManager.Register(new LowConfidenceState());
            stateManager.Register(new ArViewState());
            stateManager.Register(new FocusPartState());
            stateManager.Register(new DiseaseSimulationState(diseaseSimulation));
            stateManager.Register(new TreatmentIntroState(treatmentManager));
            stateManager.Register(new TreatmentStepState());
            stateManager.Register(new RecoveryTimelineState(recoveryManager, recoveryTimelineDays, playOnEnter: guidedStages));
            stateManager.Register(new PreventionState(preventionManager));
            stateManager.Register(new FinalReportState());
            stateManager.Register(new ErrorState());
        }

        // ------------------------------------------------------------------
        // Scan -> AI -> mapping pipeline
        // ------------------------------------------------------------------

        /// <summary>Entry point used by both the AR scanner and the VR scanner tool.</summary>
        public async Task SubmitScan(Texture2D rawImage, Rect regionOfInterest = default, float cameraMotion = 0f)
        {
            GameEvents.RaiseScanRequested();

            if (rawImage == null)
            {
                GameEvents.RaiseError("INVALID_IMAGE", "No image was captured.");
                stateManager.Transition(SimState.ScanRetry);
                return;
            }

            if (aiManager == null)
            {
                GameEvents.RaiseError("AI_UNAVAILABLE", "No AI backend is configured.");
                stateManager.Transition(SimState.Error);
                return;
            }

            if (qualitySettings != null)
            {
                var report = qualitySettings.Analyze(rawImage, cameraMotion, regionOfInterest);
                if (!report.IsAcceptable)
                {
                    Log.Warn(Log.Channel.AR, $"Scan rejected: {report.UserMessage()}");
                    GameEvents.RaiseScanQualityFailed(report);   // never sent to the classifier
                    stateManager.Transition(SimState.ScanRetry);
                    return;
                }
            }

            GameEvents.RaiseScanCompleted();
            stateManager.Transition(SimState.AiAnalysis);

            // Prepare() allocates a new Texture2D every scan. It is kept as the scan
            // thumbnail shown in the results UI and released when the next scan replaces
            // it (or on destroy), so at most one is alive at any time.
            var prepared = ImagePreprocessor.Prepare(rawImage, regionOfInterest);
            if (prepared != null && prepared != rawImage) SetLastScan(prepared);
            _pendingTrace?.Dispose();
            _pendingTrace = LeafFeatureTracer.Trace(prepared);   // where on the leaf the trouble is, from the pixels

            var result = await aiManager.Analyze(prepared);   // async: main thread never blocks
            if (CurrentState != SimState.AiAnalysis) return;   // the user cancelled (went back) while the request was out
            ApplyAnalysis(result);
        }

        /// <summary>
        /// Keeps the spots traced on the scan when there is something to show: they replace the AI's coarse regions
        /// (the demo's fixed boxes, or none) and later become the disease look of the plant. A healthy result has no spots.
        /// </summary>
        void AdoptTrace(SessionContext ctx, PlantAnalysisResult result, TraceResult trace)
        {
            if (trace == null) return;
            if (result.isHealthy || !trace.IsUsable) { trace.Dispose(); return; }
            ctx.ScanTrace = trace;
            if (result.regions.Count == 0 || result.origin == AnalysisOrigin.Mock) result.regions = trace.ToRegions();
            Log.Info(Log.Channel.AI, $"Traced on the scan: {trace.Summary()}");
        }

        /// <summary>Maps an AI result onto authored data and advances the FSM. Never throws.</summary>
        public void ApplyAnalysis(PlantAnalysisResult result)
        {
            var ctx = Context;
            var trace = _pendingTrace;
            _pendingTrace = null;
            if (ctx == null) { trace?.Dispose(); return; }

            if (result == null || result.HasError)
            {
                trace?.Dispose();
                GameEvents.RaiseError(result?.ErrorCode ?? "UNKNOWN",
                    result?.ErrorMessage ?? "The analysis could not be completed.");
                stateManager.Transition(SimState.Error);
                return;
            }

            // A fresh analysis starts a fresh session: no treatment progress may leak
            // from a previous plant into this one.
            treatmentManager?.ResetPlan();
            ctx.Reset();

            ctx.Analysis = result;
            ctx.IsDemoMode = result.IsSimulatedResult;
            ctx.Confidence = result.forceUncertain ? ConfidenceBand.Uncertain
                : aiManager != null && aiManager.Policy != null ? aiManager.Policy.Evaluate(result.confidence)
                : ConfidenceBand.Moderate;

            if (plantDatabase == null || !plantDatabase.TryResolve(result.plantId, out var plant))
            {
                GameEvents.RaiseError("UNSUPPORTED_PLANT",
                    $"'{result.plantName ?? result.plantId}' is not supported yet. Please try a supported plant.");
                trace?.Dispose();
                stateManager.Transition(SimState.Error);
                return;
            }

            ctx.Plant = plant;
            AdoptTrace(ctx, result, trace);
            ctx.Disease = result.isHealthy ? null : plant.FindDisease(result.diseaseId);
            ctx.InitialState = PlantStateEvaluator.FromAnalysis(result, plant);
            ctx.CurrentState = ctx.InitialState.Clone();

            Log.Info(Log.Channel.AI, $"Plant: {plant.plantName}");
            Log.Info(Log.Channel.AI, $"Disease: {(result.isHealthy ? "none (healthy)" : result.diseaseName)}");
            Log.Info(Log.Channel.AI, $"Confidence: {result.confidence:0.00}");

            GameEvents.RaisePlantIdentified(plant);

            stateManager.Transition(SimState.PlantIdentified);
            if (guidedStages) return;   // the classic interface continues stage by stage

            // The dashboard shows identification, the health check and the diagnosis as one screen;
            // the stages still run in order so each prepares its data (feature rows, disease events).
            stateManager.Transition(SimState.FeatureAnalysis);
            stateManager.Transition(SimState.Diagnosis);
        }

        // ------------------------------------------------------------------
        // Navigation requested by UI (UI never mutates other systems directly)
        // ------------------------------------------------------------------

        public void GoTo(SimState state) => stateManager?.Transition(state);

        public void BeginScan() => GoTo(SimState.Scan);

        /// <summary>Diagnosis -> plant view. A healthy plant has no disease to explore, so it goes to prevention.</summary>
        public void ContinueFromDiagnosis()
        {
            var ctx = Context;
            if (guidedStages)
            {
                if (ctx != null && ctx.Confidence == ConfidenceBand.Uncertain) { GoTo(SimState.LowConfidence); return; }   // uncertain results are not treated as diagnoses
                GoTo(ctx != null && ctx.IsHealthy ? SimState.Prevention : SimState.FocusPart);   // zoom in on the leaf first
                return;
            }
            GoTo(ctx != null && ctx.IsHealthy ? SimState.Prevention : SimState.ArView);
        }

        // ---- The classic interface's step-by-step navigation -------------------------------
        public void ContinueFromPlantIdentified() => GoTo(SimState.FeatureAnalysis);
        public void ContinueFromFeatures() => GoTo(SimState.Diagnosis);
        public void ContinueFromFocus() => GoTo(SimState.DiseaseSimulation);
        public void ContinueFromSimulation() => GoTo(SimState.TreatmentIntro);
        public void BeginTreatmentSteps() => GoTo(SimState.TreatmentStep);
        public void PerformTreatmentStep() => treatmentManager?.PerformCurrentStep();

        public void NextTreatmentStep()
        {
            if (treatmentManager == null) return;
            if (!treatmentManager.Advance()) GoTo(SimState.RecoveryTimeline);
            else GameEvents.RaiseStageContentChanged();   // same stage, new step
        }

        public void ContinueFromRecovery() => GoTo(SimState.Prevention);

        public void NextPreventionMeasure()
        {
            if (preventionManager == null || !preventionManager.Next())
            {
                GoTo(SimState.FinalReport);
                return;
            }
            GameEvents.RaiseStageContentChanged();   // same stage, new card
        }

        // The plant view is the hub: each of these can be opened from it, in any order, and
        // everything done so far (treatment steps, simulated days) is kept.
        public void OpenArView() => GoTo(SimState.ArView);
        public void OpenZoom() => GoTo(SimState.FocusPart);
        /// <summary>The single diseased-to-recovered animation (infection, treatment and recovery on one page).</summary>
        public void OpenJourney() => GoTo(SimState.TreatmentIntro);
        public void OpenPrevention() => GoTo(SimState.Prevention);
        public void OpenReport() => GoTo(SimState.FinalReport);

        /// <summary>Performs the current treatment step and moves on to the next one.</summary>
        public void PerformNextTreatmentStep(bool animate = true)
        {
            if (treatmentManager == null || !treatmentManager.HasPlan) return;
            treatmentManager.PerformCurrentStep(animate);
            treatmentManager.Advance();
            GameEvents.RaiseStageContentChanged();
        }

        /// <summary>How many prevention habits the user has ticked (the report shows it).</summary>
        public void SetPreventionReviewed(int count)
        {
            if (Context != null) Context.PreventionMeasuresReviewed = count;
        }

        public void RestartSession()
        {
            treatmentManager?.ResetPlan();
            Context?.Reset();
            ClearLastScan();
            GoTo(SimState.Welcome);
        }

        /// <summary>Starts another scan of a new plant without going back to the welcome screen.</summary>
        public void ScanAnother()
        {
            treatmentManager?.ResetPlan();
            Context?.Reset();
            GoTo(SimState.Scan);
        }

        public void ClearLastScan()
        {
            if (_lastScan != null) Destroy(_lastScan);
            _lastScan = null;
        }

        void SetLastScan(Texture2D texture)
        {
            ClearLastScan();
            _lastScan = texture;
        }
    }
}
