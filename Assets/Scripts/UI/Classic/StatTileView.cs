using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI.Classic
{
    /// <summary>A report tile: big number, small caption, optional accent stripe.</summary>
    public class StatTileView : MonoBehaviour
    {
        [SerializeField] TMP_Text value;
        [SerializeField] TMP_Text caption;
        [SerializeField] Image accentBar;

        public void Bind(string valueText, string captionText, Color tone)
        {
            if (value != null) { value.text = valueText; value.color = tone; }
            if (caption != null) caption.text = captionText;
            if (accentBar != null) accentBar.color = tone;
        }
    }
}
