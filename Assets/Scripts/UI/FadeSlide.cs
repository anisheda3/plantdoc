using UnityEngine;

namespace PlantDoctor.UI
{
    /// <summary>
    /// Fades and slides a panel in and out. Hidden panels stop blocking raycasts, so a
    /// fading sheet never swallows taps meant for the AR view underneath.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class FadeSlide : MonoBehaviour
    {
        [SerializeField] Vector2 hiddenOffset = new Vector2(0f, -80f);
        [SerializeField] float duration = 0.32f;
        [SerializeField] bool startVisible = true;
        [Tooltip("Deactivate the GameObject once fully hidden.")]
        [SerializeField] bool deactivateWhenHidden = false;

        CanvasGroup _group;
        RectTransform _rect;
        Vector2 _restPosition;
        float _t;           // 0 hidden .. 1 shown
        bool _target;
        bool _initialised;

        public bool IsShown => _target;

        void Init()
        {
            if (_initialised) return;
            _initialised = true;
            _group = GetComponent<CanvasGroup>();
            _rect = transform as RectTransform;
            if (_rect != null) _restPosition = _rect.anchoredPosition;
            _target = startVisible;
            _t = startVisible ? 1f : 0f;
            Apply();
        }

        void Awake() => Init();

        public void Show(bool instant = false) => Set(true, instant);
        public void Hide(bool instant = false) => Set(false, instant);

        /// <summary>Parameterless hide for persistent UnityEvent wiring (e.g. a backdrop button).</summary>
        public void HideAnimated() => Set(false);

        public void Set(bool visible, bool instant = false)
        {
            Init();
            _target = visible;
            if (visible && !gameObject.activeSelf) gameObject.SetActive(true);
            if (instant) { _t = visible ? 1f : 0f; Apply(); }
        }

        /// <summary>Replays the entrance (used when content changes in place).</summary>
        public void Replay()
        {
            Init();
            _target = true;
            _t = Mathf.Min(_t, 0.35f);
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        void Update()
        {
            float goal = _target ? 1f : 0f;
            if (Mathf.Approximately(_t, goal)) return;
            _t = Mathf.MoveTowards(_t, goal, Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
            Apply();
            if (!_target && _t <= 0f && deactivateWhenHidden) gameObject.SetActive(false);
        }

        void Apply()
        {
            float e = Ease.OutCubic(_t);
            if (_group != null)
            {
                _group.alpha = e;
                _group.blocksRaycasts = _target;
                _group.interactable = _target;
            }
            if (_rect != null) _rect.anchoredPosition = _restPosition + hiddenOffset * (1f - e);
        }
    }
}
