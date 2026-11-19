using System;
using PlantDoctor.AI;
using PlantDoctor.AR;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    public enum AppScreenId
    {
        Splash, Home, Scan, Analysis, Diagnosis, ArView, Zoom, Journey, Prevention, Report, Profile, Error
    }

    /// <summary>The AR twin, as far as the screens need it (its markers, for the highlight rings and the condition label).</summary>
    public class ARPlantTwin
    {
        public PlantTwinBuilder Builder;
    }

    /// <summary>The services every screen needs, handed out by the router so screens never search the scene.</summary>
    public class ScreenContext
    {
        public AppFlowController Flow;
        public UITheme Theme;
        public ScreenRouter Router;
        public ViewerStage Stage;
        public StageBackdrop Backdrop;
        public AppModeController Mode;
        public ITwinPlacement Placement;
        public DemoModeController Demo;
        public ARScanController Scan;
        public PlantDatabase Database;
        public ScanHistory History;
        public PlantVisualBinder Binder;
        public PlantDoctor.Diseases.DiseaseSimulationManager Simulation;
        public PlantDoctor.Simulation.RecoveryManager Recovery;
        public ARPlantTwin ArTwin;

        /// <summary>Stages the user has opened in this session (for the hub's "next step" hint and the report).</summary>
        public readonly System.Collections.Generic.HashSet<SimState> Visited = new System.Collections.Generic.HashSet<SimState>();
        /// <summary>Prevention measures the user ticked off (by index).</summary>
        public readonly System.Collections.Generic.HashSet<int> PreventionDone = new System.Collections.Generic.HashSet<int>();

        public SessionContext Session => Flow != null ? Flow.Context : null;
        public bool IsAR => Mode != null && Mode.IsAR;
    }

    /// <summary>
    /// Base class of every full-screen page. It fades in and out, tells the page when it becomes
    /// visible or hidden, and gives it the shared services. Hidden pages are deactivated, so they
    /// cost nothing while not on screen.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class ScreenView : MonoBehaviour
    {
        public abstract AppScreenId Id { get; }

        [Header("Plant backdrop")]
        [Tooltip("Canvas units at the bottom of the page taken by its sheet; the plant gets the rest.")]
        [SerializeField] float sheetHeight = 0f;
        [Tooltip("Canvas units at the top kept free for the header.")]
        [SerializeField] float heroTopInset = 170f;
        [Range(0f, 1f)] [SerializeField] float plantOpacity = 1f;
        [SerializeField] bool showBackdrop = true;

        protected float SheetHeight => sheetHeight;
        protected float HeroTopInset => heroTopInset;

        /// <summary>False on pages that show the real camera instead of the plant backdrop.</summary>
        public virtual bool BackdropVisible => showBackdrop;
        public virtual float PlantOpacity => plantOpacity;

        /// <summary>The part of the screen (0..1, from the bottom left) the plant should fill on this page.</summary>
        public virtual Rect HeroRect(float canvasHeight)
        {
            float bottom = Mathf.Clamp01(sheetHeight / canvasHeight);
            float top = Mathf.Clamp01(1f - heroTopInset / canvasHeight);
            if (top - bottom < 0.15f) top = Mathf.Min(1f, bottom + 0.15f);
            return new Rect(0.06f, bottom, 0.88f, top - bottom);
        }

        protected ScreenContext Ctx { get; private set; }
        protected UITheme Theme => Ctx?.Theme;

        CanvasGroup _group;
        RectTransform _rect;
        float _t;
        bool _target;
        bool _bound;

        public bool IsShown => _target;

        public void Bind(ScreenContext context)
        {
            Ctx = context;
            _group = GetComponent<CanvasGroup>();
            _rect = transform as RectTransform;
            _bound = true;
            OnBind();
            gameObject.SetActive(false);
        }

        protected virtual void OnBind() { }
        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }

        /// <summary>The page's content should be re-read from session state.</summary>
        public virtual void Refresh() { }

        /// <summary>Return true if the page handled "back" itself (closing a panel, for example).</summary>
        public virtual bool HandleBack() => false;

        public void Show(bool instant = false)
        {
            if (!_bound) return;
            if (!_target || !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                _target = true;
                if (instant || UITheme.ReducedMotion) _t = 1f; else _t = 0f;
                Apply();
                OnShown();
            }
            Refresh();
        }

        public void Hide(bool instant = false)
        {
            if (!_bound || !_target) return;
            _target = false;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            OnHidden();
            if (instant || UITheme.ReducedMotion) { _t = 0f; gameObject.SetActive(false); }
        }

        void Update()
        {
            if (!_bound) return;
            float goal = _target ? 1f : 0f;
            if (!Mathf.Approximately(_t, goal))
            {
                float seconds = Theme != null ? Theme.screenFade : 0.28f;
                _t = Mathf.MoveTowards(_t, goal, Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds));
                Apply();
                if (!_target && _t <= 0f) gameObject.SetActive(false);
            }
            OnTick();
        }

        protected virtual void OnTick() { }

        void Apply()
        {
            float e = Ease.OutCubic(_t);
            _group.alpha = e;
            if (_target)
            {
                _group.blocksRaycasts = _t > 0.4f;
                _group.interactable = _t > 0.4f;
            }
            if (_rect != null)
                _rect.anchoredPosition = new Vector2(0f, _target && !UITheme.ReducedMotion ? -28f * (1f - e) : 0f);
        }

        // ------------------------------------------------------------------
        // Small helpers shared by pages
        // ------------------------------------------------------------------

        protected static void Hook(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        protected static void SetText(TMP_Text text, string value)
        {
            if (text != null) text.text = value ?? string.Empty;
        }

        protected static void SetActive(Component c, bool on)
        {
            if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
        }

        protected static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        /// <summary>Removes every child of a container except those kept as templates.</summary>
        protected static void Clear(Transform container, params Transform[] keep)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                if (Array.IndexOf(keep, child) >= 0) continue;
                var element = child.GetComponent<LayoutElement>();
                if (element != null && element.ignoreLayout) continue;   // decoration (a card's shadow), not content
                Destroy(child.gameObject);
            }
        }
    }
}
