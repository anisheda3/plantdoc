// Compiled only when AR Foundation is present. The scripting define
// PLANTDOCTOR_AR_FOUNDATION is set by the versionDefines in PlantDoctor.Runtime.asmdef
// whenever com.unity.xr.arfoundation is installed. Without the package the project
// still builds — WebCamFrameSource and Demo Mode keep the whole pipeline runnable.
#if PLANTDOCTOR_AR_FOUNDATION
using System;
using PlantDoctor.Utilities;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace PlantDoctor.AR
{
    /// <summary>
    /// Device AR frame source.
    ///
    /// Frames are captured by blitting the GPU camera texture through the very material
    /// ARCameraBackground uses to draw the screen (the approach documented by AR
    /// Foundation for GPU camera access). The capture is therefore exactly what the user
    /// sees — upright, cropped to the screen — with no assumptions about sensor
    /// orientation or CPU-image conventions, so the reticle rect is the region of interest.
    ///
    /// Device motion is estimated from camera pose deltas so the quality gate can
    /// reject captures taken while the phone is still moving.
    /// </summary>
    [RequireComponent(typeof(ARCameraManager))]
    public class ARCameraFrameSource : MonoBehaviour, ICameraFrameSource
    {
        [SerializeField] ARCameraManager cameraManager;
        [SerializeField] ARCameraBackground cameraBackground;
        [Tooltip("Longest edge of a full capture. Smaller = faster and cooler on mobile.")]
        [SerializeField] int defaultLongEdge = 720;
        [Tooltip("How quickly the motion estimate follows the device (higher = more responsive).")]
        [SerializeField] float motionSmoothing = 6f;
        [Tooltip("Camera speed in metres/second treated as full motion (1.0).")]
        [SerializeField] float fullMotionSpeed = 0.6f;

        RenderTexture _target;
        Texture2D _buffer;
        bool _running;
        bool _hasFrame;
        Vector3 _lastPosition;
        Quaternion _lastRotation;
        float _motion;

        public string DisplayName => "AR camera";
        public bool IsReady => _running && _hasFrame && cameraManager != null && cameraManager.enabled;
        public CameraFailure LastFailure { get; private set; } = CameraFailure.None;
        public float CurrentMotion => Mathf.Clamp01(_motion);

        public event Action<bool> ReadyStateChanged;

        void Awake()
        {
            if (cameraManager == null) cameraManager = GetComponent<ARCameraManager>();
            if (cameraBackground == null) cameraBackground = GetComponent<ARCameraBackground>();
        }

        /// <summary>AR availability is resolved by ARAvailabilityProbe before this is called.</summary>
        public bool TryStart()
        {
            if (_running) return true;
            if (cameraManager == null || cameraBackground == null)
            {
                LastFailure = CameraFailure.InitializationFailed;
                Log.Error(Log.Channel.AR, "ARCameraFrameSource needs ARCameraManager and ARCameraBackground on the AR camera.");
                return false;
            }

            cameraManager.frameReceived += OnFrameReceived;
            _running = true;
            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
            LastFailure = CameraFailure.None;
            Log.Info(Log.Channel.AR, "AR camera source started.");
            return true;
        }

        public void StopSource()
        {
            if (cameraManager != null) cameraManager.frameReceived -= OnFrameReceived;
            bool wasReady = IsReady;
            _running = false;
            _hasFrame = false;
            if (wasReady) ReadyStateChanged?.Invoke(false);
        }

        void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            if (!_hasFrame)
            {
                _hasFrame = true;
                ReadyStateChanged?.Invoke(true);
            }

            // Combine translation speed and angular speed into one 0..1 shake estimate.
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float speed = Vector3.Distance(transform.position, _lastPosition) / dt;
            float angular = Quaternion.Angle(transform.rotation, _lastRotation) / dt;   // deg/s
            float instant = Mathf.Clamp01(speed / Mathf.Max(0.01f, fullMotionSpeed) + angular / 120f);
            _motion = Mathf.Lerp(_motion, instant, Mathf.Clamp01(dt * motionSmoothing));
            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
        }

        public Texture2D AcquireFrame(int longEdge = 0)
        {
            if (!IsReady) return null;
            var material = cameraBackground.material;
            if (material == null)
            {
                Log.Warn(Log.Channel.AR, "AR background material not ready yet.");
                return null;
            }

            float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 9f / 16f;
            var size = ViewportMapping.OutputSize(aspect, longEdge > 0 ? longEdge : defaultLongEdge);

            try
            {
                if (_target == null || _target.width != size.x || _target.height != size.y)
                {
                    if (_target != null) { _target.Release(); Destroy(_target); }
                    _target = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.ARGB32) { name = "PlantDoctor AR Capture" };
                }
                if (_buffer == null || _buffer.width != size.x || _buffer.height != size.y)
                {
                    if (_buffer != null) Destroy(_buffer);
                    _buffer = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
                }

                Graphics.Blit(null, _target, material);

                var previous = RenderTexture.active;
                RenderTexture.active = _target;
                _buffer.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0, false);
                _buffer.Apply(false);
                RenderTexture.active = previous;
                return _buffer;
            }
            catch (Exception e)
            {
                Log.Error(Log.Channel.AR, $"Camera capture failed: {e.Message}");
                return null;
            }
        }

        void OnDestroy()
        {
            StopSource();
            if (_target != null) { _target.Release(); Destroy(_target); _target = null; }
            if (_buffer != null) { Destroy(_buffer); _buffer = null; }
        }
    }
}
#endif
