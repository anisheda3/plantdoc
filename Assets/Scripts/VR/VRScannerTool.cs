using System;
using PlantDoctor.AR;
using PlantDoctor.Utilities;
using UnityEngine;

namespace PlantDoctor.VR
{
    /// <summary>
    /// Virtual "scanner" the user holds in VR. Renders the inspected leaf to a
    /// RenderTexture and exposes it as an ICameraFrameSource, so VR reuses the exact
    /// same quality gate, preprocessing and inference path as AR.
    /// </summary>
    public class VRScannerTool : MonoBehaviour, ICameraFrameSource
    {
        [SerializeField] Camera scannerCamera;
        [SerializeField] int captureResolution = 512;

        RenderTexture _renderTexture;
        Texture2D _buffer;
        bool _ready;

        public string DisplayName => "VR Virtual Scanner";
        public bool IsReady => _ready && scannerCamera != null;
        public CameraFailure LastFailure { get; private set; } = CameraFailure.None;
        public float CurrentMotion => 0f;   // the virtual camera is stable by construction

        public event Action<bool> ReadyStateChanged;

        public bool TryStart()
        {
            if (scannerCamera == null)
            {
                LastFailure = CameraFailure.InitializationFailed;
                Log.Error(Log.Channel.VR, "VRScannerTool has no scanner camera assigned.");
                return false;
            }

            _renderTexture = new RenderTexture(captureResolution, captureResolution, 24);
            scannerCamera.targetTexture = _renderTexture;
            _buffer = new Texture2D(captureResolution, captureResolution, TextureFormat.RGB24, false);
            _ready = true;
            LastFailure = CameraFailure.None;
            ReadyStateChanged?.Invoke(true);
            Log.Info(Log.Channel.VR, "Virtual scanner ready.");
            return true;
        }

        public void StopSource()
        {
            _ready = false;
            if (scannerCamera != null) scannerCamera.targetTexture = null;
            ReadyStateChanged?.Invoke(false);
        }

        /// <summary>Square virtual capture; <paramref name="longEdge"/> is fixed by captureResolution.</summary>
        public Texture2D AcquireFrame(int longEdge = 0)
        {
            if (!IsReady) return null;

            scannerCamera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = _renderTexture;
            _buffer.ReadPixels(new Rect(0, 0, captureResolution, captureResolution), 0, 0);
            _buffer.Apply(false);
            RenderTexture.active = previous;
            return _buffer;
        }

        void OnDestroy()
        {
            StopSource();
            if (_renderTexture != null) _renderTexture.Release();
            // _buffer is allocated in TryStart(); releasing the RenderTexture
            // alone left the readback Texture2D alive.
            if (_buffer != null) { Destroy(_buffer); _buffer = null; }
        }
    }
}
