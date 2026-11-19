#if PLANTDOCTOR_AR_FOUNDATION
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;

namespace PlantDoctor.AR
{
    /// <summary>
    /// Matches the twin's lighting to the real room: brightness, colour, main light
    /// direction and ambient spherical harmonics from ARCore light estimation. A plant
    /// lit by a fixed studio light looks pasted on; this is what makes it sit in the scene.
    /// Every channel is optional — devices report what they can.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class ARLightEstimation : MonoBehaviour
    {
        [SerializeField] ARCameraManager cameraManager;
        [SerializeField] float baseIntensity = 1.1f;
        [SerializeField] float smoothing = 4f;

        Light _light;
        float _targetIntensity;
        Color _targetColor = Color.white;
        Quaternion _targetRotation;

        void Awake()
        {
            _light = GetComponent<Light>();
            _targetIntensity = _light.intensity;
            _targetColor = _light.color;
            _targetRotation = transform.rotation;
        }

        void OnEnable()
        {
            if (cameraManager == null) return;
            cameraManager.requestedLightEstimation =
                LightEstimation.AmbientIntensity | LightEstimation.AmbientColor |
                LightEstimation.MainLightDirection | LightEstimation.MainLightIntensity |
                LightEstimation.AmbientSphericalHarmonics;
            cameraManager.frameReceived += OnFrame;
        }

        void OnDisable()
        {
            if (cameraManager != null) cameraManager.frameReceived -= OnFrame;
        }

        void OnFrame(ARCameraFrameEventArgs args)
        {
            var le = args.lightEstimation;

            if (le.averageMainLightBrightness.HasValue)
                _targetIntensity = baseIntensity * Mathf.Clamp(le.averageMainLightBrightness.Value * 1.6f, 0.25f, 2.2f);
            else if (le.averageBrightness.HasValue)
                _targetIntensity = baseIntensity * Mathf.Clamp(le.averageBrightness.Value * 2f, 0.25f, 2.2f);

            if (le.mainLightColor.HasValue) _targetColor = le.mainLightColor.Value;
            else if (le.colorCorrection.HasValue) _targetColor = le.colorCorrection.Value;

            if (le.mainLightDirection.HasValue && le.mainLightDirection.Value.sqrMagnitude > 0.0001f)
                _targetRotation = Quaternion.LookRotation(le.mainLightDirection.Value);

            if (le.ambientSphericalHarmonics.HasValue)
            {
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientProbe = le.ambientSphericalHarmonics.Value;
            }
        }

        void Update()
        {
            float t = Mathf.Clamp01(Time.deltaTime * smoothing);
            _light.intensity = Mathf.Lerp(_light.intensity, _targetIntensity, t);
            _light.color = Color.Lerp(_light.color, _targetColor, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, t);
        }
    }
}
#endif
