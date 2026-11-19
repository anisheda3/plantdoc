using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>Button press feedback: a quick, small shrink. Does nothing under reduced motion.</summary>
    public class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] float pressedScale = 0.97f;
        Vector3 _rest = Vector3.one;
        bool _down;

        void Awake() => _rest = transform.localScale;
        void OnDisable() { _down = false; transform.localScale = _rest; }

        public void OnPointerDown(PointerEventData e) => _down = true;
        public void OnPointerUp(PointerEventData e) => _down = false;
        public void OnPointerExit(PointerEventData e) => _down = false;

        void Update()
        {
            var want = _down && !UITheme.ReducedMotion ? _rest * pressedScale : _rest;
            if ((transform.localScale - want).sqrMagnitude < 1e-6f) return;
            transform.localScale = Vector3.Lerp(transform.localScale, want, Ease.Damp(28f, Time.unscaledDeltaTime));
        }
    }
}
