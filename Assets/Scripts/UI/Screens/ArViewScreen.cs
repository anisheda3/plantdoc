using PlantDoctor.AR;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>
    /// The plant view: the 3D plant twin standing on a real surface in AR, or the same plant on a
    /// plain backdrop when AR is switched off or unavailable. Small controls rotate, resize and
    /// move it; four tabs open the magnified leaf, the disease spread, the treatment and the
    /// recovery. Overlays stay small so the camera view stays visible.
    /// </summary>
    public class ArViewScreen : ScreenView
    {
        public override AppScreenId Id => AppScreenId.ArView;

        [Header("Header")]
        [SerializeField] Button backButton;
        [SerializeField] Button[] modeButtons = new Button[2];       // 0 = AR View, 1 = Plant View
        [SerializeField] Image[] modeBackgrounds = new Image[2];
        [SerializeField] TMP_Text[] modeLabels = new TMP_Text[2];
        [SerializeField] TMP_Text arUnavailableNote;

        [Header("Highlights")]
        [SerializeField] RectTransform overlayRoot;
        [SerializeField] RectTransform conditionCard;
        [SerializeField] TMP_Text conditionTitle;
        [SerializeField] TMP_Text conditionSubtitle;
        [SerializeField] RectTransform[] markers = new RectTransform[3];

        [Header("Tools")]
        [SerializeField] Button rotateButton;
        [SerializeField] Button scaleButton;
        [SerializeField] Button moveButton;
        [SerializeField] CanvasGroup moveGroup;

        [Header("Tabs")]
        [SerializeField] Button[] tabButtons = new Button[2];        // zoom in, cure journey
        [SerializeField] Image[] tabIcons = new Image[2];
        [SerializeField] TMP_Text[] tabLabels = new TMP_Text[2];
        [SerializeField] GameObject[] tabNextDots = new GameObject[2];

        [Header("Placement help (AR only)")]
        [SerializeField] GameObject placementHint;
        [SerializeField] TMP_Text placementHintText;
        [SerializeField] Button placeNowButton;

        enum ViewMode { AR, Plant }
        ViewMode _mode = ViewMode.Plant;
        static readonly SimState[] TabStates = { SimState.FocusPart, SimState.TreatmentIntro };

        /// <summary>In AR the real camera is the backdrop; otherwise the plant backdrop is.</summary>
        public override bool BackdropVisible => !(Ctx != null && Ctx.IsAR && _mode == ViewMode.AR);

        protected override void OnBind()
        {
            Hook(backButton, () => Ctx.Router.Back());
            Hook(modeButtons[0], () => SetMode(ViewMode.AR));
            Hook(modeButtons[1], () => SetMode(ViewMode.Plant));
            Hook(rotateButton, Rotate);
            Hook(scaleButton, Scale);
            Hook(moveButton, Move);
            Hook(tabButtons[0], () => Ctx.Flow.OpenZoom());
            Hook(tabButtons[1], () => Ctx.Flow.OpenJourney());
            Hook(placeNowButton, () =>
            {
                var placement = Ctx.Placement;
                if (placement == null) return;
                if (placement.HasSurface) placement.EnsurePlaced();   // exactly where the ring is
                else placement.PlaceInFrontOfCamera();                 // explicit fallback
            });
        }

        protected override void OnShown()
        {
            bool ar = Ctx.IsAR;
            SetActive(arUnavailableNote, !ar);
            if (modeButtons[0] != null) modeButtons[0].interactable = ar;
            SetMode(ar ? ViewMode.AR : ViewMode.Plant);
            Paint();
        }

        void SetMode(ViewMode mode)
        {
            if (mode == ViewMode.AR && !Ctx.IsAR) mode = ViewMode.Plant;
            _mode = mode;
            bool plant = mode == ViewMode.Plant;
            Ctx.Router?.RefreshBackdrop();
            if (moveGroup != null) { moveGroup.alpha = plant ? 0.35f : 1f; moveGroup.interactable = !plant; }
            PaintMode();
        }

        void PaintMode()
        {
            if (Theme == null) return;
            for (int i = 0; i < modeButtons.Length; i++)
            {
                bool on = (int)_mode == i;
                if (modeBackgrounds[i] != null) modeBackgrounds[i].color = on ? Theme.forest : Color.clear;
                if (modeLabels[i] != null)
                {
                    bool disabled = i == 0 && !Ctx.IsAR;
                    modeLabels[i].color = disabled ? new Color(1f, 1f, 1f, 0.35f) : Theme.textOnDark;
                    modeLabels[i].fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                }
            }
        }

        public override void Refresh() => Paint();

        void Paint()
        {
            var ctx = Ctx.Session;
            if (ctx == null || Theme == null) return;

            SetText(conditionTitle, ctx.IsHealthy ? "Healthy" : (ctx.Disease != null ? ctx.Disease.diseaseName : "Condition"));
            SetText(conditionSubtitle, ctx.IsHealthy ? "No disease found" : "Affected leaf");

            // The next stage worth opening is hinted with a small dot; stages already opened lose it.
            int next = -1;
            for (int i = 0; i < TabStates.Length; i++)
                if (!Ctx.Visited.Contains(TabStates[i])) { next = i; break; }
            for (int i = 0; i < tabNextDots.Length; i++) SetActive(tabNextDots[i], i == next);

        }

        // ------------------------------------------------------------------
        // Tools
        // ------------------------------------------------------------------

        void Rotate()
        {
            if (_mode == ViewMode.AR) Ctx.Placement?.RotateStep(45f);
            else Ctx.Stage?.PlantView.Nudge(45f);
        }

        void Scale()
        {
            if (_mode == ViewMode.AR) Ctx.Placement?.CycleScale();
            else Ctx.Stage?.PlantView.CycleZoom();
        }

        void Move()
        {
            if (_mode == ViewMode.AR) Ctx.Placement?.BeginReposition();
        }

        // ------------------------------------------------------------------
        // Per-frame: picture size, highlight positions, placement help
        // ------------------------------------------------------------------

        protected override void OnTick()
        {
            if (Ctx == null || Theme == null) return;
            UpdateHighlights();
            UpdatePlacementHint();
        }

        void UpdateHighlights()
        {
            var builder = _mode == ViewMode.AR ? Ctx.ArTwin?.Builder : Ctx.Stage?.PlantBuilder;
            var ctx = Ctx.Session;
            bool diseased = ctx != null && !ctx.IsHealthy;
            bool show = builder != null && builder.Markers != null && builder.Markers.Count > 0 && diseased;

            bool arPlaced = _mode != ViewMode.AR || (Ctx.Placement != null && Ctx.Placement.IsPlaced);
            show &= arPlaced;

            SetActive(conditionCard, show);
            for (int i = 0; i < markers.Length; i++)
            {
                bool has = show && i < builder.Markers.Count;
                if (markers[i] != null) markers[i].gameObject.SetActive(false);
                if (!has) continue;
                if (TryScreenPoint(builder.Markers[i].position, out var local))
                {
                    markers[i].gameObject.SetActive(true);
                    markers[i].anchoredPosition = local;
                    if (i == 0 && conditionCard != null) PlaceCard(local);
                }
                else if (i == 0) SetActive(conditionCard, false);
            }
        }

        /// <summary>The condition label sits beside the first highlight, kept inside the screen.</summary>
        void PlaceCard(Vector2 markerLocal)
        {
            var parent = overlayRoot.rect;
            var size = conditionCard.rect.size;
            bool left = markerLocal.x > 0f;
            float x = left ? markerLocal.x - size.x * 0.5f - 90f : markerLocal.x + size.x * 0.5f + 90f;
            float y = markerLocal.y + 120f;
            x = Mathf.Clamp(x, parent.xMin + size.x * 0.5f + 24f, parent.xMax - size.x * 0.5f - 200f);   // clear of the tool column
            y = Mathf.Clamp(y, parent.yMin + 480f, parent.yMax - 330f);
            conditionCard.anchoredPosition = new Vector2(x, y);
        }

        bool TryScreenPoint(Vector3 world, out Vector2 local)
        {
            local = default;
            if (overlayRoot == null) return false;
            Vector2 screen;
            if (_mode == ViewMode.AR)
            {
                var cam = Camera.main;
                if (cam == null) return false;
                var p = cam.WorldToScreenPoint(world);
                if (p.z <= 0.05f) return false;
                screen = p;
            }
            else
            {
                var view = Ctx.Stage != null ? Ctx.Stage.PlantView : null;
                var plantImage = Ctx.Backdrop != null ? Ctx.Backdrop.PlantPicture : null;
                if (view == null || plantImage == null) return false;
                var v = view.ToViewport(world);
                if (v.z <= 0f || v.x < -0.05f || v.x > 1.05f || v.y < -0.05f || v.y > 1.05f) return false;
                // The picture fills the screen: its viewport coordinates are screen fractions.
                var rect = plantImage.rectTransform;
                var canvas = rect.GetComponentInParent<Canvas>();
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                var a = RectTransformUtility.WorldToScreenPoint(canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null, corners[0]);
                var b = RectTransformUtility.WorldToScreenPoint(canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null, corners[2]);
                screen = new Vector2(Mathf.Lerp(a.x, b.x, v.x), Mathf.Lerp(a.y, b.y, v.y));
            }
            var cv = overlayRoot.GetComponentInParent<Canvas>();
            var cam2 = cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay ? cv.worldCamera : null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot, screen, cam2, out local);
        }

        void UpdatePlacementHint()
        {
            var placement = Ctx.Placement;
            bool placing = _mode == ViewMode.AR && placement != null && placement.IsPlacing;
            SetActive(placementHint, placing);
            if (!placing) return;

            if (placement.SurfaceIsPlane) SetText(placementHintText, "Surface found. Hold still, or tap Place here.");
            else if (placement.HasSurface) SetText(placementHintText, "Still measuring this surface. Tap Place here when the ring looks right.");
            else if (placement.SearchSeconds < 6f) SetText(placementHintText, "Point the phone at the floor or a table and move it slowly side to side.");
            else SetText(placementHintText, "No surface found yet. Try more light or a textured floor, or place the plant on the floor in front of you.");

            bool offerFallback = !placement.HasSurface && placement.SearchSeconds >= 6f;
            SetActive(placeNowButton, placement.HasSurface || offerFallback);
            if (placeNowButton != null)
            {
                var label = placeNowButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null) SetText(label, placement.HasSurface ? "Place here" : "Place it in front of me");
            }
        }
    }
}
