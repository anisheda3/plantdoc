using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>Pill / underline tab switcher (About | Symptoms | Impact, Lifecycle | Conditions, day tabs).</summary>
    public class SegmentedTabs : MonoBehaviour
    {
        public enum Style { Pills, Underline }

        [SerializeField] Style style = Style.Pills;
        [SerializeField] Button[] buttons = new Button[0];
        [SerializeField] Image[] backgrounds = new Image[0];
        [SerializeField] TMP_Text[] labels = new TMP_Text[0];
        [SerializeField] Image[] underlines = new Image[0];

        public event Action<int> Selected;
        public int Index { get; private set; }
        UITheme _theme;

        public void Init(UITheme theme, int startIndex = 0)
        {
            _theme = theme;
            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                buttons[i].onClick.RemoveAllListeners();
                buttons[i].onClick.AddListener(() => Select(index, true));
            }
            Select(startIndex, false);
        }

        public void SetLabel(int i, string text)
        {
            if (i >= 0 && i < labels.Length && labels[i] != null) labels[i].text = text;
        }

        public void SetInteractable(int i, bool on)
        {
            if (i >= 0 && i < buttons.Length) buttons[i].interactable = on;
        }

        public void Select(int index, bool notify)
        {
            if (buttons.Length == 0) return;
            Index = Mathf.Clamp(index, 0, buttons.Length - 1);
            Paint();
            if (notify) Selected?.Invoke(Index);
        }

        void Paint()
        {
            if (_theme == null) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                bool on = i == Index;
                if (style == Style.Pills)
                {
                    if (backgrounds[i] != null) backgrounds[i].color = on ? _theme.forest : Color.clear;
                    if (labels[i] != null) labels[i].color = on ? _theme.textOnDark : _theme.textSecondary;
                }
                else
                {
                    if (underlines.Length > i && underlines[i] != null) underlines[i].color = on ? _theme.botanical : Color.clear;
                    if (labels[i] != null) labels[i].color = on ? _theme.textPrimary : _theme.textTertiary;
                }
                if (labels[i] != null) labels[i].fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            }
        }
    }
}
