using PlantDoctor.AI;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>Observed value against the healthy value, with a word and an icon for the status.</summary>
    public class FeatureBarRow : MonoBehaviour
    {
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text valueText;
        [SerializeField] Image fill;
        [SerializeField] RectTransform marker;
        [SerializeField] StatusChipView status;

        public void Bind(UITheme theme, FeatureComparisonRow row)
        {
            if (nameText != null) nameText.text = row.DisplayName;
            if (valueText != null) valueText.text = UIText.Percent(row.Observed);
            bool abnormal = row.Status == FeatureStatus.Abnormal;
            if (fill != null)
            {
                fill.color = abnormal ? theme.amber : theme.botanical;
                var rt = fill.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(Mathf.Max(0.02f, Mathf.Clamp01(row.Observed)), 1f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
            if (marker != null)
            {
                marker.anchorMin = new Vector2(Mathf.Clamp01(row.Optimal), 0f);
                marker.anchorMax = new Vector2(Mathf.Clamp01(row.Optimal), 1f);
                marker.anchoredPosition = Vector2.zero;
            }
            if (status != null)
            {
                if (abnormal) status.Bind(theme, "Abnormal", Tone.Warning, "triangle-alert");
                else status.Bind(theme, "Normal", Tone.Good, "circle-check");
            }
        }
    }
}
