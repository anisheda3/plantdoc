using PlantDoctor.Core;
using PlantDoctor.Plants;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>A label with a thin leader line pointing at a spot on the leaf.</summary>
    public class CalloutView : MonoBehaviour
    {
        [SerializeField] RectTransform card;
        [SerializeField] TMP_Text text;
        [SerializeField] RectTransform dot;
        [SerializeField] RectTransform line;

        public void Place(Vector2 target, Vector2 cardPosition, string label)
        {
            if (text != null) text.text = label;
            card.anchoredPosition = cardPosition;
            dot.anchoredPosition = target;
            var delta = cardPosition - target;
            line.anchoredPosition = target;
            line.sizeDelta = new Vector2(delta.magnitude, line.sizeDelta.y);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        public void SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}
