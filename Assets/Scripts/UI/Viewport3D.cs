using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>
    /// A small orbit camera that renders one object (the plant twin or the hero leaf) into a
    /// transparent RenderTexture shown by a full-screen RawImage. Nothing moves on its own: the
    /// view only changes when the user drags to turn it or pinches to zoom, and it eases to a stop.
    ///
    /// The picture is always as big as the screen; <see cref="SetFrame"/> says which part of it the
    /// subject should fill, so the same plant can sit in the hero area of one page and glide to the
    /// hero area of the next. The camera is switched off whenever no screen is showing it.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class Viewport3D : MonoBehaviour
    {
        [Header("Subject")]
        [SerializeField] Transform target;
        [Tooltip("Centre of the subject, relative to the target.")]
        [SerializeField] Vector3 centreOffset = new Vector3(0f, 0.5f, 0f);
        [Tooltip("Radius of a sphere that contains the subject; the camera backs off to fit it.")]
        [SerializeField] float radius = 0.7f;
        [SerializeField] float margin = 1.08f;

        [Header("Pose")]
        [SerializeField] float fieldOfView = 26f;
        [SerializeField] float startYaw = 18f;
        [SerializeField] float startPitch = 8f;
        [SerializeField] Vector2 pitchRange = new Vector2(-6f, 38f);
        [Tooltip("Limit how far the view can be turned either way from its start (0 = free).")]
        [SerializeField] float yawLimit = 0f;
        [SerializeField] Vector2 zoomRange = new Vector2(0.6f, 1.8f);
        [Tooltip("Degrees of turn for a drag across the full width of the screen.")]
        [SerializeField] float degreesPerScreenWidth = 220f;
        [SerializeField] float damping = 5.5f;

        Camera _camera;
        RenderTexture _texture;
        float _yaw, _pitch, _yawVelocity, _pitchVelocity, _zoom = 1f, _wantZoom = 1f;
        float _wantYaw, _wantPitch;
        bool _resetting;
        bool _dragging;
        Rect _frame = new Rect(0f, 0f, 1f, 1f), _wantFrame = new Rect(0f, 0f, 1f, 1f);

        public Camera ViewCamera => _camera != null ? _camera : (_camera = GetComponent<Camera>());
        public RenderTexture Texture => _texture;
        public float Yaw => _yaw;

        /// <summary>True while the user is turning the view or it is still coasting (callouts hide meanwhile).</summary>
        public bool Interacting => _dragging || Mathf.Abs(_yawVelocity) > 6f || Mathf.Abs(_pitchVelocity) > 6f || _resetting;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.fieldOfView = fieldOfView;
            _camera.nearClipPlane = 0.02f;
            _camera.farClipPlane = 40f;
            _camera.allowHDR = false;
            _camera.enabled = false;
            ResetPose();
        }

        void OnDestroy() => ReleaseTexture();

        public void ResetPose()
        {
            _yaw = _wantYaw = startYaw; _pitch = _wantPitch = startPitch;
            _yawVelocity = _pitchVelocity = 0f; _zoom = _wantZoom = 1f; _resetting = false;
            Apply();
        }

        /// <summary>Eases back to the start view (double-tap).</summary>
        public void AnimateReset()
        {
            _wantYaw = startYaw + Mathf.Round((_yaw - startYaw) / 360f) * 360f;   // the short way round
            _wantPitch = startPitch; _wantZoom = 1f;
            _yawVelocity = _pitchVelocity = 0f;
            _resetting = true;
        }

        public void SetSubject(Transform subject, Vector3 offset, float subjectRadius)
        {
            target = subject; centreOffset = offset; radius = Mathf.Max(0.05f, subjectRadius);
            Apply();
        }

        /// <summary>Which part of the screen-sized picture the subject should fill (0..1, from the bottom left).</summary>
        public void SetFrame(Rect frame, bool immediate = false)
        {
            _wantFrame = frame;
            if (immediate) { _frame = frame; Apply(); }
        }

        public bool Active
        {
            get => _camera != null && _camera.enabled;
            set { if (ViewCamera != null) ViewCamera.enabled = value; }
        }

        /// <summary>Makes the render target match the RawImage's pixel size (called when the screen changes).</summary>
        public void Bind(RawImage image, float pixelScale = 1f, int antiAliasing = 4, Vector2? canvasSize = null)
        {
            if (image == null) return;
            var rect = canvasSize.HasValue ? new Rect(0f, 0f, canvasSize.Value.x, canvasSize.Value.y) : image.rectTransform.rect;
            if (rect.width < 8f || rect.height < 8f) return;   // not laid out yet; the next bind will do it
            var canvas = image.canvas;
            float scale = (canvas != null ? canvas.scaleFactor : 1f) * pixelScale;
            int w = Mathf.Clamp(Mathf.RoundToInt(rect.width * scale), 64, 2048);
            int h = Mathf.Clamp(Mathf.RoundToInt(rect.height * scale), 64, 2400);
            if (_texture == null || _texture.width != w || _texture.height != h)
            {
                ReleaseTexture();
                _texture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = antiAliasing, name = name + " RT" };
                _texture.Create();
                ViewCamera.targetTexture = _texture;
            }
            image.texture = _texture;
            image.color = new Color(1f, 1f, 1f, image.color.a > 0.001f ? image.color.a : 1f);
        }

        void ReleaseTexture()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_texture != null) { _texture.Release(); Destroy(_texture); _texture = null; }
        }

        // ------------------------------------------------------------------
        // Input (fed by Viewport3DInput on the RawImage)
        // ------------------------------------------------------------------

        public void BeginDrag() { _dragging = true; _resetting = false; _yawVelocity = _pitchVelocity = 0f; }
        public void EndDrag() => _dragging = false;

        public void Drag(Vector2 deltaPixels)
        {
            float perPixel = degreesPerScreenWidth / Mathf.Max(300f, Screen.width);
            float dYaw = -deltaPixels.x * perPixel;
            float dPitch = -deltaPixels.y * perPixel * 0.55f;
            _yaw = ClampYaw(_yaw + dYaw);
            _pitch = Mathf.Clamp(_pitch + dPitch, pitchRange.x, pitchRange.y);
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.008f);
            // Smooth the release velocity so one jittery frame cannot fling the view.
            _yawVelocity = Mathf.Lerp(_yawVelocity, dYaw / dt, 0.5f);
            _pitchVelocity = Mathf.Lerp(_pitchVelocity, dPitch / dt, 0.5f);
            _wantYaw = _yaw; _wantPitch = _pitch;
        }

        public void Zoom(float factor) => _wantZoom = Mathf.Clamp(_wantZoom * factor, zoomRange.x, zoomRange.y);

        static readonly float[] ZoomSteps = { 1f, 0.82f, 0.66f, 1.3f };
        int _zoomStep;

        /// <summary>Cycles through preset distances (the plant view's "scale" control).</summary>
        public void CycleZoom()
        {
            _zoomStep = (_zoomStep + 1) % ZoomSteps.Length;
            _wantZoom = Mathf.Clamp(ZoomSteps[_zoomStep], zoomRange.x, zoomRange.y);
        }

        /// <summary>Turns the view by a fixed amount (the AR "rotate" control, keyboard, tests).</summary>
        public void Nudge(float yawDegrees) { _yaw = ClampYaw(_yaw + yawDegrees); _yawVelocity = 0f; _resetting = false; }

        float ClampYaw(float yaw) => yawLimit > 0f ? Mathf.Clamp(yaw, startYaw - yawLimit, startYaw + yawLimit) : yaw;

        // ------------------------------------------------------------------

        void LateUpdate()
        {
            if (!Active) return;
            float dt = Time.unscaledDeltaTime;

            if (_resetting)
            {
                float k = 1f - Mathf.Exp(-9f * dt);
                _yaw = Mathf.Lerp(_yaw, _wantYaw, k);
                _pitch = Mathf.Lerp(_pitch, _wantPitch, k);
                if (Mathf.Abs(_yaw - _wantYaw) < 0.2f && Mathf.Abs(_pitch - _wantPitch) < 0.2f) { _yaw = _wantYaw; _pitch = _wantPitch; _resetting = false; }
            }
            else if (!_dragging)
            {
                // Coast to a stop after a flick; never self-start.
                _yaw = ClampYaw(_yaw + _yawVelocity * dt);
                _pitch = Mathf.Clamp(_pitch + _pitchVelocity * dt, pitchRange.x, pitchRange.y);
                float decay = 1f - Mathf.Exp(-damping * dt);
                _yawVelocity = Mathf.Lerp(_yawVelocity, 0f, decay);
                _pitchVelocity = Mathf.Lerp(_pitchVelocity, 0f, decay);
                if (Mathf.Abs(_yawVelocity) < 1.5f) _yawVelocity = 0f;
                if (Mathf.Abs(_pitchVelocity) < 1.5f) _pitchVelocity = 0f;
            }

            _zoom = Mathf.Lerp(_zoom, _wantZoom, 1f - Mathf.Exp(-10f * dt));
            float f = 1f - Mathf.Exp(-7f * dt);
            _frame = new Rect(Mathf.Lerp(_frame.x, _wantFrame.x, f), Mathf.Lerp(_frame.y, _wantFrame.y, f),
                              Mathf.Lerp(_frame.width, _wantFrame.width, f), Mathf.Lerp(_frame.height, _wantFrame.height, f));
            Apply();
        }

        void Apply()
        {
            if (target == null) return;
            var cam = ViewCamera;
            float aspect = _texture != null ? _texture.width / (float)_texture.height : Mathf.Max(0.2f, cam.aspect);
            float halfV = fieldOfView * 0.5f * Mathf.Deg2Rad;
            float frameH = Mathf.Clamp(_frame.height, 0.08f, 1f), frameW = Mathf.Clamp(_frame.width, 0.08f, 1f);
            // The subject has to fit the frame, not the whole picture.
            float visibleV = Mathf.Atan(Mathf.Tan(halfV) * frameH);
            float visibleH = Mathf.Atan(Mathf.Tan(halfV) * aspect * frameW);
            float distance = Mathf.Max(radius / Mathf.Sin(visibleV), radius / Mathf.Sin(Mathf.Max(visibleH, 0.05f))) * margin * _zoom;

            var centre = target.TransformPoint(centreOffset);
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            cam.transform.SetPositionAndRotation(centre - rotation * Vector3.forward * distance, rotation);
            cam.fieldOfView = fieldOfView;   // also clears any earlier custom projection

            // Slide the subject from the middle of the picture to the middle of its frame.
            var projection = Matrix4x4.Perspective(fieldOfView, aspect, cam.nearClipPlane, cam.farClipPlane);
            projection[0, 2] = -(_frame.center.x * 2f - 1f);
            projection[1, 2] = -(_frame.center.y * 2f - 1f);
            cam.projectionMatrix = projection;
        }

        /// <summary>Where a world point lands in the picture, 0..1 from the bottom-left (z &lt; 0 = behind the camera).</summary>
        public Vector3 ToViewport(Vector3 world) => ViewCamera.WorldToViewportPoint(world);

        /// <summary>Renders the current view once into another texture.</summary>
        public void CaptureTo(RenderTexture destination)
        {
            if (destination == null) return;
            var cam = ViewCamera;
            bool was = cam.enabled;
            var previous = cam.targetTexture;
            Apply();
            cam.targetTexture = destination;
            cam.Render();
            cam.targetTexture = previous;
            cam.enabled = was;
        }
    }
}
