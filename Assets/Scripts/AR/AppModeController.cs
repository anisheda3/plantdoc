using System;
using System.Collections;
using PlantDoctor.Plants;
using PlantDoctor.Utilities;
using UnityEngine;

namespace PlantDoctor.AR
{
    /// <summary>How the app presents the camera and the plant twin on this device.</summary>
    public enum AppMode
    {
        /// <summary>Still checking whether ARCore/ARKit is available.</summary>
        Resolving,
        /// <summary>Live AR: camera feed background, plant twin placed on a real surface.</summary>
        AR,
        /// <summary>No AR support: plain phone camera for scanning, twin shown in a 3D studio.</summary>
        Camera
    }

    /// <summary>Implemented by the AR Foundation availability check (compiled only with the package).</summary>
    public interface IARAvailability
    {
        IEnumerator CheckAvailability(Action<bool> done);
    }

    /// <summary>
    /// Decides at startup whether this device runs the full AR experience or the camera
    /// fallback, and switches the scene rigs accordingly. Everything else in the app
    /// reads <see cref="Mode"/> instead of checking for AR support itself.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class AppModeController : MonoBehaviour
    {
        [Header("Detection")]
        [Tooltip("Component implementing IARAvailability (ARAvailabilityProbe). Empty = never use AR.")]
        [SerializeField] MonoBehaviour arAvailabilityProbe;
        [Tooltip("The Editor has no ARCore; use the studio fallback there even if the probe exists.")]
        [SerializeField] bool forceCameraModeInEditor = true;

        [Header("Rigs")]
        [SerializeField] GameObject[] arOnlyObjects;
        [SerializeField] GameObject[] cameraModeOnlyObjects;

        [Header("Scanning")]
        [SerializeField] ARScanController scanController;
        [Tooltip("ARCameraFrameSource on the AR camera.")]
        [SerializeField] MonoBehaviour arFrameSource;
        [SerializeField] WebCamFrameSource webCamFrameSource;

        [Header("Presentation")]
        [Tooltip("The AR twin's presenter: placed in the world in AR mode, hidden otherwise (the viewer twin covers non-AR devices).")]
        [SerializeField] PlantTwinPresenter twinPresenter;
        [Tooltip("PlantTwinPlacer (AR only). Enabled in AR mode.")]
        [SerializeField] MonoBehaviour arPlacer;

        public AppMode Mode { get; private set; } = AppMode.Resolving;
        public bool IsAR => Mode == AppMode.AR;

        public event Action<AppMode> ModeResolved;

        IEnumerator Start()
        {
            bool arAvailable = false;
            var probe = arAvailabilityProbe as IARAvailability;

            bool skip = probe == null || (forceCameraModeInEditor && Application.isEditor);
            if (!skip)
                yield return probe.CheckAvailability(ok => arAvailable = ok);

            Apply(arAvailable ? AppMode.AR : AppMode.Camera);
        }

        void Apply(AppMode mode)
        {
            Mode = mode;
            bool ar = mode == AppMode.AR;

            SetActive(arOnlyObjects, ar);
            SetActive(cameraModeOnlyObjects, !ar);

            if (arPlacer != null) arPlacer.enabled = ar;
            if (twinPresenter != null) twinPresenter.SetPresentationMode(ar ? TwinPresentation.PlacedInWorld : TwinPresentation.Hidden);

            if (scanController != null)
            {
                ICameraFrameSource source = ar ? arFrameSource as ICameraFrameSource : webCamFrameSource;
                scanController.SetFrameSource(source);
            }

            Log.Info(Log.Channel.AR, $"App mode resolved: {mode}");
            ModeResolved?.Invoke(mode);
        }

        static void SetActive(GameObject[] objects, bool active)
        {
            if (objects == null) return;
            foreach (var go in objects) if (go != null) go.SetActive(active);
        }
    }
}
