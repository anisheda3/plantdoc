using PlantDoctor.AR;
using PlantDoctor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI.Classic
{
    /// <summary>
    /// The scan screen: animated framing reticle, live quality chips (light, focus, leaf
    /// coverage), a one-line hint, and the shutter. The reticle's on-screen rect is fed
    /// to the scan controller as the region of interest every frame, so what the user
    /// frames is precisely what gets checked and analysed.
    /// </summary>
    public class ScannerView : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] AppFlowController flow;
        [SerializeField] ARScanController scanController;
        [SerializeField] DemoModeController demoMode;
        [SerializeField] ClassicTheme theme;
        [SerializeField] FadeSlide group;

        [Header("Reticle")]
        [SerializeField] RectTransform reticle;
        [SerializeField] Image[] corners;
        [SerializeField] RectTransform scanLine;
        [SerializeField] Image scanLineImage;

        [Header("Feedback")]
        [SerializeField] ChipView lightChip;
        [SerializeField] ChipView focusChip;
        [SerializeField] ChipView leafChip;
        [SerializeField] TMP_Text hint;

        [Header("Capture")]
        [SerializeField] Button shutter;
        [SerializeField] Image shutterRing;
        [SerializeField] Image shutterCore;
        [SerializeField] GameObject analyzingGroup;
        [SerializeField] TMP_Text analyzingLabel;
        [SerializeField] Button demoButton;

        readonly Vector3[] _corners = new Vector3[4];
        float _sweep;

        void OnEnable()
        {
            if (shutter != null) shutter.onClick.AddListener(Capture);
            if (demoButton != null) demoButton.onClick.AddListener(RunDemo);
        }

        void OnDisable()
        {
            if (shutter != null) shutter.onClick.RemoveListener(Capture);
            if (demoButton != null) demoButton.onClick.RemoveListener(RunDemo);
        }

        void Capture() => scanController?.CaptureAndSubmit();

        async void RunDemo()
        {
            if (demoMode != null) await demoMode.RunSelectedScenario();
        }

        void Update()
        {
            if (flow == null || theme == null) return;

            var state = flow.CurrentState;
            bool scanStage = state == SimState.Scan || state == SimState.ScanRetry;
            bool analyzing = state == SimState.AiAnalysis;
            group?.Set(scanStage || analyzing);
            if (!(scanStage || analyzing)) return;

            UpdateRegionOfInterest();

            bool cameraReady = scanController != null && scanController.CameraReady;
            bool hasQuality = cameraReady && scanController.HasLiveQuality;
            var q = hasQuality ? scanController.LiveQuality : null;

            bool lightOk = q != null && !q.Issues.Contains(ScanIssue.InsufficientLighting);
            bool focusOk = q != null && !q.Issues.Contains(ScanIssue.ExcessiveBlur) && !q.Issues.Contains(ScanIssue.ExcessiveMotion);
            bool leafOk = q != null && !q.Issues.Contains(ScanIssue.LeafTooSmall);
            bool allOk = q != null && q.IsAcceptable;

            if (q == null)
            {
                var idle = theme.textTertiary;
                lightChip?.Bind("Light", idle, false, theme);
                focusChip?.Bind("Focus", idle, false, theme);
                leafChip?.Bind("Leaf", idle, false, theme);
            }
            else
            {
                lightChip?.Bind("Light", lightOk ? theme.accent : theme.warning, false, theme);
                focusChip?.Bind(q.Issues.Contains(ScanIssue.ExcessiveMotion) ? "Hold still" : "Focus",
                    focusOk ? theme.accent : theme.warning, false, theme);
                leafChip?.Bind("Leaf", leafOk ? theme.accent : theme.warning, false, theme);
            }

            // Reticle colour: neutral while starting, amber while guiding, mint when ready.
            var tone = analyzing ? theme.info : q == null ? theme.textSecondary : allOk ? theme.accent : theme.warning;
            if (corners != null)
                foreach (var c in corners) if (c != null) c.color = Color.Lerp(c.color, tone, Ease.Damp(10f, Time.unscaledDeltaTime));

            // Sweep line: slow while framing, fast while the AI is looking.
            if (scanLine != null && reticle != null)
            {
                _sweep += Time.unscaledDeltaTime * (analyzing ? 1.6f : 0.55f);
                float y = Mathf.Lerp(-0.42f, 0.42f, Ease.InOutSine(Mathf.PingPong(_sweep, 1f)));
                scanLine.anchoredPosition = new Vector2(0f, y * reticle.rect.height);
                if (scanLineImage != null)
                    scanLineImage.color = new Color(tone.r, tone.g, tone.b, analyzing ? 0.85f : (cameraReady ? 0.55f : 0.15f));
            }

            if (hint != null)
            {
                hint.text = analyzing ? "Hold on — reading the leaf"
                    : !cameraReady ? (string.IsNullOrEmpty(scanController?.CameraStatusMessage)
                        ? "Starting camera…" : scanController.CameraStatusMessage)
                    : q == null ? "Fill the frame with one leaf"
                    : allOk ? "Looks good — tap to scan"
                    : q.UserMessage();
            }

            if (analyzingGroup != null) analyzingGroup.SetActive(analyzing);
            if (analyzingLabel != null) analyzingLabel.text = "Analyzing leaf";

            bool canShoot = !analyzing && cameraReady && !scanController.IsCapturing;
            if (shutter != null)
            {
                shutter.gameObject.SetActive(!analyzing);
                shutter.interactable = canShoot;
            }
            if (shutterRing != null) shutterRing.color = canShoot ? (allOk ? theme.accent : theme.textPrimary) : theme.textTertiary;
            if (shutterCore != null) shutterCore.color = canShoot ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            if (demoButton != null) demoButton.gameObject.SetActive(!analyzing);
        }

        /// <summary>Converts the reticle's screen rect into the normalised viewport ROI.</summary>
        void UpdateRegionOfInterest()
        {
            if (reticle == null || scanController == null || Screen.width <= 0 || Screen.height <= 0) return;
            reticle.GetWorldCorners(_corners);   // screen-space for an overlay canvas
            float xMin = _corners[0].x / Screen.width, yMin = _corners[0].y / Screen.height;
            float xMax = _corners[2].x / Screen.width, yMax = _corners[2].y / Screen.height;
            var rect = Rect.MinMaxRect(Mathf.Clamp01(xMin), Mathf.Clamp01(yMin), Mathf.Clamp01(xMax), Mathf.Clamp01(yMax));
            if (rect.width > 0.05f && rect.height > 0.05f) scanController.SetRegionOfInterest(rect);
        }
    }
}
