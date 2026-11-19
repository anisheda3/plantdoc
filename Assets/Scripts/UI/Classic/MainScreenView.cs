using System.Collections.Generic;
using PlantDoctor.AI;
using PlantDoctor.AR;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using PlantDoctor.Treatment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI.Classic
{
    /// <summary>
    /// Production UI controller: a floating bottom sheet whose content follows the stage.
    ///
    /// It only READS state and calls public navigation methods on AppFlowController —
    /// it never mutates simulation systems, matching the project's rule that UI buttons
    /// must not drive unrelated systems directly. Text comes from <see cref="StageCopy"/>
    /// (pure, testable); this class only lays it out.
    /// </summary>
    public class MainScreenView : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] AppFlowController flow;
        [SerializeField] DemoModeController demoMode;
        [SerializeField] AppModeController modeController;
        [Tooltip("PlantTwinPlacer in AR builds (implements ITwinPlacement). Optional.")]
        [SerializeField] MonoBehaviour twinPlacement;
        [SerializeField] PlantVisualBinder visualBinder;
        [SerializeField] ClassicTheme theme;
        [SerializeField] HudView hud;

        [Header("Sheet")]
        [SerializeField] RectTransform sheet;
        [SerializeField] FadeSlide sheetGroup;
        [SerializeField] RectTransform sheetContent;
        [SerializeField] ScrollRect sheetScroll;
        [SerializeField] float sheetMaxScreenFraction = 0.6f;
        [Tooltip("Sheet height that is not scroll content: handle + action bar.")]
        [SerializeField] float sheetChromeHeight = 240f;

        [Header("Sheet content")]
        [SerializeField] GameObject heroMark;
        [SerializeField] TMP_Text eyebrow;
        [SerializeField] TMP_Text bodyTitle;
        [SerializeField] TMP_Text bodyText;
        [SerializeField] TMP_Text captionText;
        [SerializeField] GameObject thumbnailGroup;
        [SerializeField] RawImage thumbnail;
        [Tooltip("Outline of the leaf and of the spots traced on the scan, drawn over the photo.")]
        [SerializeField] RawImage traceOverlay;
        [SerializeField] RectTransform markerRoot;
        [SerializeField] Image markerTemplate;
        [SerializeField] TMP_Text thumbnailCaption;
        [SerializeField] Transform chipRoot;
        [SerializeField] ChipView chipTemplate;
        [SerializeField] Transform meterRoot;
        [SerializeField] MeterView meterTemplate;
        [SerializeField] Transform featureListRoot;
        [SerializeField] FeatureRowView featureRowPrefab;
        [SerializeField] Transform tileRoot;
        [SerializeField] StatTileView tileTemplate;

        [Header("Detail popup")]
        [SerializeField] GameObject detailPanel;
        [SerializeField] FadeSlide detailGroup;
        [SerializeField] TMP_Text detailTitle;
        [SerializeField] TMP_Text detailBody;
        [SerializeField] Button detailClose;

        [Header("Actions")]
        [SerializeField] Button primaryButton;
        [SerializeField] TMP_Text primaryButtonLabel;
        [SerializeField] Button secondaryButton;
        [SerializeField] TMP_Text secondaryButtonLabel;

        [Header("Status")]
        [SerializeField] GameObject errorBar;
        [SerializeField] FadeSlide errorGroup;
        [SerializeField] TMP_Text errorText;
        [SerializeField] float errorSeconds = 5f;
        [SerializeField] FadeSlide placementHint;
        [SerializeField] TMP_Text placementHintText;

        readonly List<FeatureRowView> _rows = new List<FeatureRowView>();
        readonly List<MeterView> _meters = new List<MeterView>();
        readonly List<ChipView> _chips = new List<ChipView>();
        readonly List<StatTileView> _tiles = new List<StatTileView>();
        readonly List<Image> _markers = new List<Image>();

        LiveMeters _live = LiveMeters.None;
        string _lastError;
        float _errorUntil;

        ITwinPlacement Placement => twinPlacement as ITwinPlacement;

        [System.Flags]
        enum LiveMeters { None = 0, Severity = 1, Lesions = 2, Yellowing = 4, Wilting = 8, Growth = 16 }

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        void OnEnable()
        {
            GameEvents.OnStateChanged += HandleStateChanged;
            GameEvents.OnStageContentChanged += Refresh;
            GameEvents.OnAIAnalysisFailed += HandleAnalysisFailed;
            GameEvents.OnErrorRaised += HandleError;
            GameEvents.OnScanQualityFailed += HandleScanRejected;
            GameEvents.OnTreatmentStepCompleted += HandleStepCompleted;
            GameEvents.OnRecoveryTick += HandleRecoveryTick;

            if (detailClose != null) detailClose.onClick.AddListener(CloseDetail);
            CloseDetail();
            HideError(true);
            Refresh();
        }

        void OnDisable()
        {
            GameEvents.OnStateChanged -= HandleStateChanged;
            GameEvents.OnStageContentChanged -= Refresh;
            GameEvents.OnAIAnalysisFailed -= HandleAnalysisFailed;
            GameEvents.OnErrorRaised -= HandleError;
            GameEvents.OnScanQualityFailed -= HandleScanRejected;
            GameEvents.OnTreatmentStepCompleted -= HandleStepCompleted;
            GameEvents.OnRecoveryTick -= HandleRecoveryTick;

            if (detailClose != null) detailClose.onClick.RemoveListener(CloseDetail);
        }

        void HandleStateChanged(SimState from, SimState to)
        {
            if (to != SimState.Error && to != SimState.ScanRetry) HideError(false);
            Refresh();
            // Re-play the entrance only if this stage shows the sheet; replaying a hidden
            // sheet would force it back on top of the scanner.
            if (sheetGroup != null && sheetGroup.IsShown) sheetGroup.Replay();
            if (sheetScroll != null) sheetScroll.verticalNormalizedPosition = 1f;
        }

        void HandleAnalysisFailed(string message) => ShowError(message);
        void HandleError(string code, string message) => ShowError(message);
        void HandleScanRejected(ScanQualityReport report) => ShowError(report.UserMessage());
        void HandleStepCompleted(TreatmentStep step, int index, int total) => Refresh();

        void HandleRecoveryTick(int day, PlantHealthState state)
        {
            if (flow == null || flow.CurrentState != SimState.RecoveryTimeline) return;
            if (eyebrow != null) eyebrow.text = $"DAY {day}";
        }

        // ------------------------------------------------------------------
        // Main refresh
        // ------------------------------------------------------------------

        public void Refresh()
        {
            if (flow == null || theme == null) return;

            var state = flow.CurrentState;
            var ctx = flow.Context;
            hud?.Refresh(state, ctx);

            ClearDynamic();
            var copy = StageCopy.For(state, ctx, flow, theme, _lastError);

            bool sheetVisible = state != SimState.Scan && state != SimState.ScanRetry && state != SimState.AiAnalysis;
            sheetGroup?.Set(sheetVisible);

            if (heroMark != null) heroMark.SetActive(state == SimState.Welcome);
            SetText(eyebrow, copy.Eyebrow, copy.EyebrowColor);
            SetText(bodyTitle, copy.Title, theme.textPrimary);
            SetText(bodyText, copy.Body, theme.textSecondary);
            SetText(captionText, copy.Caption, theme.textTertiary);

            foreach (var chip in copy.Chips) AddChip(chip.Text, chip.Color);

            BuildStageExtras(state, ctx);
            ConfigureButtons(state, ctx);
        }

        void BuildStageExtras(SimState state, SessionContext ctx)
        {
            _live = LiveMeters.None;
            bool showThumb = false;

            switch (state)
            {
                case SimState.PlantIdentified:
                    if (ctx?.Analysis != null)
                        AddMeter("Match confidence", ctx.Analysis.confidence, theme.ConfidenceColor(ctx.Confidence));
                    showThumb = true;
                    break;

                case SimState.FeatureAnalysis:
                    BuildFeatureRows();
                    break;

                case SimState.Diagnosis:
                    if (ctx?.Analysis != null && !ctx.IsHealthy)
                        AddMeter("Severity", ctx.Analysis.severity, theme.StatusColorForRisk(ctx.Analysis.severity));
                    showThumb = true;
                    break;

                case SimState.FocusPart:
                    showThumb = true;
                    break;

                case SimState.DiseaseSimulation:
                    _live = LiveMeters.Severity | LiveMeters.Lesions | LiveMeters.Yellowing | LiveMeters.Wilting;
                    break;

                case SimState.TreatmentStep:
                case SimState.RecoveryTimeline:
                    _live = LiveMeters.Severity | LiveMeters.Lesions | LiveMeters.Growth;
                    break;

                case SimState.FinalReport:
                    BuildReportTiles(ctx?.BuildReport());
                    break;
            }

            if (_live != LiveMeters.None) BuildLiveMeters();
            BuildThumbnail(showThumb ? ctx : null);
        }

        void Update()
        {
            if (flow == null || theme == null) return;

            if (_live != LiveMeters.None) UpdateLiveMeters();
            UpdatePlacementHint();
            UpdateSheetHeight();

            if (_errorUntil > 0f && Time.unscaledTime > _errorUntil) HideError(false);
        }

        // ------------------------------------------------------------------
        // Live meters mirror what the twin is showing right now
        // ------------------------------------------------------------------

        static readonly (LiveMeters flag, string label, bool lowerIsBetter)[] LiveSpec =
        {
            (LiveMeters.Severity, "Disease severity", true),
            (LiveMeters.Lesions, "Lesions", true),
            (LiveMeters.Yellowing, "Yellowing", true),
            (LiveMeters.Wilting, "Wilting", true),
            (LiveMeters.Growth, "Healthy growth", false),
        };

        void BuildLiveMeters()
        {
            var s = CurrentVisualState();
            foreach (var spec in LiveSpec)
                if ((_live & spec.flag) != 0)
                {
                    float v = Read(s, spec.flag);
                    AddMeter(spec.label, v, ToneFor(v, spec.lowerIsBetter), instant: true);
                }
        }

        void UpdateLiveMeters()
        {
            var s = CurrentVisualState();
            int i = 0;
            foreach (var spec in LiveSpec)
            {
                if ((_live & spec.flag) == 0) continue;
                if (i >= _meters.Count) break;
                float v = Read(s, spec.flag);
                _meters[i++].SetValue(v, ToneFor(v, spec.lowerIsBetter));
            }
        }

        PlantHealthState CurrentVisualState() =>
            flow.Context?.CurrentState ?? (visualBinder != null ? visualBinder.Displayed : new PlantHealthState());

        static float Read(PlantHealthState s, LiveMeters flag) => flag switch
        {
            LiveMeters.Severity => s.diseaseSeverity,
            LiveMeters.Lesions => s.lesionDensity,
            LiveMeters.Yellowing => s.yellowing,
            LiveMeters.Wilting => s.wilting,
            LiveMeters.Growth => s.healthyGrowth,
            _ => 0f
        };

        Color ToneFor(float v, bool lowerIsBetter) =>
            theme.StatusColorForRisk(lowerIsBetter ? v : 1f - v);

        // ------------------------------------------------------------------
        // AR placement hint
        // ------------------------------------------------------------------

        void UpdatePlacementHint()
        {
            if (placementHint == null) return;
            var p = Placement;
            bool placing = p != null && p.IsPlacing && modeController != null && modeController.IsAR;
            placementHint.Set(placing);
            if (placing && placementHintText != null)
                placementHintText.text = p.HasSurface
                    ? "Placing your plant twin on this surface…"
                    : "Your plant twin is on its way — point at a table to place it there";
        }

        // ------------------------------------------------------------------
        // Sheet sizing: hug the content, cap at a fraction of the screen, scroll beyond
        // ------------------------------------------------------------------

        void UpdateSheetHeight()
        {
            if (sheet == null || sheetContent == null) return;
            var canvas = sheet.GetComponentInParent<Canvas>();
            float canvasHeight = canvas != null ? ((RectTransform)canvas.transform).rect.height : 1920f;

            float content = LayoutUtility.GetPreferredHeight(sheetContent);
            float target = Mathf.Clamp(content + sheetChromeHeight, 360f, canvasHeight * sheetMaxScreenFraction);
            var size = sheet.sizeDelta;
            size.y = Mathf.Lerp(size.y, target, Ease.Damp(14f, Time.unscaledDeltaTime));
            sheet.sizeDelta = size;
        }

        // ------------------------------------------------------------------
        // Buttons
        // ------------------------------------------------------------------

        void ConfigureButtons(SimState state, SessionContext ctx)
        {
            string primary = StageDescriptor.NextAction(state);
            string secondary = null;
            System.Action primaryAction = null;
            System.Action secondaryAction = null;

            switch (state)
            {
                case SimState.Welcome:
                    primaryAction = flow.BeginScan;
                    secondary = "Try a demo";
                    secondaryAction = RunDemo;
                    break;

                case SimState.PlantIdentified:
                    primaryAction = () => { Placement?.EnsurePlaced(); flow.ContinueFromPlantIdentified(); };
                    break;

                case SimState.FeatureAnalysis: primaryAction = flow.ContinueFromFeatures; break;

                case SimState.Diagnosis:
                    if (ctx != null && ctx.IsHealthy) primary = "Prevention tips";
                    else if (ctx != null && ctx.Confidence == ConfidenceBand.Uncertain) primary = "Continue";
                    else primary = "Zoom in on the leaf";
                    primaryAction = flow.ContinueFromDiagnosis;
                    break;

                case SimState.FocusPart: primaryAction = flow.ContinueFromFocus; break;

                case SimState.DiseaseSimulation: primaryAction = flow.ContinueFromSimulation; break;
                case SimState.TreatmentIntro:    primaryAction = flow.BeginTreatmentSteps; break;

                case SimState.TreatmentStep:
                {
                    var t = flow.Treatment;
                    bool performed = t != null && t.CurrentStepPerformed;
                    bool last = t != null && t.Plan != null && t.CurrentIndex + 1 >= t.Plan.StepCount;
                    if (!performed)
                    {
                        primary = "Do this step";
                        primaryAction = flow.PerformTreatmentStep;
                        secondary = "Skip";
                        secondaryAction = flow.NextTreatmentStep;
                    }
                    else
                    {
                        primary = last ? "See recovery" : "Next step";
                        primaryAction = flow.NextTreatmentStep;
                    }
                    break;
                }

                case SimState.RecoveryTimeline: primaryAction = flow.ContinueFromRecovery; break;

                case SimState.Prevention:
                {
                    var p = flow.Prevention;
                    bool last = p == null || p.Count == 0 || p.Index + 1 >= p.Count;
                    primary = last ? "See report" : "Next tip";
                    primaryAction = flow.NextPreventionMeasure;
                    break;
                }

                case SimState.LowConfidence:
                    primary = "Rescan";
                    primaryAction = flow.BeginScan;
                    secondary = "Start over";
                    secondaryAction = flow.RestartSession;
                    break;

                case SimState.FinalReport:
                    primary = "Scan another plant";
                    primaryAction = flow.ScanAnother;
                    secondary = "Home";
                    secondaryAction = flow.RestartSession;
                    break;

                case SimState.Error:
                    primary = "Try again";
                    primaryAction = flow.BeginScan;
                    secondary = "Try a demo";
                    secondaryAction = RunDemo;
                    break;

                default:
                    primary = null;
                    break;
            }

            Apply(primaryButton, primaryButtonLabel, primary, primaryAction);
            Apply(secondaryButton, secondaryButtonLabel, secondary, secondaryAction);
        }

        static void Apply(Button button, TMP_Text label, string text, System.Action action)
        {
            if (button == null) return;
            bool visible = !string.IsNullOrEmpty(text);
            button.gameObject.SetActive(visible);
            if (!visible) return;

            if (label != null) label.text = text;
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(() => action());
        }

        async void RunDemo()
        {
            if (demoMode == null)
            {
                ShowError("Demo mode is not configured.");
                return;
            }
            await demoMode.RunSelectedScenario();
        }

        // ------------------------------------------------------------------
        // Dynamic content builders
        // ------------------------------------------------------------------

        void BuildFeatureRows()
        {
            if (featureListRoot == null || featureRowPrefab == null || flow.Features == null) return;
            foreach (var row in flow.Features.Rows)
            {
                var view = Instantiate(featureRowPrefab, featureListRoot);
                view.gameObject.SetActive(true);
                view.Bind(row, theme, ShowFeatureDetail);
                _rows.Add(view);
            }
        }

        void BuildReportTiles(HealthReport r)
        {
            if (r == null || tileRoot == null || tileTemplate == null) return;
            if (r.IsHealthy)
            {
                AddTile($"{r.Confidence:P0}", "AI confidence", theme.ConfidenceColor(flow.Context.Confidence));
                AddTile("Healthy", "Condition", theme.accent);
                AddTile(r.PreventionScore, "Prevention", theme.info);
                return;
            }
            AddTile($"{r.InitialSeverity:P0}", "Severity at scan", theme.StatusColorForRisk(r.InitialSeverity));
            AddTile($"{r.FinalSeverity:P0}", "Severity now", theme.StatusColorForRisk(r.FinalSeverity));
            AddTile($"{r.RecoveryPercent:P0}", "Recovery", theme.accent);
            AddTile($"{r.TreatmentStepsCompleted}/{r.TreatmentStepsTotal}", "Steps done", theme.info);
            AddTile(r.PreventionScore, "Prevention", theme.info);
            AddTile($"{r.Confidence:P0}", "AI confidence", theme.ConfidenceColor(flow.Context.Confidence));
        }

        void BuildThumbnail(SessionContext ctx)
        {
            if (thumbnailGroup == null) return;
            var scan = flow.LastScanImage;
            bool show = ctx?.Analysis != null && scan != null;
            thumbnailGroup.SetActive(show);
            if (!show) return;

            if (thumbnail != null) thumbnail.texture = scan;
            var trace = ctx.ScanTrace;
            var outline = trace != null && !ctx.IsHealthy ? trace.Overlay : null;
            if (traceOverlay != null)
            {
                traceOverlay.gameObject.SetActive(outline != null);
                if (outline != null) traceOverlay.texture = outline;
            }
            if (thumbnailCaption != null)
                thumbnailCaption.text = outline != null ? $"Traced on your scan · {trace.Caption()}. Rough estimate from colour."
                    : ctx.Analysis.regions.Count > 0 && !ctx.IsHealthy ? "Your scan · highlighted areas the AI flagged"
                    : "Your scan";

            if (outline != null) return;   // the traced outline replaces the coarse boxes
            if (markerRoot == null || markerTemplate == null || ctx.IsHealthy) return;
            foreach (var region in ctx.Analysis.regions)
            {
                if (region == null || region.score < 0.15f) continue;
                var marker = Instantiate(markerTemplate, markerRoot);
                marker.gameObject.SetActive(true);
                // Regions are normalised image rects with a top-left origin.
                var b = region.bounds;
                var rt = marker.rectTransform;
                rt.anchorMin = new Vector2(Mathf.Clamp01(b.x), Mathf.Clamp01(1f - b.y - b.height));
                rt.anchorMax = new Vector2(Mathf.Clamp01(b.x + b.width), Mathf.Clamp01(1f - b.y));
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                marker.color = region.type == "yellowing" ? theme.warning : theme.danger;
                _markers.Add(marker);
            }
        }

        void AddMeter(string label, float value, Color color, bool instant = false)
        {
            if (meterRoot == null || meterTemplate == null) return;
            var m = Instantiate(meterTemplate, meterRoot);
            m.gameObject.SetActive(true);
            m.Bind(label, value, color, instant);
            _meters.Add(m);
        }

        void AddChip(string text, Color color)
        {
            if (chipRoot == null || chipTemplate == null || string.IsNullOrEmpty(text)) return;
            var c = Instantiate(chipTemplate, chipRoot);
            c.gameObject.SetActive(true);
            c.Bind(text, color, false, theme);
            _chips.Add(c);
        }

        void AddTile(string value, string caption, Color tone)
        {
            var t = Instantiate(tileTemplate, tileRoot);
            t.gameObject.SetActive(true);
            t.Bind(value, caption, tone);
            _tiles.Add(t);
        }

        void ClearDynamic()
        {
            Clear(_rows); Clear(_meters); Clear(_chips); Clear(_tiles); Clear(_markers);
            if (chipRoot != null) chipRoot.gameObject.SetActive(true);
        }

        static void Clear<T>(List<T> items) where T : Component
        {
            foreach (var i in items) if (i != null) Destroy(i.gameObject);
            items.Clear();
        }

        void LateUpdate()
        {
            // Hide empty containers so the layout has no dead gaps.
            SetActiveIfChanged(chipRoot, _chips.Count > 0);
            SetActiveIfChanged(meterRoot, _meters.Count > 0);
            SetActiveIfChanged(featureListRoot, _rows.Count > 0);
            SetActiveIfChanged(tileRoot, _tiles.Count > 0);
            if (captionText != null) captionText.gameObject.SetActive(!string.IsNullOrEmpty(captionText.text));
            if (bodyText != null) bodyText.gameObject.SetActive(!string.IsNullOrEmpty(bodyText.text));
        }

        static void SetActiveIfChanged(Transform t, bool active)
        {
            if (t != null && t.gameObject.activeSelf != active) t.gameObject.SetActive(active);
        }

        // ------------------------------------------------------------------
        // Feature detail popup
        // ------------------------------------------------------------------

        void ShowFeatureDetail(string featureId)
        {
            var row = flow.Features?.Get(featureId);
            if (row == null || detailPanel == null) return;

            GameEvents.RaiseFeatureSelected(featureId);
            detailPanel.SetActive(true);
            detailGroup?.Show();

            if (detailTitle != null) detailTitle.text = row.DisplayName;
            if (detailBody != null) detailBody.text = StageCopy.FeatureDetail(row, theme);
        }

        void CloseDetail()
        {
            if (detailGroup != null) detailGroup.Hide();
            else if (detailPanel != null) detailPanel.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Error toast
        // ------------------------------------------------------------------

        void ShowError(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            _lastError = message;
            if (errorText != null) errorText.text = message;
            if (errorGroup != null) errorGroup.Show();
            else if (errorBar != null) errorBar.SetActive(true);
            _errorUntil = Time.unscaledTime + errorSeconds;
        }

        void HideError(bool instant)
        {
            _errorUntil = 0f;
            if (errorGroup != null) errorGroup.Hide(instant);
            else if (errorBar != null) errorBar.SetActive(false);
        }

        static void SetText(TMP_Text target, string text, Color color)
        {
            if (target == null) return;
            target.text = text ?? string.Empty;
            target.color = color;
        }
    }
}
