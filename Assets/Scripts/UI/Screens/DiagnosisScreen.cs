using PlantDoctor.AI;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{

    /// <summary>
    /// The diagnosis result: what the plant is, what is wrong, how serious it looks, and the
    /// evidence. Honest about its source (DEMO AI is always labelled; a numeric confidence is
    /// shown only for a real model) and about uncertainty (a low-confidence result is offered
    /// as a possibility with a rescan, never as a diagnosis).
    /// </summary>
    public class DiagnosisScreen : ScreenView
    {
        public override AppScreenId Id => AppScreenId.Diagnosis;

        [Header("Header")]
        [SerializeField] Button backButton;
        [SerializeField] Button reportButton;

        [Header("Summary")]
        [SerializeField] CoverImage scanCover;
        [SerializeField] TMP_Text plantName;
        [SerializeField] TMP_Text scientificName;
        [SerializeField] TMP_Text conditionName;
        [SerializeField] StatusChipView severityChip;
        [SerializeField] StatusChipView sourceChip;
        [SerializeField] StatusChipView confidenceChip;
        [SerializeField] GameObject uncertaintyBanner;
        [SerializeField] TMP_Text uncertaintyText;

        [Header("Tabs")]
        [SerializeField] SegmentedTabs tabs;
        [SerializeField] GameObject aboutPanel;
        [SerializeField] GameObject symptomsPanel;
        [SerializeField] GameObject impactPanel;
        [SerializeField] ScrollRect scroll;

        [Header("About")]
        [SerializeField] TMP_Text aboutWhat;
        [SerializeField] TMP_Text aboutCauses;

        [Header("Symptoms")]
        [SerializeField] GameObject photoBlock;
        [SerializeField] CoverImage[] photos = new CoverImage[3];
        [SerializeField] TMP_Text photoCaption;
        [SerializeField] RectTransform bulletRoot;
        [SerializeField] BulletRowView bulletTemplate;
        [SerializeField] GameObject featureBlock;
        [SerializeField] RectTransform featureRoot;
        [SerializeField] FeatureBarRow featureTemplate;
        [SerializeField] TMP_Text featureCaption;

        [Header("Impact")]
        [SerializeField] TMP_Text impactEffect;
        [SerializeField] TMP_Text impactUntreated;
        [SerializeField] TMP_Text impactSeverity;

        [Header("Actions")]
        [SerializeField] Button primaryButton;
        [SerializeField] TMP_Text primaryLabel;
        [SerializeField] Image primaryIcon;
        [SerializeField] Button secondaryButton;
        [SerializeField] TMP_Text secondaryLabel;

        protected override void OnBind()
        {
            if (bulletTemplate != null) bulletTemplate.gameObject.SetActive(false);
            if (featureTemplate != null) featureTemplate.gameObject.SetActive(false);
            Hook(backButton, () => Ctx.Router.Back());
            Hook(reportButton, () => Ctx.Flow?.OpenReport());
            tabs.Init(Theme, 0);
            tabs.Selected += _ => ShowTab();
        }

        protected override void OnShown()
        {
            tabs.Select(0, false);
            ShowTab();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        void ShowTab()
        {
            SetActive(aboutPanel, tabs.Index == 0);
            SetActive(symptomsPanel, tabs.Index == 1);
            SetActive(impactPanel, tabs.Index == 2);
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        public override void Refresh()
        {
            var ctx = Ctx.Session;
            var a = ctx?.Analysis;
            if (a == null || Theme == null) return;

            var plant = ctx.Plant;
            var disease = ctx.Disease;
            bool healthy = ctx.IsHealthy;
            bool uncertain = ctx.Confidence == ConfidenceBand.Uncertain;

            // ---- scan photo and names
            Texture photo = Ctx.Flow.LastScanImage;
            if (photo == null && disease != null && disease.referencePhotos != null && disease.referencePhotos.Length > 0)
                photo = disease.referencePhotos[0];
            scanCover?.Set(photo);
            if (scanCover != null && scanCover.transform.parent != null) scanCover.transform.parent.gameObject.SetActive(photo != null);

            SetText(plantName, plant != null ? plant.plantName : a.plantName);
            bool hasLatin = plant != null && !string.IsNullOrEmpty(plant.scientificName);
            SetActive(scientificName, hasLatin);
            SetText(scientificName, hasLatin ? $"({plant.scientificName})" : "");
            string condition = healthy ? "Healthy" : (disease != null ? disease.diseaseName : a.diseaseName);
            SetText(conditionName, uncertain && !healthy ? $"Possibly {condition}" : condition);

            // ---- chips: severity (word + icon), source, confidence only from a real model
            if (healthy) severityChip.Bind(Theme, "No disease found", Tone.Good, "circle-check");
            else
            {
                var band = a.severity >= 0.55f ? Tone.Alert : a.severity >= 0.25f ? Tone.Warning : Tone.Good;
                severityChip.Bind(Theme, UITheme.SeverityLabel(a.severity), band, band == Tone.Alert ? "triangle-alert" : band == Tone.Warning ? "info" : "circle-check");
            }
            if (a.IsSimulatedResult) sourceChip.Bind(Theme, "DEMO AI · simulated", Tone.Demo, "flask-conical");
            else sourceChip.Bind(Theme, a.origin == AnalysisOrigin.Remote ? "Server AI" : "On-device AI", Tone.Info, "microscope");
            SetActive(confidenceChip, !a.IsSimulatedResult);
            if (!a.IsSimulatedResult)
                confidenceChip.Bind(Theme, $"{UIText.Percent(a.confidence)} confidence", uncertain ? Tone.Warning : Tone.Good, uncertain ? "triangle-alert" : "circle-check");

            // ---- uncertainty
            SetActive(uncertaintyBanner, uncertain);
            SetText(uncertaintyText, "We're not sure yet. The model's confidence is low, so this is a possibility, not a diagnosis. " +
                                     "Rescan in brighter, even light with one leaf filling the frame, or ask a local agricultural expert.");

            // ---- About
            if (healthy)
            {
                SetText(aboutWhat, "No signs of the diseases we can recognise were found on this leaf.");
                SetText(aboutCauses, "Keep it that way with the prevention habits in the next steps. One leaf cannot show the whole plant, so keep checking.");
            }
            else
            {
                SetText(aboutWhat, disease != null ? disease.description : "No description is available for this condition yet.");
                SetText(aboutCauses, disease != null ? disease.causes : "");
            }

            // ---- Symptoms
            FillSymptoms(disease, healthy, a);

            // ---- Impact
            SetText(impactEffect, healthy ? "A healthy plant keeps all its leaf area for growing and fruiting." : (disease != null ? disease.effectOnPlant : ""));
            SetText(impactUntreated, !healthy && disease != null && !string.IsNullOrEmpty(disease.untreatedOutcome)
                ? "If left untreated: " + disease.untreatedOutcome : "");
            SetText(impactSeverity, healthy ? "" : SeverityText(a.severity));

            // ---- Actions
            if (uncertain && !healthy)
            {
                SetText(primaryLabel, "Rescan the leaf");
                SetIcon(primaryIcon, "scan-line");
                Hook(primaryButton, () => Ctx.Flow.BeginScan());
                SetActive(secondaryButton, true);
                SetText(secondaryLabel, "View the possible match in AR");
                Hook(secondaryButton, () => Ctx.Flow.OpenArView());
            }
            else
            {
                SetText(primaryLabel, healthy ? "Prevention tips" : "View in AR");
                SetIcon(primaryIcon, healthy ? "shield-check" : "box");
                Hook(primaryButton, () => Ctx.Flow.ContinueFromDiagnosis());
                SetActive(secondaryButton, false);
            }
        }

        void SetIcon(Image image, string name)
        {
            if (image == null) return;
            var sprite = Theme.Icon(name);
            image.sprite = sprite;
            image.gameObject.SetActive(sprite != null);
        }

        void FillSymptoms(Diseases.DiseaseData disease, bool healthy, PlantAnalysisResult a)
        {
            // Reference photos of the typical symptoms (licensed images, never the user's plant).
            int shownPhotos = 0;
            if (!healthy && disease != null && disease.referencePhotos != null)
                for (int i = 0; i < photos.Length; i++)
                {
                    bool has = i < disease.referencePhotos.Length && disease.referencePhotos[i] != null;
                    photos[i].transform.parent.gameObject.SetActive(has);
                    if (has) { photos[i].Set(disease.referencePhotos[i]); shownPhotos++; }
                }
            SetActive(photoBlock, shownPhotos > 0);
            SetText(photoCaption, "Typical symptoms · reference photos, not your plant");

            Clear(bulletRoot, bulletTemplate.transform);
            if (!healthy && disease != null)
            {
                if (disease.symptoms != null)
                    foreach (var s in disease.symptoms) AddBullet(s, Theme.amber);
                if (disease.conditionFactors != null)
                    for (int i = 0; i < Mathf.Min(2, disease.conditionFactors.Length); i++) AddBullet(disease.conditionFactors[i], Theme.leaf);
            }
            else AddBullet("No visible lesions, spots or yellowing on this leaf", Theme.leaf);

            Clear(featureRoot, featureTemplate.transform);
            var rows = Ctx.Flow.Features != null ? Ctx.Flow.Features.Rows : null;
            bool hasRows = rows != null && rows.Count > 0;
            SetActive(featureBlock, hasRows);
            if (hasRows)
                foreach (var row in rows)
                {
                    var view = Instantiate(featureTemplate, featureRoot);
                    view.gameObject.SetActive(true);
                    view.Bind(Theme, row);
                }
            SetText(featureCaption, a.IsSimulatedResult
                ? "Demo values, simulated for this scenario. The tick marks a healthy value."
                : "Visual AI estimates from the photo, not sensor readings. The tick marks a healthy value.");
        }

        void AddBullet(string text, Color dot)
        {
            if (string.IsNullOrEmpty(text)) return;
            var row = Instantiate(bulletTemplate, bulletRoot);
            row.gameObject.SetActive(true);
            row.Bind(text, dot);
        }

        static string SeverityText(float severity) =>
            severity >= 0.55f ? "Severe: a large share of the leaf is affected. Act soon, and check the neighbouring plants."
            : severity >= 0.25f ? "Moderate: the infection is established but still manageable if you start now."
            : "Mild: caught early. Early action keeps it small.";
    }
}
