#if PLANTDOCTOR_AR_FOUNDATION
using System;
using System.Collections;
using PlantDoctor.Utilities;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace PlantDoctor.AR
{
    /// <summary>
    /// Asks AR Foundation whether this device supports AR, and offers to install
    /// Google Play Services for AR when it is missing but installable.
    /// </summary>
    public class ARAvailabilityProbe : MonoBehaviour, IARAvailability
    {
        [SerializeField] float timeoutSeconds = 12f;

        public IEnumerator CheckAvailability(Action<bool> done)
        {
            float started = Time.realtimeSinceStartup;

            if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
                yield return ARSession.CheckAvailability();

            if (ARSession.state == ARSessionState.NeedsInstall)
            {
                Log.Info(Log.Channel.AR, "AR services need installing; prompting the user.");
                yield return ARSession.Install();
            }

            // Install can leave the session briefly in Installing; wait it out, bounded.
            while (ARSession.state == ARSessionState.Installing &&
                   Time.realtimeSinceStartup - started < timeoutSeconds)
                yield return null;

            var state = ARSession.state;
            bool supported = state == ARSessionState.Ready ||
                             state == ARSessionState.SessionInitializing ||
                             state == ARSessionState.SessionTracking;

            Log.Info(Log.Channel.AR, $"AR availability: {state} -> {(supported ? "AR mode" : "camera mode")}");
            done?.Invoke(supported);
        }
    }
}
#endif
