using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>A small bottom snackbar for transient messages (never used for results).</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ToastView : MonoBehaviour
    {
        [SerializeField] TMP_Text message;
        [SerializeField] Image background;
        CanvasGroup _group;
        float _hideAt;

        void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
        }

        public void Show(UITheme theme, string text, Tone tone = Tone.Dark, float seconds = 3.2f)
        {
            if (message == null) return;
            var (back, fore) = tone == Tone.Dark
                ? (new Color(theme.forestDeep.r, theme.forestDeep.g, theme.forestDeep.b, 0.94f), theme.textOnDark)
                : StatusChipView.Colors(theme, tone);
            if (background != null) background.color = back;
            message.text = text;
            message.color = fore;
            gameObject.SetActive(true);
            _group.alpha = 1f;
            _hideAt = Time.unscaledTime + seconds;
        }

        void Update()
        {
            if (_group.alpha <= 0f) return;
            if (Time.unscaledTime > _hideAt) _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, Time.unscaledDeltaTime * 3f);
        }
    }
}
