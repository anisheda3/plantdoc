#if PLANTDOCTOR_AR_FOUNDATION
using System;
using System.Collections.Generic;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using PlantDoctor.Utilities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace PlantDoctor.AR
{
    /// <summary>
    /// Places the virtual plant twin on a real surface next to the scanned plant.
    ///
    /// After identification the twin places itself: on the surface under the screen centre
    /// as soon as one is found, or in front of the camera if none turns up within a couple
    /// of seconds, so it always appears without the user having to know to tap. Afterwards:
    /// one-finger drag rotates, pinch scales, a quick tap on another surface moves it.
    ///
    /// The twin is never parented to its ARAnchor. ARCore can drop anchors when tracking
    /// hiccups, and AR Foundation then destroys the anchor's GameObject; a parented twin
    /// would be destroyed with it and vanish for the rest of the session.
    /// </summary>
    public class PlantTwinPlacer : MonoBehaviour, ITwinPlacement
    {
        [Header("AR")]
        [SerializeField] ARRaycastManager raycastManager;
        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] Camera arCamera;

        [Header("Twin")]
        [SerializeField] Transform twinRoot;
        [SerializeField] PlantTwinPresenter presenter;
        [SerializeField] Transform placementReticle;
        [Tooltip("Twin scale in AR. 1 = life-size (~0.9 m tall); 0.35 sits nicely on a table.")]
        [SerializeField] float defaultScale = 0.35f;
        [SerializeField] Vector2 scaleRange = new Vector2(0.12f, 1.2f);
        [SerializeField] float fallbackDistance = 0.9f;

        [Header("Interface")]
        [Tooltip("Classic interface: the placed twin stays in the world from the identification to the report. Off for the dashboard interface, where it only shows on the plant (AR) view.")]
        [SerializeField] bool twinInAllStages;

        [Header("Automatic placement")]
        [Tooltip("Place on a surface once the centre of the screen has rested on it this long.")]
        [SerializeField] float surfaceSettleSeconds = 0.6f;
        [Tooltip("Preset sizes for the scale control: small / medium / large.")]
        [SerializeField] float[] scalePresets = { 0.2f, 0.35f, 0.6f };

        [Header("Gestures")]
        [SerializeField] float rotateDegreesPerScreen = 360f;
        [SerializeField] float tapMaxSeconds = 0.25f;
        [SerializeField] float tapMaxPixels = 18f;

        static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();
        static readonly List<RaycastResult> UiHits = new List<RaycastResult>();
        static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>How trustworthy the surface under the reticle is. Only a real detected plane places by itself.</summary>
        enum SurfaceQuality { None, FeaturePoint, EstimatedPlane, Plane }

        ARAnchorManager _anchorManager;
        ARPlane _reticlePlane;
        SurfaceQuality _quality;
        MaterialPropertyBlock _reticleBlock;
        Renderer _reticleRenderer;
        Vector3 _smoothedPosition;
        bool _reticleInitialised;

        GameObject _anchorGo;
        ARAnchor _anchor;
        bool _placed;
        bool _everPlaced;
        float _placingSince = -1f;
        float _surfaceSince = -1f;
        bool _hasReticlePose;
        Pose _reticlePose;
        float _touchStartTime;
        Vector2 _touchStartPos;
        bool _touchIsDrag;
        float _pinchStartDistance;
        float _pinchStartScale;

        public bool IsPlaced => _placed;
        public bool IsPlacing => isActiveAndEnabled && !_placed && WantsTwin;
        public bool HasSurface => _hasReticlePose;
        public bool SurfaceIsPlane => _hasReticlePose && _quality == SurfaceQuality.Plane;
        public float SearchSeconds => _placingSince < 0f ? 0f : Time.unscaledTime - _placingSince;

        /// <summary>Raised when the twin lands on a surface (or in front of the camera).</summary>
        public event Action Placed;

        AppFlowController _flow;
        bool WantsTwin => _flow != null && StageWantsTwin(_flow.CurrentState);

        // Dashboard interface: the twin only appears on the plant (AR) view, every other screen is an opaque page.
        // Classic interface: it appears once the plant is identified and stays through the guided stages.
        bool StageWantsTwin(SimState s)
        {
            if (!twinInAllStages) return s == SimState.ArView;
            // (At the zoom step only the magnified leaf is shown, so the placed plant steps out of the picture.)
            return s != SimState.Welcome && s != SimState.Scan && s != SimState.ScanRetry && s != SimState.AiAnalysis && s != SimState.Error && s != SimState.FocusPart;
        }

        void Awake()
        {
            _flow = FindObjectOfType<AppFlowController>();
            if (arCamera == null) arCamera = Camera.main;
            _anchorManager = FindObjectOfType<ARAnchorManager>();
            if (placementReticle != null) _reticleRenderer = placementReticle.GetComponentInChildren<Renderer>(true);
        }

        void OnEnable() => GameEvents.OnStateChanged += HandleStateChanged;
        void OnDisable() => GameEvents.OnStateChanged -= HandleStateChanged;

        void HandleStateChanged(SimState from, SimState to)
        {
            if (to == SimState.Welcome || to == SimState.Scan) { ResetPlacement(); return; }

            // Once placed, the twin stays where it is; it is only shown while its stages are open.
            if (StageWantsTwin(to)) { if (_placed) presenter?.Reveal(false); }
            else presenter?.Conceal();
        }

        /// <summary>Places the twin now if the user has not done so yet.</summary>
        public void EnsurePlaced()
        {
            if (_placed || !isActiveAndEnabled) return;
            if (_hasReticlePose) PlaceAt(_reticlePose);
            else PlaceInFrontOfCamera();
        }

        public void ResetPlacement()
        {
            _placed = false;
            _everPlaced = false;
            _placingSince = -1f;
            _surfaceSince = -1f;
            if (twinRoot != null) twinRoot.SetParent(null, true);
            if (_anchorGo != null) Destroy(_anchorGo);
            _anchorGo = null;
            _anchor = null;
            presenter?.Conceal();
            SetPlanesVisible(false);
        }

        void Update()
        {
            if (arCamera == null) return;

            bool placing = IsPlacing;
            UpdateReticle(placing);
            SetPlanesVisible(placing);
            if (placing) AutoPlace();
            else _placingSince = -1f;

            if (!WantsTwin) return;
            HandleTouches(placing);
        }

        /// <summary>
        /// The twin must appear on its own: users (and demo audiences) should not need to
        /// discover that the screen has to be tapped.
        /// </summary>
        void AutoPlace()
        {
            float now = Time.unscaledTime;
            if (_placingSince < 0f) _placingSince = now;

            // Only ever on a real surface, and only a detected plane places by itself; an estimated
            // surface waits for the user's tap, because it can be centimetres off.
            if (_hasReticlePose && _quality == SurfaceQuality.Plane)
            {
                if (_surfaceSince < 0f) _surfaceSince = now;
                if (now - _surfaceSince >= surfaceSettleSeconds) PlaceAt(_reticlePose, _reticlePlane);
            }
            else _surfaceSince = -1f;
        }

        void LateUpdate()
        {
            // Follow the anchor as ARCore refines its pose; if the anchor is lost, the twin
            // simply stays where it is.
            if (!_placed || _anchor == null || twinRoot == null) return;
            if (_anchor.trackingState == TrackingState.Tracking)
                twinRoot.position = _anchor.transform.position;
        }

        /// <summary>Best surface under a screen point: a detected plane, else an estimated plane, else a feature point.</summary>
        bool TryRaycast(Vector2 screen, out Pose pose, out ARPlane plane, out SurfaceQuality quality)
        {
            pose = default; plane = null; quality = SurfaceQuality.None;
            if (raycastManager == null) return false;

            if (raycastManager.Raycast(screen, Hits, TrackableType.PlaneWithinPolygon))
            {
                foreach (var hit in Hits)
                {
                    // Floors and tables only: a wall or a tilted plane would stand the plant sideways.
                    if (Vector3.Dot(hit.pose.up, Vector3.up) < 0.8f) continue;
                    pose = hit.pose; plane = hit.trackable as ARPlane; quality = SurfaceQuality.Plane;
                    return true;
                }
            }
            if (raycastManager.Raycast(screen, Hits, TrackableType.PlaneEstimated))
            {
                var hit = Hits[0];
                if (Vector3.Dot(hit.pose.up, Vector3.up) >= 0.8f)
                {
                    pose = hit.pose; quality = SurfaceQuality.EstimatedPlane;
                    return true;
                }
            }
            if (raycastManager.Raycast(screen, Hits, TrackableType.FeaturePoint))
            {
                // A feature point has no reliable orientation: stand the plant upright on it.
                pose = new Pose(Hits[0].pose.position, Quaternion.identity); quality = SurfaceQuality.FeaturePoint;
                return true;
            }
            return false;
        }

        void UpdateReticle(bool placing)
        {
            _hasReticlePose = false;
            _reticlePlane = null;
            _quality = SurfaceQuality.None;
            if (placing && TryRaycast(new Vector2(Screen.width * 0.5f, Screen.height * 0.42f), out var pose, out _reticlePlane, out _quality))
            {
                _hasReticlePose = true;
                // Smooth the reticle so hand shake does not make it jitter; the smoothed pose is what gets placed.
                _smoothedPosition = _reticleInitialised ? Vector3.Lerp(_smoothedPosition, pose.position, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime)) : pose.position;
                _reticleInitialised = true;
                pose.position = _smoothedPosition;
                _reticlePose = pose;
            }
            if (!_hasReticlePose) _reticleInitialised = false;

            if (placementReticle == null) return;
            placementReticle.gameObject.SetActive(placing && _hasReticlePose);
            if (!_hasReticlePose) return;

            placementReticle.SetPositionAndRotation(_reticlePose.position + _reticlePose.up * 0.002f,
                _quality == SurfaceQuality.Plane
                    ? Quaternion.FromToRotation(Vector3.up, _reticlePose.up) * YawFacingCamera(_reticlePose.position)
                    : YawFacingCamera(_reticlePose.position));
            // The same footprint the plant will have, so the user sees exactly where it will stand.
            placementReticle.localScale = Vector3.one * Mathf.Max(0.12f, defaultScale * 0.8f);
            TintReticle(_quality == SurfaceQuality.Plane ? new Color(0.55f, 0.85f, 0.45f, 1f) : new Color(0.96f, 0.72f, 0.28f, 1f));
        }

        void TintReticle(Color color)
        {
            if (_reticleRenderer == null) return;
            if (_reticleBlock == null) _reticleBlock = new MaterialPropertyBlock();
            _reticleRenderer.GetPropertyBlock(_reticleBlock);
            _reticleBlock.SetColor(ColorId, color);
            _reticleRenderer.SetPropertyBlock(_reticleBlock);
        }

        void HandleTouches(bool placing)
        {
            if (Input.touchCount == 2 && _placed)
            {
                var a = Input.GetTouch(0);
                var b = Input.GetTouch(1);
                float d = Vector2.Distance(a.position, b.position);
                if (a.phase == TouchPhase.Began || b.phase == TouchPhase.Began)
                {
                    _pinchStartDistance = Mathf.Max(1f, d);
                    _pinchStartScale = twinRoot.localScale.x;
                }
                else if (_pinchStartDistance > 0f)
                {
                    float s = Mathf.Clamp(_pinchStartScale * d / _pinchStartDistance, scaleRange.x, scaleRange.y);
                    twinRoot.localScale = Vector3.one * s;
                }
                _touchIsDrag = true;   // a pinch never ends as a tap
                return;
            }

            if (Input.touchCount != 1) return;
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                if (IsOverUI(t.fingerId)) { _touchStartTime = -1f; return; }
                _touchStartTime = Time.unscaledTime;
                _touchStartPos = t.position;
                _touchIsDrag = false;
                return;
            }
            if (_touchStartTime < 0f) return;   // touch started on the UI

            if (t.phase == TouchPhase.Moved && _placed)
            {
                if ((t.position - _touchStartPos).magnitude > tapMaxPixels) _touchIsDrag = true;
                if (_touchIsDrag)
                {
                    float yaw = -t.deltaPosition.x / Mathf.Max(1f, Screen.width) * rotateDegreesPerScreen;
                    twinRoot.Rotate(Vector3.up, yaw, Space.World);
                }
            }
            else if (t.phase == TouchPhase.Ended)
            {
                bool isTap = !_touchIsDrag && Time.unscaledTime - _touchStartTime <= tapMaxSeconds;
                if (isTap) HandleTap(t.position, placing);
                _touchStartTime = -1f;
            }
        }

        void HandleTap(Vector2 screenPosition, bool placing)
        {
            // A tap puts the plant exactly where the finger points, on the best surface there.
            if (TryRaycast(screenPosition, out var pose, out var plane, out _))
            {
                PlaceAt(pose, plane);
                return;
            }
            if (placing && _hasReticlePose) PlaceAt(_reticlePose, _reticlePlane);
        }

        void PlaceAt(Pose pose, ARPlane plane = null)
        {
            if (twinRoot == null) return;
            bool first = !_everPlaced;
            _everPlaced = true;

            twinRoot.SetParent(null, true);
            if (_anchorGo != null) Destroy(_anchorGo);

            twinRoot.position = pose.position;
            twinRoot.rotation = YawFacingCamera(pose.position);
            if (first) twinRoot.localScale = Vector3.one * defaultScale;

            // Optional refinement only: the twin is visible whether or not the anchor works.
            _anchor = null;
            if (plane != null && _anchorManager != null)
                _anchor = _anchorManager.AttachAnchor(plane, pose);   // tied to the detected plane: it stays put as the plane is refined
            if (_anchor != null) _anchorGo = _anchor.gameObject;
            else
            {
                _anchorGo = new GameObject("PlantTwin Anchor");
                _anchorGo.transform.SetPositionAndRotation(pose.position, Quaternion.identity);
                _anchor = _anchorGo.AddComponent<ARAnchor>();
            }

            _placed = true;
            if (first) presenter?.Reveal(true);
            Log.Info(Log.Channel.AR, $"Plant twin placed at {pose.position} ({(plane != null ? "on plane" : "on reticle")})");
            Placed?.Invoke();
        }

        public void PlaceInFrontOfCamera()
        {
            if (arCamera == null) return;
            var cam = arCamera.transform;
            var forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) forward = cam.up;   // looking straight down
            // Stand it on the floor, about a metre below a phone held at chest height.
            var position = cam.position + forward.normalized * fallbackDistance + Vector3.down * 1.0f;
            Log.Warn(Log.Channel.AR, "Placing the twin on the floor in front of the user (chosen by the user).");
            PlaceAt(new Pose(position, Quaternion.identity));
        }

        public void RotateStep(float degrees)
        {
            if (_placed && twinRoot != null) twinRoot.Rotate(Vector3.up, degrees, Space.World);
        }

        public void CycleScale()
        {
            if (!_placed || twinRoot == null || scalePresets == null || scalePresets.Length == 0) return;
            float current = twinRoot.localScale.x;
            int next = 0;
            for (int i = 0; i < scalePresets.Length; i++)
                if (current < scalePresets[i] * 0.98f) { next = i; break; } else next = (i + 1) % scalePresets.Length;
            twinRoot.localScale = Vector3.one * scalePresets[next];
        }

        public void BeginReposition()
        {
            if (!_placed) return;
            _placed = false;            // the twin stays visible while the user aims at a new spot
            _placingSince = -1f;
            _surfaceSince = -1f;
        }

        Quaternion YawFacingCamera(Vector3 position)
        {
            var toCamera = arCamera.transform.position - position;
            toCamera.y = 0f;
            return toCamera.sqrMagnitude < 0.0001f ? Quaternion.identity
                : Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }

        bool _planesVisible = true;
        int _lastPlaneCount = -1;

        /// <summary>Detected surfaces are only drawn while the user is choosing where to place the twin.</summary>
        void SetPlanesVisible(bool visible)
        {
            if (planeManager == null) return;
            int count = planeManager.trackables.count;
            if (_planesVisible == visible && count == _lastPlaneCount) return;   // new planes must adopt the current state too
            _planesVisible = visible;
            _lastPlaneCount = count;
            foreach (var plane in planeManager.trackables)
                plane.gameObject.SetActive(visible);
        }

        /// <summary>
        /// True only when the finger is on something the user can press. Labels and panels that
        /// merely cover the screen must not swallow placement taps and drags.
        /// </summary>
        static bool IsOverUI(int fingerId)
        {
            if (EventSystem.current == null) return false;
            var data = new PointerEventData(EventSystem.current);
            for (int i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).fingerId == fingerId) data.position = Input.GetTouch(i).position;
            UiHits.Clear();
            EventSystem.current.RaycastAll(data, UiHits);
            foreach (var hit in UiHits)
                if (hit.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() != null) return true;
            return false;
        }
    }
}
#endif
