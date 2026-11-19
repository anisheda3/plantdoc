using System;
using PlantDoctor.Core;
using PlantDoctor.Treatment;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>One numbered treatment step: title, explanation, photo, safety note and its action.</summary>
    public class StepCardView : MonoBehaviour
    {
        public enum State { Done, Current, Waiting }

        [SerializeField] Image numberCircle;
        [SerializeField] TMP_Text numberText;
        [SerializeField] Image doneIcon;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text body;
        [SerializeField] CoverImage photo;
        [SerializeField] GameObject photoFrame;
        [SerializeField] GameObject safetyBox;
        [SerializeField] TMP_Text safetyText;
        [SerializeField] Button actionButton;
        [SerializeField] Image actionBackground;
        [SerializeField] TMP_Text actionLabel;
        [SerializeField] StatusChipView statusChip;

        public void Bind(UITheme theme, int index, TreatmentStep step, State state, Action onApply)
        {
            if (numberText != null) { numberText.text = (index + 1).ToString(); numberText.gameObject.SetActive(state != State.Done); }
            if (doneIcon != null) doneIcon.gameObject.SetActive(state == State.Done);
            if (numberCircle != null) numberCircle.color = state == State.Waiting ? theme.ivoryDeep : theme.forest;
            if (numberText != null) numberText.color = state == State.Waiting ? theme.textSecondary : theme.textOnDark;
            if (title != null) title.text = step.title;
            if (body != null) body.text = step.instruction;

            bool hasPhoto = step.illustration != null;
            if (photoFrame != null) photoFrame.SetActive(hasPhoto);
            if (hasPhoto && photo != null) photo.Set(step.illustration);

            string safety = SafetyText(step);
            if (safetyBox != null) safetyBox.SetActive(!string.IsNullOrEmpty(safety));
            if (safetyText != null) safetyText.text = safety;

            bool current = state == State.Current;
            if (actionButton != null)
            {
                actionButton.gameObject.SetActive(state != State.Done);
                actionButton.interactable = current;
                actionButton.onClick.RemoveAllListeners();
                if (current && onApply != null) actionButton.onClick.AddListener(() => onApply());
            }
            if (actionBackground != null) actionBackground.color = current ? theme.forest : theme.ivoryDeep;
            if (actionLabel != null)
            {
                actionLabel.text = current ? "Mark as done" : "Do the earlier step first";
                actionLabel.color = current ? theme.textOnDark : theme.textTertiary;
            }
            if (statusChip != null)
            {
                statusChip.gameObject.SetActive(state == State.Done);
                if (state == State.Done) statusChip.Bind(theme, "Marked as done", Tone.Good, "circle-check");
            }
        }

        /// <summary>Label-following guidance only. No dose is ever invented: the product's own label decides.</summary>
        public static string SafetyText(TreatmentStep step)
        {
            if (step == null) return "";
            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(step.safetyNote)) parts.Add(step.safetyNote.Trim());
            var chem = step.chemicalInfo;
            if (chem != null && chem.involvesChemicalProduct)
            {
                parts.Add("Use a product registered for this crop where you live, and follow its label for the dose, protective equipment and waiting times.");
                if (!string.IsNullOrWhiteSpace(chem.safetyNotes) && !chem.safetyNotes.Contains("TODO")) parts.Add(chem.safetyNotes.Trim());
            }
            return string.Join("\n", parts);
        }
    }
}
