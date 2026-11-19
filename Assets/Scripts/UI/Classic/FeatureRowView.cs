using PlantDoctor.AI;
using PlantDoctor.Plants;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI.Classic
{
    /// <summary>
    /// One row of the OBSERVED vs OPTIMAL table: name, observed value, a bar with a marker
    /// at the healthy target, a status chip and the measurement source. Tappable for detail.
    /// </summary>
    public class FeatureRowView : MonoBehaviour
    {
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text observedLabel;
        [SerializeField] TMP_Text optimalLabel;
        [SerializeField] TMP_Text sourceLabel;
        [SerializeField] Image observedBar;
        [SerializeField] RectTransform optimalMarker;
        [SerializeField] Image statusPill;
        [SerializeField] TMP_Text statusText;
        [SerializeField] Button button;

        FeatureComparisonRow _row;

        public string FeatureId => _row?.FeatureId;

        public void Bind(FeatureComparisonRow row, ClassicTheme theme, System.Action<string> onSelected)
        {
            _row = row;
            if (row == null || theme == null) return;

            bool abnormal = row.Status == FeatureStatus.Abnormal;
            float gap = Mathf.Abs(row.Observed - row.Optimal);
            Color tone = !abnormal ? theme.accent : gap > 0.4f ? theme.danger : theme.warning;

            SetText(nameLabel, row.DisplayName);
            SetText(observedLabel, $"{row.Observed:P0}");
            if (observedLabel != null) observedLabel.color = tone;
            SetText(optimalLabel, $"Healthy {row.Optimal:P0}");
            SetText(sourceLabel, row.SourceLabel);

            // Bar length = observed value. Colour reflects how far off-target it is.
            if (observedBar != null)
            {
                MeterView.SetFill(observedBar, row.Observed);
                observedBar.color = tone;
            }

            // Marker sits at the healthy target so the gap is visible at a glance.
            if (optimalMarker != null)
            {
                optimalMarker.anchorMin = optimalMarker.anchorMax = new Vector2(Mathf.Clamp01(row.Optimal), 0.5f);
                optimalMarker.anchoredPosition = Vector2.zero;
            }

            if (statusPill != null) statusPill.color = new Color(tone.r, tone.g, tone.b, 0.18f);
            SetText(statusText, abnormal ? "Off target" : "Normal");
            if (statusText != null) statusText.color = tone;

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onSelected?.Invoke(row.FeatureId));
            }
        }

        static void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value;
        }
    }
}
