using UnityEngine;

namespace PlantDoctor.Utilities
{
    /// <summary>Channelled, configurable structured logging: [AI] Plant: Tomato</summary>
    public static class Log
    {
        public enum Channel { Core, AI, Sim, Treatment, UI, AR, VR }

        /// <summary>Bitmask of enabled channels; set from AppConfig at boot.</summary>
        public static int EnabledMask = ~0;
        public static bool Enabled = true;

        static bool IsOn(Channel c) => Enabled && (EnabledMask & (1 << (int)c)) != 0;

        public static void Info(Channel c, string message)
        {
            if (IsOn(c)) Debug.Log($"[{c.ToString().ToUpperInvariant()}] {message}");
        }

        public static void Warn(Channel c, string message)
        {
            if (IsOn(c)) Debug.LogWarning($"[{c.ToString().ToUpperInvariant()}] {message}");
        }

        public static void Error(Channel c, string message)
        {
            Debug.LogError($"[{c.ToString().ToUpperInvariant()}] {message}");
        }
    }
}
