using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlantDoctor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>One row of the recent-scans list (also used by My Plants).</summary>
    public class ScanRowView : MonoBehaviour
    {
        [SerializeField] RawImage photo;
        [SerializeField] CoverImage cover;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text subtitle;
        [SerializeField] TMP_Text time;
        [SerializeField] StatusChipView chip;
        [SerializeField] Button button;

        public void Bind(UITheme theme, ScanRecord record, Texture2D thumbnail, Action onClick)
        {
            if (cover != null) cover.Set(thumbnail);
            if (photo != null && thumbnail == null) photo.color = theme.sageSoft;
            if (title != null) title.text = record.plantName;
            if (subtitle != null) subtitle.text = record.isHealthy ? "Healthy" : record.diseaseName;
            if (time != null) time.text = UIText.Relative(record.LocalTime);
            if (chip != null)
            {
                if (record.isHealthy) chip.Bind(theme, "Healthy", Tone.Good, "circle-check");
                else chip.Bind(theme, UITheme.SeverityLabel(record.severity), record.severity >= 0.55f ? Tone.Alert : Tone.Warning,
                    record.severity >= 0.55f ? "triangle-alert" : "info");
            }
            if (button != null) { button.onClick.RemoveAllListeners(); if (onClick != null) button.onClick.AddListener(() => onClick()); }
        }
    }
}
