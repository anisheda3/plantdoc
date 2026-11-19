using PlantDoctor.AR;
using PlantDoctor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI.Classic
{
    /// <summary>
    /// Going back through the guided flow. The arrow in the top bar returns to the previous step (and is
    /// the app's brand mark on the first page, where there is nothing to go back to); the house button
    /// returns to the starting page after asking, because everything done so far is lost. Android's back
    /// key does what the arrow does. During the analysis, going back cancels it.
    /// </summary>
    public class ClassicNavigation : MonoBehaviour
    {
        [SerializeField] AppFlowController flow;
        [SerializeField] AppModeController modeController;

        [Header("Top bar")]
        [SerializeField] Button backButton;
        [SerializeField] Image backIcon;
        [SerializeField] Sprite arrowSprite;
        [SerializeField] Sprite brandSprite;
        [SerializeField] Color arrowColor = Color.white;
        [SerializeField] Color brandColor = Color.white;
        [SerializeField] Button homeButton;

        [Header("Confirmation")]
        [SerializeField] FadeSlide confirmGroup;
        [SerializeField] TMP_Text confirmTitle;
        [SerializeField] TMP_Text confirmBody;
        [SerializeField] Button stayButton;
        [SerializeField] Button leaveButton;
        [SerializeField] TMP_Text leaveLabel;

        System.Action _confirmed;

        public bool ConfirmOpen => confirmGroup != null && confirmGroup.IsShown;

        /// <summary>The step the back arrow goes to from <paramref name="state"/>; null on the first page.</summary>
        public static SimState? BackTarget(SimState state, bool healthy)
        {
            switch (state)
            {
                case SimState.Welcome: return null;
                case SimState.Scan: case SimState.ScanRetry: return SimState.Welcome;
                case SimState.AiAnalysis: return SimState.Scan;                 // cancels the analysis
                case SimState.PlantIdentified: return SimState.Scan;            // discards this result: scan again
                case SimState.FeatureAnalysis: return SimState.PlantIdentified;
                case SimState.Diagnosis: return SimState.FeatureAnalysis;
                case SimState.LowConfidence: return SimState.Diagnosis;
                case SimState.ArView: return SimState.Diagnosis;
                case SimState.FocusPart: return SimState.Diagnosis;
                case SimState.DiseaseSimulation: return SimState.FocusPart;
                case SimState.TreatmentIntro: return SimState.DiseaseSimulation;
                case SimState.TreatmentStep: return SimState.TreatmentIntro;
                case SimState.RecoveryTimeline: return SimState.TreatmentStep;
                case SimState.Prevention: return healthy ? SimState.Diagnosis : SimState.RecoveryTimeline;
                case SimState.FinalReport: return SimState.Prevention;
                case SimState.Error: return SimState.Welcome;
                default: return SimState.Welcome;
            }
        }

        /// <summary>True when leaving would throw away a result the user has already seen.</summary>
        public static bool WouldLoseProgress(SimState state) =>
            state != SimState.Welcome && state != SimState.Scan && state != SimState.ScanRetry && state != SimState.AiAnalysis && state != SimState.Error;

        void OnEnable()
        {
            GameEvents.OnStateChanged += HandleStateChanged;
            if (backButton != null) backButton.onClick.AddListener(GoBack);
            if (homeButton != null) homeButton.onClick.AddListener(GoHome);
            if (stayButton != null) stayButton.onClick.AddListener(CloseConfirm);
            if (leaveButton != null) leaveButton.onClick.AddListener(ConfirmLeave);
            Paint(flow != null ? flow.CurrentState : SimState.Welcome);
        }

        void OnDisable()
        {
            GameEvents.OnStateChanged -= HandleStateChanged;
            if (backButton != null) backButton.onClick.RemoveListener(GoBack);
            if (homeButton != null) homeButton.onClick.RemoveListener(GoHome);
            if (stayButton != null) stayButton.onClick.RemoveListener(CloseConfirm);
            if (leaveButton != null) leaveButton.onClick.RemoveListener(ConfirmLeave);
        }

        void HandleStateChanged(SimState from, SimState to) => Paint(to);

        void Paint(SimState state)
        {
            bool first = state == SimState.Welcome;
            if (backIcon != null)
            {
                backIcon.sprite = first ? brandSprite : arrowSprite;
                backIcon.color = first ? brandColor : arrowColor;
            }
            if (backButton != null) backButton.interactable = !first;
            if (homeButton != null) homeButton.gameObject.SetActive(!first);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))   // Android's back button arrives as Escape
            {
                if (ConfirmOpen) CloseConfirm(); else GoBack();
            }
        }

        public void GoBack()
        {
            if (flow == null || ConfirmOpen) return;
            var state = flow.CurrentState;
            var target = BackTarget(state, flow.Context != null && flow.Context.IsHealthy);
            if (target == null) return;

            if (state == SimState.AiAnalysis) flow.AI?.CancelActiveRequest();
            if (state == SimState.PlantIdentified) { flow.ScanAnother(); return; }   // a new scan starts clean
            flow.GoTo(target.Value);
        }

        public void GoHome()
        {
            if (flow == null) return;
            var state = flow.CurrentState;
            if (state == SimState.Welcome) return;
            if (state == SimState.AiAnalysis) flow.AI?.CancelActiveRequest();

            if (WouldLoseProgress(state)) { OpenConfirm(StartOver); return; }
            StartOver();
        }

        void StartOver() => flow?.RestartSession();

        void OpenConfirm(System.Action onLeave)
        {
            _confirmed = onLeave;
            if (confirmTitle != null) confirmTitle.text = "Go back to the start?";
            if (confirmBody != null) confirmBody.text = "This check will end and its results will be lost. Your plant and the scan are not saved.";
            if (leaveLabel != null) leaveLabel.text = "Start over";
            confirmGroup?.Show();
        }

        public void CloseConfirm()
        {
            _confirmed = null;
            confirmGroup?.Hide();
        }

        public void ConfirmLeave()
        {
            var action = _confirmed;
            CloseConfirm();
            action?.Invoke();
        }
    }
}
