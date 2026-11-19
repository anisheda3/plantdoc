using PlantDoctor.Diseases;
using System.Collections.Generic;
using System.Text;
using PlantDoctor.AI;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using PlantDoctor.Treatment;
using UnityEngine;

namespace PlantDoctor.UI.Classic
{
    /// <summary>Everything the bottom sheet says for one stage. Pure data.</summary>
    public class SheetCopy
    {
        public string Eyebrow = string.Empty;
        public Color EyebrowColor = Color.white;
        public string Title = string.Empty;
        public string Body = string.Empty;
        public string Caption = string.Empty;
        public readonly List<(string Text, Color Color)> Chips = new List<(string, Color)>();
    }

    /// <summary>
    /// Writes the copy for every stage from session state. Deterministic and free of scene
    /// references, so wording and honesty rules (DEMO marking, "possible" for uncertain
    /// results, educational disclaimers) can be unit-tested.
    /// </summary>
    public static class StageCopy
    {
        public static SheetCopy For(SimState state, SessionContext ctx, AppFlowController flow, ClassicTheme theme, string lastError = null)
        {
            var c = new SheetCopy { EyebrowColor = theme.accent };
            var a = ctx?.Analysis;

            switch (state)
            {
                case SimState.Welcome:
                    c.Eyebrow = "AI PLANT DOCTOR";
                    c.Title = "Is your plant healthy?";
                    c.Body = "Scan one leaf. You'll get the likely condition, how it compares with a healthy plant, " +
                             "and a step-by-step treatment you can watch work on a 3D twin of your plant.";
                    c.Chips.Add(("Scan", theme.accent));
                    c.Chips.Add(("Diagnose", theme.info));
                    c.Chips.Add(("Treat", theme.warning));
                    c.Chips.Add(("Prevent", theme.accent));
                    if (flow != null && flow.AI != null && flow.AI.Mode == InferenceMode.Mock)
                        c.Caption = "Demo AI is on: diagnoses are simulated until a trained model is connected (Settings).";
                    break;

                case SimState.PlantIdentified:
                    c.Eyebrow = "PLANT IDENTIFIED";
                    c.Title = ctx?.Plant?.plantName ?? a?.plantName ?? "Unknown plant";
                    if (a != null)
                    {
                        c.Body = $"{ConfidencePhrase(ctx.Confidence)} — {a.confidence:P0} match.";
                        c.Chips.Add((BandLabel(ctx.Confidence), theme.ConfidenceColor(ctx.Confidence)));
                        if (a.IsSimulatedResult) c.Chips.Add(("DEMO AI", theme.demoBanner));
                    }
                    c.Caption = a != null && a.IsSimulatedResult ? DemoModeController.DemoBannerText : string.Empty;
                    break;

                case SimState.FeatureAnalysis:
                    c.Eyebrow = "HEALTH CHECK";
                    c.Title = "Compared with a healthy plant";
                    c.Body = "Each bar is what the AI estimated from your leaf; the white tick marks a healthy value. Tap a row to learn more.";
                    c.Caption = "Values are visual AI estimates from the photo, not sensor measurements.";
                    break;

                case SimState.Diagnosis:
                    FillDiagnosis(c, ctx, theme);
                    break;

                case SimState.LowConfidence:
                    c.Eyebrow = "UNCERTAIN RESULT";
                    c.EyebrowColor = theme.warning;
                    c.Title = "We're not sure yet";
                    c.Body = "The AI couldn't identify the condition confidently, so we won't call it a diagnosis.\n\n" +
                             "• Rescan in brighter, even light\n• Fill the frame with one affected leaf\n• Hold the phone steady\n\n" +
                             "If symptoms persist, ask a local agricultural extension service.";
                    break;

                case SimState.FocusPart:
                    c.Eyebrow = "ZOOM IN";
                    c.EyebrowColor = theme.warning;
                    string organ = ctx?.Disease == null ? "leaf" : ctx.Disease.affectedPart switch
                    {
                        AffectedPart.Fruit => "fruit",
                        AffectedPart.Head => "head",
                        AffectedPart.Root => "root",
                        _ => "leaf"
                    };
                    c.Title = $"The affected {organ}";
                    c.Body = ctx?.Disease != null
                        ? $"A magnified {organ} shows where {ctx.Disease.diseaseName.ToLowerInvariant()} appears: the labels point at the real spots. Turn it with your finger."
                        : "A magnified leaf. Turn it with your finger.";
                    c.Caption = "A generated stand-in leaf that explains the disease, not a photo of your leaf. Your scan is shown for comparison.";
                    break;

                case SimState.DiseaseSimulation:
                    c.Eyebrow = "HOW IT SPREAD";
                    c.EyebrowColor = theme.warning;
                    c.Title = ctx?.Disease != null ? $"{ctx.Disease.diseaseName} on your plant" : "Disease progression";
                    c.Body = "Watch the twin: it replays how the infection developed, from healthy tissue to what we see now.";
                    if (!string.IsNullOrEmpty(ctx?.Disease?.untreatedOutcome))
                        c.Caption = $"Left untreated: {ctx.Disease.untreatedOutcome}";
                    break;

                case SimState.TreatmentIntro:
                {
                    var plan = flow?.Treatment?.Plan;
                    c.Eyebrow = "TREATMENT PLAN";
                    c.Title = plan?.planName ?? "Treatment plan";
                    var sb = new StringBuilder();
                    sb.AppendLine($"{plan?.StepCount ?? 0} steps. After each one the twin fast-forwards a few days so you can see the effect.");
                    if (plan?.steps != null)
                    {
                        sb.AppendLine();
                        for (int i = 0; i < plan.steps.Length; i++)
                            if (plan.steps[i] != null) sb.AppendLine($"<color=#{ClassicTheme.Hex(theme.accent)}>{i + 1}</color>   {plan.steps[i].title}");
                    }
                    var methods = Db(ctx)?.methods;
                    if (methods != null && methods.Length > 0)
                    {
                        // From the content database: what each kind of treatment can and cannot do.
                        sb.AppendLine();
                        sb.AppendLine($"<color=#{ClassicTheme.Hex(theme.textPrimary)}><b>What each method can't do</b></color>");
                        foreach (var m in methods) sb.AppendLine($"•  {m.name}: {m.limitations}");
                    }
                    c.Body = sb.ToString().TrimEnd();
                    c.Caption = plan?.disclaimer ?? string.Empty;
                    break;
                }

                case SimState.TreatmentStep:
                    FillTreatmentStep(c, flow, theme);
                    break;

                case SimState.RecoveryTimeline:
                    c.Eyebrow = $"DAY {ctx?.SimulatedDay ?? 0}";
                    c.Title = "Recovery over the next days";
                {
                    var db = Db(ctx);
                    var sb = new StringBuilder();
                    sb.AppendLine(!string.IsNullOrEmpty(db?.recoveryNote) ? db.recoveryNote
                        : "New growth replaces damaged tissue. Severity falls, but some scarring remains — treatment slows and contains disease rather than erasing it.");
                    if (db?.followups != null && db.followups.Length > 0)
                    {
                        // From the content database: what to check at the follow-up, what this case shows, and when to act again.
                        sb.AppendLine();
                        sb.AppendLine($"<color=#{ClassicTheme.Hex(theme.textPrimary)}><b>Follow-up checks</b></color>");
                        foreach (var f in db.followups) sb.AppendLine($"•  {f.prompt}: {f.finding}");
                    }
                    c.Body = sb.ToString().TrimEnd();
                    break;
                }

                case SimState.Prevention:
                {
                    var m = flow?.Prevention?.Current;
                    if (m != null)
                    {
                        c.Eyebrow = Spaced(m.category.ToString());
                        c.EyebrowColor = theme.info;
                        c.Title = m.title;
                        c.Body = m.detail;
                        c.Chips.Add(($"Tip {flow.Prevention.Index + 1} of {flow.Prevention.Count}", theme.info));
                    }
                    else
                    {
                        c.Eyebrow = "PREVENTION";
                        c.EyebrowColor = theme.info;
                        c.Title = "Keep it healthy";
                        c.Body = "Water at the base early in the day, give plants room for air to move, remove fallen leaves, " +
                                 "and check the lower leaves every week — early spots are far easier to manage.";
                    }
                    break;
                }

                case SimState.FinalReport:
                    FillReport(c, ctx, theme);
                    break;

                case SimState.Error:
                    c.Eyebrow = "SOMETHING WENT WRONG";
                    c.EyebrowColor = theme.danger;
                    c.Title = "That didn't work";
                    c.Body = string.IsNullOrEmpty(lastError) ? "The session could not continue." : lastError;
                    c.Caption = "You can scan again, or explore the full experience in Demo Mode.";
                    break;

                default:
                    c.Title = StageDescriptor.FriendlyTitle(state);
                    c.Body = StageDescriptor.Instruction(state);
                    break;
            }
            return c;
        }

