using PlantDoctor.AR;
using PlantDoctor.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>
    /// Full-screen live camera preview for the non-AR camera mode (AR mode draws its own
    /// background). Uses the source's ViewportMapping in a tiny shader, so the preview
    /// is rotated, mirrored and aspect-filled exactly like the captured frame.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class CameraPreviewView : MonoBehaviour
    {
        [SerializeField] ARScanController scanController;
        [SerializeField] AppModeController modeController;
        [SerializeField] AppFlowController flow;
        [Tooltip("The classic interface keeps the camera behind the analysis step; the dashboard interface has its own page there.")]
        [SerializeField] bool showWhileAnalysing = true;

        static readonly int RowUId = Shader.PropertyToID("_RowU");
        static readonly int RowVId = Shader.PropertyToID("_RowV");

        RawImage _image;
        Material _material;

        void Awake()
        {
            _image = GetComponent<RawImage>();
            if (_image.material != null && _image.material.shader.name == "PlantDoctor/UI/CameraPreview")
            {
                _material = new Material(_image.material);   // per-instance copy; the asset stays untouched
                _image.material = _material;
            }
            _image.enabled = false;
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        void LateUpdate()
        {
            var preview = scanController != null ? scanController.Source as ICameraPreviewSource : null;
            bool scanning = flow != null && (flow.CurrentState == SimState.Scan ||
                                             flow.CurrentState == SimState.ScanRetry ||
                                             (showWhileAnalysing && flow.CurrentState == SimState.AiAnalysis));
            bool cameraMode = modeController == null || modeController.Mode == AppMode.Camera;
            var texture = preview?.PreviewTexture;

            bool show = cameraMode && scanning && texture != null;
            _image.enabled = show;
            if (!show) return;

            _image.texture = texture;
            if (_material != null)
            {
                var m = preview.PreviewMapping;
                _material.SetVector(RowUId, m.RowU);
                _material.SetVector(RowVId, m.RowV);
            }
        }
    }
}
