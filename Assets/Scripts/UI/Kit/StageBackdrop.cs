using PlantDoctor.Plants;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>
    /// The one background of the whole app: a deep botanical gradient with a soft light, and the
    /// plant standing in it. It sits behind every page, so the plant is always there; each page
    /// only says which part of the screen is left for the plant (its hero area) and how visible
    /// the plant should be. The plant glides from one hero area to the next.
    /// It is hidden on the camera page and on the AR view, where the real camera is the backdrop.
    /// </summary>
    public class StageBackdrop : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Image gradient;
        [SerializeField] Image glow;
        [SerializeField] RawImage plantPicture;
        [SerializeField, Range(0.4f, 1f)] float renderScale = 0.7f;

        ViewerStage _stage;
        UITheme _theme;
        bool _visible;
        float _plantOpacity = 1f, _shownPlantOpacity;
        Rect _hero = new Rect(0.1f, 0.3f, 0.8f, 0.6f);
        Rect _shownHero = new Rect(0.1f, 0.3f, 0.8f, 0.6f);
        bool _first = true;
        ScreenView _page;
        float _heroForHeight;

        public bool Visible => _visible;
        public RawImage PlantPicture => plantPicture;

        public void Init(ViewerStage stage, UITheme theme)
        {
            _stage = stage; _theme = theme;
            if (gradient != null && theme != null) gradient.color = theme.backdropTop;
            if (_stage != null && plantPicture != null)
            {
                var input = plantPicture.GetComponent<Viewport3DInput>();
                if (input != null) input.Viewport = _stage.PlantView;
            }
            SetVisible(false);
        }

        /// <summary>Takes on what the page that just appeared needs.</summary>
        public void Apply(ScreenView page)
        {
            if (page == null) return;
            _page = page;
            _heroForHeight = 0f;   // recompute for the real canvas height as soon as it is known
            bool wanted = page.BackdropVisible;
            var canvasRect = ((RectTransform)transform).rect;
            if (canvasRect.height < 100f) { Canvas.ForceUpdateCanvases(); canvasRect = ((RectTransform)transform).rect; }
            _hero = page.HeroRect(canvasRect.height >= 100f ? canvasRect.height : 2340f);
            _plantOpacity = page.PlantOpacity;
            SetVisible(wanted);
            if (_first && group != null) group.alpha = wanted ? 1f : 0f;   // no fade-in on the very first page
            if (_stage?.PlantView != null) _stage.PlantView.SetFrame(_hero, _first || UITheme.ReducedMotion);
            if (wanted && _stage?.PlantView != null && plantPicture != null)
            {
                if (_first) Canvas.ForceUpdateCanvases();   // the picture needs its real size before its render texture is made
                _stage.PlantView.Bind(plantPicture, renderScale, 2);   // no empty first frames
            }
            _first = false;
        }

        /// <summary>
        /// For interfaces that are not made of ScreenView pages (the classic one): says directly whether the plant
        /// is wanted, how visible it is and where it should stand. Cheap to call every frame; only changes are applied.
        /// </summary>
        public void ApplyDirect(bool visible, float plantOpacity, Rect hero, bool immediate = false)
        {
            _page = null;
            bool changed = visible != _visible || !Mathf.Approximately(plantOpacity, _plantOpacity) || hero != _hero;
            if (!changed && !_first) return;
            _hero = hero;
            _plantOpacity = plantOpacity;
            SetVisible(visible);
            if (_first && group != null) group.alpha = visible ? 1f : 0f;
            if (_stage?.PlantView != null)
            {
                _stage.PlantView.SetFrame(hero, immediate || _first || UITheme.ReducedMotion);
                if (visible && plantPicture != null) _stage.PlantView.Bind(plantPicture, renderScale, 2);
            }
            _first = false;
        }

        void SetVisible(bool visible)
        {
            _visible = visible;
            if (group != null) group.blocksRaycasts = visible;
            gameObject.SetActive(true);
            if (group != null && !visible) group.alpha = 0f;
            _stage?.ShowPlant(visible && _plantOpacity > 0.01f);
        }

        void Update()
        {
            if (group == null) return;
            if (_page != null)
            {
                // The canvas is not laid out on the very first frames, and its height changes with the
                // screen shape: whenever it differs from what the hero was computed for, compute it again.
                float h = ((RectTransform)transform).rect.height;
                if (h >= 100f && !Mathf.Approximately(h, _heroForHeight))
                {
                    _heroForHeight = h;
                    _hero = _page.HeroRect(h);
                    if (_stage?.PlantView != null) _stage.PlantView.SetFrame(_hero, true);
                }
            }
            float goal = _visible ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, goal, Time.unscaledDeltaTime / 0.25f);

            if (!_visible) return;
            _stage?.ShowPlant(_plantOpacity > 0.01f);
            _shownPlantOpacity = Mathf.MoveTowards(_shownPlantOpacity, _plantOpacity, Time.unscaledDeltaTime / 0.35f);
            if (plantPicture != null)
            {
                var c = plantPicture.color; c.a = _shownPlantOpacity; plantPicture.color = c;
                plantPicture.raycastTarget = _plantOpacity > 0.5f;
                if (_stage?.PlantView != null && Time.frameCount % 20 == 0) _stage.PlantView.Bind(plantPicture, renderScale, 2);
            }

            // The light behind the plant follows the plant's hero area.
            _shownHero = new Rect(Mathf.Lerp(_shownHero.x, _hero.x, 0.12f), Mathf.Lerp(_shownHero.y, _hero.y, 0.12f),
                                  Mathf.Lerp(_shownHero.width, _hero.width, 0.12f), Mathf.Lerp(_shownHero.height, _hero.height, 0.12f));
            if (glow != null)
            {
                var rt = glow.rectTransform;
                var canvas = ((RectTransform)transform).rect;
                rt.anchorMin = rt.anchorMax = new Vector2(_shownHero.center.x, _shownHero.center.y);
                rt.anchoredPosition = Vector2.zero;
                float size = Mathf.Max(_shownHero.height * canvas.height, _shownHero.width * canvas.width) * 1.25f;
                rt.sizeDelta = new Vector2(size, size);
                var gc = glow.color; gc.a = 0.30f * Mathf.Clamp01(_shownPlantOpacity + 0.35f); glow.color = gc;
            }
        }
    }
}
