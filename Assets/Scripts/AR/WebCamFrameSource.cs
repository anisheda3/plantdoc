using System;
using System.Collections;
using PlantDoctor.Utilities;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace PlantDoctor.AR
{
    /// <summary>
    /// Plain camera source backed by WebCamTexture. Used on phones without ARCore support
    /// and in the Editor, so the whole scan workflow runs without AR.
    ///
    /// Handles the parts that make raw phone cameras awkward: the runtime permission
    /// prompt, picking the rear camera, and sensor rotation / mirroring. Frames are
    /// returned viewport-aligned (exactly what the preview shows), see ICameraFrameSource.
    /// </summary>
    public class WebCamFrameSource : MonoBehaviour, ICameraFrameSource, ICameraPreviewSource
    {
        [SerializeField] int requestedWidth = 1280;
        [SerializeField] int requestedHeight = 720;
        [SerializeField] int requestedFps = 30;
        [Tooltip("Prefer the rear camera on phones. The Editor uses the first device found.")]
        [SerializeField] bool preferRearCamera = true;
        [SerializeField] int defaultLongEdge = 720;

        WebCamTexture _webCam;
        Texture2D _buffer;
        Color32[] _pixelCache;
        Color32[] _outCache;
        bool _ready;
        bool _starting;

        public string DisplayName => "Phone camera";
        public bool IsReady => _ready && _webCam != null && _webCam.isPlaying && _webCam.width > 16;
        public CameraFailure LastFailure { get; private set; } = CameraFailure.None;
        public float CurrentMotion => 0f;   // no tracking data without AR; the blur gate covers shake

        public Texture PreviewTexture => _webCam != null && _webCam.isPlaying ? _webCam : null;

        public ViewportMapping PreviewMapping => _webCam == null
            ? ViewportMapping.Identity
            : ViewportMapping.ForRotatedTexture(_webCam.width, _webCam.height,
                _webCam.videoRotationAngle, _webCam.videoVerticallyMirrored,
                Screen.height > 0 ? Screen.width / (float)Screen.height : 9f / 16f);

        public event Action<bool> ReadyStateChanged;

        public bool TryStart()
        {
            if (IsReady || _starting) return true;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                // The OS dialog is asynchronous. Report "starting" now and finish once
                // the user answers, instead of failing on the very first call.
                _starting = true;
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => { _starting = false; StartDevice(); };
                callbacks.PermissionDenied += _ => { _starting = false; Fail(CameraFailure.PermissionDenied); };
                callbacks.PermissionDeniedAndDontAskAgain += _ => { _starting = false; Fail(CameraFailure.PermissionDenied); };
                Permission.RequestUserPermission(Permission.Camera, callbacks);
                return true;
            }
#endif
            if (Application.platform == RuntimePlatform.IPhonePlayer &&
                !Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                _starting = true;
                StartCoroutine(RequestIosAuthorization());
                return true;
            }

            return StartDevice();
        }

        IEnumerator RequestIosAuthorization()
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            _starting = false;
            if (Application.HasUserAuthorization(UserAuthorization.WebCam)) StartDevice();
            else Fail(CameraFailure.PermissionDenied);
        }

        bool StartDevice()
        {
            var devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
                return Fail(CameraFailure.NoCameraAvailable);

            string deviceName = devices[0].name;
            if (preferRearCamera)
                foreach (var d in devices)
                    if (!d.isFrontFacing) { deviceName = d.name; break; }

            try
            {
                if (_webCam != null) Destroy(_webCam);
                _webCam = new WebCamTexture(deviceName, requestedWidth, requestedHeight, requestedFps);
                _webCam.Play();
                _ready = true;
                LastFailure = CameraFailure.None;
                ReadyStateChanged?.Invoke(true);
                Log.Info(Log.Channel.AR, $"Camera started: {deviceName}");
                return true;
            }
            catch (Exception e)
            {
                Log.Error(Log.Channel.AR, $"Camera start failed: {e.Message}");
                return Fail(CameraFailure.InitializationFailed);
            }
        }

        bool Fail(CameraFailure failure)
        {
            LastFailure = failure;
            _ready = false;
            Log.Warn(Log.Channel.AR, $"Camera unavailable: {failure}");
            ReadyStateChanged?.Invoke(false);
            return false;
        }

        public void StopSource()
        {
            if (_webCam != null && _webCam.isPlaying) _webCam.Stop();
            _ready = false;
            ReadyStateChanged?.Invoke(false);
        }

        public Texture2D AcquireFrame(int longEdge = 0)
        {
            if (!IsReady) return null;

            int sw = _webCam.width, sh = _webCam.height;
            if (_pixelCache == null || _pixelCache.Length != sw * sh)
                _pixelCache = new Color32[sw * sh];
            _webCam.GetPixels32(_pixelCache);

            float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 9f / 16f;
            var size = ViewportMapping.OutputSize(aspect, longEdge > 0 ? longEdge : defaultLongEdge);

            if (_buffer == null || _buffer.width != size.x || _buffer.height != size.y)
            {
                if (_buffer != null) Destroy(_buffer);
                _buffer = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
                _outCache = new Color32[size.x * size.y];
            }

            PreviewMapping.Resample(_pixelCache, sw, sh, _outCache, size.x, size.y);
            _buffer.SetPixels32(_outCache);
            _buffer.Apply(false);
            return _buffer;
        }

        void OnDestroy()
        {
            StopSource();
            if (_webCam != null) Destroy(_webCam);
            if (_buffer != null) Destroy(_buffer);
        }
    }
}
