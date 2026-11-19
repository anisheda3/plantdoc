using System.Text;
using PlantDoctor.AI;
using PlantDoctor.Core;
using PlantDoctor.Diseases;
using PlantDoctor.Plants;
using PlantDoctor.Treatment;
using PlantDoctor.Utilities;
using UnityEngine;

namespace PlantDoctor.UI.Classic
{
    /// <summary>
    /// Renders the guided flow. It only reads state and raises navigation calls on
    /// AppFlowController; it never mutates simulation systems itself.
    /// Text is produced through BuildPanelText so it can be unit-tested and reused
    /// by Canvas UI, UI Toolkit or a VR world-space panel.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [SerializeField] AppFlowController flow;

        public string CurrentPanelText { get; private set; } = string.Empty;
        public string BannerText { get; private set; } = string.Empty;
        public string ErrorText { get; private set; } = string.Empty;
        public float AnalysisProgress { get; private set; }

        void OnEnable()
        {
            // Named handlers only. Anonymous lambdas cannot be unsubscribed,
            // so they kept this object alive on the static GameEvents delegates
            // for the lifetime of the process.
            GameEvents.OnStateChanged += HandleStateChanged;
            GameEvents.OnAIAnalysisProgress += HandleAnalysisProgress;
            GameEvents.OnScanQualityFailed += HandleScanQualityFailed;
            GameEvents.OnErrorRaised += HandleError;
            GameEvents.OnAIAnalysisFailed += HandleAnalysisFailed;
        }

        void OnDisable()
        {
            GameEvents.OnStateChanged -= HandleStateChanged;
            GameEvents.OnAIAnalysisProgress -= HandleAnalysisProgress;
            GameEvents.OnScanQualityFailed -= HandleScanQualityFailed;
            GameEvents.OnErrorRaised -= HandleError;
            GameEvents.OnAIAnalysisFailed -= HandleAnalysisFailed;
        }

        void HandleAnalysisProgress(float p) => AnalysisProgress = p;


        void HandleScanQualityFailed(Core.ScanQualityReport r) => ErrorText = r.UserMessage();


        void HandleAnalysisFailed(string message) => ErrorText = message;


        void HandleError(string code, string message)
        {
            ErrorText = message;
            Log.Warn(Log.Channel.UI, $"{code}: {message}");
        }

        void HandleStateChanged(SimState from, SimState to)
        {
            ErrorText = string.Empty;
            Refresh();
        }

        public void Refresh()
        {
            var ctx = flow != null ? flow.Context : null;
            var state = flow != null ? flow.CurrentState : SimState.Welcome;
            BannerText = (ctx != null && ctx.IsDemoMode) ? DemoModeController.DemoBannerText : string.Empty;
            CurrentPanelText = BuildPanelText(state, ctx, flow);
        }

