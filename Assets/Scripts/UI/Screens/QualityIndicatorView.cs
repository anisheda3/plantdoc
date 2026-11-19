using PlantDoctor.AI;
using PlantDoctor.AR;
using PlantDoctor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>One of the three live checks under the viewfinder: Light, Focus, Leaf.</summary>
    public class QualityIndicatorView : MonoBehaviour
    {
        public enum State { Idle, Good, Adjust }

        [SerializeField] Image ring;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text status;

        State _state = (State)(-1);
        string _text;

        public void Bind(UITheme theme, State state, string statusText)
        {
            if (state == _state && statusText == _text) return;
            _state = state; _text = statusText;
            var tone = state == State.Good ? theme.leaf : state == State.Adjust ? theme.amber : new Color(1f, 1f, 1f, 0.45f);
            if (ring != null) ring.color = tone;
            if (icon != null) icon.color = tone;
            if (status != null) { status.text = statusText; status.color = state == State.Idle ? theme.textOnDarkMuted : tone; }
            if (label != null) label.color = theme.textOnDark;
        }
    }
}
