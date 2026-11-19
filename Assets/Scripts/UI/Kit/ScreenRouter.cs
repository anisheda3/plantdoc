using System;
using System.Collections;
using PlantDoctor.AI;
using PlantDoctor.AR;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using PlantDoctor.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>
    /// Decides which page is on screen. The guided diagnosis follows the flow's state; the
    /// Profile page is opened from the home screen's gear. It also owns back navigation (the
    /// on-screen arrows and Android's back key), the persistent plant backdrop, the toast, the
    /// confirmation dialog and the splash on start-up. Pages only show content and raise
    /// requests; they do not decide where to go.
    /// </summary>
    public class ScreenRouter : MonoBehaviour
    {
        [Header("Services")]
        [SerializeField] AppFlowController flow;
        [SerializeField] UITheme theme;
        [SerializeField] AppModeController mode;
        [SerializeField] MonoBehaviour twinPlacement;
        [SerializeField] DemoModeController demo;
        [SerializeField] ARScanController scan;
        [SerializeField] ViewerStage stage;
        [SerializeField] PlantDatabase database;
        [SerializeField] PlantVisualBinder binder;
        [SerializeField] ScanHistory history;
        [SerializeField] PlantDoctor.Diseases.DiseaseSimulationManager simulation;
        [SerializeField] PlantDoctor.Simulation.RecoveryManager recovery;
        [SerializeField] PlantTwinBuilder arTwinBuilder;

        [Header("Chrome")]
        [SerializeField] ScreenView[] screens = new ScreenView[0];
        [SerializeField] StageBackdrop backdrop;
        [SerializeField] ToastView toast;
        [SerializeField] ConfirmDialogView confirm;

        [Header("Start-up")]
        [SerializeField] float splashSeconds = 1.9f;
        [SerializeField] float splashMaxSeconds = 6f;

        public ScreenContext Context { get; private set; }
        public AppScreenId Current { get; private set; } = AppScreenId.Splash;
        public bool Booting { get; private set; } = true;

        public event Action<AppScreenId> ScreenChanged;

        readonly System.Collections.Generic.Dictionary<AppScreenId, ScreenView> _byId =
            new System.Collections.Generic.Dictionary<AppScreenId, ScreenView>();

        void Awake()
        {
            Context = new ScreenContext
            {
                Flow = flow, Theme = theme, Router = this, Stage = stage, Mode = mode,
                Placement = twinPlacement as ITwinPlacement, Demo = demo, Scan = scan,
                Database = database, History = history, Binder = binder, Backdrop = backdrop,
                Simulation = simulation, Recovery = recovery, ArTwin = new ARPlantTwin { Builder = arTwinBuilder }
            };
            backdrop?.Init(stage, theme);
            foreach (var s in screens)
            {
                if (s == null) continue;
                _byId[s.Id] = s;
                s.Bind(Context);
            }
            confirm?.Close();
        }

        void OnEnable()
        {
            GameEvents.OnStateChanged += HandleStateChanged;
            GameEvents.OnStageContentChanged += HandleContentChanged;
            GameEvents.OnErrorRaised += HandleError;
        }

        void OnDisable()
        {
            GameEvents.OnStateChanged -= HandleStateChanged;
            GameEvents.OnStageContentChanged -= HandleContentChanged;
            GameEvents.OnErrorRaised -= HandleError;
        }

        IEnumerator Start()
        {
            Display(AppScreenId.Splash);
            float started = Time.unscaledTime;
            while (Booting)
            {
                float elapsed = Time.unscaledTime - started;
                bool modeKnown = mode == null || mode.Mode != AppMode.Resolving;
                if (elapsed >= splashSeconds && (modeKnown || elapsed >= splashMaxSeconds)) break;
                yield return null;
            }
            FinishBoot();
        }

        /// <summary>Leaves the splash now (tests and the editor use this).</summary>
        public void FinishBoot()
        {
            if (!Booting) return;
            Booting = false;
            Display(ScreenForState(CurrentState));
        }

        SimState CurrentState => flow != null ? flow.CurrentState : SimState.Welcome;

        // ------------------------------------------------------------------
        // State -> page
        // ------------------------------------------------------------------

        public static AppScreenId ScreenForState(SimState state)
        {
            switch (state)
            {
                case SimState.Welcome: return AppScreenId.Home;
                case SimState.Scan: case SimState.ScanRetry: return AppScreenId.Scan;
                case SimState.AiAnalysis: return AppScreenId.Analysis;
                case SimState.PlantIdentified: case SimState.FeatureAnalysis:
                case SimState.Diagnosis: case SimState.LowConfidence: return AppScreenId.Diagnosis;
                case SimState.ArView: return AppScreenId.ArView;
                case SimState.FocusPart: return AppScreenId.Zoom;
                // Infection, treatment and recovery are one continuous animation on one page.
                case SimState.DiseaseSimulation: case SimState.TreatmentIntro: case SimState.TreatmentStep:
                case SimState.RecoveryTimeline: return AppScreenId.Journey;
                case SimState.Prevention: return AppScreenId.Prevention;
                case SimState.FinalReport: return AppScreenId.Report;
                case SimState.Error: return AppScreenId.Error;
                default: return AppScreenId.Home;
            }
        }

        bool _lingering;

        void HandleStateChanged(SimState from, SimState to)
        {
            if (to == SimState.Welcome || to == SimState.Scan || to == SimState.AiAnalysis)
            {
                Context.Visited.Clear();
                Context.PreventionDone.Clear();
            }
            Context.Visited.Add(to);
            if (Booting) return;

            // When the answer arrives, let the analysis checklist finish for a moment (every line
            // it ticks has really happened) instead of cutting away in the same frame.
            if (Current == AppScreenId.Analysis && ScreenForState(to) == AppScreenId.Diagnosis && !_lingering)
            {
                _lingering = true;
                StartCoroutine(LingerOnAnalysis());
                return;
            }
            if (_lingering) return;
            Display(ScreenForState(to));
        }

        IEnumerator LingerOnAnalysis()
        {
            if (_byId.TryGetValue(AppScreenId.Analysis, out var page) && page is AnalysisScreen analysis) analysis.MarkComplete();
            yield return new WaitForSecondsRealtime(UITheme.ReducedMotion ? 0.2f : 0.85f);
            _lingering = false;
            if (!Booting) Display(ScreenForState(CurrentState));
        }

        void HandleContentChanged()
        {
            if (!Booting && _byId.TryGetValue(Current, out var screen)) screen.Refresh();
        }

        void HandleError(string code, string message)
        {
            // Problems inside a page (a rejected photo, a retry) are shown by that page; the
            // router only adds a toast so nothing fails silently.
            if (CurrentState != SimState.Error && !Booting && !string.IsNullOrEmpty(message) && Current != AppScreenId.Scan)
                Toast(message, Tone.Alert);
        }

        public void Display(AppScreenId id)
        {
            bool changed = id != Current || !_byId.TryGetValue(id, out var shown) || !shown.IsShown;
            if (!_byId.TryGetValue(id, out var next)) { Debug.LogWarning($"[AI Plant Doctor] No page registered for {id}"); return; }

            foreach (var kv in _byId)
                if (kv.Key != id) kv.Value.Hide();
            Current = id;
            next.Show();
            RefreshBackdrop();
            if (changed) ScreenChanged?.Invoke(id);
        }

        /// <summary>Tells the plant backdrop what the page on screen needs (it also follows AR / plant view switches).</summary>
        public void RefreshBackdrop()
        {
            if (backdrop != null && _byId.TryGetValue(Current, out var page)) backdrop.Apply(page);
        }

        // ------------------------------------------------------------------
        // Profile and back
        // ------------------------------------------------------------------

        public void ShowProfile() => Display(AppScreenId.Profile);

        /// <summary>The on-screen back arrow and Android's back key.</summary>
        public void Back()
        {
            if (Booting) return;
            if (confirm != null && confirm.IsOpen) { confirm.Close(); return; }
            if (_byId.TryGetValue(Current, out var page) && page.HandleBack()) return;

            var ctx = flow != null ? flow.Context : null;
            switch (Current)
            {
                case AppScreenId.Home: break;                               // nothing behind the home screen
                case AppScreenId.Profile: Display(ScreenForState(CurrentState)); break;
                case AppScreenId.Scan: flow?.GoTo(SimState.Welcome); break;
                case AppScreenId.Analysis:
                    Ask("Stop the analysis?", "Your leaf is still being analysed. You can keep waiting or stop and scan again.",
                        "Stop", "Keep waiting", () => { flow?.AI?.CancelActiveRequest(); flow?.BeginScan(); });
                    break;
                case AppScreenId.Diagnosis: flow?.GoTo(SimState.Welcome); break;
                case AppScreenId.ArView: flow?.GoTo(SimState.Diagnosis); break;
                case AppScreenId.Zoom: case AppScreenId.Journey: flow?.OpenArView(); break;
                case AppScreenId.Prevention:
                    if (ctx != null && ctx.IsHealthy) flow?.GoTo(SimState.Diagnosis); else flow?.OpenJourney();
                    break;
                case AppScreenId.Report: flow?.OpenPrevention(); break;
                case AppScreenId.Error: flow?.GoTo(SimState.Welcome); break;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Back();   // Android's back button arrives as Escape
        }

        // ------------------------------------------------------------------
        // Shared chrome
        // ------------------------------------------------------------------

        public void Toast(string message, Tone tone = Tone.Dark)
        {
            if (toast != null && theme != null) toast.Show(theme, message, tone);
        }

        /// <summary>The saved summary of a past scan, with the option to delete it.</summary>
        public void ShowScanDetail(ScanRecord record)
        {
            if (record == null) return;
            string source = record.IsDemo ? "DEMO AI (simulated)" : record.origin == "server" ? "Server AI" : "On-device AI";
            string result = record.isHealthy ? "No disease detected." :
                $"{record.diseaseName}, {UITheme.SeverityLabel(record.severity).ToLowerInvariant()} severity.";
            string confidence = record.IsDemo ? "" : $"\nConfidence {UIText.Percent(record.confidence)}.";
            Ask($"{record.plantName}", $"{result}{confidence}\n{UIText.Relative(record.LocalTime)}\nSource: {source}",
                "Delete scan", "Close", () => history?.Delete(record.id));
        }

        public void Ask(string title, string message, string confirmLabel, string cancelLabel, Action onConfirm)
        {
            if (confirm == null) { onConfirm?.Invoke(); return; }
            confirm.Open(title, message, confirmLabel, cancelLabel, onConfirm);
        }
    }
}
