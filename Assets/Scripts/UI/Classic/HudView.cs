using PlantDoctor.AI;
using PlantDoctor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI.Classic
{
    /// <summary>
    /// Floating top bar: brand mark, stage title, "Step n of 7" with a segmented progress
    /// bar, the backend badge, and the settings button. The DEMO badge stays visible
    /// whenever results are simulated — it is the app's honesty marker.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [SerializeField] ClassicTheme theme;
        [SerializeField] AppFlowController flow;
        [SerializeField] TMP_Text stepLabel;
        [SerializeField] TMP_Text titleLabel;
        [Tooltip("One Image per user-facing step (7).")]
        [SerializeField] Image[] stepSegments;
        [SerializeField] ChipView backendChip;
        [SerializeField] Button settingsButton;
        [SerializeField] SettingsView settings;
        [SerializeField] float fillSmoothing = 8f;

        float _progress;
        float _shownProgress;

        void OnEnable()
        {
            GameEvents.OnStateChanged += HandleStateChanged;
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            if (flow != null && flow.AI != null) flow.AI.BackendChanged += RefreshBadge;
        }

        void OnDisable()
        {
            GameEvents.OnStateChanged -= HandleStateChanged;
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OpenSettings);
            if (flow != null && flow.AI != null) flow.AI.BackendChanged -= RefreshBadge;
        }

        void Start() => Refresh(flow != null ? flow.CurrentState : SimState.Welcome, flow?.Context);

        void OpenSettings() => settings?.Open();

        void HandleStateChanged(SimState from, SimState to) => Refresh(to, flow?.Context);

        public void Refresh(SimState state, SessionContext context)
        {
            int step = Mathf.RoundToInt(StageDescriptor.Progress(state) * StageDescriptor.TotalUserFacingSteps);
            if (stepLabel != null)
                stepLabel.text = step > 0 ? $"STEP {step} OF {StageDescriptor.TotalUserFacingSteps}" : "AI PLANT DOCTOR";
            if (titleLabel != null) titleLabel.text = StageDescriptor.FriendlyTitle(state);
            _progress = step;
            RefreshBadge();
        }

        void RefreshBadge()
        {
            if (backendChip == null || theme == null) return;
            var ctx = flow?.Context;
            bool resultIsDemo = ctx != null && ctx.Analysis != null && ctx.IsDemoMode;
            bool backendIsDemo = flow == null || flow.AI == null || flow.AI.Mode == InferenceMode.Mock;

            if (resultIsDemo || (ctx?.Analysis == null && backendIsDemo))
                backendChip.Bind("DEMO AI", theme.demoBanner, false, theme);
            else if (flow != null && flow.AI != null && flow.AI.Mode == InferenceMode.Remote)
                backendChip.Bind("SERVER AI", theme.info, false, theme);
            else
                backendChip.Bind("ON-DEVICE", theme.info, false, theme);
        }

        void Update()
        {
            _shownProgress = Mathf.Lerp(_shownProgress, _progress, Ease.Damp(fillSmoothing, Time.unscaledDeltaTime));
            if (stepSegments == null || theme == null) return;
            for (int i = 0; i < stepSegments.Length; i++)
            {
                if (stepSegments[i] == null) continue;
                float fill = Mathf.Clamp01(_shownProgress - i);
                stepSegments[i].color = Color.Lerp(theme.divider, theme.accent, fill);
            }
        }
    }
}
