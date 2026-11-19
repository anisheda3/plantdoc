using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>A compact status chip: soft tinted pill, optional icon, label. Tone also changes the word, never only the colour.</summary>
    public class StatusChipView : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text label;

        public static (Color back, Color fore) Colors(UITheme t, Tone tone)
        {
            switch (tone)
            {
                case Tone.Good: return (t.sageSoft, t.leaf);
                case Tone.Warning: return (t.amberSoft, t.amber);
                case Tone.Alert: return (t.redSoft, t.red);
                case Tone.Info: return (new Color(0.55f, 0.75f, 0.95f, 0.10f), new Color(0.66f, 0.82f, 0.98f));
                case Tone.Demo: return (t.amberSoft, t.amber);
                case Tone.Dark: return (t.glassDark, t.textOnDark);
                default: return (t.ivoryDeep, t.textSecondary);
            }
        }

        public void Bind(UITheme theme, string text, Tone tone, string iconName = null)
        {
            var (back, fore) = Colors(theme, tone);
            if (background != null) background.color = back;
            if (label != null) { label.text = text; label.color = fore; }
            if (icon != null)
            {
                var sprite = string.IsNullOrEmpty(iconName) ? null : theme.Icon(iconName);
                icon.gameObject.SetActive(sprite != null);
                icon.sprite = sprite;
                icon.color = fore;
            }
        }
    }
}
