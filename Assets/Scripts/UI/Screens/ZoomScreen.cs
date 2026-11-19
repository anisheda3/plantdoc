using System.Collections.Generic;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>
    /// The magnified leaf: one carefully made leaf you can turn with a finger, with callouts that
    /// point at where this disease actually shows on it. The thumbnails switch the big picture
    /// between the generated 3D leaf, the photo that was analysed and the simulation map.
    /// The 3D leaf is a stand-in (never a photo of the user's leaf) and the screen says so.
    /// </summary>
    public class ZoomScreen : ScreenView
    {
        public override AppScreenId Id => AppScreenId.Zoom;

        [Header("Header")]
        [SerializeField] Button backButton;

        [Header("Main picture")]
        [SerializeField] RawImage leafImage;
        [SerializeField] RectTransform calloutLayer;
        [SerializeField] CalloutView[] callouts = new CalloutView[3];
        [SerializeField] CoverImage photoView;
        [SerializeField] TMP_Text photoBadge;

        [Header("Thumbnails")]
        [SerializeField] Button[] thumbButtons = new Button[3];
        [SerializeField] CoverImage[] thumbImages = new CoverImage[3];
        [SerializeField] Image[] thumbFrames = new Image[3];
        [SerializeField] Texture2D leafThumbnail;

        [Header("Text")]
        [SerializeField] TMP_Text tip;
        [SerializeField] Button nextButton;

        LeafSpecimen.LesionSpots _spots;
        Diseases.DiseaseData _spotsFor;
        AI.TraceResult _spotsTrace;
        int _view;

        protected override void OnBind()
        {
            Hook(backButton, () => Ctx.Router.Back());
            Hook(nextButton, () => Ctx.Flow.OpenJourney());
            for (int i = 0; i < thumbButtons.Length; i++)
            {
                int index = i;
                Hook(thumbButtons[i], () => SelectView(index));
            }
            if (leafImage != null && Ctx.Stage != null)
            {
                var input = leafImage.GetComponent<Viewport3DInput>();
                if (input != null) input.Viewport = Ctx.Stage.LeafView;
            }
        }

        /// <summary>The plant stays behind the leaf, faint, as if the leaf had been lifted out of it.</summary>
        public override Rect HeroRect(float canvasHeight) => new Rect(0.02f, 0.2f, 0.96f, 0.72f);

        Rect LeafRect(float canvasHeight)
        {
            float bottom = Mathf.Clamp01(SheetHeight / canvasHeight), top = Mathf.Clamp01(1f - HeroTopInset / canvasHeight);
            return new Rect(0.04f, bottom, 0.92f, Mathf.Max(0.2f, top - bottom));
        }

        protected override void OnShown()
        {
            SelectView(0);
            if (Ctx.Stage != null)
            {
                Ctx.Stage.LeafView.ResetPose();
                var canvasRect = ((RectTransform)transform).rect;
                Ctx.Stage.LeafView.SetFrame(LeafRect(canvasRect.height), true);
            }
        }

        protected override void OnHidden() => Ctx.Stage?.ShowLeaf(false);

        public override void Refresh()
        {
            var ctx = Ctx.Session;
            if (ctx == null || Theme == null) return;

            // The generated leaf shows the plant's current state (it follows the shared health binder).
            Ctx.Binder?.SetTargetState(ctx.CurrentState, immediate: true);

            thumbImages[0]?.Set(leafThumbnail);
            thumbImages[1]?.Set(ScanPhoto());
            thumbImages[2]?.Set(ctx.Disease?.simulationProfile != null ? ctx.Disease.simulationProfile.lesionMask : null);
            SelectView(_view);
        }

        Texture ScanPhoto()
        {
            var ctx = Ctx.Session;
            if (Ctx.Flow.LastScanImage != null) return Ctx.Flow.LastScanImage;
            var photos = ctx?.Disease?.referencePhotos;
            return photos != null && photos.Length > 0 ? photos[0] : null;
        }

        void SelectView(int index)
        {
            _view = Mathf.Clamp(index, 0, 2);
            bool leaf = _view == 0;
            SetActive(leafImage, leaf);
            SetActive(calloutLayer, leaf);
            SetActive(photoView.transform.parent.gameObject, !leaf);      // the rounded frame around the picture
            SetActive(photoBadge.transform.parent.gameObject, !leaf);     // and its caption pill
            Ctx.Stage?.ShowLeaf(leaf);
            if (leaf && Ctx.Stage != null) Ctx.Stage.LeafView.Bind(leafImage, 0.7f, 2);

            if (!leaf)
            {
                var tex = _view == 1 ? ScanPhoto() : Ctx.Session?.Disease?.simulationProfile?.lesionMask;
                photoView.Set(tex);
                SetText(photoBadge, _view == 1
                    ? (Ctx.Session != null && Ctx.Session.IsDemoMode ? "Reference photo · demo scan" : "Your scan")
                    : "Simulation map · where the generated leaf shows spots");
            }
            SetText(tip, _view == 0
                ? "This is a magnified 3D model of the affected leaf, generated to explain the disease. It is a stand-in, not a photo of your leaf."
                : _view == 1 ? "The photo that was analysed. Compare it with the generated leaf."
                : "The pattern used to draw the spots and yellowing on the generated leaf.");

            for (int i = 0; i < thumbFrames.Length; i++)
                if (thumbFrames[i] != null) thumbFrames[i].color = i == _view ? Theme.leaf : new Color(1f, 1f, 1f, 0.28f);
        }

        // ------------------------------------------------------------------
        // Callouts follow the real lesions on the generated leaf
        // ------------------------------------------------------------------

        protected override void OnTick()
        {
            if (Ctx == null || Theme == null || _view != 0) return;
            if (Ctx.Stage != null && leafImage != null && Time.frameCount % 20 == 0) Ctx.Stage.LeafView.Bind(leafImage, 0.7f, 2);
            UpdateCallouts();
        }

        void UpdateCallouts()
        {
            var ctx = Ctx.Session;
            var stage = Ctx.Stage;
            var disease = ctx?.Disease;
            bool show = ctx != null && !ctx.IsHealthy && disease?.simulationProfile != null && stage != null && stage.Leaf != null
                        && Ctx.Binder != null && Ctx.Binder.Displayed.lesionDensity > 0.18f && !stage.LeafView.Interacting;
            foreach (var c in callouts) if (c != null) c.SetVisible(show);
            if (!show) return;

            var trace = ctx.ScanTrace;
            if (_spots == null || _spotsFor != disease || _spotsTrace != trace)
            {
                _spots = trace != null
                    ? stage.Leaf.FindSpots(trace.HeroLesionMask, trace.HeroYellowMask)
                    : stage.Leaf.FindSpots(disease.simulationProfile.lesionMask, disease.simulationProfile.yellowingMask);
                _spotsFor = disease;
                _spotsTrace = trace;
            }

            var rect = calloutLayer.rect;
            Vector2 ToLocal(Vector2 maskUv)
            {
                var vp = stage.LeafView.ToViewport(stage.Leaf.WorldPointAtMask(maskUv));
                return new Vector2(rect.xMin + vp.x * rect.width, rect.yMin + vp.y * rect.height);
            }

            // Label cards stack on the right; the highest target gets the highest card so no leader lines cross.
            var hero = LeafRect(rect.height);
            Vector2 Card(float x, float y) => new Vector2(rect.xMin + x * rect.width, rect.yMin + (hero.yMin + y * hero.height) * rect.height);

            var targets = new List<(int index, Vector2 point, string label)>();
            if (_spots.LesionMaskUv.Count > 0) targets.Add((0, ToLocal(_spots.LesionMaskUv[0]), disease.lesionLabel));
            if (_spots.HaloMaskUv.HasValue) targets.Add((1, ToLocal(_spots.HaloMaskUv.Value), disease.haloLabel));
            if (_spots.AreaMaskUv.HasValue) targets.Add((2, ToLocal(_spots.AreaMaskUv.Value), disease.areaLabel));
            targets.Sort((a, b) => b.point.y.CompareTo(a.point.y));

            float[] cardY = { 0.9f, 0.62f, 0.34f };
            for (int i = 0; i < callouts.Length; i++)
            {
                bool used = i < targets.Count;
                if (callouts[i] == null) continue;
                callouts[i].SetVisible(used);
                if (used) callouts[i].Place(targets[i].point, Card(0.76f, cardY[Mathf.Min(i, cardY.Length - 1)]), targets[i].label);
            }
        }
    }
}
