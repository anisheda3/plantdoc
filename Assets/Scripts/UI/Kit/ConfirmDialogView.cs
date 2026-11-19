using System;
using System.Collections;
using PlantDoctor.AI;
using PlantDoctor.AR;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using PlantDoctor.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>A small confirmation dialog (stop an analysis, discard a report).</summary>
    public class ConfirmDialogView : MonoBehaviour
    {
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text message;
        [SerializeField] Button confirmButton;
        [SerializeField] TMP_Text confirmLabel;
        [SerializeField] Button cancelButton;
        [SerializeField] TMP_Text cancelLabel;
        [SerializeField] Button scrim;

        Action _onConfirm;
        public bool IsOpen => gameObject.activeSelf;

        public void Open(string t, string m, string confirmText, string cancelText, Action onConfirm)
        {
            if (title != null) title.text = t;
            if (message != null) message.text = m;
            if (confirmLabel != null) confirmLabel.text = confirmText;
            if (cancelLabel != null) cancelLabel.text = cancelText;
            _onConfirm = onConfirm;
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() => { var act = _onConfirm; Close(); act?.Invoke(); });
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(Close);
            if (scrim != null) { scrim.onClick.RemoveAllListeners(); scrim.onClick.AddListener(Close); }
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            _onConfirm = null;
            gameObject.SetActive(false);
        }
    }
}
