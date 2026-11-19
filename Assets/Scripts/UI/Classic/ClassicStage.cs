using System.Collections.Generic;
using PlantDoctor.AR;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.UI.Classic
{
    /// <summary>
    /// The 3D side of the classic interface. The plant stands behind the translucent chrome and always
    /// sits in the free area above the bottom sheet (the area changes as the sheet grows, and the plant
    /// glides with it); nothing in it moves on its own. At the "Zoom in" step the magnified leaf takes that
    /// area, with labels that point at the real spots, and the plant fades back behind it.
    ///
    /// On phones with AR the real camera is the backdrop and the plant stands in the world instead
    /// (PlantTwinPlacer), so only the leaf is drawn here.
    /// </summary>
    public class ClassicStage : MonoBehaviour
    {
        [SerializeField] AppFlowController flow;
        [SerializeField] AppModeController modeController;
        [SerializeField] ViewerStage stage;
        [SerializeField] StageBackdrop backdrop;
        [SerializeField] PlantVisualBinder binder;

        [Header("Layout")]
        [SerializeField] RectTransform sheet;
        [SerializeField] RectTransform hud;

        [Header("Leaf (Zoom in)")]
        [SerializeField] RawImage leafPicture;
        [SerializeField] RectTransform calloutLayer;
        [SerializeField] CalloutView[] callouts = new CalloutView[3];

        LeafSpecimen.LesionSpots _spots;
        Diseases.DiseaseData _spotsFor;
        AI.TraceResult _spotsTrace;
        Rect _lastHero;
        bool _leafWasShown;
        // The leaf waits, invisible, until the sheet below it has finished sliding in, then fades in where it belongs: it never
        // glides or grows on its own (user, 10 Nov 2026: "the leaf moves by itself").
        Rect _leafFrame;
        int _leafStillFrames;
        float _leafFade;

        void Start()
        {
            backdrop?.Init(stage, null);
            if (leafPicture != null && stage != null)
            {
                var input = leafPicture.GetComponent<Viewport3DInput>();
                if (input != null) input.Viewport = stage.LeafView;
            }
        }

        void Update()
        {
            if (flow == null || stage == null) return;
            var state = flow.CurrentState;
            bool ar = modeController != null && modeController.IsAR;
            bool camera = state == SimState.Scan || state == SimState.ScanRetry || state == SimState.AiAnalysis;
            bool leaf = state == SimState.FocusPart;

            var hero = HeroRect();
            // The plant is drawn here only without AR (with AR it stands in the world) and never over the camera page.
            backdrop?.ApplyDirect(!ar && !camera && !leaf, 1f, hero);   // at the zoom step only the leaf is shown

            if (leaf != _leafWasShown)
            {
                _leafWasShown = leaf;
                if (leafPicture != null) leafPicture.gameObject.SetActive(leaf);
                if (calloutLayer != null) calloutLayer.gameObject.SetActive(leaf);
                stage.ShowLeaf(leaf);
                if (leaf)
                {
                    // A fruit, head or root disease is shown on that organ, not on a leaf.
                    var part = flow.Context?.Disease != null ? flow.Context.Disease.affectedPart : Diseases.AffectedPart.Leaf;
                    if (stage.Leaf != null && stage.Leaf.SetPart(part))
                        stage.LeafView.SetSubject(stage.Leaf.transform, Vector3.zero, stage.Leaf.FrameRadius);
                    _spots = null;
                    stage.LeafView.ResetPose();
                    _leafFrame = hero;
                    _leafStillFrames = 0;
                    _leafFade = 0f;
                    stage.LeafView.SetFrame(hero, true);
                    var ctx = flow.Context;
                    if (ctx != null) binder?.SetTargetState(ctx.CurrentState, immediate: true);
                }
            }
            if (!leaf) return;

            // Hold the leaf back until its frame stops changing, then show it in place.
            bool layoutMoving = Mathf.Abs(hero.yMin - _leafFrame.yMin) > 0.0015f || Mathf.Abs(hero.yMax - _leafFrame.yMax) > 0.0015f;
            _leafStillFrames = layoutMoving ? 0 : _leafStillFrames + 1;
            if (_leafFade <= 0f)
            {
                _leafFrame = hero;
                stage.LeafView.SetFrame(hero, true);
                if (_leafStillFrames >= 4) _leafFade = 0.0001f;
            }
            else
            {
                _leafFade = Mathf.Min(1f, _leafFade + Time.unscaledDeltaTime / 0.25f);
                // Once shown, the leaf only moves if the free area itself changes for good (the user resizes the sheet).
                if (Mathf.Abs(hero.yMin - _leafFrame.yMin) > 0.03f || Mathf.Abs(hero.yMax - _leafFrame.yMax) > 0.03f)
                {
                    _leafFrame = hero;
                    stage.LeafView.SetFrame(hero, true);
                }
            }
            // Bind first: it resets a fully transparent picture to opaque, which the fade then corrects in the same frame.
            if (leafPicture != null && Time.frameCount % 20 == 0) stage.LeafView.Bind(leafPicture, 0.7f, 2);
            if (leafPicture != null)
            {
                var c = leafPicture.color;
                leafPicture.color = new Color(c.r, c.g, c.b, Mathf.SmoothStep(0f, 1f, _leafFade));
            }
            UpdateCallouts(_leafFade >= 1f ? _leafFrame : hero, _leafFade >= 1f);
        }

        /// <summary>The free area above the bottom sheet and below the top bar, as a fraction of the screen.</summary>
        Rect HeroRect()
        {
            float top = 1f, bottom = 0f;
            var canvas = GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            float height = cam != null ? cam.pixelHeight : Screen.height;
            if (height < 10f) return new Rect(0.08f, 0.35f, 0.84f, 0.55f);

            if (sheet != null && sheet.gameObject.activeInHierarchy)
            {
                var corners = new Vector3[4];
                sheet.GetWorldCorners(corners);
                bottom = Mathf.Clamp01(RectTransformUtility.WorldToScreenPoint(cam, corners[1]).y / height);
            }
            if (hud != null && hud.gameObject.activeInHierarchy)
            {
                var corners = new Vector3[4];
                hud.GetWorldCorners(corners);
                top = Mathf.Clamp01(RectTransformUtility.WorldToScreenPoint(cam, corners[0]).y / height);
            }
            else top = 0.9f;

            bottom = Mathf.Max(bottom, 0.12f);
            if (top - bottom < 0.2f) top = Mathf.Min(1f, bottom + 0.2f);
            var wanted = new Rect(0.06f, bottom + 0.01f, 0.88f, top - bottom - 0.02f);
            // Follow the sheet smoothly instead of snapping with every small change.
            if (_lastHero.width <= 0f) _lastHero = wanted;
            _lastHero = new Rect(Mathf.Lerp(_lastHero.x, wanted.x, 0.25f), Mathf.Lerp(_lastHero.y, wanted.y, 0.25f),
                                 Mathf.Lerp(_lastHero.width, wanted.width, 0.25f), Mathf.Lerp(_lastHero.height, wanted.height, 0.25f));
            return _lastHero;
        }

        // ------------------------------------------------------------------
        // Labels that follow the real spots on the leaf
        // ------------------------------------------------------------------

        void UpdateCallouts(Rect hero, bool leafShown)
        {
            var ctx = flow.Context;
            var disease = ctx?.Disease;
            bool show = leafShown && ctx != null && !ctx.IsHealthy && disease?.simulationProfile != null && stage.Leaf != null && binder != null
                        && binder.Displayed.lesionDensity > 0.18f && !stage.LeafView.Interacting;
            foreach (var c in callouts) if (c != null) c.SetVisible(show);
            if (!show || calloutLayer == null) return;

            var trace = ctx.ScanTrace;
            if (_spots == null || _spotsFor != disease || _spotsTrace != trace)
            {
                // The labels point at the spots traced on the user's own leaf when there are some.
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
            Vector2 Card(float x, float y) => new Vector2(rect.xMin + x * rect.width, rect.yMin + (hero.yMin + y * hero.height) * rect.height);

            var targets = new List<(Vector2 point, string label)>();
            if (_spots.LesionMaskUv.Count > 0) targets.Add((ToLocal(_spots.LesionMaskUv[0]), disease.lesionLabel));
            if (_spots.HaloMaskUv.HasValue) targets.Add((ToLocal(_spots.HaloMaskUv.Value), disease.haloLabel));
            if (_spots.AreaMaskUv.HasValue) targets.Add((ToLocal(_spots.AreaMaskUv.Value), disease.areaLabel));
            targets.Sort((a, b) => b.point.y.CompareTo(a.point.y));   // the highest target gets the highest card: no crossing lines

            float[] cardY = { 0.88f, 0.58f, 0.28f };
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
