using System;
using UnityEngine;

namespace PlantDoctor.AR
{
    /// <summary>Why a camera source could not start. Drives user-facing recovery copy.</summary>
    public enum CameraFailure
    {
        None,
        PermissionDenied,
        NoCameraAvailable,
        NotSupportedOnDevice,
        InitializationFailed
    }

    /// <summary>
    /// Abstraction over "something that can give us a frame to analyze".
    /// Implemented by AR Foundation (device AR), WebCamTexture (phones without ARCore,
    /// and the Editor) and the VR virtual scanner. The scan workflow depends only on
    /// this, so the pipeline is identical in AR, VR and the Editor.
    /// </summary>
    public interface ICameraFrameSource
    {
        string DisplayName { get; }
        bool IsReady { get; }
        CameraFailure LastFailure { get; }

        /// <summary>Raised when a frame source becomes ready or drops out.</summary>
        event Action<bool> ReadyStateChanged;

        /// <summary>Starts the source. Returns false and sets LastFailure on error.</summary>
        bool TryStart();

        void StopSource();

        /// <summary>
        /// Latest frame as a CPU-readable texture, or null if unavailable.
        /// Screen-backed sources (AR camera, webcam preview) return the image exactly as
        /// the user sees it on screen: upright, screen aspect, (0,0) = bottom-left of the
        /// viewport. A normalised screen rect (the scan reticle) is therefore also a valid
        /// region of interest in this texture.
        /// The returned texture is owned by the source; callers must not destroy it.
        /// </summary>
        /// <param name="longEdge">Requested size of the longest edge; 0 = source default.</param>
        Texture2D AcquireFrame(int longEdge = 0);

        /// <summary>Normalised 0..1 device motion magnitude, used for the blur/motion gate.</summary>
        float CurrentMotion { get; }
    }

    /// <summary>
    /// Implemented by sources whose live preview must be drawn by the UI (the webcam).
    /// The AR camera does not implement it: AR Foundation renders its own background.
    /// </summary>
    public interface ICameraPreviewSource
    {
        /// <summary>The live texture, or null before the camera has started.</summary>
        Texture PreviewTexture { get; }

        /// <summary>Maps screen-viewport UV to <see cref="PreviewTexture"/> UV (rotation, mirroring, aspect-fill).</summary>
        ViewportMapping PreviewMapping { get; }
    }

    /// <summary>Maps a camera failure onto copy the user can act on.</summary>
    public static class CameraFailureText
    {
        public static string Describe(CameraFailure failure) => failure switch
        {
            CameraFailure.PermissionDenied =>
                "Camera permission was denied. Enable camera access in your device settings, or continue in Demo Mode.",
            CameraFailure.NoCameraAvailable =>
                "No camera was found on this device. You can still explore the simulation in Demo Mode.",
            CameraFailure.NotSupportedOnDevice =>
                "AR is not supported on this device. You can still explore the simulation in Demo Mode.",
            CameraFailure.InitializationFailed =>
                "The camera could not be started. Restart the app, or continue in Demo Mode.",
            _ => string.Empty
        };
    }
}
