using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI.Classic
{
    /// <summary>A small rounded status chip: coloured dot + label (scan quality, demo badge, categories).</summary>
    public class ChipView : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] Image dot;
        [SerializeField] TMP_Text label;

        public void Bind(string text, Color tone, bool filled = false, ClassicTheme theme = null)
        {
            if (label != null)
            {
                label.text = text;
                label.color = filled && theme != null ? theme.textOnAccent : (theme != null ? theme.textPrimary : Color.white);
            }
            if (dot != null)
            {
                dot.gameObject.SetActive(!filled);
                dot.color = tone;
            }
            if (background != null)
                background.color = filled ? tone : new Color(tone.r, tone.g, tone.b, 0.16f);
        }
    }
}
