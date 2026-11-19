using PlantDoctor.AI;
using PlantDoctor.AR;
using PlantDoctor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>One line of the analysis checklist.</summary>
    public class AnalysisStepRow : MonoBehaviour
    {
        public enum State { Pending, Active, Done }

        [SerializeField] GameObject doneIcon;
        [SerializeField] GameObject activeSpinner;
        [SerializeField] GameObject pendingIcon;
        [SerializeField] TMP_Text label;

        public State Current { get; private set; } = (State)(-1);

        public void Set(UITheme theme, State state)
        {
            if (state == Current) return;
            Current = state;
            if (doneIcon != null) doneIcon.SetActive(state == State.Done);
            if (activeSpinner != null) activeSpinner.SetActive(state == State.Active);
            if (pendingIcon != null) pendingIcon.SetActive(state == State.Pending);
            if (label != null)
            {
                label.color = state == State.Pending ? theme.textOnDarkMuted : theme.textOnDark;
                label.fontStyle = state == State.Active ? FontStyles.Bold : FontStyles.Normal;
            }
        }
    }
}
