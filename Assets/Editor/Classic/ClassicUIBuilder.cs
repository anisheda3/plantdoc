#if UNITY_EDITOR
using PlantDoctor.AR;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using PlantDoctor.UI;
using PlantDoctor.UI.Classic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// Builds the production Canvas UI from code, fully wired. Everything is generated so
    /// there are no prefab merge issues and the layout can be regenerated after any change.
    ///
    /// Layout (portrait, 1080-wide reference):
    ///   camera preview (camera mode) · bottom legibility scrim
    ///   safe area:
    ///     floating top bar (brand, stage, step segments, AI badge, settings)
    ///     scanner (reticle, quality chips, hint, shutter)
    ///     AR placement hint · error toast
    ///     bottom sheet (eyebrow, title, chips, scan thumbnail, meters, rows, tiles, body, caption, actions)
    ///   modals: feature detail, settings
    /// </summary>
    public static class ClassicUIBuilder
    {
        public const string ThemePath = "Assets/Data/Config/ClassicTheme.asset";
        const string PreviewMaterialPath = "Assets/Materials/M_UICameraPreview.mat";

        public class Context
        {
            public AppFlowController Flow;
            public DemoModeController Demo;
            public AppModeController Mode;
            public ARScanController Scan;
            public MonoBehaviour TwinPlacement;
            public PlantVisualBinder Binder;
            public ViewerStage Stage;
        }

        static ClassicTheme _t;

        [MenuItem("Tools/AI Plant Doctor/Classic UI/Build UI (current scene)")]
        public static void BuildUIMenu()
        {
            var flow = Object.FindObjectOfType<AppFlowController>();
            if (flow == null)
            {
                EditorUtility.DisplayDialog("No scene systems found",
                    "Open the PlantDoctor scene (or run 'Build App Scene') first.", "OK");
                return;
            }
            Build(new Context
            {
                Flow = flow,
                Demo = Object.FindObjectOfType<DemoModeController>(),
                Mode = Object.FindObjectOfType<AppModeController>(),
                Scan = Object.FindObjectOfType<ARScanController>(),
                Binder = Object.FindObjectOfType<PlantVisualBinder>(),
            });
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }

        public static GameObject Build(Context c)
        {
            _t = LoadOrCreateTheme();

            var existing = Object.FindObjectOfType<MainScreenView>();
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null) CreateEventSystem();

            // ---------- Canvas ----------
            var canvasGo = new GameObject("UI_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;        // match width: tall phones get more vertical room, not smaller text
            scaler.referencePixelsPerUnit = 100f;
            var root = canvasGo.transform;
            var screen = canvasGo.AddComponent<MainScreenView>();

            // ---------- Camera preview (camera mode only) ----------
            var preview = new GameObject("CameraPreview", typeof(RectTransform), typeof(RawImage));
            preview.transform.SetParent(root, false);
            Stretch(preview.transform);
            var raw = preview.GetComponent<RawImage>();
            raw.material = PreviewMaterial();
            raw.color = Color.white;
            raw.raycastTarget = false;
            var previewView = preview.AddComponent<CameraPreviewView>();

            // ---------- The plant stage: the plant (and, at the zoom step, the leaf) behind the chrome ----------
            var backdrop = BuildBackdrop(root);
            backdrop.transform.SetAsFirstSibling();
            BuildLeafLayer(root, out var leafPicture, out var calloutLayer, out var callouts);

            // ---------- Legibility scrims ----------
            var bottomScrim = Img(root, "BottomScrim", new Color(_t.scrim.r, _t.scrim.g, _t.scrim.b, 0.85f), _t.gradientSprite);
            bottomScrim.rectTransform.localScale = new Vector3(1f, -1f, 1f);          // dark at the bottom
            Place(bottomScrim.rectTransform, new Vector2(0, 0), new Vector2(1, 0.5f), Vector2.zero, Vector2.zero);
            bottomScrim.raycastTarget = false;
            var topScrim = Img(root, "TopScrim", new Color(_t.scrim.r, _t.scrim.g, _t.scrim.b, 0.6f), _t.gradientSprite);
            Place(topScrim.rectTransform, new Vector2(0, 0.82f), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            topScrim.raycastTarget = false;

            // ---------- Safe area ----------
            var safe = NewRect("SafeArea", root);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var hud = BuildHud(safe, c, out var settingsSlot);
            var scanner = BuildScanner(safe, c);
            var (hintGroup, hintText) = BuildPlacementHint(safe);
            var (errGo, errGroup, errText) = BuildToast(safe);
            var sheetRefs = BuildSheet(safe);

            // ---------- Modals ----------
            var detail = BuildDetailModal(root, out var detailGroup, out var detailTitle, out var detailBody, out var detailClose);
            var settings = BuildSettingsModal(root, c);
            Wire(hud, ("settings", settings));

            // ---------- Wiring ----------
            Wire(previewView, ("scanController", c.Scan), ("modeController", c.Mode), ("flow", c.Flow));
            Wire(screen,
                ("flow", c.Flow), ("demoMode", c.Demo), ("modeController", c.Mode),
                ("twinPlacement", c.TwinPlacement), ("visualBinder", c.Binder),
                ("theme", _t), ("hud", hud),
                ("sheet", sheetRefs.Sheet), ("sheetGroup", sheetRefs.SheetGroup),
                ("sheetContent", sheetRefs.Content), ("sheetScroll", sheetRefs.Scroll),
                ("heroMark", sheetRefs.Hero), ("eyebrow", sheetRefs.Eyebrow),
                ("bodyTitle", sheetRefs.Title), ("bodyText", sheetRefs.Body), ("captionText", sheetRefs.Caption),
                ("thumbnailGroup", sheetRefs.ThumbGroup), ("thumbnail", sheetRefs.Thumb), ("traceOverlay", sheetRefs.TraceOverlay),
                ("markerRoot", sheetRefs.MarkerRoot), ("markerTemplate", sheetRefs.MarkerTemplate),
                ("thumbnailCaption", sheetRefs.ThumbCaption),
                ("chipRoot", sheetRefs.ChipRoot), ("chipTemplate", sheetRefs.ChipTemplate),
                ("meterRoot", sheetRefs.MeterRoot), ("meterTemplate", sheetRefs.MeterTemplate),
                ("featureListRoot", sheetRefs.FeatureRoot), ("featureRowPrefab", sheetRefs.FeatureTemplate),
                ("tileRoot", sheetRefs.TileRoot), ("tileTemplate", sheetRefs.TileTemplate),
                ("detailPanel", detail), ("detailGroup", detailGroup),
                ("detailTitle", detailTitle), ("detailBody", detailBody), ("detailClose", detailClose),
                ("primaryButton", sheetRefs.Primary), ("primaryButtonLabel", sheetRefs.PrimaryLabel),
                ("secondaryButton", sheetRefs.Secondary), ("secondaryButtonLabel", sheetRefs.SecondaryLabel),
                ("errorBar", errGo), ("errorGroup", errGroup), ("errorText", errText),
                ("placementHint", hintGroup), ("placementHintText", hintText));

            var stageView = canvasGo.AddComponent<ClassicStage>();
            Wire(stageView, ("flow", c.Flow), ("modeController", c.Mode), ("stage", c.Stage), ("backdrop", backdrop),
                ("binder", c.Binder), ("sheet", sheetRefs.Sheet), ("hud", (RectTransform)hud.transform),
                ("leafPicture", leafPicture), ("calloutLayer", calloutLayer));
            WireArray(stageView, "callouts", callouts);

            var confirmModal = BuildConfirmModal(root, out var confirmGroup, out var confirmTitle, out var confirmBody,
                out var stayButton, out var leaveButton, out var leaveLabel);
            var navigation = canvasGo.AddComponent<ClassicNavigation>();
            Wire(navigation, ("flow", c.Flow), ("modeController", c.Mode), ("backButton", NavBackButton), ("backIcon", NavBackIcon),
                ("arrowSprite", IconSprite("chevron-left")), ("brandSprite", _t.leafSprite), ("homeButton", NavHomeButton),
                ("confirmGroup", confirmGroup), ("confirmTitle", confirmTitle), ("confirmBody", confirmBody),
                ("stayButton", stayButton), ("leaveButton", leaveButton), ("leaveLabel", leaveLabel));
            var navSo = new SerializedObject(navigation);
            navSo.FindProperty("arrowColor").colorValue = _t.textPrimary;
            navSo.FindProperty("brandColor").colorValue = _t.accent;
            navSo.ApplyModifiedPropertiesWithoutUndo();

            // Order matters for draw order: sheet above scanner, toasts above all.
            scanner.transform.SetSiblingIndex(1);
            hintGroup.transform.SetAsLastSibling();
            errGo.transform.SetAsLastSibling();
            hud.transform.SetAsLastSibling();
            confirmModal.transform.SetAsLastSibling();   // above the top bar

            SetLayerRecursively(canvasGo, LayerMask.NameToLayer("UI"));   // the studio camera draws the UI layer only
            Debug.Log("[AI Plant Doctor] Classic UI built.");
            return canvasGo;
        }

        static Button NavBackButton, NavHomeButton;
        static Image NavBackIcon;

        static Sprite IconSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Icons/{name}.png");

        /// <summary>"Go back to the start?" with Stay / Start over.</summary>
        static GameObject BuildConfirmModal(Transform parent, out FadeSlide group, out TMP_Text title, out TMP_Text body,
            out Button stay, out Button leave, out TMP_Text leaveLabel)
        {
            var modal = NewRect("ConfirmModal", parent);
            Stretch(modal);
            group = AddFade(modal.gameObject, new Vector2(0, -60), false);

            var scrim = Img(modal, "Scrim", new Color(0, 0, 0, 0.6f), null);
            Stretch(scrim.rectTransform);
            scrim.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // swallow taps behind the dialog

            var card = Img(modal, "Card", _t.panelRaised, _t.roundedSprite, Radius(_t.sheetRadius));
            Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-460, -300), new Vector2(460, 300));
            Shadow(card.rectTransform);

            title = Txt(card.transform, "Title", "Go back to the start?", _t.fontTitle - 8, _t.textPrimary, _t.fontBold, TextAlignmentOptions.TopLeft);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(52, -150), new Vector2(-52, -48));
            title.enableAutoSizing = true; title.fontSizeMin = 30; title.fontSizeMax = _t.fontTitle - 8;
            body = Txt(card.transform, "Body", "", _t.fontBody, _t.textSecondary, _t.fontRegular, TextAlignmentOptions.TopLeft);
            Place(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(52, 190), new Vector2(-52, -160));
            body.enableAutoSizing = true; body.fontSizeMin = 22; body.fontSizeMax = _t.fontBody;

            var row = NewRect("Buttons", card.transform);
            Place(row, new Vector2(0, 0), new Vector2(1, 0), new Vector2(52, 44), new Vector2(-52, 44 + _t.buttonHeight));
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            (stay, _) = PillButton(row, "Stay", "Stay", false);
            (leave, leaveLabel) = PillButton(row, "StartOver", "Start over", true);
            modal.gameObject.SetActive(true);
            return modal.gameObject;
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            if (layer < 0) return;
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        // ==================================================================
        // Plant stage
        // ==================================================================

        /// <summary>The backdrop of the whole app: a deep botanical gradient, a soft light and the plant, behind everything.</summary>
        static StageBackdrop BuildBackdrop(Transform parent)
        {
            var root = NewRect("PlantBackdrop", parent);
            Stretch(root);
            var group = root.gameObject.AddComponent<CanvasGroup>();

            var baseImg = Img(root, "Base", new Color(_t.background.r, _t.background.g, _t.background.b, 1f), null);
            Stretch(baseImg.transform); baseImg.raycastTarget = false;
            var gradient = Img(root, "Gradient", new Color(0.10f, 0.24f, 0.18f, 1f), _t.gradientSprite);
            Stretch(gradient.transform); gradient.raycastTarget = false;

            var glow = Img(root, "Glow", new Color(_t.accent.r, _t.accent.g, _t.accent.b, 0.20f),
                AssetDatabase.LoadAssetAtPath<Sprite>(UIShapeLibrary.GlowPath));
            glow.raycastTarget = false;
            glow.preserveAspect = false;

            var plantRt = NewRect("PlantPicture", root);
            Stretch(plantRt);
            var plantRaw = plantRt.gameObject.AddComponent<RawImage>();
            plantRaw.raycastTarget = true;
            plantRaw.color = new Color(1, 1, 1, 0);
            plantRt.gameObject.AddComponent<Viewport3DInput>();

            var view = root.gameObject.AddComponent<StageBackdrop>();
            Wire(view, ("group", group), ("gradient", gradient), ("glow", glow), ("plantPicture", plantRaw));
            return view;
        }

        /// <summary>The magnified leaf (a transparent full-screen picture you can turn) and the labels that point at its spots.</summary>
        static void BuildLeafLayer(Transform parent, out RawImage leafRaw, out RectTransform calloutLayer, out CalloutView[] callouts)
        {
            var leafRt = NewRect("LeafPicture", parent);
            Stretch(leafRt);
            leafRaw = leafRt.gameObject.AddComponent<RawImage>();
            leafRaw.raycastTarget = true;
            leafRaw.color = new Color(1, 1, 1, 0);
            leafRt.gameObject.AddComponent<Viewport3DInput>();
            leafRt.gameObject.SetActive(false);

            calloutLayer = NewRect("LeafCallouts", parent);
            Stretch(calloutLayer);
            callouts = new[] { Callout(calloutLayer, "Callout0"), Callout(calloutLayer, "Callout1"), Callout(calloutLayer, "Callout2") };
            calloutLayer.gameObject.SetActive(false);
        }

        static CalloutView Callout(Transform parent, string name)
        {
            var go = NewRect(name, parent);
            Stretch(go);
            var line = Img(go, "Line", new Color(1, 1, 1, 0.85f), null);
            line.raycastTarget = false;
            var lr = line.rectTransform;
            lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.pivot = new Vector2(0, 0.5f);
            lr.sizeDelta = new Vector2(100, 3);

            var dot = Img(go, "Dot", Color.white, _t.circleSprite);
            dot.raycastTarget = false;
            var dr = dot.rectTransform;
            dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 0.5f);
            dr.sizeDelta = new Vector2(28, 28);
            var core = Img(dot.transform, "Core", _t.danger, _t.circleSprite);
            core.raycastTarget = false;
            var cr = core.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(14, 14);
            cr.anchoredPosition = Vector2.zero;

            var card = Img(go, "Card", new Color(_t.panel.r, _t.panel.g, _t.panel.b, 0.92f), _t.roundedSprite, Radius(24));
            card.raycastTarget = false;
            var cardRt = card.rectTransform;
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(380, 92);
            var text = Txt(card.transform, "Text", "Label", _t.fontCaption + 3, _t.textPrimary, _t.fontMedium, TextAlignmentOptions.Center);
            Stretch(text.transform);
            ((RectTransform)text.transform).offsetMin = new Vector2(16, 6);
            ((RectTransform)text.transform).offsetMax = new Vector2(-16, -6);

            var view = go.gameObject.AddComponent<CalloutView>();
            Wire(view, ("card", cardRt), ("text", text), ("dot", dr), ("line", lr));
            return view;
        }

        // ==================================================================
        // HUD
        // ==================================================================

        static HudView BuildHud(Transform parent, Context c, out Transform settingsSlot)
        {
            var hudRt = NewRect("Hud", parent);
            Place(hudRt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(28, -200), new Vector2(-28, -24));
            var hud = hudRt.gameObject.AddComponent<HudView>();

            // Floating pill bar.
            var pill = Img(hudRt, "Bar", _t.panel, _t.roundedSprite, Radius(64));
            Place(pill.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -128), new Vector2(0, 0));
            Shadow(pill.rectTransform);

            // Brand badge on the first page, the back arrow everywhere else (ClassicNavigation swaps the picture).
            var badge = Img(pill.transform, "Badge", _t.accentSoft, _t.circleSprite);
            Place(badge.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, -46), new Vector2(110, 46));
            var backButton = badge.gameObject.AddComponent<Button>();
            backButton.targetGraphic = badge;
            var leaf = Img(badge.transform, "Leaf", _t.accent, _t.leafSprite);
            Place(leaf.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 16), new Vector2(-16, -16));
            leaf.raycastTarget = false;
            NavBackButton = backButton; NavBackIcon = leaf;

            var step = Txt(pill.transform, "Step", "AI PLANT DOCTOR", _t.fontOverline, _t.accent, _t.fontSemiBold, TextAlignmentOptions.BottomLeft);
            step.characterSpacing = 8f;
            Place(step.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(132, 2), new Vector2(-480, -16));
            var title = Txt(pill.transform, "Title", "Plant Doctor", _t.fontHeading - 2, _t.textPrimary, _t.fontSemiBold, TextAlignmentOptions.TopLeft);
            Place(title.rectTransform, new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(132, 10), new Vector2(-480, 4));
            title.overflowMode = TextOverflowModes.Ellipsis;
            title.enableWordWrapping = false;

            // AI badge.
            var chip = BuildChip(pill.transform, "AiBadge");
            var chipRt = (RectTransform)chip.transform;
            Place(chipRt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-408, -30), new Vector2(-208, 30));
            Object.DestroyImmediate(chip.GetComponent<LayoutElement>());

            // Home button: back to the starting page.
            var homeBtn = Img(pill.transform, "Home", _t.panelSunken, _t.circleSprite);
            Place(homeBtn.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-196, -44), new Vector2(-108, 44));
            var homeButton = homeBtn.gameObject.AddComponent<Button>();
            homeButton.targetGraphic = homeBtn;
            var homeIcon = Img(homeBtn.transform, "Icon", _t.textSecondary, IconSprite("house"));
            Place(homeIcon.rectTransform, Vector2.zero, Vector2.one, new Vector2(20, 20), new Vector2(-20, -20));
            homeIcon.raycastTarget = false;
            NavHomeButton = homeButton;

            // Settings button.
            var gearBtn = Img(pill.transform, "Settings", _t.panelSunken, _t.circleSprite);
            Place(gearBtn.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-106, -44), new Vector2(-18, 44));
            var gearButton = gearBtn.gameObject.AddComponent<Button>();
            gearButton.targetGraphic = gearBtn;
            var gear = Img(gearBtn.transform, "Gear", _t.textSecondary, _t.gearSprite);
            Place(gear.rectTransform, Vector2.zero, Vector2.one, new Vector2(20, 20), new Vector2(-20, -20));
            gear.raycastTarget = false;
            settingsSlot = gearBtn.transform;

            // Step segments under the bar.
            var steps = NewRect("Steps", hudRt);
            Place(steps, new Vector2(0, 1), new Vector2(1, 1), new Vector2(36, -160), new Vector2(-36, -150));
            var hlg = steps.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            var segments = new Image[StageDescriptor.TotalUserFacingSteps];
            for (int i = 0; i < segments.Length; i++)
                segments[i] = Img(steps, $"Seg{i + 1}", _t.divider, _t.roundedSprite, Radius(5));

            Wire(hud, ("theme", _t), ("flow", c.Flow), ("stepLabel", step), ("titleLabel", title),
                ("backendChip", chip), ("settingsButton", gearButton));
            WireArray(hud, "stepSegments", segments);
            return hud;
        }

        // ==================================================================
        // Scanner
        // ==================================================================

        static ScannerView BuildScanner(Transform parent, Context c)
        {
            var rt = NewRect("Scanner", parent);
            Stretch(rt);
            var group = AddFade(rt.gameObject, new Vector2(0, 0), false);
            var view = rt.gameObject.AddComponent<ScannerView>();

            // Reticle: a square frame slightly above centre.
            var reticle = NewRect("Reticle", rt);
            Place(reticle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-380, -300), new Vector2(380, 460));
            var corners = new Image[4];
            var cornerSpecs = new[] { (0f, 0f, 0f), (1f, 0f, 90f), (1f, 1f, 180f), (0f, 1f, 270f) };
            for (int i = 0; i < 4; i++)
            {
                var (ax, ay, rot) = cornerSpecs[i];
                var corner = Img(reticle, $"Corner{i}", _t.textSecondary, _t.cornerSprite);
                var crt = corner.rectTransform;
                crt.anchorMin = crt.anchorMax = new Vector2(ax, ay);
                crt.pivot = new Vector2(0f, 0f);
                crt.sizeDelta = new Vector2(150, 150);
                crt.anchoredPosition = Vector2.zero;
                crt.localEulerAngles = new Vector3(0, 0, rot);
                corner.raycastTarget = false;
                corners[i] = corner;
            }

            var line = Img(reticle, "ScanLine", _t.accent, _t.roundedSprite, Radius(4));
            var lrt = line.rectTransform;
            lrt.anchorMin = new Vector2(0.06f, 0.5f); lrt.anchorMax = new Vector2(0.94f, 0.5f);
            lrt.sizeDelta = new Vector2(0, 8); lrt.anchoredPosition = Vector2.zero;
            line.raycastTarget = false;
            var glow = Img(line.transform, "Glow", new Color(1, 1, 1, 0.25f), _t.softShadowSprite, 1.4f);
            Place(glow.rectTransform, Vector2.zero, Vector2.one, new Vector2(-30, -30), new Vector2(30, 30));
            glow.raycastTarget = false;

            // Analyzing indicator inside the reticle.
            var analyzing = NewRect("Analyzing", reticle);
            Place(analyzing, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-220, -120), new Vector2(220, 140));
            var spinner = Img(analyzing, "Spinner", _t.info, AssetDatabase.LoadAssetAtPath<Sprite>(UIShapeLibrary.ArcPath));
            Place(spinner.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-60, -120), new Vector2(60, 0));
            spinner.gameObject.AddComponent<Spinner>();
            var analyzingLabel = Txt(analyzing, "Label", "Analyzing leaf", _t.fontHeading, _t.textPrimary, _t.fontSemiBold, TextAlignmentOptions.Center);
            Place(analyzingLabel.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 90));
            analyzing.gameObject.SetActive(false);

            // Quality chips above the reticle.
            var chips = NewRect("QualityChips", rt);
            Place(chips, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-380, 490), new Vector2(380, 556));
            var chl = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            chl.spacing = 16; chl.childAlignment = TextAnchor.MiddleCenter;
            chl.childControlWidth = true; chl.childControlHeight = true;
            chl.childForceExpandWidth = false; chl.childForceExpandHeight = true;
            var light = BuildChip(chips, "Light");
            var focus = BuildChip(chips, "Focus");
            var leafChip = BuildChip(chips, "Leaf");

            // Hint pill below the reticle.
            var hintBg = Img(rt, "HintPill", _t.panel, _t.roundedSprite, Radius(42));
            Place(hintBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-420, -404), new Vector2(420, -320));
            hintBg.raycastTarget = false;
            var hint = Txt(hintBg.transform, "Hint", "Fill the frame with one leaf", _t.fontBody, _t.textPrimary, _t.fontMedium, TextAlignmentOptions.Center);
            Place(hint.rectTransform, Vector2.zero, Vector2.one, new Vector2(28, 0), new Vector2(-28, 0));
            hint.enableAutoSizing = true; hint.fontSizeMin = 22; hint.fontSizeMax = _t.fontBody;

            // Shutter.
            var shutterRt = NewRect("Shutter", rt);
            Place(shutterRt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-104, 120), new Vector2(104, 328));
            var ring = Img(shutterRt, "Ring", Color.white, _t.ringSprite);
            Stretch(ring.rectTransform);
            ring.gameObject.AddComponent<Pulse>();
            var core = Img(shutterRt, "Core", Color.white, _t.circleSprite);
            Place(core.rectTransform, Vector2.zero, Vector2.one, new Vector2(26, 26), new Vector2(-26, -26));
            var shutter = shutterRt.gameObject.AddComponent<Button>();
            shutter.targetGraphic = core;
            var leafIcon = Img(core.transform, "Icon", _t.accentDim, _t.leafSprite);
            Place(leafIcon.rectTransform, Vector2.zero, Vector2.one, new Vector2(42, 42), new Vector2(-42, -42));
            leafIcon.raycastTarget = false;

            // Demo shortcut.
            var (demoBtn, _) = TextButton(rt, "DemoButton", "Try demo", _t.textPrimary);
            Place((RectTransform)demoBtn.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 170), new Vector2(330, 280));

            Wire(view, ("flow", c.Flow), ("scanController", c.Scan), ("demoMode", c.Demo), ("theme", _t), ("group", group),
                ("reticle", reticle), ("scanLine", lrt), ("scanLineImage", line),
                ("lightChip", light), ("focusChip", focus), ("leafChip", leafChip), ("hint", hint),
                ("shutter", shutter), ("shutterRing", ring), ("shutterCore", core),
                ("analyzingGroup", analyzing.gameObject), ("analyzingLabel", analyzingLabel), ("demoButton", demoBtn));
            WireArray(view, "corners", corners);
            return view;
        }

        // ==================================================================
        // Bottom sheet
        // ==================================================================

        class SheetRefs
        {
            public RectTransform Sheet, Content;
            public FadeSlide SheetGroup;
            public ScrollRect Scroll;
            public GameObject Hero, ThumbGroup;
            public TMP_Text Eyebrow, Title, Body, Caption, ThumbCaption, PrimaryLabel, SecondaryLabel;
            public RawImage Thumb, TraceOverlay;
            public RectTransform MarkerRoot;
            public Image MarkerTemplate;
            public Transform ChipRoot, MeterRoot, FeatureRoot, TileRoot;
            public ChipView ChipTemplate;
            public MeterView MeterTemplate;
            public FeatureRowView FeatureTemplate;
            public StatTileView TileTemplate;
            public Button Primary, Secondary;
        }

        static SheetRefs BuildSheet(Transform parent)
        {
            var r = new SheetRefs();
            const float actionsHeight = 196f;

            var sheet = NewRect("Sheet", parent);
            sheet.anchorMin = new Vector2(0, 0); sheet.anchorMax = new Vector2(1, 0);
            sheet.pivot = new Vector2(0.5f, 0f);
            sheet.offsetMin = new Vector2(16, 12); sheet.offsetMax = new Vector2(-16, 12);
            sheet.sizeDelta = new Vector2(-32, 760);
            sheet.anchoredPosition = new Vector2(0, 12);
            r.Sheet = sheet;
            r.SheetGroup = AddFade(sheet.gameObject, new Vector2(0, -140), true);

            Shadow(sheet);
            var bg = Img(sheet, "Background", _t.panel, _t.roundedSprite, Radius(_t.sheetRadius));
            Stretch(bg.rectTransform);
            var handle = Img(sheet, "Handle", _t.divider, _t.roundedSprite, Radius(5));
            Place(handle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-46, -26), new Vector2(46, -16));

            // Scrollable content.
            var scrollRt = NewRect("Scroll", sheet);
            Place(scrollRt, Vector2.zero, Vector2.one, new Vector2(0, actionsHeight), new Vector2(0, -36));
            scrollRt.gameObject.AddComponent<RectMask2D>();
            scrollRt.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);   // drag target for the whole viewport
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;
            r.Scroll = scroll;

            var content = NewRect("Content", scrollRt);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(48, 48, 24, 24);
            vlg.spacing = 22;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = scrollRt;
            r.Content = content;

            // Hero mark (welcome only).
            var hero = NewRect("Hero", content);
            Le(hero.gameObject, 132);
            var heroBadge = Img(hero, "Badge", _t.accentSoft, _t.circleSprite);
            Place(heroBadge.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, -66), new Vector2(132, 66));
            var heroLeaf = Img(heroBadge.transform, "Leaf", _t.accent, _t.leafSprite);
            Place(heroLeaf.rectTransform, Vector2.zero, Vector2.one, new Vector2(24, 24), new Vector2(-24, -24));
            r.Hero = hero.gameObject;

            r.Eyebrow = Txt(content, "Eyebrow", "", _t.fontOverline, _t.accent, _t.fontSemiBold, TextAlignmentOptions.TopLeft);
            r.Eyebrow.characterSpacing = 10f;
            r.Title = Txt(content, "Title", "", _t.fontTitle, _t.textPrimary, _t.fontBold, TextAlignmentOptions.TopLeft);
            r.Title.lineSpacing = -8f;

            // Chips row.
            var chips = NewRect("Chips", content);
            var chl = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            chl.spacing = 14; chl.childAlignment = TextAnchor.MiddleLeft;
            chl.childControlWidth = true; chl.childControlHeight = true;
            chl.childForceExpandWidth = false; chl.childForceExpandHeight = false;
            Le(chips.gameObject, _t.chipHeight);
            r.ChipRoot = chips;
            r.ChipTemplate = BuildChip(chips, "ChipTemplate");
            r.ChipTemplate.gameObject.SetActive(false);

            // Scan thumbnail with flagged regions.
            var thumbGroup = NewRect("ScanThumbnail", content);
            Le(thumbGroup.gameObject, 250);
            var mask = Img(thumbGroup, "Frame", Color.white, _t.roundedSprite, Radius(28));
            Place(mask.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, -125), new Vector2(250, 125));
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var thumb = new GameObject("Image", typeof(RectTransform), typeof(RawImage));
            thumb.transform.SetParent(mask.transform, false);
            Stretch(thumb.transform);
            r.Thumb = thumb.GetComponent<RawImage>();
            r.Thumb.raycastTarget = false;
            var outline = new GameObject("TraceOverlay", typeof(RectTransform), typeof(RawImage));
            outline.transform.SetParent(mask.transform, false);
            Stretch(outline.transform);
            r.TraceOverlay = outline.GetComponent<RawImage>();
            r.TraceOverlay.raycastTarget = false;
            outline.SetActive(false);
            var markers = NewRect("Markers", mask.transform);
            Stretch(markers);
            r.MarkerRoot = markers;
            r.MarkerTemplate = Img(markers, "MarkerTemplate", _t.danger, _t.outlineSprite, Radius(14));
            r.MarkerTemplate.raycastTarget = false;
            r.MarkerTemplate.gameObject.SetActive(false);
            r.ThumbCaption = Txt(thumbGroup, "Caption", "Your scan", _t.fontCaption, _t.textSecondary, _t.fontMedium, TextAlignmentOptions.MidlineLeft);
            Place(r.ThumbCaption.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(282, 0), new Vector2(0, 0));
            r.ThumbGroup = thumbGroup.gameObject;

            // Meters.
            var meters = NewRect("Meters", content);
            Vertical(meters, 18);
            r.MeterRoot = meters;
            r.MeterTemplate = BuildMeterTemplate(meters);

            // Feature rows.
            var features = NewRect("Features", content);
            Vertical(features, 16);
            r.FeatureRoot = features;
            r.FeatureTemplate = BuildFeatureRowTemplate(features);

            // Report tiles.
            var tiles = NewRect("Tiles", content);
            var grid = tiles.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(310, 176);
            grid.spacing = new Vector2(18, 18);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            tiles.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            r.TileRoot = tiles;
            r.TileTemplate = BuildTileTemplate(tiles);

            r.Body = Txt(content, "Body", "", _t.fontBody, _t.textSecondary, _t.fontRegular, TextAlignmentOptions.TopLeft);
            r.Body.lineSpacing = 6f;
            r.Body.paragraphSpacing = 6f;
            r.Caption = Txt(content, "Caption", "", _t.fontCaption, _t.textTertiary, _t.fontRegular, TextAlignmentOptions.TopLeft);

            // Actions.
            var actions = NewRect("Actions", sheet);
            Place(actions, new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 34), new Vector2(-40, 34 + _t.buttonHeight));
            var hlg = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            (r.Secondary, r.SecondaryLabel) = PillButton(actions, "SecondaryButton", "Back", false);
            (r.Primary, r.PrimaryLabel) = PillButton(actions, "PrimaryButton", "Continue", true);
            r.Primary.GetComponent<LayoutElement>().flexibleWidth = 1.6f;
            return r;
        }

        static MeterView BuildMeterTemplate(Transform parent)
        {
            var go = NewRect("MeterTemplate", parent);
            Le(go.gameObject, 84);
            var view = go.gameObject.AddComponent<MeterView>();

            var label = Txt(go, "Label", "Severity", _t.fontCaption + 2, _t.textSecondary, _t.fontMedium, TextAlignmentOptions.BottomLeft);
            Place(label.rectTransform, new Vector2(0, 1), new Vector2(0.7f, 1), new Vector2(0, -50), new Vector2(0, 0));
            var value = Txt(go, "Value", "0%", _t.fontBody, _t.textPrimary, _t.fontSemiBold, TextAlignmentOptions.BottomRight);
            Place(value.rectTransform, new Vector2(0.7f, 1), new Vector2(1, 1), new Vector2(0, -52), new Vector2(0, 0));

            var track = Img(go, "Track", _t.divider, _t.roundedSprite, Radius(9));
            Place(track.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 4), new Vector2(0, 22));
            var fill = Img(track.transform, "Fill", _t.accent, _t.roundedSprite, Radius(9));
            Place(fill.rectTransform, Vector2.zero, new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);

            Wire(view, ("label", label), ("value", value), ("fill", fill));
            go.gameObject.SetActive(false);
            return view;
        }

        static FeatureRowView BuildFeatureRowTemplate(Transform parent)
        {
            var card = Img(parent, "FeatureRowTemplate", _t.panelRaised, _t.roundedSprite, Radius(_t.cardRadius));
            Le(card.gameObject, 168);
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            var view = card.gameObject.AddComponent<FeatureRowView>();
            var t = card.transform;

            var name = Txt(t, "Name", "Leaf Color", _t.fontBody, _t.textPrimary, _t.fontSemiBold, TextAlignmentOptions.MidlineLeft);
            Place(name.rectTransform, new Vector2(0, 1), new Vector2(0.65f, 1), new Vector2(32, -70), new Vector2(0, -16));
            var observed = Txt(t, "Observed", "72%", _t.fontHeading, _t.accent, _t.fontBold, TextAlignmentOptions.MidlineRight);
            Place(observed.rectTransform, new Vector2(0.65f, 1), new Vector2(1, 1), new Vector2(0, -72), new Vector2(-32, -14));

            var track = Img(t, "Track", _t.divider, _t.roundedSprite, Radius(8));
            Place(track.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(32, -16), new Vector2(-32, 0));
            var bar = Img(track.transform, "Bar", _t.accent, _t.roundedSprite, Radius(8));
            Place(bar.rectTransform, Vector2.zero, new Vector2(0.7f, 1), Vector2.zero, Vector2.zero);
            var marker = Img(track.transform, "HealthyMarker", _t.textPrimary, _t.roundedSprite, Radius(3));
            var mr = marker.rectTransform;
            mr.anchorMin = mr.anchorMax = new Vector2(0.95f, 0.5f);
            mr.sizeDelta = new Vector2(6, 34);
            mr.anchoredPosition = Vector2.zero;

            var pill = Img(t, "Status", _t.accentSoft, _t.roundedSprite, Radius(22));
            Place(pill.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 14), new Vector2(204, 58));
            var pillText = Txt(pill.transform, "Text", "Normal", _t.fontOverline, _t.accent, _t.fontSemiBold, TextAlignmentOptions.Center);
            Stretch(pillText.rectTransform);
            var optimal = Txt(t, "Healthy", "Healthy 95%", _t.fontCaption, _t.textSecondary, _t.fontRegular, TextAlignmentOptions.MidlineLeft);
            Place(optimal.rectTransform, new Vector2(0, 0), new Vector2(0.6f, 0), new Vector2(224, 14), new Vector2(0, 58));
            var source = Txt(t, "Source", "VISUAL AI ESTIMATE", _t.fontOverline - 2, _t.textTertiary, _t.fontMedium, TextAlignmentOptions.MidlineRight);
            Place(source.rectTransform, new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(0, 14), new Vector2(-32, 58));

            Wire(view, ("nameLabel", name), ("observedLabel", observed), ("optimalLabel", optimal),
                ("sourceLabel", source), ("observedBar", bar), ("optimalMarker", mr),
                ("statusPill", pill), ("statusText", pillText), ("button", button));
            card.gameObject.SetActive(false);
            return view;
        }

        static StatTileView BuildTileTemplate(Transform parent)
        {
            var card = Img(parent, "TileTemplate", _t.panelRaised, _t.roundedSprite, Radius(_t.cardRadius));
            var view = card.gameObject.AddComponent<StatTileView>();
            var bar = Img(card.transform, "Accent", _t.accent, _t.roundedSprite, Radius(4));
            Place(bar.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -30), new Vector2(84, -22));
            var value = Txt(card.transform, "Value", "68%", _t.fontTitle - 4, _t.accent, _t.fontBold, TextAlignmentOptions.BottomLeft);
            Place(value.rectTransform, new Vector2(0, 0.38f), new Vector2(1, 1), new Vector2(28, 0), new Vector2(-16, -34));
            value.enableAutoSizing = true; value.fontSizeMin = 28; value.fontSizeMax = _t.fontTitle - 4;
            var caption = Txt(card.transform, "Caption", "Recovery", _t.fontCaption, _t.textSecondary, _t.fontMedium, TextAlignmentOptions.TopLeft);
            Place(caption.rectTransform, new Vector2(0, 0), new Vector2(1, 0.38f), new Vector2(28, 12), new Vector2(-16, 0));
            Wire(view, ("value", value), ("caption", caption), ("accentBar", bar));
            card.gameObject.SetActive(false);
            return view;
        }

        // ==================================================================
        // Small overlays
        // ==================================================================

        static (FadeSlide, TMP_Text) BuildPlacementHint(Transform parent)
        {
            var bg = Img(parent, "PlacementHint", _t.panel, _t.roundedSprite, Radius(44));
            Place(bg.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-440, -312), new Vector2(440, -224));
            bg.raycastTarget = false;
            var group = AddFade(bg.gameObject, new Vector2(0, 40), false);
            var dot = Img(bg.transform, "Dot", _t.accent, _t.circleSprite);
            Place(dot.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(34, -11), new Vector2(56, 11));
            dot.gameObject.AddComponent<Pulse>();
            var text = Txt(bg.transform, "Text", "Move your phone slowly to find a surface", _t.fontCaption + 2, _t.textPrimary, _t.fontMedium, TextAlignmentOptions.MidlineLeft);
            Place(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(76, 0), new Vector2(-28, 0));
            return (group, text);
        }

        static (GameObject, FadeSlide, TMP_Text) BuildToast(Transform parent)
        {
            var bg = Img(parent, "Toast", new Color(0.25f, 0.07f, 0.06f, 0.94f), _t.roundedSprite, Radius(_t.cardRadius));
            Place(bg.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -360), new Vector2(-40, -220));
            bg.raycastTarget = false;
            var group = AddFade(bg.gameObject, new Vector2(0, 60), false);
            var bar = Img(bg.transform, "Accent", _t.danger, _t.roundedSprite, Radius(4));
            Place(bar.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(18, 22), new Vector2(26, -22));
            var text = Txt(bg.transform, "Text", "", _t.fontCaption + 2, _t.textPrimary, _t.fontMedium, TextAlignmentOptions.MidlineLeft);
            Place(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(48, 10), new Vector2(-28, -10));
            text.enableAutoSizing = true; text.fontSizeMin = 20; text.fontSizeMax = _t.fontCaption + 2;
            return (bg.gameObject, group, text);
        }

        static GameObject BuildDetailModal(Transform parent, out FadeSlide group, out TMP_Text title, out TMP_Text body, out Button close)
        {
            var modal = NewRect("DetailModal", parent);
            Stretch(modal);
            group = AddFade(modal.gameObject, new Vector2(0, -60), false);

            var scrim = Img(modal, "Scrim", new Color(0, 0, 0, 0.6f), null);
            Stretch(scrim.rectTransform);
            var scrimButton = scrim.gameObject.AddComponent<Button>();
            scrimButton.transition = Selectable.Transition.None;

            var card = Img(modal, "Card", _t.panelRaised, _t.roundedSprite, Radius(_t.sheetRadius));
            Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-480, -420), new Vector2(480, 420));
            Shadow(card.rectTransform);

            title = Txt(card.transform, "Title", "", _t.fontTitle - 6, _t.textPrimary, _t.fontBold, TextAlignmentOptions.TopLeft);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(52, -130), new Vector2(-52, -48));
            body = Txt(card.transform, "Body", "", _t.fontBody, _t.textSecondary, _t.fontRegular, TextAlignmentOptions.TopLeft);
            Place(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(52, 190), new Vector2(-52, -150));
            body.enableAutoSizing = true; body.fontSizeMin = 22; body.fontSizeMax = _t.fontBody;

            Button btn;
            (btn, _) = PillButton(card.transform, "Close", "Got it", true);
            Place((RectTransform)btn.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(52, 44), new Vector2(-52, 44 + _t.buttonHeight));
            close = btn;
            // Tapping outside the card closes it, like the button.
            UnityEditor.Events.UnityEventTools.AddPersistentListener(scrimButton.onClick, group.HideAnimated);
            return modal.gameObject;
        }

        static SettingsView BuildSettingsModal(Transform parent, Context c)
        {
            var modal = NewRect("SettingsModal", parent);
            Stretch(modal);
            var group = AddFade(modal.gameObject, new Vector2(0, -200), false);
            var view = modal.gameObject.AddComponent<SettingsView>();

            var scrim = Img(modal, "Scrim", new Color(0, 0, 0, 0.62f), null);
            Stretch(scrim.rectTransform);
            var backdrop = scrim.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;

            var card = Img(modal, "Card", _t.panelRaised, _t.roundedSprite, Radius(_t.sheetRadius));
            Place(card.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 12), new Vector2(-16, 2030));
            Shadow(card.rectTransform);
            var ct = card.transform;

            var title = Txt(ct, "Title", "Settings", _t.fontTitle - 4, _t.textPrimary, _t.fontBold, TextAlignmentOptions.MidlineLeft);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(52, -140), new Vector2(-160, -40));

            var closeImg = Img(ct, "Close", _t.panelSunken, _t.circleSprite);
            Place(closeImg.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-140, -130), new Vector2(-48, -38));
            var closeBtn = closeImg.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            var x = Txt(closeImg.transform, "X", "×", _t.fontTitle, _t.textSecondary, _t.fontMedium, TextAlignmentOptions.Center);
            Stretch(x.rectTransform);

            float y = -180f;
            Section(ct, "AI BACKEND", ref y);
            var (demoTab, serverTab) = Segmented2(ct, "Demo AI", "Server AI", ref y);

            var serverFields = NewRect("ServerFields", ct);
            Place(serverFields, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, y - 330), new Vector2(0, y));
            float fy = 0f;
            var endpoint = Input(serverFields, "Endpoint", "http://192.168.1.20:8000/v1/analyze", ref fy);
            var apiKey = Input(serverFields, "ApiKey", "API key (optional)", ref fy);
            var (testBtn, _) = PillButton(serverFields, "Test", "Test connection", false);
            Place((RectTransform)testBtn.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(52, fy - 104), new Vector2(-52, fy));
            y -= 340f;

            var lower = NewRect("Lower", ct);
            Stretch(lower);
            var status = Txt(lower, "Status", "", _t.fontCaption, _t.textSecondary, _t.fontRegular, TextAlignmentOptions.TopLeft);
            Place(status.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(52, y - 120), new Vector2(-52, y));
            y -= 140f;

            // Labels are placeholders; SettingsView names them from the plant database. The demo picks only matter for the Demo AI,
            // so the whole block hides when Server AI is chosen (and the sheet gets shorter).
            var demoBlock = NewRect("DemoBlock", lower);
            Stretch(demoBlock);
            float demoTop = y;
            Section(demoBlock, "DEMO PLANT", ref y);
            // 37 plants (PlantVillage, PlantSeg, then mango, sugarcane, chilli, cotton): three rows that scroll sideways. Same order as SettingsView.plantIds.
            var plants = ScrollChips(demoBlock, "Plants", new[] { "Tomato", "Potato", "Pepper", "Apple", "Blueberry", "Cherry", "Corn",
                "Grape", "Orange", "Peach", "Raspberry", "Soybean", "Squash", "Strawberry", "Banana", "Basil", "Bean", "Broccoli",
                "Cabbage", "Carrot", "Cauliflower", "Celery", "Coffee", "Cucumber", "Eggplant", "Garlic", "Ginger", "Lettuce", "Maple",
                "Plum", "Rice", "Tobacco", "Wheat", "Mango", "Sugarcane", "Chilli", "Cotton" }, ref y, rows: 3);
            Section(demoBlock, "DEMO CONDITION", ref y);
            // Up to nine diseases (tomato) plus Healthy, two to a row.
            var scenarios = GridChips(demoBlock, "Scenarios", 10, 2, ref y);
            float demoHeight = demoTop - y;

            var (save, _) = PillButton(ct, "Save", "Save", true);
            Place((RectTransform)save.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(52, 52), new Vector2(-52, 52 + _t.buttonHeight));

            Wire(view, ("flow", c.Flow), ("theme", _t), ("group", group), ("closeButton", closeBtn), ("backdropButton", backdrop),
                ("demoTab", demoTab), ("serverTab", serverTab), ("serverFields", serverFields.gameObject),
                ("endpointInput", endpoint), ("apiKeyInput", apiKey), ("testButton", testBtn),
                ("statusText", status), ("saveButton", save), ("lowerBlock", lower), ("card", card.rectTransform));
            var so = new SerializedObject(view);
            so.FindProperty("demoBlock").objectReferenceValue = demoBlock.gameObject;
            so.FindProperty("demoBlockHeight").floatValue = demoHeight;
            so.ApplyModifiedPropertiesWithoutUndo();
            WireArray(view, "plantButtons", plants);
            WireArray(view, "scenarioButtons", scenarios);
            return view;
        }

        static void Section(Transform parent, string label, ref float y)
        {
            var t = Txt(parent, label, label, _t.fontOverline, _t.textTertiary, _t.fontSemiBold, TextAlignmentOptions.BottomLeft);
            t.characterSpacing = 10f;
            Place(t.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(52, y - 50), new Vector2(-52, y));
            y -= 66f;
        }

        static (Button, Button) Segmented2(Transform parent, string a, string b, ref float y)
        {
            var row = NewRect("Segmented", parent);
            Place(row, new Vector2(0, 1), new Vector2(1, 1), new Vector2(52, y - 112), new Vector2(-52, y));
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            var (ba, _) = PillButton(row, a, a, false);
            var (bb, _) = PillButton(row, b, b, false);
            y -= 136f;
            return (ba, bb);
        }

        static Button[] Segmented(Transform parent, string name, string[] labels, ref float y)
        {
            var row = NewRect(name, parent);
            Place(row, new Vector2(0, 1), new Vector2(1, 1), new Vector2(52, y - 112), new Vector2(-52, y));
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            var buttons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                var (b, l) = PillButton(row, labels[i], labels[i], false);
                l.fontSize = _t.fontCaption + 2;
                buttons[i] = b;
            }
            y -= 136f;
            return buttons;
        }

        static Button[] ScrollChips(Transform parent, string name, string[] labels, ref float y, int rows = 1)
        {
            if (rows > 1) return ScrollGrid(parent, name, labels, ref y, rows);
            var view = NewRect(name, parent);
            Place(view, new Vector2(0, 1), new Vector2(1, 1), new Vector2(52, y - 112), new Vector2(-52, y));
            view.gameObject.AddComponent<RectMask2D>();
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            var content = NewRect("Content", view);
            content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 0.5f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var hlg = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content; scroll.viewport = view;
            var buttons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                var (b, l) = PillButton(content, labels[i], labels[i], false);
                l.fontSize = _t.fontCaption + 2;
                var le = b.GetComponent<LayoutElement>();
                le.flexibleWidth = 0f;
                le.minWidth = le.preferredWidth = 70f + labels[i].Length * 20f;   // narrow enough that the next plant peeks in: the strip scrolls
                buttons[i] = b;
            }
            y -= 136f;
            return buttons;
        }

        /// <summary>Several rows of equal chips that scroll sideways together (the 33 demo plants).</summary>
        static Button[] ScrollGrid(Transform parent, string name, string[] labels, ref float y, int rows)
        {
            const float cellW = 230f, cellH = 92f, gap = 14f;
            float height = rows * cellH + (rows - 1) * gap;
            var view = NewRect(name, parent);
            Place(view, new Vector2(0, 1), new Vector2(1, 1), new Vector2(52, y - height), new Vector2(-52, y));
            view.gameObject.AddComponent<RectMask2D>();
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            var content = NewRect("Content", view);
            content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 0.5f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var g = content.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(cellW, cellH);
            g.spacing = new Vector2(gap, gap);
            g.startAxis = GridLayoutGroup.Axis.Vertical;          // fill each column top to bottom, then the next column
            g.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            g.constraintCount = rows;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content; scroll.viewport = view;
            var buttons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                var (b, l) = PillButton(content, labels[i], labels[i], false);
                l.fontSize = _t.fontCaption + 2;
                l.enableWordWrapping = false; l.fontSizeMin = 20f;
                buttons[i] = b;
            }
            y -= height + 24f;
            return buttons;
        }

        static Button[] GridChips(Transform parent, string name, int count, int columns, ref float y)
        {
            const float rowHeight = 84f, gap = 12f;
            int rows = (count + columns - 1) / columns;
            float height = rows * rowHeight + (rows - 1) * gap;
            var grid = NewRect(name, parent);
            Place(grid, new Vector2(0, 1), new Vector2(1, 1), new Vector2(52, y - height), new Vector2(-52, y));
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2((1080f - 32f - 104f - gap * (columns - 1)) / columns, rowHeight);
            g.spacing = new Vector2(gap, gap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = columns;
            var buttons = new Button[count];
            for (int i = 0; i < count; i++)
            {
                var (b, l) = PillButton(grid, $"Condition{i}", i == count - 1 ? "Healthy" : "Condition", false);
                l.fontSize = _t.fontCaption + 2;
                l.enableWordWrapping = true; l.fontSizeMin = 20f;
                l.margin = new Vector4(18, 4, 18, 4);
                buttons[i] = b;
            }
            y -= height + 24f;
            return buttons;
        }

        static TMP_InputField Input(Transform parent, string name, string placeholder, ref float y)
        {
            var bg = Img(parent, name, _t.panelSunken, _t.roundedSprite, Radius(30));
            Place(bg.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(52, y - 104), new Vector2(-52, y));
            var input = bg.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = bg;

            var area = NewRect("Text Area", bg.transform);
            Place(area, Vector2.zero, Vector2.one, new Vector2(34, 10), new Vector2(-34, -10));
            area.gameObject.AddComponent<RectMask2D>();

            var ph = Txt(area, "Placeholder", placeholder, _t.fontCaption + 2, _t.textTertiary, _t.fontRegular, TextAlignmentOptions.MidlineLeft);
            Stretch(ph.rectTransform);
            ph.enableWordWrapping = false;
            var text = Txt(area, "Text", "", _t.fontCaption + 2, _t.textPrimary, _t.fontMedium, TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform);
            text.enableWordWrapping = false;
            text.raycastTarget = false;

            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = ph;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.keyboardType = TouchScreenKeyboardType.URL;
            input.caretColor = _t.accent;
            input.selectionColor = new Color(_t.accent.r, _t.accent.g, _t.accent.b, 0.3f);
            if (_t.fontMedium != null) input.fontAsset = _t.fontMedium;
            input.pointSize = _t.fontCaption + 2;
            y -= 122f;
            return input;
        }

        // ==================================================================
        // Components
        // ==================================================================

        static ChipView BuildChip(Transform parent, string name)
        {
            var bg = Img(parent, name, _t.accentSoft, _t.roundedSprite, Radius(_t.chipHeight * 0.5f));
            var view = bg.gameObject.AddComponent<ChipView>();
            var hlg = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(24, 26, 0, 0);
            hlg.spacing = 12;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            var le = bg.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = _t.chipHeight;

            var dot = Img(bg.transform, "Dot", _t.accent, _t.circleSprite);
            var dle = dot.gameObject.AddComponent<LayoutElement>();
            dle.minWidth = dle.preferredWidth = 18; dle.minHeight = dle.preferredHeight = 18;
            dot.raycastTarget = false;

            var label = Txt(bg.transform, "Label", name, _t.fontCaption, _t.textPrimary, _t.fontSemiBold, TextAlignmentOptions.MidlineLeft);
            label.enableWordWrapping = false;

            Wire(view, ("background", bg), ("dot", dot), ("label", label));
            return view;
        }

        static (Button, TMP_Text) PillButton(Transform parent, string name, string label, bool filled)
        {
            var img = Img(parent, name, filled ? _t.accent : _t.panelSunken, _t.roundedSprite, Radius(_t.buttonHeight * 0.5f));
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = _t.buttonHeight;
            le.flexibleWidth = 1f;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            var text = Txt(img.transform, "Label", label, _t.fontButton, filled ? _t.textOnAccent : _t.textPrimary,
                _t.fontSemiBold, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            text.enableWordWrapping = false;
            text.enableAutoSizing = true; text.fontSizeMin = 22; text.fontSizeMax = _t.fontButton;
            return (btn, text);
        }

        static (Button, TMP_Text) TextButton(Transform parent, string name, string label, Color color)
        {
            var img = Img(parent, name, new Color(0, 0, 0, 0.35f), _t.roundedSprite, Radius(55));
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var text = Txt(img.transform, "Label", label, _t.fontCaption + 2, color, _t.fontMedium, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            return (btn, text);
        }

        static void Shadow(RectTransform target)
        {
            var sh = Img(target.parent, target.name + "Shadow", new Color(0, 0, 0, 0.45f), _t.softShadowSprite, Radius(40));
            var rt = sh.rectTransform;
            rt.anchorMin = target.anchorMin; rt.anchorMax = target.anchorMax; rt.pivot = target.pivot;
            rt.offsetMin = target.offsetMin + new Vector2(-28, -36);
            rt.offsetMax = target.offsetMax + new Vector2(28, 20);
            sh.raycastTarget = false;
            sh.transform.SetSiblingIndex(target.GetSiblingIndex());
            // Follow the target if it moves (the sheet animates).
            sh.transform.SetParent(target, true);
            sh.transform.SetAsFirstSibling();
        }

        // ==================================================================
        // Primitives
        // ==================================================================

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image Img(Transform parent, string name, Color color, Sprite sprite, float ppuMultiplier = 1f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            if (sprite != null)
            {
                img.sprite = sprite;
                bool sliced = sprite.border.sqrMagnitude > 0f;
                img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
                img.pixelsPerUnitMultiplier = ppuMultiplier;
                img.preserveAspect = !sliced && sprite != _t.gradientSprite;
            }
            return img;
        }

        /// <summary>Image.pixelsPerUnitMultiplier that renders S_Rounded with the given corner radius.</summary>
        static float Radius(float radius) => UIShapeLibrary.RoundedRadius / Mathf.Max(1f, radius);

        static TMP_Text Txt(Transform parent, string name, string content, float size, Color color,
            TMP_FontAsset font, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.enableWordWrapping = true;
            t.richText = true;
            t.raycastTarget = false;
            return t;
        }

        static FadeSlide AddFade(GameObject go, Vector2 hiddenOffset, bool startVisible)
        {
            go.AddComponent<CanvasGroup>();
            var f = go.AddComponent<FadeSlide>();
            var so = new SerializedObject(f);
            so.FindProperty("hiddenOffset").vector2Value = hiddenOffset;
            so.FindProperty("startVisible").boolValue = startVisible;
            so.ApplyModifiedPropertiesWithoutUndo();
            return f;
        }

        static void Le(GameObject go, float height)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = height;
        }

        static void Vertical(Transform t, float spacing)
        {
            var v = t.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childControlHeight = true; v.childForceExpandHeight = false;
            v.childControlWidth = true; v.childForceExpandWidth = true;
        }

        static void Stretch(Transform t)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        }

        static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
        }

        static Material PreviewMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PreviewMaterialPath);
            if (mat != null) return mat;
            var shader = Shader.Find("PlantDoctor/UI/CameraPreview");
            if (shader == null) return null;
            mat = new Material(shader) { name = "M_UICameraPreview" };
            AssetDatabase.CreateAsset(mat, PreviewMaterialPath);
            return mat;
        }

        public static void Wire(Object target, params (string field, Object value)[] assignments)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in assignments)
            {
                var prop = so.FindProperty(field);
                if (prop == null)
                {
                    Debug.LogWarning($"[AI Plant Doctor] Field '{field}' not found on {target.GetType().Name}.");
                    continue;
                }
                prop.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[AI Plant Doctor] Array '{field}' not found on {target.GetType().Name}."); return; }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Loads the theme, resets it to the current defaults, and attaches fonts and sprites.</summary>
        public static ClassicTheme LoadOrCreateTheme()
        {
            var fresh = ScriptableObject.CreateInstance<ClassicTheme>();
            var theme = AssetDatabase.LoadAssetAtPath<ClassicTheme>(ThemePath);
            if (theme == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
                if (!AssetDatabase.IsValidFolder("Assets/Data/Config")) AssetDatabase.CreateFolder("Assets/Data", "Config");
                AssetDatabase.CreateAsset(fresh, ThemePath);
                theme = fresh;
            }
            else
            {
                // The palette and type scale changed in the redesign; old serialized values
                // (e.g. 17 pt body text) would otherwise silently win over the new defaults.
                EditorUtility.CopySerialized(fresh, theme);
                Object.DestroyImmediate(fresh);
            }

            var fonts = PoppinsFontBuilder.BuildAll();
            fonts.TryGetValue("Regular", out theme.fontRegular);
            fonts.TryGetValue("Medium", out theme.fontMedium);
            fonts.TryGetValue("SemiBold", out theme.fontSemiBold);
            fonts.TryGetValue("Bold", out theme.fontBold);

            theme.roundedSprite = Sprite(UIShapeLibrary.RoundedPath);
            theme.outlineSprite = Sprite(UIShapeLibrary.OutlinePath);
            theme.circleSprite = Sprite(UIShapeLibrary.CirclePath);
            theme.ringSprite = Sprite(UIShapeLibrary.RingPath);
            theme.softShadowSprite = Sprite(UIShapeLibrary.ShadowPath);
            theme.leafSprite = Sprite(UIShapeLibrary.LeafPath);
            theme.checkSprite = Sprite(UIShapeLibrary.CheckPath);
            theme.gearSprite = Sprite(UIShapeLibrary.GearPath);
            theme.cornerSprite = Sprite(UIShapeLibrary.CornerPath);
            theme.gradientSprite = Sprite(UIShapeLibrary.GradientPath);

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            return theme;
        }

        static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
#endif