        static void FillDiagnosis(SheetCopy c, SessionContext ctx, ClassicTheme theme)
        {
            var a = ctx?.Analysis;
            if (ctx != null && ctx.IsHealthy)
            {
                c.Eyebrow = "NO DISEASE DETECTED";
                c.Title = "Your plant looks healthy";
                c.Body = "No signs of the diseases we can recognise. Keep it that way with a few simple habits.";
                if (a != null) c.Chips.Add(($"{a.confidence:P0} confident", theme.ConfidenceColor(ctx.Confidence)));
                return;
            }

            var band = ctx?.Confidence ?? ConfidenceBand.Uncertain;
            var d = ctx?.Disease;
            c.Eyebrow = Headline(band);
            c.EyebrowColor = theme.ConfidenceColor(band);
            c.Title = d?.diseaseName ?? a?.diseaseName ?? "Unknown condition";

            if (d != null)
            {
                c.Chips.Add((d.pathogenType switch
                {
                    PathogenType.Oomycete => "Water mould",
                    PathogenType.Abiotic => "Disorder",
                    var t => t.ToString()
                }, theme.info));
                if (a != null) c.Chips.Add(($"{d.BandFor(a.severity)} severity", theme.StatusColorForRisk(a.severity)));
            }
            if (a != null && a.IsSimulatedResult) c.Chips.Add(("DEMO AI", theme.demoBanner));

            var sb = new StringBuilder();
            if (d != null)
            {
                Section(sb, "What is it?", d.description, theme);
                Section(sb, "What causes it?", d.causes, theme);
                if (d.symptoms != null && d.symptoms.Length > 0)
                {
                    sb.AppendLine($"<color=#{ClassicTheme.Hex(theme.textPrimary)}><b>Signs to look for</b></color>");
                    foreach (var s in d.symptoms) sb.AppendLine($"•  {s}");
                    sb.AppendLine();
                }
            }
            var db = Db(ctx);
            if (db?.evidence != null && db.evidence.Length > 0)
            {
                var names = new List<string>();
                foreach (var e in db.evidence) if (!string.IsNullOrEmpty(e.publisher) && !names.Contains(e.publisher)) names.Add(e.publisher);
                sb.AppendLine($"<color=#{ClassicTheme.Hex(theme.textTertiary)}>Sources: {string.Join(", ", names)}</color>");
                sb.AppendLine();
            }
            if (band == ConfidenceBand.Uncertain)
                sb.AppendLine($"<color=#{ClassicTheme.Hex(theme.danger)}>This is a possibility, not a diagnosis. Rescan, or consult an agricultural expert.</color>");
            c.Body = sb.ToString().TrimEnd();
            c.Caption = a != null && a.IsSimulatedResult ? DemoModeController.DemoBannerText : string.Empty;
        }

