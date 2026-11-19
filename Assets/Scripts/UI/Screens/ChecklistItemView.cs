using System;
using System.Collections;
using System.Collections.Generic;
using PlantDoctor.AI;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using PlantDoctor.Treatment;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>One prevention measure: icon, title, detail and a tick the user can set.</summary>
    public class ChecklistItemView : MonoBehaviour
    {
        [SerializeField] Image iconTile;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text detail;
        [SerializeField] Image tick;
        [SerializeField] Button button;

        public static string IconFor(PreventionCategory c) => c switch
        {
            PreventionCategory.CropHygiene => "shield-check",
            PreventionCategory.WaterManagement => "droplets",
            PreventionCategory.AirCirculation => "wind",
            PreventionCategory.Monitoring => "search",
            PreventionCategory.EnvironmentalManagement => "sun",
            _ => "sprout"
        };

        public void Bind(UITheme theme, PreventionMeasure m, bool done, Action onToggle)
        {
            if (title != null) title.text = m.title;
            if (detail != null) detail.text = m.detail;
            if (icon != null) icon.sprite = theme.Icon(IconFor(m.category));
            if (iconTile != null) iconTile.color = theme.sageSoft;
            if (icon != null) icon.color = theme.botanical;
            if (tick != null)
            {
                tick.sprite = theme.Icon(done ? "circle-check" : "circle");
                tick.color = done ? theme.botanical : theme.textTertiary;
            }
            if (button != null) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => onToggle?.Invoke()); }
        }
    }
}
