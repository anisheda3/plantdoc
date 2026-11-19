using System.Collections.Generic;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using PlantDoctor.Treatment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>
    /// The whole story of the disease on one page and in one animation: how the infection developed
    /// (its lifecycle), the treatment steps, and the recovery that follows, from "diseased" to
    /// "recovered". The plant in the backdrop (and a small leaf beside it) plays through it; one
    /// timeline can be played, paused or scrubbed in either direction.
    ///
    /// It is a simulation: the outcome assumes every step is followed, uses the same recovery rates
    /// and residual floors as the rest of the app, and the page says nothing is promised. The
    /// steps listed below the timeline can be marked done; that is the user's own record and is
    /// what the health report shows.
    /// </summary>
    public class JourneyScreen : ScreenView
    {
        public override AppScreenId Id => AppScreenId.Journey;

        [Header("Header")]
        [SerializeField] Button backButton;
        [SerializeField] RawImage leafInset;
        [SerializeField] TMP_Text insetCaption;

        [Header("Timeline")]
        [SerializeField] SegmentedTabs phaseTabs;
        [SerializeField] TMP_Text stageMeta;
        [SerializeField] TMP_Text stageTitle;
        [SerializeField] TMP_Text stageBody;
        [SerializeField] Button playButton;
        [SerializeField] Image playIcon;
        [SerializeField] Slider progress;
        [SerializeField] RectTransform tickRoot;
        [SerializeField] StatusChipView outcomeChip;

        [Header("Steps")]
        [SerializeField] RectTransform stepsRoot;
        [SerializeField] StepCardView stepTemplate;
        [SerializeField] TMP_Text stepsTitle;
        [SerializeField] TMP_Text stepsHint;
        [SerializeField] TMP_Text disclaimer;
        [SerializeField] Button nextButton;
        [SerializeField] ScrollRect scroll;

        const float SecondsPerSegment = 1.45f;
        // The leaf is drawn into a screen-shaped picture; the inset shows this middle part of it.
        static readonly Rect InsetFrame = new Rect(0.2f, 0.36f, 0.6f, 0.277f);
        static readonly string[] PhaseNames = { "Infection", "Treatment", "Recovery" };

        class Key
        {
            public int phase;
            public string meta, title, body;
            public PlantHealthState state;
        }

        readonly List<Key> _keys = new List<Key>();
        readonly List<Image> _ticks = new List<Image>();
        int _diseasedIndex;
        float _p;                 // 0..1 along the whole timeline
        bool _playing, _setting;
        int _shownKey = -1;

        protected override void OnBind()
        {
            if (stepTemplate != null) stepTemplate.gameObject.SetActive(false);
            Hook(backButton, () => Ctx.Router.Back());
            Hook(nextButton, () => Ctx.Flow.OpenPrevention());
            Hook(playButton, TogglePlay);
            phaseTabs.Init(Theme, 0);
            phaseTabs.Selected += JumpToPhase;
            if (progress != null) progress.onValueChanged.AddListener(v => { if (_setting) return; _playing = false; SetProgress(v, false); });
        }

        protected override void OnShown()
        {
            var ctx = Ctx.Session;
            if (ctx == null) return;
            BuildKeys(ctx);
            BuildTicks();

            if (Ctx.Stage != null)
            {
                Ctx.Stage.ShowLeaf(leafInset != null);
                Ctx.Stage.LeafView.ResetPose();
                Ctx.Stage.LeafView.SetFrame(InsetFrame, true);
                BindInset();
            }

            // Open on the diseased plant, as scanned, and let the cure play.
            _shownKey = -1;
            SetProgress(_keys.Count > 1 ? _diseasedIndex / (float)(_keys.Count - 1) : 0f);
            _playing = !UITheme.ReducedMotion;
            SetText(disclaimer, "A simulation that assumes every step is followed. Real recovery depends on the weather, the plant and how closely the plan is followed; nothing here is a promise.");
            Refresh();
        }

        protected override void OnHidden()
        {
            _playing = false;
            Ctx.Stage?.ShowLeaf(false);
            // Leave the plant exactly as the session has it, not as the animation left it.
            var ctx = Ctx.Session;
            if (ctx != null) Ctx.Binder?.SetTargetState(ctx.CurrentState, immediate: true);
        }

        void BindInset()
        {
            if (leafInset == null || Ctx.Stage == null) return;
            Ctx.Stage.LeafView.Bind(leafInset, 0.5f, 2, ((RectTransform)transform).rect.size);
            leafInset.uvRect = InsetFrame;
        }

        // ------------------------------------------------------------------
        // The keyframes: infection stages, treatment steps, recovery days
        // ------------------------------------------------------------------

        void BuildKeys(SessionContext ctx)
        {
            _keys.Clear();
            var disease = ctx.Disease;
            var plan = disease != null ? disease.treatmentPlan : null;

            var peak = ctx.InitialState.Clone();
            var healthy = new PlantHealthState { healthyGrowth = Mathf.Max(0.85f, peak.healthyGrowth) };

            // Infection: the lifecycle of the disease, from healthy to the state that was scanned.
            var lifecycle = disease != null && disease.lifecycleStages != null && disease.lifecycleStages.Length > 0
                ? disease.lifecycleStages : new[] { "The infection takes hold.", "It spreads across the leaf.", "Spots and yellowing appear.", "The plant is visibly affected." };
            _keys.Add(new Key { phase = 0, meta = "Before the infection", title = "A healthy plant", body = "This is how the plant looked before the disease arrived.", state = healthy });
            for (int i = 0; i < lifecycle.Length; i++)
            {
                float t = (i + 1) / (float)lifecycle.Length;
                bool last = i == lifecycle.Length - 1;
                _keys.Add(new Key
                {
                    phase = 0,
                    meta = $"Infection · stage {i + 1} of {lifecycle.Length}",
                    title = last ? "Diseased, as scanned" : $"Stage {i + 1}",
                    body = lifecycle[i],
                    state = PlantHealthState.Lerp(healthy, peak, Ease.InOutSine(t))
                });
            }
            _diseasedIndex = _keys.Count - 1;

            // Treatment: the plan's steps applied one after the other.
            var current = peak.Clone();
            float severityFloor = plan != null ? plan.residualSeverityFloor : 0f, damageFloor = plan != null ? plan.residualDamageFloor : 0f;
            int steps = plan != null && plan.steps != null ? plan.steps.Length : 0;
            for (int i = 0; i < steps; i++)
            {
                var step = plan.steps[i];
                if (step == null) continue;
                current.Apply(step.effect);
                current.ApplyFloors(severityFloor, damageFloor);
                _keys.Add(new Key
                {
                    phase = 1,
                    meta = $"Treatment · step {i + 1} of {steps}",
                    title = step.title,
                    body = step.instruction,
                    state = current.Clone()
                });
            }

            // Recovery: a few days later, from the state the treatment leaves the plant in.
            int[] days = { 3, 7, 14 };
            string[] titles = { "Day 3 · recovery begins", "Day 7 · recovering", "Recovered" };
            string[] bodies =
            {
                "The spread slows and healthy growth resumes, if the plan is followed.",
                "Fewer new spots appear, and new leaves grow in clean.",
                "The plant has largely recovered. Some marks can stay: recovery is rarely total."
            };
            for (int i = 0; i < days.Length; i++)
            {
                var projected = Ctx.Recovery != null ? Ctx.Recovery.Project(current, days[i]) : current.Clone();
                _keys.Add(new Key { phase = 2, meta = $"Recovery · day {days[i]} (simulated)", title = titles[i], body = bodies[i], state = projected });
            }
        }

        void BuildTicks()
        {
            foreach (var t in _ticks) if (t != null) Destroy(t.gameObject);
            _ticks.Clear();
            if (tickRoot == null || Theme == null || Theme.circleSprite == null) return;
            for (int i = 0; i < _keys.Count; i++)
            {
                var go = new GameObject("Tick" + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(tickRoot, false);
                var rt = (RectTransform)go.transform;
                float x = _keys.Count > 1 ? i / (float)(_keys.Count - 1) : 0f;
                rt.anchorMin = rt.anchorMax = new Vector2(x, 0.5f);
                rt.sizeDelta = new Vector2(16, 16);
                rt.anchoredPosition = Vector2.zero;
                var img = go.GetComponent<Image>();
                img.sprite = Theme.circleSprite;
                img.raycastTarget = false;
                _ticks.Add(img);
            }
            PaintTicks();
        }

        void PaintTicks()
        {
            int reached = Mathf.RoundToInt(_p * (_keys.Count - 1));
            for (int i = 0; i < _ticks.Count; i++)
            {
                var c = _keys[i].phase == 0 ? Theme.amber : _keys[i].phase == 1 ? Theme.textOnDark : Theme.leaf;
                c.a = i <= reached ? 0.95f : 0.35f;
                _ticks[i].color = c;
            }
        }

        // ------------------------------------------------------------------
        // Playback
        // ------------------------------------------------------------------

        void TogglePlay()
        {
            if (_p >= 0.999f) SetProgress(_keys.Count > 1 ? _diseasedIndex / (float)(_keys.Count - 1) : 0f);   // replay the cure
            _playing = !_playing;
        }

        void JumpToPhase(int phase)
        {
            _playing = false;
            for (int i = 0; i < _keys.Count; i++)
                if (_keys[i].phase == phase) { SetProgress(i / (float)Mathf.Max(1, _keys.Count - 1)); return; }
        }

        void SetProgress(float p, bool updateSlider = true)
        {
            _p = Mathf.Clamp01(p);
            if (updateSlider && progress != null) { _setting = true; progress.value = _p; _setting = false; }
            ApplyState();
        }

        void ApplyState()
        {
            if (_keys.Count < 2 || Ctx.Binder == null) return;
            float x = _p * (_keys.Count - 1);
            int k = Mathf.Min(_keys.Count - 2, Mathf.FloorToInt(x));
            float t = Mathf.Clamp01(x - k);
            Ctx.Binder.SetTargetState(PlantHealthState.Lerp(_keys[k].state, _keys[k + 1].state, Ease.InOutSine(t)), immediate: true);

            int key = Mathf.Clamp(Mathf.RoundToInt(x), 0, _keys.Count - 1);
            if (key != _shownKey) ShowKey(key);
            PaintTicks();
        }

        void ShowKey(int index)
        {
            _shownKey = index;
            var key = _keys[index];
            SetText(stageMeta, key.meta);
            SetText(stageTitle, key.title);
            SetText(stageBody, key.body);
            if (phaseTabs != null && phaseTabs.Index != key.phase) phaseTabs.Select(key.phase, false);
            bool recovered = index == _keys.Count - 1;
            SetActive(outcomeChip, recovered);
            if (recovered && outcomeChip != null) outcomeChip.Bind(Theme, "Recovered · simulated", Tone.Good, "circle-check");
            else if (index == _diseasedIndex && outcomeChip != null)
            {
                SetActive(outcomeChip, true);
                outcomeChip.Bind(Theme, Ctx.Session != null && Ctx.Session.Analysis != null && Ctx.Session.Analysis.IsSimulatedResult ? "Diseased · demo result" : "Diseased · as scanned", Tone.Warning, "triangle-alert");
            }
        }

        protected override void OnTick()
        {
            if (Ctx == null || Theme == null || _keys.Count < 2) return;
            if (_playing)
            {
                float seconds = _keys.Count * SecondsPerSegment;
                _p += Time.unscaledDeltaTime / seconds;
                if (_p >= 1f) { _p = 1f; _playing = false; }
                SetProgress(_p);
            }
            if (playIcon != null) playIcon.sprite = Theme.Icon(_playing ? "pause" : _p >= 0.999f ? "refresh-cw" : "play");
            if (Time.frameCount % 20 == 0) BindInset();
        }

        // ------------------------------------------------------------------
        // The treatment steps the user can tick off
        // ------------------------------------------------------------------

        public override void Refresh()
        {
            var flow = Ctx.Flow;
            var treatment = flow.Treatment;
            var plan = treatment != null ? treatment.Plan : null;
            Clear(stepsRoot, stepTemplate.transform);

            bool has = plan != null && plan.steps != null && plan.steps.Length > 0;
            SetActive(stepsTitle, has);
            SetActive(stepsHint, has);
            if (!has) return;

            int done = treatment.CompletedCount;
            SetText(stepsTitle, "Your treatment steps");
            SetText(stepsHint, done >= plan.steps.Length
                ? $"All {plan.steps.Length} steps marked as done. They appear in your report."
                : $"{done} of {plan.steps.Length} marked as done. Tick a step once you have really done it; it goes into your report.");

            for (int i = 0; i < plan.steps.Length; i++)
            {
                var step = plan.steps[i];
                if (step == null) continue;
                var card = Instantiate(stepTemplate, stepsRoot);
                card.gameObject.SetActive(true);
                var state = i < done ? StepCardView.State.Done : i == done ? StepCardView.State.Current : StepCardView.State.Waiting;
                card.Bind(Theme, i, step, state, () => { Ctx.Flow.PerformNextTreatmentStep(false); Refresh(); });
            }
        }
    }
}
