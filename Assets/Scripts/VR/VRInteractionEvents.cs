using System;
using UnityEngine;

namespace PlantDoctor.VR
{
    /// <summary>
    /// Thin, package-agnostic wrapper over VR interaction. Concrete XR Interaction
    /// Toolkit components forward into these hooks, so gameplay code never takes a
    /// hard dependency on XRI and the project compiles without the package.
    /// </summary>
    public class VRInteractionEvents : MonoBehaviour
    {
        public event Action<GameObject> Grabbed;
        public event Action<GameObject> Released;
        public event Action<GameObject> Hovered;
        public event Action<GameObject> Activated;

        /// <summary>Called by an XRGrabInteractable UnityEvent (or by tests).</summary>
        public void NotifyGrabbed(GameObject target) => Grabbed?.Invoke(target);
        public void NotifyReleased(GameObject target) => Released?.Invoke(target);
        public void NotifyHovered(GameObject target) => Hovered?.Invoke(target);
        public void NotifyActivated(GameObject target) => Activated?.Invoke(target);
    }
}
