using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI.Classic
{
    /// <summary>
    /// A labelled horizontal meter (severity, lesions, recovery...). The bar animates
    /// toward its value, so live simulation numbers read as motion rather than flicker.
    /// </summary>
    public class MeterView : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text value;
        [SerializeField] Image fill;
        [SerializeField] float sharpness = 9f;

        float _target;
        float _shown;

        public void Bind(string text, float value01, Color color, bool instant = false)
        {
            if (label != null) label.text = text;
            _target = Mathf.Clamp01(value01);
            if (instant) _shown = _target;
            if (fill != null) fill.color = color;
            Apply();
        }

        /// <summary>Updates only the value (used every frame for live meters).</summary>
        public void SetValue(float value01, Color color)
        {
            _target = Mathf.Clamp01(value01);
            if (fill != null) fill.color = color;
        }

        void Update()
        {
            if (Mathf.Abs(_shown - _target) < 0.0005f) return;
            _shown = Mathf.Lerp(_shown, _target, Ease.Damp(sharpness, Time.unscaledDeltaTime));
            Apply();
        }

        void Apply()
        {
            SetFill(fill, _shown);
            if (value != null) value.text = $"{Mathf.RoundToInt(_target * 100f)}%";
        }

        /// <summary>
        /// Filled images use fillAmount; sliced (rounded) bars grow by anchor instead,
        /// because a Filled image cannot keep 9-sliced rounded ends.
        /// </summary>
        public static void SetFill(Image bar, float value01)
        {
            if (bar == null) return;
            value01 = Mathf.Clamp01(value01);
            if (bar.type == Image.Type.Filled) { bar.fillAmount = value01; return; }
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
            rt.anchorMax = new Vector2(Mathf.Max(0.001f, value01), rt.anchorMax.y);
            bar.enabled = value01 > 0.004f;
        }
    }
}
