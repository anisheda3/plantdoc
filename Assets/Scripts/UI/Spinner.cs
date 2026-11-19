using UnityEngine;

namespace PlantDoctor.UI
{
    /// <summary>Continuous rotation (loading spinners).</summary>
    public class Spinner : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = -280f;
        void Update() => transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
    }
}
