using System;
using System.Collections;
using System.Collections.Generic;
using PlantDoctor.AI;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using PlantDoctor.Treatment;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>
    /// The final report: what was found, what the user did in the app (only what really
    /// happened), and a Save Report that writes a real PDF. Failures are shown, not hidden.
    /// </summary>
    public class ReportScreen : ScreenView
    {
        public override AppScreenId Id => AppScreenId.Report;

        [SerializeField] Button backButton;
        [SerializeField] Button exportIconButton;
        [SerializeField] CoverImage photo;
        [SerializeField] TMP_Text plantName;
        [SerializeField] TMP_Text scientificName;
        [SerializeField] TMP_Text conditionName;
        [SerializeField] StatusChipView severityChip;
        [SerializeField] StatusChipView sourceChip;
        [SerializeField] TMP_Text summary;
        [SerializeField] RectTransform symptomsRoot;
        [SerializeField] RectTransform treatmentRoot;
        [SerializeField] RectTransform preventionRoot;
        [SerializeField] RectTransform actionsRoot;
        [SerializeField] CheckRowView rowTemplate;
        [SerializeField] TMP_Text notes;
        [SerializeField] Button saveButton;
        [SerializeField] TMP_Text saveLabel;
        [SerializeField] TMP_Text saveStatus;
        [SerializeField] Button againButton;
        [SerializeField] ScrollRect scroll;

        ReportData _data;
        ScanRecord _record;

        protected override void OnBind()
        {
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            Hook(backButton, () => Ctx.Router.Back());
            Hook(saveButton, Save);
            Hook(exportIconButton, Save);
            Hook(againButton, () => Ctx.Flow.ScanAnother());
        }

        protected override void OnShown()
        {
            SetText(saveStatus, "");
            SetText(saveLabel, "Save Report");
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        public override void Refresh()
        {
            var ctx = Ctx.Session;
            if (ctx?.Analysis == null || Theme == null) return;
            _record = Ctx.History != null && Ctx.History.Records.Count > 0 ? Ctx.History.Records[0] : null;
            _data = BuildData(Ctx, _record);

            photo?.Set(ReportPhoto());
            SetText(plantName, _data.PlantName);
            SetActive(scientificName, !string.IsNullOrEmpty(_data.ScientificName));
            SetText(scientificName, string.IsNullOrEmpty(_data.ScientificName) ? "" : $"({_data.ScientificName})");
            SetText(conditionName, _data.Condition);
            if (ctx.IsHealthy) severityChip.Bind(Theme, "No disease found", Tone.Good, "circle-check");
            else
            {
                var band = ctx.Analysis.severity >= 0.55f ? Tone.Alert : ctx.Analysis.severity >= 0.25f ? Tone.Warning : Tone.Good;
                severityChip.Bind(Theme, _data.SeverityLabel, band, band == Tone.Alert ? "triangle-alert" : "info");
            }
            sourceChip.Bind(Theme, _data.SourceText, _data.IsDemo ? Tone.Demo : Tone.Info, _data.IsDemo ? "flask-conical" : "microscope");
            SetText(summary, _data.Summary);

            Fill(symptomsRoot, _data.Symptoms.ConvertAll(s => (s, true)), Theme.amber, bullets: true);
            Fill(treatmentRoot, _data.Treatment, Theme.botanical);
            Fill(preventionRoot, _data.Prevention, Theme.botanical);
            Fill(actionsRoot, _data.Actions.ConvertAll(a => (a, true)), Theme.botanical);
            SetText(notes, string.Join("\n\n", _data.Notes));
        }

        void Fill(RectTransform root, List<(string text, bool done)> items, Color tint, bool bullets = false)
        {
            if (root == null) return;
            Clear(root, rowTemplate.transform);
            // The whole section (title and card) goes when it has nothing to say.
            if (root.parent != null) root.parent.gameObject.SetActive(items.Count > 0);
            foreach (var (text, done) in items)
            {
                var row = Instantiate(rowTemplate, root);
                row.gameObject.SetActive(true);
                row.Bind(Theme, text, done);
            }
        }

        Texture ReportPhoto()
        {
            if (Ctx.Flow.LastScanImage != null) return Ctx.Flow.LastScanImage;
            var p = Ctx.Session?.Disease?.referencePhotos;
            return p != null && p.Length > 0 ? p[0] : null;
        }

        // ------------------------------------------------------------------
        // Data
        // ------------------------------------------------------------------

        public static ReportData BuildData(ScreenContext c, ScanRecord record)
        {
            var ctx = c.Session;
            var a = ctx.Analysis;
            var plant = ctx.Plant;
            var disease = ctx.Disease;
            bool healthy = ctx.IsHealthy;
            bool uncertain = ctx.Confidence == ConfidenceBand.Uncertain;

            var d = new ReportData
            {
                PlantName = plant != null ? plant.plantName : a.plantName,
                ScientificName = plant != null ? plant.scientificName : "",
                Condition = healthy ? "Healthy" : (disease != null ? disease.diseaseName : a.diseaseName),
                SeverityLabel = healthy ? "No disease found" : UITheme.SeverityLabel(a.severity),
                IsDemo = a.IsSimulatedResult,
                SourceText = a.IsSimulatedResult ? "DEMO AI (simulated, not a real prediction)" : a.origin == AnalysisOrigin.Remote ? "Server AI" : "On-device AI",
                ScanTime = record != null ? record.LocalTime : DateTime.Now,
            };
            if (uncertain && !healthy) d.Condition = "Possibly " + d.Condition;
            if (!a.IsSimulatedResult) d.ConfidenceText = UIText.Percent(a.confidence) + (uncertain ? " (low: treat as a possibility)" : "");

            string plantLower = d.PlantName.ToLowerInvariant();
            d.Summary = healthy
                ? $"No signs of the diseases we can recognise were found on this {plantLower} leaf. Keep checking regularly and follow the prevention habits."
                : uncertain
                    ? $"The model was not confident: {d.Condition.ToLowerInvariant()} is a possibility on this {plantLower}, not a diagnosis. Rescan in better light or ask a local expert."
                    : $"{d.Condition} detected on this {plantLower} ({d.SeverityLabel.ToLowerInvariant()} severity). Follow the recommended treatment plan to recover and prevent further spread.";
            if (a.IsSimulatedResult) d.Summary += " This was a demonstration result.";

            if (!healthy && disease != null && disease.symptoms != null) d.Symptoms.AddRange(disease.symptoms);

            var plan = disease != null ? disease.treatmentPlan : null;
            if (!healthy && plan != null && plan.steps != null)
                for (int i = 0; i < plan.steps.Length; i++)
                    if (plan.steps[i] != null) d.Treatment.Add((plan.steps[i].title, i < ctx.CompletedTreatmentSteps));

            var prevention = c.Flow.Prevention != null ? c.Flow.Prevention.Plan : null;
            if (prevention != null && prevention.measures != null)
                for (int i = 0; i < prevention.measures.Length; i++)
                    d.Prevention.Add((prevention.measures[i].title, c.PreventionDone.Contains(i)));

            // Only things that really happened.
            d.Actions.Add($"{(a.IsSimulatedResult ? "Demo scan run" : "Scanned")} on {d.ScanTime:d MMM yyyy, h:mm tt}");
            d.Actions.Add($"Diagnosis completed ({(a.IsSimulatedResult ? "DEMO AI" : a.origin == AnalysisOrigin.Remote ? "Server AI" : "On-device AI")})");
            if (c.Visited.Contains(SimState.ArView)) d.Actions.Add(c.IsAR ? "Plant viewed in AR" : "Plant viewed in 3D (AR not available on this device)");
            if (c.Visited.Contains(SimState.FocusPart)) d.Actions.Add("Magnified leaf viewed");
            if (c.Visited.Contains(SimState.DiseaseSimulation)) d.Actions.Add("Disease spread viewed");
            if (c.Visited.Contains(SimState.TreatmentIntro) || c.Visited.Contains(SimState.TreatmentStep))
            {
                d.Actions.Add("Treatment plan generated");
                if (ctx.TotalTreatmentSteps > 0) d.Actions.Add($"{ctx.CompletedTreatmentSteps} of {ctx.TotalTreatmentSteps} treatment steps applied (simulated)");
            }
            if (c.Visited.Contains(SimState.RecoveryTimeline)) d.Actions.Add("Recovery simulation viewed");
            if (prevention != null && c.Visited.Contains(SimState.Prevention))
                d.Actions.Add($"Prevention checklist: {c.PreventionDone.Count} of {prevention.measures.Length} ticked");

            if (a.IsSimulatedResult) d.Notes.Add("DEMO AI: this result was simulated to show how the app works. It is not a real model prediction.");
            d.Notes.Add("This report is educational guidance, not a professional diagnosis. Simulations (spread, recovery) are illustrations and do not predict your plant.");
            if (plan != null && !string.IsNullOrEmpty(plan.disclaimer)) d.Notes.Add(plan.disclaimer);

            var photo = c.Flow.LastScanImage;
            if (photo == null && disease != null && disease.referencePhotos != null && disease.referencePhotos.Length > 0) photo = disease.referencePhotos[0];
            if (photo != null)
            {
                var copy = ScanHistory.ReadableCopy(photo, 520);
                d.PhotoJpeg = copy.EncodeToJPG(86);
                d.PhotoWidth = copy.width; d.PhotoHeight = copy.height;
                UnityEngine.Object.Destroy(copy);
            }
            return d;
        }

        void Save()
        {
            if (_data == null) Refresh();
            if (_data == null) return;
            try
            {
                var pdf = PdfReport.Build(_data);
                var result = ReportExporter.Save(pdf, _data.SuggestedFileName);
                SetText(saveStatus, result.Message);
                if (saveStatus != null) saveStatus.color = result.Success ? Theme.botanical : Theme.red;
                if (result.Success)
                {
                    SetText(saveLabel, "Saved. Save again");
                    if (_record != null) Ctx.History?.SetReportFile(_record.id, _data.SuggestedFileName);
                    Ctx.Router.Toast("Report saved", Tone.Good);
                }
            }
            catch (Exception e)
            {
                SetText(saveStatus, "The report could not be created: " + e.Message);
                if (saveStatus != null) saveStatus.color = Theme.red;
            }
        }
    }
}