        static void FillTreatmentStep(SheetCopy c, AppFlowController flow, ClassicTheme theme)
        {
            var t = flow?.Treatment;
            var step = t?.CurrentStep;
            if (step == null) { c.Title = "Treatment"; return; }

            c.Eyebrow = $"STEP {t.CurrentIndex + 1} OF {t.Plan.StepCount}";
            c.Title = step.title;
            c.Chips.Add((ModeLabel(step.interactionMode), theme.info));
            c.Chips.Add(($"+{step.simulatedDaysAdvanced} days", theme.textSecondary));
            if (t.CurrentStepPerformed) c.Chips.Add(("Done", theme.accent));

            var sb = new StringBuilder();
            sb.AppendLine(step.instruction);
            sb.AppendLine();
            Section(sb, "Why this helps", step.rationale, theme);

            var chem = step.chemicalInfo;
            if (chem != null && chem.involvesChemicalProduct)
            {
                sb.AppendLine($"<color=#{ClassicTheme.Hex(theme.warning)}><b>Product guidance</b></color>");
                sb.AppendLine($"Active ingredient: {chem.productOrActiveIngredient}");
                sb.AppendLine($"Dosage: {chem.dosage}");
                sb.AppendLine($"Method: {chem.applicationMethod}");
                sb.AppendLine($"Pre-harvest interval: {chem.preHarvestInterval}");
                c.Caption = $"{chem.safetyNotes}\n{chem.localRegulationsNote}";
            }
            c.Body = sb.ToString().TrimEnd();
        }

