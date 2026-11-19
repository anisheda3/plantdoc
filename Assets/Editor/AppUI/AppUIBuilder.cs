#if UNITY_EDITOR
using System.Collections.Generic;
using PlantDoctor.AR;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using PlantDoctor.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static PlantDoctor.EditorTools.UIKit;

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// Builds the whole app UI into the scene from the design tokens and reusable components in
    /// <see cref="UIKit"/>. Screens (one partial file each group) are children of one canvas and
    /// are shown by the ScreenRouter:
    ///
    ///   UI_Canvas
    ///     CameraPreview     (live camera on phones without AR)
    ///     Splash, Home, Scan, Analysis, Diagnosis, ArView, Zoom, Spread, Treatment, Recovery,
    ///     Prevention, Report, Guide, MyPlants, Profile, Error
    ///     BottomNav, Toast, ConfirmDialog
    /// </summary>
    public static partial class AppUIBuilder
    {
        public const string ThemePath = "Assets/Data/Config/UITheme.asset";
        const string IconLibraryPath = "Assets/Data/Config/IconLibrary.asset";
        const string CreditsPath = "Assets/Data/Config/Credits.txt";
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
            public PlantDatabase Database;
            public PlantTwinBuilder ArTwinBuilder;
            public ScanHistory History;
            public PlantDoctor.Diseases.DiseaseSimulationManager Simulation;
            public PlantDoctor.Simulation.RecoveryManager Recovery;
        }

        static Context C;
        static readonly List<ScreenView> Screens = new List<ScreenView>();

        public static GameObject Build(Context c)
        {
            C = c;
            T = UIKit.T = LoadOrCreateTheme();
            Screens.Clear();

            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null) CreateEventSystem();

            var canvasGo = new GameObject("UI_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2340);
            scaler.matchWidthOrHeight = 0f;        // match width: tall phones get more vertical room, not smaller text
            scaler.referencePixelsPerUnit = 100f;
            var root = canvasGo.transform;

            // The plant backdrop sits behind every page; the live camera preview (phones without AR) is above it.
            var backdrop = BuildBackdrop(root);

            var preview = NewRect("CameraPreview", root);
            Stretch(preview);
            var raw = preview.gameObject.AddComponent<RawImage>();
            raw.material = PreviewMaterial();
            raw.raycastTarget = false;
            var previewView = preview.gameObject.AddComponent<CameraPreviewView>();
            Wire(previewView, ("scanController", C.Scan), ("modeController", C.Mode), ("flow", C.Flow));
            WireBool(previewView, "showWhileAnalysing", false);   // the analysis page covers the camera itself

            BuildSplash(root);
            BuildHome(root);
            BuildScan(root);
            BuildAnalysis(root);
            BuildDiagnosis(root);
            BuildArView(root);
            BuildZoom(root);
            BuildJourney(root);
            BuildPrevention(root);
            BuildReport(root);
            BuildProfile(root);
            BuildError(root);

            var toast = BuildToast(root);
            var confirm = BuildConfirm(root);

            var router = canvasGo.AddComponent<ScreenRouter>();
            Wire(router, ("flow", C.Flow), ("theme", T), ("mode", C.Mode), ("twinPlacement", C.TwinPlacement), ("demo", C.Demo),
                ("scan", C.Scan), ("stage", C.Stage), ("database", C.Database), ("binder", C.Binder), ("history", C.History),
                ("simulation", C.Simulation), ("recovery", C.Recovery), ("arTwinBuilder", C.ArTwinBuilder),
                ("backdrop", backdrop), ("toast", toast), ("confirm", confirm));
            WireArray(router, "screens", Screens.ToArray());

            Debug.Log("[AI Plant Doctor] UI built.");
            return canvasGo;
        }

        // ==================================================================
        // Theme and assets
        // ==================================================================

        public static UITheme LoadOrCreateTheme()
        {
            var fresh = ScriptableObject.CreateInstance<UITheme>();
            var theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
            if (theme == null)
            {
                EnsureFolder("Assets/Data/Config");
                AssetDatabase.CreateAsset(fresh, ThemePath);
                theme = fresh;
            }
            else
            {
                // Tokens live in code; reset the asset so an old saved value never wins over the design.
                EditorUtility.CopySerialized(fresh, theme);
                Object.DestroyImmediate(fresh);
            }

            var fonts = FontAssetBuilder.BuildAll();
            fonts.TryGetValue("Regular", out theme.fontRegular);
            fonts.TryGetValue("Medium", out theme.fontMedium);
            fonts.TryGetValue("SemiBold", out theme.fontSemiBold);
            fonts.TryGetValue("Bold", out theme.fontBold);

            theme.roundedSprite = Sprite(UIShapeLibrary.RoundedPath);
            theme.outlineSprite = Sprite(UIShapeLibrary.OutlinePath);
            theme.circleSprite = Sprite(UIShapeLibrary.CirclePath);
            theme.ringSprite = Sprite(UIShapeLibrary.RingPath);
            theme.softShadowSprite = Sprite(UIShapeLibrary.ShadowPath);
            theme.gradientSprite = Sprite(UIShapeLibrary.GradientPath);
            theme.arcSprite = Sprite(UIShapeLibrary.ArcPath);
            theme.glowSprite = Sprite(UIShapeLibrary.GlowPath);
            theme.icons = BuildIconLibrary();

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            return theme;
        }

        static IconLibrary BuildIconLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<IconLibrary>(IconLibraryPath);
            if (library == null)
            {
                EnsureFolder("Assets/Data/Config");
                library = ScriptableObject.CreateInstance<IconLibrary>();
                AssetDatabase.CreateAsset(library, IconLibraryPath);
            }
            var names = new List<string>();
            var sprites = new List<Sprite>();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Art/Icons" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;
                names.Add(System.IO.Path.GetFileNameWithoutExtension(path));
                sprites.Add(sprite);
            }
            library.Set(names.ToArray(), sprites.ToArray());
            EditorUtility.SetDirty(library);
            return library;
        }

        static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

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

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // ==================================================================
        // Shared building blocks used by several screens
        // ==================================================================

        const string Photos = "Assets/Art/Photos";

        /// <summary>A photo that fills a rect edge to edge (no rounded corners).</summary>
        static (RectTransform rect, RawImage raw, CoverImage cover) FullPhoto(Transform parent, string name, Texture texture)
        {
            var rt = NewRect(name, parent);
            Stretch(rt);
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            raw.texture = texture;
            var cover = rt.gameObject.AddComponent<CoverImage>();
            return (rt, raw, cover);
        }

        static T AddScreen<T>(RectTransform root) where T : ScreenView
        {
            var view = root.gameObject.AddComponent<T>();
            Screens.Add(view);
            return view;
        }

        /// <summary>Height of the call-to-action bar inside a sheet.</summary>
        const float CtaHeight = 236f;

        /// <summary>
        /// A page made of a header over the plant and a bottom sheet with a scrolling body and an optional
        /// call-to-action. The caller tells the page how tall the sheet is, so the plant can use the rest.
        /// </summary>
        static (RectTransform root, RectTransform safe, ScrollRect scroll, VerticalLayoutGroup content, Button back, Button right, Button cta, TextMeshProUGUI ctaLabel)
            SheetPage(Transform parent, string name, string title, string rightIcon, string ctaLabel, string ctaIcon, float sheetHeight)
        {
            var (root, safe, _) = PageRoot(parent, name, Color.clear);
            var (header, back, _, right) = AppHeader(safe, title, true, rightIcon);
            var (bg, sheet) = Sheet(safe, sheetHeight);

            float ctaSpace = string.IsNullOrEmpty(ctaLabel) ? 0f : CtaHeight;
            var body = NewRect("Body", sheet);
            Place(body, Vector2.zero, Vector2.one, new Vector2(0, ctaSpace), new Vector2(0, -46));
            var (scroll, content) = VScroll(body, "Scroll", 24, -1, 6, 40);
            Stretch((RectTransform)scroll.transform);

            Button cta = null; TextMeshProUGUI label = null;
            if (ctaSpace > 0f)
            {
                var (bar, button, text) = CtaBar(sheet, ctaLabel, ctaIcon);
                cta = button; label = text;
            }
            return (root, safe, scroll, content, back, right, cta, label);
        }

        /// <summary>A heading plus optional caption used to open a section in a scrolling page.</summary>
        static TextMeshProUGUI SectionTitle(Transform parent, string text, string name = "SectionTitle")
        {
            var t = Heading(parent, name, text, T.fontH3 + 2);
            Elem(t.gameObject, -1, 54, -1, -1, 1, 0);
            return t;
        }

        /// <summary>A white card that grows with its content; add children to card.transform.</summary>
        static (Image card, VerticalLayoutGroup layout) PaddedCard(Transform parent, string name, int pad = 34, float spacing = 14, Color? color = null)
        {
            var card = Card(parent, name, color);
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(pad, pad, pad, pad);
            layout.spacing = spacing;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var fitter = card.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return (card, layout);
        }

        // ------------------------------------------------------------------
        // Plant backdrop, toast, confirmation dialog
        // ------------------------------------------------------------------

        /// <summary>The app's one background: gradient, a soft light and the plant, behind every page.</summary>
        static StageBackdrop BuildBackdrop(Transform parent)
        {
            var root = NewRect("Backdrop", parent);
            Stretch(root);
            var group = root.gameObject.AddComponent<CanvasGroup>();

            var baseColor = Box(root, "Base", T.backdropBottom);
            Stretch(baseColor.rectTransform);
            var gradient = Gradient(root, "Gradient", T.backdropTop);
            Stretch(gradient.rectTransform);

            var glow = NewRect("Glow", root).gameObject.AddComponent<Image>();
            glow.sprite = T.glowSprite;
            glow.color = new Color(T.leaf.r, T.leaf.g, T.leaf.b, 0.30f);
            glow.raycastTarget = false;

            var plantRt = NewRect("PlantPicture", root);
            Stretch(plantRt);
            var plantRaw = plantRt.gameObject.AddComponent<RawImage>();
            plantRaw.raycastTarget = true;
            plantRaw.color = new Color(1, 1, 1, 0);
            plantRt.gameObject.AddComponent<Viewport3DInput>();

            var view = root.gameObject.AddComponent<StageBackdrop>();
            Wire(view, ("group", group), ("gradient", gradient), ("glow", glow), ("plantPicture", plantRaw));
            root.SetAsFirstSibling();
            return view;
        }

        static ToastView BuildToast(Transform parent)
        {
            var root = NewRect("Toast", parent);
            Place(root, new Vector2(0, 0), new Vector2(1, 0), new Vector2(48, 250), new Vector2(-48, 250 + 120));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var bg = Box(root, "Background", new Color(0.05f, 0.13f, 0.09f, 0.96f), 34);
            Stretch(bg.rectTransform);
            var text = Text(root, "Message", "", T.fontSmall + 1, T.textOnDark, T.fontMedium, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 30, 10, 30, 10);
            var view = root.gameObject.AddComponent<ToastView>();
            Wire(view, ("message", text), ("background", bg));
            group.alpha = 0f;
            return view;
        }

        static ConfirmDialogView BuildConfirm(Transform parent)
        {
            var root = NewRect("ConfirmDialog", parent);
            Stretch(root);
            var scrim = Box(root, "Scrim", new Color(0.04f, 0.1f, 0.07f, 0.62f), 0, true);
            Stretch(scrim.rectTransform);
            var scrimButton = scrim.gameObject.AddComponent<Button>();
            scrimButton.transition = Selectable.Transition.None;

            var card = Card(root, "Card", new Color(0.06f, 0.14f, 0.10f, 0.97f), 44);
            var cardStroke = Outline(card.transform, "Stroke", T.glassStroke, 44); Stretch(cardStroke.rectTransform);
            Pin(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(920, 560), Vector2.zero);
            var stack = VStack(card.transform, "Stack", 22, 48, 48, 48, 40);
            Stretch(stack.transform);
            var title = Heading(stack.transform, "Title", "Title", T.fontH2);
            var message = Body(stack.transform, "Message", "Message");
            Elem(message.gameObject, -1, 130, -1, -1, 1, 1);
            var row = HStack(stack.transform, "Buttons", 20, 0, 0, 0, 0, TextAnchor.MiddleCenter, true);
            Elem(row.gameObject, -1, T.buttonHeight - 16, -1, T.buttonHeight - 16, 1, 0);
            var (cancel, cancelLabel, _, cancelBg) = ButtonBase(row.transform, "Cancel", "Cancel", null, T.ivoryDeep, T.textPrimary, T.buttonHeight - 16, T.buttonRadius);
            var (confirm, confirmLabel, _, _) = ButtonBase(row.transform, "Confirm", "Confirm", null, T.forest, T.textOnDark, T.buttonHeight - 16, T.buttonRadius);
            Elem(cancel.gameObject, -1, -1, -1, -1, 1, 0); Elem(confirm.gameObject, -1, -1, -1, -1, 1, 0);

            var view = root.gameObject.AddComponent<ConfirmDialogView>();
            Wire(view, ("title", title), ("message", message), ("confirmButton", confirm), ("confirmLabel", confirmLabel),
                ("cancelButton", cancel), ("cancelLabel", cancelLabel), ("scrim", scrimButton));
            root.gameObject.SetActive(false);
            return view;
        }
    }
}
#endif