        /// <summary>Pure text builder — deterministic and testable.</summary>
        public static string BuildPanelText(SimState state, SessionContext ctx, AppFlowController flow)
        {
            var sb = new StringBuilder();
            sb.AppendLine(StageDescriptor.Header(state));
            sb.AppendLine();

            switch (state)
            {
                case SimState.PlantIdentified:
                    sb.AppendLine(ctx?.Plant?.plantName ?? "Unknown plant");
                    sb.AppendLine($"Confidence: {(ctx?.Analysis?.confidence ?? 0f):P0}");
                    break;

                case SimState.FeatureAnalysis:
                    sb.AppendLine("PARAMETER        OBSERVED   OPTIMAL   STATUS   SOURCE");
                    if (flow?.Features != null)
                        foreach (var row in flow.Features.Rows)
                            sb.AppendLine($"{row.DisplayName,-16} {row.Observed,8:P0} {row.Optimal,9:P0}   {row.StatusGlyph,-6} {row.SourceLabel}");
                    break;

                case SimState.Diagnosis when ctx != null && ctx.IsHealthy:
                    sb.AppendLine("NO DISEASE DETECTED");
                    sb.AppendLine($"Confidence: {(ctx.Analysis?.confidence ?? 0f):P0}");
                    break;

                case SimState.Diagnosis:
                {
                    var band = ctx?.Confidence ?? ConfidenceBand.Uncertain;
                    sb.AppendLine(band switch
                    {
                        ConfidenceBand.High => "DISEASE DETECTED",
                        ConfidenceBand.Moderate => "LIKELY DISEASE",
                        _ => "POSSIBLE DISEASE"
                    });
                    sb.AppendLine(ctx?.Disease?.diseaseName ?? ctx?.Analysis?.diseaseName ?? "Unknown");
                    sb.AppendLine($"Confidence: {(ctx?.Analysis?.confidence ?? 0f):P0}");
                    if (ctx?.Disease != null && ctx.Analysis != null)
                        sb.AppendLine($"Severity: {ctx.Disease.BandFor(ctx.Analysis.severity)}");
                    if (band == ConfidenceBand.Uncertain)
                        sb.AppendLine("The result is uncertain. Consider rescanning or consulting an expert.");
                    break;
                }

                case SimState.TreatmentStep:
                {
                    var step = flow?.Treatment?.CurrentStep;
                    if (step != null)
                    {
                        sb.AppendLine($"TREATMENT STEP {flow.Treatment.CurrentIndex + 1} / {flow.Treatment.Plan.StepCount}");
                        sb.AppendLine(step.title);
                        sb.AppendLine(step.instruction);
                        sb.AppendLine($"Why: {step.rationale}");
                        if (step.chemicalInfo != null && step.chemicalInfo.involvesChemicalProduct)
                        {
                            sb.AppendLine($"Product: {step.chemicalInfo.productOrActiveIngredient}");
                            sb.AppendLine($"Dosage: {step.chemicalInfo.dosage}");
                            sb.AppendLine($"Safety: {step.chemicalInfo.safetyNotes}");
                            sb.AppendLine($"Pre-harvest interval: {step.chemicalInfo.preHarvestInterval}");
                            sb.AppendLine(step.chemicalInfo.localRegulationsNote);
                        }
                    }
                    break;
                }

                case SimState.RecoveryTimeline:
                    sb.AppendLine($"Day {ctx?.SimulatedDay ?? 0}");
                    sb.AppendLine($"Severity: {(ctx?.CurrentState?.diseaseSeverity ?? 0f):P0}");
                    break;

                case SimState.Prevention:
                {
                    var measure = flow?.Prevention?.Current;
                    if (measure != null)
                    {
                        sb.AppendLine($"{measure.category}: {measure.title}");
                        sb.AppendLine(measure.detail);
                        sb.AppendLine($"({flow.Prevention.Index + 1} / {flow.Prevention.Count})");
                    }
                    break;
                }

                case SimState.FinalReport:
                {
                    var r = ctx?.BuildReport();
                    if (r != null)
                    {
                        sb.AppendLine($"Plant: {r.PlantName}");
                        sb.AppendLine($"Detected Disease: {r.DiseaseName}");
                        sb.AppendLine($"AI Confidence: {r.Confidence:P0} ({r.ConfidenceBandLabel})");
                        sb.AppendLine($"Initial Severity: {r.InitialSeverity:P0}");
                        sb.AppendLine($"Final Severity: {r.FinalSeverity:P0}");
                        sb.AppendLine($"Recovery: {r.RecoveryPercent:P0}");
                        sb.AppendLine($"Treatment Completed: {r.TreatmentStepsCompleted} / {r.TreatmentStepsTotal}");
                        sb.AppendLine($"Prevention Score: {r.PreventionScore}");
                        if (r.SimulatedAiResult) sb.AppendLine(DemoModeController.DemoBannerText);
                    }
                    break;
                }

                default:
                    sb.AppendLine(StageDescriptor.Instruction(state));
                    break;
            }

            var next = StageDescriptor.NextAction(state);
            if (!string.IsNullOrEmpty(next)) { sb.AppendLine(); sb.AppendLine($"[ {next} ]"); }
            return sb.ToString();
        }
    }
}
