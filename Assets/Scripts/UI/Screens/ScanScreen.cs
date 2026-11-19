using PlantDoctor.AI;
using PlantDoctor.AR;
using PlantDoctor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>
    /// The scan page: live camera behind, a framing guide, three live quality checks, and the
    /// shutter. Everything shown comes from the real validation (ARScanController.LiveQuality):
    /// the indicators and the message are never invented, and the shutter submits through the
    /// normal capture path, which rejects a bad photo with its actual reason.
    /// </summary>
    public class ScanScreen : ScreenView
    {
        public override AppScreenId Id => AppScreenId.Scan;

        [Header("Frame")]
        [SerializeField] RectTransform reticle;
        [SerializeField] Image[] corners = new Image[4];

        [Header("Feedback")]
        [SerializeField] TMP_Text instruction;
        [SerializeField] TMP_Text statusBanner;
        [SerializeField] Image statusBannerBackground;
        [SerializeField] QualityIndicatorView lightIndicator;
        [SerializeField] QualityIndicatorView focusIndicator;
        [SerializeField] QualityIndicatorView leafIndicator;

        [Header("Controls")]
        [SerializeField] Button backButton;
        [SerializeField] Button shutter;
        [SerializeField] Image shutterRing;
        [SerializeField] Image shutterCore;
        [SerializeField] Button demoButton;

        readonly Vector3[] _corners = new Vector3[4];
        string _rejection;
        float _rejectionUntil;

        protected override void OnBind()
        {
            Hook(backButton, () => Ctx.Router.Back());
            Hook(shutter, () => Ctx.Scan?.CaptureAndSubmit());
            Hook(demoButton, RunDemo);
            SetText(instruction, "Position a single leaf within the frame");
        }

        protected override void OnShown()
        {
            GameEvents.OnScanQualityFailed += HandleQualityFailed;
            _rejection = null;
        }

        protected override void OnHidden() => GameEvents.OnScanQualityFailed -= HandleQualityFailed;

        void HandleQualityFailed(ScanQualityReport report)
        {
            _rejection = report.UserMessage();     // the real reason the photo was refused
            _rejectionUntil = Time.unscaledTime + 4f;
        }

        async void RunDemo()
        {
            if (Ctx.Demo != null) await Ctx.Demo.RunSelectedScenario();
        }

        protected override void OnTick()
        {
            if (Ctx == null || Theme == null || Ctx.Flow == null) return;
            UpdateRegionOfInterest();

            var scan = Ctx.Scan;
            bool cameraReady = scan != null && scan.CameraReady;
            var q = cameraReady && scan.HasLiveQuality ? scan.LiveQuality : null;

            bool lightOk = q != null && !q.Issues.Contains(ScanIssue.InsufficientLighting);
            bool motion = q != null && q.Issues.Contains(ScanIssue.ExcessiveMotion);
            bool blur = q != null && q.Issues.Contains(ScanIssue.ExcessiveBlur);
            bool leafOk = q != null && !q.Issues.Contains(ScanIssue.LeafTooSmall);
            bool allOk = q != null && q.IsAcceptable;

            if (q == null)
            {
                lightIndicator?.Bind(Theme, QualityIndicatorView.State.Idle, "Checking");
                focusIndicator?.Bind(Theme, QualityIndicatorView.State.Idle, "Checking");
                leafIndicator?.Bind(Theme, QualityIndicatorView.State.Idle, "Checking");
            }
            else
            {
                lightIndicator?.Bind(Theme, lightOk ? QualityIndicatorView.State.Good : QualityIndicatorView.State.Adjust, lightOk ? "Good" : "Adjust");
                focusIndicator?.Bind(Theme, !motion && !blur ? QualityIndicatorView.State.Good : QualityIndicatorView.State.Adjust,
                    motion ? "Hold steady" : blur ? "Adjust" : "Good");
                leafIndicator?.Bind(Theme, leafOk ? QualityIndicatorView.State.Good : QualityIndicatorView.State.Adjust, leafOk ? "Detected" : "Move closer");
            }

            // The line under the frame: a refused photo's real reason first, then the live guidance.
            bool showRejection = !string.IsNullOrEmpty(_rejection) && Time.unscaledTime < _rejectionUntil;
            string banner; Color tone;
            if (showRejection) { banner = _rejection; tone = Theme.red; }
            else if (!cameraReady)
            {
                banner = string.IsNullOrEmpty(scan?.CameraStatusMessage) ? "Starting camera…" : scan.CameraStatusMessage;
                tone = Theme.amber;
            }
            else if (q == null) { banner = "Checking the view…"; tone = Theme.textOnDarkMuted; }
            else if (allOk) { banner = "Ready to scan"; tone = Theme.leaf; }
            else { banner = !lightOk ? "Adjust lighting" : motion ? "Hold steady" : !leafOk ? "Move closer to the leaf" : q.UserMessage(); tone = Theme.amber; }
            SetText(statusBanner, banner);
            if (statusBanner != null) statusBanner.color = Color.white;
            if (statusBannerBackground != null) statusBannerBackground.color = new Color(tone.r, tone.g, tone.b, tone == Theme.textOnDarkMuted ? 0.35f : 0.92f);

            var cornerTone = showRejection ? Theme.red : q == null ? Color.white : allOk ? Theme.leaf : Theme.amber;
            foreach (var c in corners)
                if (c != null) c.color = Color.Lerp(c.color, cornerTone, Ease.Damp(10f, Time.unscaledDeltaTime));

            bool canShoot = cameraReady && !scan.IsCapturing;
            if (shutter != null) shutter.interactable = canShoot;
            if (shutterRing != null) shutterRing.color = canShoot ? (allOk ? Theme.leaf : Color.white) : new Color(1f, 1f, 1f, 0.4f);
            if (shutterCore != null) shutterCore.color = canShoot ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        }

        /// <summary>The frame's on-screen rectangle is the region the checks and the analysis use.</summary>
        void UpdateRegionOfInterest()
        {
            if (reticle == null || Ctx.Scan == null || Screen.width <= 0 || Screen.height <= 0) return;
            reticle.GetWorldCorners(_corners);   // screen-space for an overlay canvas
            var canvas = reticle.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null)
                for (int i = 0; i < 4; i++) _corners[i] = canvas.worldCamera.WorldToScreenPoint(_corners[i]);
            float xMin = _corners[0].x / Screen.width, yMin = _corners[0].y / Screen.height;
            float xMax = _corners[2].x / Screen.width, yMax = _corners[2].y / Screen.height;
            var rect = Rect.MinMaxRect(Mathf.Clamp01(xMin), Mathf.Clamp01(yMin), Mathf.Clamp01(xMax), Mathf.Clamp01(yMax));
            if (rect.width > 0.05f && rect.height > 0.05f) Ctx.Scan.SetRegionOfInterest(rect);
        }
    }
}
