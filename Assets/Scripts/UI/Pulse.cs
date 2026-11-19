using UnityEngine;

namespace PlantDoctor.UI
{
    /// <summary>Gentle scale "breathing" to draw attention to a primary control.</summary>
    public class Pulse : MonoBehaviour
    {
        [SerializeField] float amplitude = 0.04f;
        [SerializeField] float speed = 2.2f;
        void Update() => transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * speed) * amplitude);
        void OnDisable() => transform.localScale = Vector3.one;
    }
}