        static void FillReport(SheetCopy c, SessionContext ctx, ClassicTheme theme)
        {
            var r = ctx?.BuildReport();
            c.Eyebrow = "HEALTH REPORT";
            if (r == null) { c.Title = "No session yet"; return; }

            c.Title = r.IsHealthy ? $"{r.PlantName} · healthy" : $"{r.PlantName} · {r.DiseaseName}";
            var sb = new StringBuilder();
            if (r.IsHealthy)
            {
                sb.AppendLine("No disease detected. Keep up the prevention habits and rescan if anything changes.");
            }
            else
            {
                sb.AppendLine($"<color=#{ClassicTheme.Hex(theme.textPrimary)}><b>What you did</b></color>");
                sb.AppendLine("•  Identified the symptoms and the likely cause");
                sb.AppendLine("•  Compared the leaf with a healthy plant");
                sb.AppendLine($"•  Completed {r.TreatmentStepsCompleted} of {r.TreatmentStepsTotal} treatment steps");
                sb.AppendLine("•  Watched the plant recover over simulated days");
                sb.AppendLine("•  Reviewed how to prevent it coming back");
                var followups = Db(ctx)?.followups;
                if (followups != null && followups.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"<color=#{ClassicTheme.Hex(theme.textPrimary)}><b>Keep checking</b></color>");
                    foreach (var f in followups) sb.AppendLine($"•  {f.escalation}");
                }
            }
            c.Body = sb.ToString().TrimEnd();

            c.Caption = r.IsHealthy ? string.Empty
                : "Some severity remains because treatment is never modelled as a guaranteed cure — damaged tissue does not regenerate.";
            if (r.SimulatedAiResult)
                c.Caption = (c.Caption + "\n" + DemoModeController.DemoBannerText).Trim();
        }

        public static string FeatureDetail(FeatureComparisonRow row, ClassicTheme theme)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Observed  <b>{row.Observed:P0}</b>      Healthy  <b>{row.Optimal:P0}</b>");
            sb.AppendLine($"<color=#{ClassicTheme.Hex(theme.textTertiary)}>{row.SourceLabel}</color>");
            sb.AppendLine();
            Section(sb, "What this means", row.Meaning, theme);
            Section(sb, "Why it matters", row.WhyItMatters, theme);
            return sb.ToString().TrimEnd();
        }

        public static string Headline(ConfidenceBand band) => band switch
        {
            ConfidenceBand.High => "DISEASE DETECTED",
            ConfidenceBand.Moderate => "LIKELY DISEASE",
            _ => "POSSIBLE DISEASE"
        };

        static string BandLabel(ConfidenceBand band) => band switch
        {
            ConfidenceBand.High => "High confidence",
            ConfidenceBand.Moderate => "Moderate confidence",
            _ => "Low confidence"
        };

        static string ConfidencePhrase(ConfidenceBand band) => band switch
        {
            ConfidenceBand.High => "A confident identification",
            ConfidenceBand.Moderate => "A probable identification",
            _ => "A tentative identification"
        };

        static string ModeLabel(TreatmentInteractionMode mode) => mode switch
        {
            TreatmentInteractionMode.TapRegion => "Remove",
            TreatmentInteractionMode.GrabAndRemove => "Remove",
            TreatmentInteractionMode.SprayApply => "Spray",
            TreatmentInteractionMode.AdjustSetting => "Adjust care",
            _ => "Learn"
        };

        /// <summary>The content database's record of the session's disease (follow-ups, method limits, sources), or null.</summary>
        static ContentDatabase.Disease Db(SessionContext ctx) =>
            ctx?.Plant != null && ctx.Disease != null ? ContentDatabase.Find(ctx.Plant.plantId, ctx.Disease.diseaseId) : null;

        static void Section(StringBuilder sb, string heading, string body, ClassicTheme theme)
        {
            if (string.IsNullOrWhiteSpace(body)) return;
            sb.AppendLine($"<color=#{ClassicTheme.Hex(theme.textPrimary)}><b>{heading}</b></color>");
            sb.AppendLine(body);
            sb.AppendLine();
        }

        public static string Spaced(string pascalCase)
        {
            var sb = new StringBuilder();
            foreach (char ch in pascalCase)
            {
                if (char.IsUpper(ch) && sb.Length > 0) sb.Append(' ');
                sb.Append(char.ToUpperInvariant(ch));
            }
            return sb.ToString();
        }
    }
}
