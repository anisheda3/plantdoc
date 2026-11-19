using System;
using System.Collections;
using PlantDoctor.Core;
using PlantDoctor.Utilities;
using UnityEngine;

namespace PlantDoctor.AR
{
    /// <summary>
    /// Drives the guided scanning workflow:
    /// live quality feedback -> capture -> ROI crop -> hand off to AppFlowController.
    /// Contains no AI or disease logic; it is purely the input layer.
    ///
    /// The frame source is chosen at runtime by AppModeController (AR camera on ARCore
    /// phones, plain camera elsewhere), and the camera is only sampled while a scan
    /// stage is on screen, to save battery and heat.
    /// </summary>
    public class ARScanController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] AppFlowController flow;
        [Tooltip("Optional default source (must implement ICameraFrameSource). AppModeController normally sets it.")]
        [SerializeField] MonoBehaviour frameSourceBehaviour;
        [SerializeField] AI.ImageQualityAnalyzer qualitySettings;

        [Header("Reticle / ROI")]
        [Tooltip("Normalised viewport rect the user is asked to fill with the leaf. Updated live by ScannerView.")]
        [SerializeField] Rect regionOfInterest = new Rect(0.15f, 0.36f, 0.7f, 0.32f);

        [Header("Live feedback")]
        [SerializeField] float qualitySampleInterval = 0.3f;
        [Tooltip("Long edge of the frames used for the live quality meter (small = cheap).")]
        [SerializeField] int previewLongEdge = 288;
        [Tooltip("Long edge of the frame actually submitted for analysis.")]
        [SerializeField] int captureLongEdge = 960;

        ICameraFrameSource _source;
        Coroutine _qualityLoop;
        bool _isCapturing;
        bool _paused;

        /// <summary>Latest live quality reading, for the reticle colour / hint text.</summary>
        public ScanQualityReport LiveQuality { get; private set; } = new ScanQualityReport();
        public bool HasLiveQuality { get; private set; }
        public bool CameraReady => _source != null && _source.IsReady;
        public bool IsCapturing => _isCapturing;
        public ICameraFrameSource Source => _source;
        public string CameraStatusMessage { get; private set; } = string.Empty;

        /// <summary>Raised when the camera starts, stops or fails.</summary>
        public event Action CameraStatusChanged;

        void Awake()
        {
            if (_source == null && frameSourceBehaviour != null)
            {
                _source = frameSourceBehaviour as ICameraFrameSource;
                if (_source == null)
                    Log.Error(Log.Channel.AR, $"{frameSourceBehaviour.GetType().Name} does not implement ICameraFrameSource.");
            }
        }

        void OnEnable() => GameEvents.OnStateChanged += HandleStateChanged;

        void OnDisable()
        {
            GameEvents.OnStateChanged -= HandleStateChanged;
            StopQualityLoop();
        }

        /// <summary>Selects the camera used for scanning. Safe to call before or after Start.</summary>
        public void SetFrameSource(ICameraFrameSource source)
        {
            if (ReferenceEquals(source, _source)) return;
            StopQualityLoop();
            if (_source != null) _source.ReadyStateChanged -= HandleSourceReady;
            _source = source;
            if (_source != null) _source.ReadyStateChanged += HandleSourceReady;

            if (flow != null && IsScanStage(flow.CurrentState)) StartCamera();
        }

        static bool IsScanStage(SimState s) => s == SimState.Scan || s == SimState.ScanRetry;

        void HandleStateChanged(SimState from, SimState to)
        {
            if (IsScanStage(to)) StartCamera();
            else StopQualityLoop();
        }

        void HandleSourceReady(bool ready)
        {
            if (!ready && _source != null && _source.LastFailure != CameraFailure.None)
            {
                CameraStatusMessage = CameraFailureText.Describe(_source.LastFailure);
                GameEvents.RaiseError("CAMERA_UNAVAILABLE", CameraStatusMessage);
            }
            CameraStatusChanged?.Invoke();
        }

        /// <summary>
        /// Android holds the camera device exclusively. If we keep it open while
        /// backgrounded, resuming often fails with "camera already in use" or
        /// returns a black feed. Release on pause, reacquire on resume.
        /// </summary>
        void OnApplicationPause(bool paused)
        {
            if (!isActiveAndEnabled) return;
            _paused = paused;
            if (paused)
            {
                StopQualityLoop();
                if (_source is WebCamFrameSource) _source.StopSource();
            }
            else if (flow != null && IsScanStage(flow.CurrentState))
            {
                StartCamera();
            }
        }

        /// <summary>Starts the camera, reporting a friendly message on failure instead of throwing.</summary>
        public bool StartCamera()
        {
            if (_paused) return false;
            if (_source == null)
            {
                CameraStatusMessage = "No camera source is configured. Use Demo Mode to explore the simulation.";
                CameraStatusChanged?.Invoke();
                return false;
            }

            if (!_source.TryStart())
            {
                CameraStatusMessage = CameraFailureText.Describe(_source.LastFailure);
                GameEvents.RaiseError("CAMERA_UNAVAILABLE", CameraStatusMessage);
                Log.Warn(Log.Channel.AR, $"Camera start failed: {_source.LastFailure}");
                CameraStatusChanged?.Invoke();
                return false;
            }

            CameraStatusMessage = string.Empty;
            if (_qualityLoop == null && isActiveAndEnabled) _qualityLoop = StartCoroutine(QualityFeedbackLoop());
            CameraStatusChanged?.Invoke();
            return true;
        }

        void StopQualityLoop()
        {
            if (_qualityLoop != null) { StopCoroutine(_qualityLoop); _qualityLoop = null; }
            HasLiveQuality = false;
        }

        /// <summary>Continuously samples frame quality so the UI can guide the user before capture.</summary>
        IEnumerator QualityFeedbackLoop()
        {
            var wait = new WaitForSeconds(qualitySampleInterval);
            while (enabled)
            {
                if (!_isCapturing && _source != null && _source.IsReady && qualitySettings != null)
                {
                    var frame = _source.AcquireFrame(previewLongEdge);
                    if (frame != null)
                    {
                        LiveQuality = qualitySettings.Analyze(frame, _source.CurrentMotion, regionOfInterest);
                        HasLiveQuality = true;
                    }
                }
                yield return wait;
            }
            _qualityLoop = null;
        }

        /// <summary>
        /// Called by the shutter button. Captures the current frame and submits it.
        /// The quality gate inside AppFlowController.SubmitScan is authoritative.
        /// </summary>
        public async void CaptureAndSubmit()
        {
            if (_isCapturing) return;
            if (flow == null)
            {
                Log.Error(Log.Channel.AR, "AppFlowController is not assigned.");
                return;
            }

            if (!CameraReady && !StartCamera()) return;
            if (!CameraReady)
            {
                GameEvents.RaiseError("CAMERA_STARTING", "The camera is still starting. Try again in a moment.");
                return;
            }

            _isCapturing = true;
            try
            {
                var frame = _source.AcquireFrame(captureLongEdge);
                if (frame == null)
                {
                    GameEvents.RaiseError("INVALID_IMAGE", "The camera frame could not be read. Please try again.");
                    return;
                }

                Log.Info(Log.Channel.AR, $"Captured frame {frame.width}x{frame.height}; submitting for analysis.");
                await flow.SubmitScan(frame, regionOfInterest, _source.CurrentMotion);
            }
            catch (Exception e)
            {
                Log.Error(Log.Channel.AR, $"Scan failed: {e.Message}");
                GameEvents.RaiseError("SCAN_FAILED", "Something went wrong while scanning. Please try again.");
            }
            finally
            {
                _isCapturing = false;
            }
        }

        public void SetRegionOfInterest(Rect normalized) => regionOfInterest = normalized;
        public Rect RegionOfInterest => regionOfInterest;
    }
}
