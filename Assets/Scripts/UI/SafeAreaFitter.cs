using UnityEngine;

namespace PlantDoctor.UI
{
    /// <summary>Keeps a RectTransform inside the device safe area (notches, home indicators).</summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class SafeAreaFitter : MonoBehaviour
    {
        RectTransform _rect;
        Rect _lastSafeArea;
        ScreenOrientation _lastOrientation;

        void Awake() => _rect = GetComponent<RectTransform>();
        void OnEnable() => Apply();

        void Update()
        {
            if (Screen.safeArea != _lastSafeArea || Screen.orientation != _lastOrientation)
                Apply();
        }

        void Apply()
        {
            var safeArea = Screen.safeArea;
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width; anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width; anchorMax.y /= Screen.height;

            _rect.anchorMin = anchorMin;
            _rect.anchorMax = anchorMax;
            _lastSafeArea = safeArea;
            _lastOrientation = Screen.orientation;
        }
    }
}