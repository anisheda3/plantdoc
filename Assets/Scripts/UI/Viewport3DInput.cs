using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>
    /// Forwards drags, pinches, the mouse wheel and a double tap on a RawImage to a
    /// <see cref="Viewport3D"/>. A pinch zooms and never also turns the view.
    /// </summary>
    public class Viewport3DInput : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, IPointerClickHandler
    {
        [SerializeField] Viewport3D viewport;
        float _pinchStart;

        public Viewport3D Viewport { get => viewport; set => viewport = value; }

        public void OnBeginDrag(PointerEventData e) { if (viewport != null) viewport.BeginDrag(); }
        public void OnEndDrag(PointerEventData e) { if (viewport != null) viewport.EndDrag(); }

        public void OnDrag(PointerEventData e)
        {
            if (viewport == null) return;
            if (Input.touchCount >= 2) return;
            viewport.Drag(e.delta);
        }

        public void OnScroll(PointerEventData e)
        {
            if (viewport != null) viewport.Zoom(1f - e.scrollDelta.y * 0.06f);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (viewport != null && e.clickCount >= 2 && !e.dragging) viewport.AnimateReset();
        }

        void Update()
        {
            if (viewport == null || Input.touchCount != 2) { _pinchStart = 0f; return; }
            var a = Input.GetTouch(0); var b = Input.GetTouch(1);
            float d = Vector2.Distance(a.position, b.position);
            if (_pinchStart > 0f) viewport.Zoom(_pinchStart / Mathf.Max(d, 1f));
            _pinchStart = d;
        }
    }
}
