#if UNITY_EDITOR
using PlantDoctor.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static PlantDoctor.EditorTools.UIKit;

namespace PlantDoctor.EditorTools
{
    public static partial class AppUIBuilder
    {
        // ==================================================================
        // 1. Splash
        // ==================================================================

        static void BuildSplash(Transform parent)
        {
            var (root, safe, _) = PageRoot(parent, "Splash", Color.clear);
            var view = AddScreen<SplashScreen>(root);
            Backdrop(view, 900, 120);

            var logo = NewRect("Logo", safe);
            Pin(logo, new Vector2(0.5f, 0f), new Vector2(190, 190), new Vector2(0, 690));
            var frame = Icon(logo, "Frame", "scan", 190, T.leaf);
            Pin(frame.rectTransform, Mid, new Vector2(190, 190), Vector2.zero);
            var sprout = Icon(logo, "Sprout", "sprout", 96, T.textPrimary);
            Pin(sprout.rectTransform, Mid, new Vector2(96, 96), new Vector2(0, -2));

            var title = Text(safe, "Title", "Plant Doctor", 92, T.textPrimary, T.fontBold, TextAlignmentOptions.Center, wrap: false);
            Pin(title.rectTransform, new Vector2(0.5f, 0f), new Vector2(980, 120), new Vector2(0, 500));
            var tagline = Text(safe, "Tagline", "SCAN  ·  DIAGNOSE  ·  TREAT  ·  GROW", T.fontSmall - 2, T.leaf, T.fontSemiBold, TextAlignmentOptions.Center, wrap: false);
            tagline.characterSpacing = 6f;
            Pin(tagline.rectTransform, new Vector2(0.5f, 0f), new Vector2(980, 60), new Vector2(0, 420));

            var spinner = Icon(safe, "Spinner", "circle", 64, new Color(1, 1, 1, 0.85f));
            spinner.sprite = T.arcSprite;
            Pin(spinner.rectTransform, new Vector2(0.5f, 0f), new Vector2(64, 64), new Vector2(0, 250));
            spinner.gameObject.AddComponent<Spinner>();

            var slogan = Text(safe, "Slogan", "Healthier plants, greener tomorrow", T.fontSmall, T.textSecondary, T.fontRegular, TextAlignmentOptions.Center, wrap: false);
            Pin(slogan.rectTransform, new Vector2(0.5f, 0f), new Vector2(980, 60), new Vector2(0, 130));
        }

        // ==================================================================
        // 2. Home: the plant, and one thing to do
        // ==================================================================

        static void BuildHome(Transform parent)
        {
            var (root, safe, _) = PageRoot(parent, "Home", Color.clear);
            var view = AddScreen<HomeScreen>(root);
            Backdrop(view, 780, 230);

            // Top bar: wordmark and the gear.
            var brand = HStack(safe, "Brand", 16, 0, 0, 0, 0, TextAnchor.MiddleLeft, false);
            Pin((RectTransform)brand.transform, new Vector2(0, 1), new Vector2(700, 100), new Vector2(T.pagePadding, -34), new Vector2(0, 1));
            Icon(brand.transform, "Mark", "sprout", 60, T.leaf);
            var wordmark = Text(brand.transform, "Wordmark", "Plant Doctor", T.fontH3 + 4, T.textPrimary, T.fontSemiBold, TextAlignmentOptions.MidlineLeft, wrap: false);
            Elem(wordmark.gameObject, -1, -1, -1, -1, 0, 0);

            var (profile, pBg, _) = CircleIcon(safe, "Profile", "settings", 104, T.glassDark, T.textPrimary, 52);
            Pin((RectTransform)profile.transform, new Vector2(1, 1), new Vector2(104, 104), new Vector2(-T.pagePadding, -30), new Vector2(1, 1));
            var pStroke = Outline(pBg.transform, "Stroke", T.glassStroke, 52);
            pStroke.sprite = T.ringSprite; pStroke.type = Image.Type.Simple;
            Stretch(pStroke.rectTransform); Ignore(pStroke.gameObject);

            // Bottom: a line of welcome and the scan button.
            var greeting = Text(safe, "Greeting", "Keep your plants healthy", 56, T.textPrimary, T.fontSemiBold, TextAlignmentOptions.Center, wrap: false);
            Pin(greeting.rectTransform, new Vector2(0.5f, 0f), new Vector2(980, 84), new Vector2(0, 600));
            var sub = Text(safe, "Subtitle", "Scan one leaf to check it for disease", T.fontBody - 2, T.textSecondary, T.fontRegular, TextAlignmentOptions.Center, wrap: false);
            Pin(sub.rectTransform, new Vector2(0.5f, 0f), new Vector2(980, 56), new Vector2(0, 530));

            var holder = NewRect("ScanButton", safe);
            Pin(holder, new Vector2(0.5f, 0f), new Vector2(360, 360), new Vector2(0, 170));
            var halo = Circle(holder, "Halo", new Color(T.forest.r, T.forest.g, T.forest.b, 0.20f));
            Pin(halo.rectTransform, Mid, new Vector2(360, 360), Vector2.zero);
            var ring = Icon(holder, "Ring", "circle", 306, new Color(T.leaf.r, T.leaf.g, T.leaf.b, 0.60f));
            ring.sprite = T.ringSprite;
            Pin(ring.rectTransform, Mid, new Vector2(306, 306), Vector2.zero);
            var core = Circle(holder, "Core", T.forest, true);
            Pin(core.rectTransform, Mid, new Vector2(244, 244), Vector2.zero);
            Glow(core.rectTransform, new Color(T.forest.r, T.forest.g, T.forest.b, 0.55f), 40f);
            var coreIcon = Icon(core.transform, "Icon", "scan-line", 112, T.textOnDark);
            Pin(coreIcon.rectTransform, Mid, new Vector2(112, 112), Vector2.zero);
            var scan = MakeButton(core.gameObject, core);

            var label = Text(safe, "ScanLabel", "Scan a plant", T.fontH3, T.textPrimary, T.fontSemiBold, TextAlignmentOptions.Center, wrap: false);
            Pin(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(700, 60), new Vector2(0, 96));

            Wire(view, ("greeting", greeting), ("scanButton", scan), ("profileButton", profile));
        }

        // ==================================================================
        // 4. Analysis
        // ==================================================================

        static AnalysisStepRow AnalysisRow(Transform parent, string label)
        {
            var row = HStack(parent, "Step", 26, 0, 0, 0, 0, TextAnchor.MiddleLeft, false);
            Elem(row.gameObject, -1, 76, -1, 76, 1, 0);
            var holder = NewRect("Indicator", row.transform);
            Fixed(holder.gameObject, 52, 52);
            var done = Icon(holder, "Done", "circle-check", 52, T.leaf);
            Pin(done.rectTransform, Mid, new Vector2(52, 52), Vector2.zero);
            var spinner = Icon(holder, "Active", "circle", 52, new Color(1, 1, 1, 0.95f));
            spinner.sprite = T.arcSprite;
            Pin(spinner.rectTransform, Mid, new Vector2(52, 52), Vector2.zero);
            spinner.gameObject.AddComponent<Spinner>();
            var pending = Icon(holder, "Pending", "circle", 52, new Color(1, 1, 1, 0.35f));
            Pin(pending.rectTransform, Mid, new Vector2(52, 52), Vector2.zero);
            var text = Text(row.transform, "Label", label, T.fontH3 - 4, T.textOnDark, T.fontMedium, TextAlignmentOptions.MidlineLeft, wrap: false);
            Elem(text.gameObject, -1, -1, -1, -1, 1, 0);

            var view = row.gameObject.AddComponent<AnalysisStepRow>();
            Wire(view, ("doneIcon", done.gameObject), ("activeSpinner", spinner.gameObject), ("pendingIcon", pending.gameObject), ("label", text));
            return view;
        }

        static void BuildAnalysis(Transform parent)
        {
            var (root, safe, _) = PageRoot(parent, "Analysis", Color.clear);
            var view = AddScreen<AnalysisScreen>(root);
            Backdrop(view, 0, 170, 0.30f);

            var (back, _, _) = CircleIcon(safe, "Back", "chevron-left", 100, T.glassDark, T.textPrimary, 54);
            Pin((RectTransform)back.transform, new Vector2(0, 1), new Vector2(100, 100), new Vector2(34, -26));

            // One quiet arc turns while the request is out (no invented percentage).
            var ringHolder = NewRect("Ring", safe);
            Pin(ringHolder, new Vector2(0.5f, 1f), new Vector2(320, 320), new Vector2(0, -250));
            var track = Icon(ringHolder, "Track", "circle", 320, new Color(1, 1, 1, 0.14f));
            track.sprite = T.ringSprite;
            Pin(track.rectTransform, Mid, new Vector2(320, 320), Vector2.zero);
            var arc = Icon(ringHolder, "Arc", "circle", 320, T.leaf);
            arc.sprite = T.arcSprite;
            Pin(arc.rectTransform, Mid, new Vector2(320, 320), Vector2.zero);
            arc.gameObject.AddComponent<Spinner>();
            var sprout = Icon(ringHolder, "Sprout", "sprout", 140, T.leaf);
            Pin(sprout.rectTransform, Mid, new Vector2(140, 140), Vector2.zero);

            var headline = Text(safe, "Headline", "Analysing your plant…", 56, T.textOnDark, T.fontSemiBold, TextAlignmentOptions.Center, wrap: false);
            Pin(headline.rectTransform, new Vector2(0.5f, 1f), new Vector2(980, 90), new Vector2(0, -640));

            var panel = Card(safe, "StepsPanel", new Color(0.04f, 0.11f, 0.075f, 0.62f), 44);
            Pin(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(900, 520), new Vector2(0, -780), new Vector2(0.5f, 1f));
            var steps = VStack(panel.transform, "Steps", 4, 56, 36, 40, 30, TextAnchor.UpperLeft, true);
            Stretch(steps.transform);
            string[] labels = { "Processing image", "Identifying plant species", "Detecting disease patterns", "Generating 3D plant model", "Preparing treatment plan" };
            var rows = new AnalysisStepRow[5];
            for (int i = 0; i < 5; i++) rows[i] = AnalysisRow(steps.transform, labels[i]);

            var caption = Text(safe, "Caption", "", T.fontSmall, T.textOnDarkMuted, T.fontRegular, TextAlignmentOptions.Center);
            Pin(caption.rectTransform, new Vector2(0.5f, 1f), new Vector2(860, 90), new Vector2(0, -1340), new Vector2(0.5f, 1f));

            var tip = Card(safe, "Tip", new Color(0.04f, 0.11f, 0.075f, 0.62f), 38);
            Pin(tip.rectTransform, new Vector2(0.5f, 0), new Vector2(984, 170), new Vector2(0, 90), new Vector2(0.5f, 0));
            var tipRow = HStack(tip.transform, "Row", 26, 34, 0, 34, 0, TextAnchor.MiddleLeft, true);
            Stretch(tipRow.transform);
            Icon(tipRow.transform, "Bulb", "lightbulb", 64, T.amber);
            var tipText = VStack(tipRow.transform, "Text", 4, 0, 0, 0, 0, TextAnchor.MiddleLeft, true);
            Elem(tipText.gameObject, -1, -1, -1, -1, 1, 0);
            Text(tipText.transform, "Title", "Tip", T.fontH3 - 4, T.textOnDark, T.fontBold);
            Text(tipText.transform, "Body", "Good light and a sharp photo make the answer more reliable.", T.fontSmall, T.textOnDarkMuted, T.fontRegular);

            Wire(view, ("backButton", back), ("caption", caption), ("headline", headline));
            WireArray(view, "rows", rows);
        }

        // ==================================================================
        // 6. Plant view (AR or the plant in its backdrop)
        // ==================================================================

        static void BuildArView(Transform parent)
        {
            var (root, safe, _) = PageRoot(parent, "ArView", Color.clear);
            var view = AddScreen<ArViewScreen>(root);
            Backdrop(view, 330, 230);

            // The plant itself is in the backdrop (or in the AR world); this page only adds controls.
            var overlay = NewRect("Overlay", root);
            Stretch(overlay);
            overlay.SetSiblingIndex(0);

            // Highlight rings on the affected leaves
            var markers = new RectTransform[3];
            for (int i = 0; i < 3; i++)
            {
                var m = NewRect("Marker" + i, overlay);
                Pin(m, Mid, new Vector2(128, 128), Vector2.zero);
                var ring = Circle(m, "Ring", new Color(T.amber.r, T.amber.g, T.amber.b, 0.95f));
                ring.sprite = T.ringSprite; Stretch(ring.rectTransform);
                var dot = Circle(m, "Dot", T.amber);
                Pin(dot.rectTransform, Mid, new Vector2(26, 26), Vector2.zero);
                m.gameObject.AddComponent<Pulse>();
                m.gameObject.SetActive(false);
                markers[i] = m;
            }

            // Condition label, beside the first highlight
            var card = Card(overlay, "ConditionCard", new Color(0.05f, 0.13f, 0.09f, 0.90f), 36);
            Pin(card.rectTransform, Mid, new Vector2(430, 118), new Vector2(-220, 300));
            var cStroke = Outline(card.transform, "Stroke2", T.glassStroke, 36); Stretch(cStroke.rectTransform); Ignore(cStroke.gameObject);
            var cRow = HStack(card.transform, "Row", 18, 20, 0, 20, 0, TextAnchor.MiddleLeft, true);
            Stretch(cRow.transform);
            var alert = Circle(cRow.transform, "Alert", T.red);
            Fixed(alert.gameObject, 72, 72);
            var alertIcon = Icon(alert.transform, "Icon", "triangle-alert", 40, Color.white);
            Pin(alertIcon.rectTransform, Mid, new Vector2(40, 40), Vector2.zero);
            var cText = VStack(cRow.transform, "Text", 0, 0, 0, 0, 0, TextAnchor.MiddleLeft, true);
            Elem(cText.gameObject, -1, -1, -1, -1, 1, 0);
            var cTitle = Text(cText.transform, "Title", "Early Blight", T.fontSmall + 1, T.textPrimary, T.fontBold, TextAlignmentOptions.MidlineLeft, wrap: false);
            var cSub = Text(cText.transform, "Sub", "Affected leaf", T.fontMeta, T.textSecondary, T.fontRegular, TextAlignmentOptions.MidlineLeft, wrap: false);
            card.gameObject.SetActive(false);

            // Top: back + AR View / Plant View
            var (back, _, _) = CircleIcon(safe, "Back", "chevron-left", 100, T.glassDark, T.textPrimary, 54);
            Pin((RectTransform)back.transform, new Vector2(0, 1), new Vector2(100, 100), new Vector2(34, -26));

            var toggle = Box(safe, "Toggle", T.glassDark, 52, true);
            Pin(toggle.rectTransform, new Vector2(0.5f, 1), new Vector2(560, 104), new Vector2(40, -24), new Vector2(0.5f, 1));
            var tStroke = Outline(toggle.transform, "Stroke", T.glassStroke, 52); Stretch(tStroke.rectTransform); Ignore(tStroke.gameObject);
            var tRow = HStack(toggle.transform, "Row", 6, 8, 8, 8, 8, TextAnchor.MiddleCenter, true);
            Stretch(tRow.transform);
            tRow.childForceExpandWidth = true;
            var modeButtons = new Button[2]; var modeBgs = new Image[2]; var modeLabels = new TextMeshProUGUI[2];
            string[] modeNames = { "AR View", "Plant View" };
            for (int i = 0; i < 2; i++)
            {
                var item = Box(tRow.transform, modeNames[i].Replace(" ", ""), Color.clear, 44, true);
                Elem(item.gameObject, -1, -1, -1, -1, 1, 1);
                modeButtons[i] = MakeButton(item.gameObject, item);
                modeBgs[i] = item;
                modeLabels[i] = Text(item.transform, "Label", modeNames[i], T.fontSmall + 2, T.textOnDark, T.fontSemiBold, TextAlignmentOptions.Center, wrap: false);
                Stretch(modeLabels[i].rectTransform);
            }
            var note = Text(safe, "ArUnavailable", "AR isn't available on this device, so you are seeing the plant view.", T.fontMeta + 1, T.textOnDarkMuted, T.fontMedium, TextAlignmentOptions.Center);
            Pin(note.rectTransform, new Vector2(0.5f, 1), new Vector2(860, 80), new Vector2(0, -150), new Vector2(0.5f, 1));
            note.gameObject.SetActive(false);

            // Right: rotate / scale / move
            var tools = VStack(safe, "Tools", 16, 0, 0, 0, 0, TextAnchor.MiddleCenter, false);
            Pin((RectTransform)tools.transform, new Vector2(1, 0.5f), new Vector2(150, 520), new Vector2(-34, 90), new Vector2(1, 0.5f));
            tools.childForceExpandWidth = false;
            var (rotate, _, _, _) = ToolButton(tools.transform, "Rotate", "rotate-3d", "Rotate");
            var (scale, _, _, _) = ToolButton(tools.transform, "Scale", "maximize-2", "Scale");
            var (move, moveBg, _, _) = ToolButton(tools.transform, "Move", "move", "Move");
            var moveGroup = moveBg.gameObject.AddComponent<CanvasGroup>();

            // Bottom: the two ways on
            var tabsRow = HStack(safe, "Tabs", 18, 0, 0, 0, 0, TextAnchor.MiddleCenter, true);
            Place((RectTransform)tabsRow.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 44), new Vector2(-40, 236));
            tabsRow.childForceExpandWidth = true;
            string[] tabNames = { "Zoom in", "Cure journey" };
            string[] tabSubs = { "Look closely at a leaf", "Diseased to recovered" };
            string[] tabIconNames = { "zoom-in", "sparkles" };
            var tabButtons = new Button[2]; var tabIcons = new Image[2]; var tabLabels = new TextMeshProUGUI[2]; var tabDots = new GameObject[2];
            for (int i = 0; i < 2; i++)
            {
                var item = Box(tabsRow.transform, tabNames[i], i == 1 ? T.forest : T.glassDark, 52, true);
                Elem(item.gameObject, -1, -1, -1, -1, 1, 1);
                if (i == 0) { var tabStroke = Outline(item.transform, "Stroke", T.glassStroke, 52); Stretch(tabStroke.rectTransform); Ignore(tabStroke.gameObject); }
                else Glow(item.rectTransform, new Color(T.forest.r, T.forest.g, T.forest.b, 0.40f), 22f);
                tabButtons[i] = MakeButton(item.gameObject, item);
                var row = HStack(item.transform, "Row", 20, 30, 0, 24, 0, TextAnchor.MiddleLeft, true);
                Stretch(row.transform);
                row.childForceExpandHeight = false;   // keep the icon circle round
                var holder = Circle(row.transform, "IconCircle", new Color(1, 1, 1, 0.16f));
                Fixed(holder.gameObject, 84, 84);
                tabIcons[i] = Icon(holder.transform, "Icon", tabIconNames[i], 44, T.textOnDark);
                Pin(tabIcons[i].rectTransform, Mid, new Vector2(44, 44), Vector2.zero);
                var col = VStack(row.transform, "Text", 2, 0, 0, 0, 0, TextAnchor.MiddleLeft, true);
                Elem(col.gameObject, -1, -1, -1, -1, 1, 0);
                tabLabels[i] = Text(col.transform, "Label", tabNames[i], T.fontSmall + 2, T.textOnDark, T.fontSemiBold, TextAlignmentOptions.MidlineLeft, wrap: false);
                Text(col.transform, "Sub", tabSubs[i], T.fontMeta - 3, i == 1 ? T.textOnDarkMuted : T.textSecondary, T.fontRegular, TextAlignmentOptions.MidlineLeft, wrap: false);
                var dot = Circle(item.transform, "NextDot", T.amber);
                Pin(dot.rectTransform, new Vector2(1, 1), new Vector2(22, 22), new Vector2(-22, -22));
                dot.gameObject.SetActive(false);
                tabDots[i] = dot.gameObject;
            }

            // Placement help (AR only)
            var hint = Box(safe, "PlacementHint", T.glassDark, 44, true);
            Pin(hint.rectTransform, new Vector2(0.5f, 0.3f), new Vector2(920, 270), Vector2.zero);
            var hStroke = Outline(hint.transform, "Stroke", T.glassStroke, 44); Stretch(hStroke.rectTransform); Ignore(hStroke.gameObject);
            var hStack = VStack(hint.transform, "Stack", 16, 36, 26, 36, 26, TextAnchor.MiddleCenter, true);
            Stretch(hStack.transform);
            var hText = Text(hStack.transform, "Text", "Point the phone at the floor and move it slowly", T.fontSmall + 2, T.textOnDark, T.fontMedium, TextAlignmentOptions.Center);
            var (placeNow, _, _, _) = ButtonBase(hStack.transform, "PlaceNow", "Place here", null, T.forest, T.textOnDark, 100, 50);
            placeNow.gameObject.SetActive(false);
            hint.gameObject.SetActive(false);

            Wire(view, ("backButton", back), ("overlayRoot", overlay),
                ("conditionCard", card.rectTransform), ("conditionTitle", cTitle), ("conditionSubtitle", cSub), ("arUnavailableNote", note),
                ("rotateButton", rotate), ("scaleButton", scale), ("moveButton", move), ("moveGroup", moveGroup),
                ("placementHint", hint.gameObject), ("placementHintText", hText), ("placeNowButton", placeNow));
            WireArray(view, "modeButtons", modeButtons);
            WireArray(view, "modeBackgrounds", modeBgs);
            WireArray(view, "modeLabels", modeLabels);
            WireArray(view, "markers", markers);
            WireArray(view, "tabButtons", tabButtons);
            WireArray(view, "tabIcons", tabIcons);
            WireArray(view, "tabLabels", tabLabels);
            WireArray(view, "tabNextDots", tabDots);
        }

        // ==================================================================
        // 7. Magnified leaf
        // ==================================================================

        static void BuildZoom(Transform parent)
        {
            const float sheetHeight = 780f;
            var (root, safe, _) = PageRoot(parent, "Zoom", Color.clear);
            var view = AddScreen<ZoomScreen>(root);
            Backdrop(view, sheetHeight, 170, 0.14f);
            var (header, back, _, _) = AppHeader(safe, "Magnified leaf", true, null);

            // The leaf: a full-screen picture with a transparent background; drag to turn it.
            var leafRt = NewRect("LeafPicture", root);
            Stretch(leafRt);
            var leafRaw = leafRt.gameObject.AddComponent<RawImage>();
            leafRaw.raycastTarget = true;
            leafRaw.color = new Color(1, 1, 1, 0);
            leafRt.gameObject.AddComponent<Viewport3DInput>();
            leafRt.SetSiblingIndex(0);

            // Your scan / the simulation map, when a thumbnail is chosen
            var (photoFrame, photoRaw, photoCover) = Photo(root, "ScanPicture", null, 48);
            Place(photoFrame, new Vector2(0.07f, 0f), new Vector2(0.93f, 1f), new Vector2(0, sheetHeight + 40), new Vector2(0, -200));
            photoFrame.GetComponent<Image>().color = new Color(1, 1, 1, 0.06f);
            photoFrame.SetSiblingIndex(1);

            var layer = NewRect("Callouts", root);
            Stretch(layer);
            layer.SetSiblingIndex(2);
            var callouts = new[] { Callout(layer, "Callout0"), Callout(layer, "Callout1"), Callout(layer, "Callout2") };

            var badge = Box(photoFrame, "Badge", T.glassDark, 30);
            Pin(badge.rectTransform, new Vector2(0, 1), new Vector2(700, 70), new Vector2(26, -26), new Vector2(0, 1));
            var badgeText = Text(badge.transform, "Text", "Your scan", T.fontMeta + 1, T.textOnDark, T.fontMedium, TextAlignmentOptions.MidlineLeft, wrap: false);
            Stretch(badgeText.rectTransform, 24, 0, 24, 0);
            badge.gameObject.SetActive(false);

            // The sheet: three views of the leaf, a note, and the way on.
            var (sheetBg, sheet) = Sheet(safe, sheetHeight);
            var stack = VStack(sheet, "Stack", 22, (int)T.pagePadding, 52, (int)T.pagePadding, 0, TextAnchor.UpperLeft, true);
            Place((RectTransform)stack.transform, Vector2.zero, Vector2.one, new Vector2(0, CtaHeight), new Vector2(0, 0));

            var thumbRow = HStack(stack.transform, "Thumbs", 22, 0, 0, 0, 0, TextAnchor.UpperLeft, false);
            Elem(thumbRow.gameObject, -1, 232, -1, 232, 1, 0);
            thumbRow.childForceExpandWidth = false;
            string[] thumbNames = { "3D leaf", "Your scan", "Spots map" };
            var thumbButtons = new Button[3]; var thumbCovers = new CoverImage[3]; var thumbFrames = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var col = VStack(thumbRow.transform, "Thumb" + i, 8, 0, 0, 0, 0, TextAnchor.UpperCenter, false);
                Fixed(col.gameObject, 190, 232);
                var (frame, raw, cover) = Photo(col.transform, "Frame", null, 30);
                Fixed(frame.gameObject, 190, 190);
                raw.color = new Color(1, 1, 1, 0.08f);
                var outline = Outline(frame, "Outline", Color.white, 30);
                Stretch(outline.rectTransform);
                thumbButtons[i] = MakeButton(frame.gameObject, frame.GetComponent<Image>());
                thumbCovers[i] = cover; thumbFrames[i] = outline;
                Text(col.transform, "Label", thumbNames[i], T.fontMeta - 1, T.textSecondary, T.fontMedium, TextAlignmentOptions.Center, wrap: false);
            }

            var tipCard = Card(stack.transform, "Tip", null, 34);
            var tipRow = tipCard.gameObject.AddComponent<HorizontalLayoutGroup>();
            tipRow.padding = new RectOffset(30, 30, 24, 24); tipRow.spacing = 22; tipRow.childAlignment = TextAnchor.MiddleLeft;
            tipRow.childControlWidth = true; tipRow.childControlHeight = true; tipRow.childForceExpandWidth = false; tipRow.childForceExpandHeight = false;
            FitHeight(tipCard.gameObject);
            Icon(tipCard.transform, "Bulb", "lightbulb", 54, T.amber);
            var tip = Text(tipCard.transform, "Text", "", T.fontSmall - 1, T.textSecondary, T.fontRegular);
            Elem(tip.gameObject, -1, -1, -1, -1, 1, 0);

            var (bar, cta, ctaLabel) = CtaBar(sheet, "Next: the cure journey", "sparkles");

            Wire(view, ("backButton", back), ("leafImage", leafRaw), ("calloutLayer", layer), ("photoView", photoCover),
                ("photoBadge", badgeText), ("leafThumbnail", Tex("Assets/Art/Leaf/leaf_single.png")), ("tip", tip), ("nextButton", cta));
            WireArray(view, "callouts", callouts);
            WireArray(view, "thumbButtons", thumbButtons);
            WireArray(view, "thumbImages", thumbCovers);
            WireArray(view, "thumbFrames", thumbFrames);
        }

        // ==================================================================
        // 8. The cure journey: infection, treatment and recovery in one animation
        // ==================================================================

        static void BuildJourney(Transform parent)
        {
            const float sheetHeight = 1320f;
            var (root, safe, _) = PageRoot(parent, "Journey", Color.clear);
            var view = AddScreen<JourneyScreen>(root);
            Backdrop(view, sheetHeight, 170);
            var (header, back, _, _) = AppHeader(safe, "From diseased to recovered", true, null);

            // A small leaf beside the plant, in the same state.
            var inset = NewRect("LeafInset", safe);
            Pin(inset, new Vector2(1, 1), new Vector2(250, 250), new Vector2(-44, -190), new Vector2(1, 1));
            var insetBg = Circle(inset, "Bg", new Color(0.04f, 0.11f, 0.075f, 0.55f));
            Stretch(insetBg.rectTransform);
            var insetMask = NewRect("Mask", inset);
            Stretch(insetMask, 6, 6, 6, 6);
            var maskImg = insetMask.gameObject.AddComponent<Image>();
            maskImg.sprite = T.circleSprite; maskImg.color = Color.white;
            insetMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var insetRaw = NewRect("Leaf", insetMask).gameObject.AddComponent<RawImage>();
            insetRaw.raycastTarget = false;
            insetRaw.color = new Color(1, 1, 1, 0);
            Stretch(insetRaw.rectTransform);
            insetRaw.uvRect = new Rect(0.2f, 0.36f, 0.6f, 0.277f);   // the picture is screen-shaped; the inset shows its middle
            var insetRing = Icon(inset, "Ring", "circle", 250, new Color(1, 1, 1, 0.30f));
            insetRing.sprite = T.ringSprite;
            Pin(insetRing.rectTransform, Mid, new Vector2(250, 250), Vector2.zero);

            var (sheetBg, sheet) = Sheet(safe, sheetHeight);

            // Pinned top: phase tabs, the stage, the timeline.
            var pinned = VStack(sheet, "Pinned", 14, (int)T.pagePadding, 50, (int)T.pagePadding, 0, TextAnchor.UpperLeft, true);
            Place((RectTransform)pinned.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -700), new Vector2(0, 0));

            var phaseTabs = Tabs(pinned.transform, "Phases", new[] { "Infection", "Treatment", "Recovery" }, SegmentedTabs.Style.Pills, 88);

            var meta = Text(pinned.transform, "Meta", "Infection · stage 4 of 4", T.fontMeta, T.leaf, T.fontSemiBold, TextAlignmentOptions.TopLeft, wrap: false);
            meta.characterSpacing = 2f;
            Elem(meta.gameObject, -1, 40, -1, 40, 1, 0);
            var title = Text(pinned.transform, "Title", "Diseased, as scanned", T.fontH2 - 2, T.textPrimary, T.fontBold, TextAlignmentOptions.TopLeft);
            title.enableAutoSizing = true; title.fontSizeMin = 30; title.fontSizeMax = T.fontH2 - 2; title.overflowMode = TextOverflowModes.Ellipsis; title.enableWordWrapping = false;
            Elem(title.gameObject, -1, 62, -1, 62, 1, 0);
            var body = Text(pinned.transform, "Body", "", T.fontSmall, T.textSecondary, T.fontRegular);
            body.enableAutoSizing = true; body.fontSizeMin = 22; body.fontSizeMax = T.fontSmall; body.overflowMode = TextOverflowModes.Ellipsis;
            Elem(body.gameObject, -1, 124, -1, 124, 1, 0);

            var player = HStack(pinned.transform, "Player", 20, 0, 0, 0, 0, TextAnchor.MiddleLeft, false);
            Elem(player.gameObject, -1, 120, -1, 120, 1, 0);
            var (play, _, playIcon) = CircleIcon(player.transform, "Play", "play", 108, T.forest, T.textOnDark, 50);
            Glow(((RectTransform)play.transform), new Color(T.forest.r, T.forest.g, T.forest.b, 0.45f), 20f);
            var timeline = NewRect("Timeline", player.transform);
            Elem(timeline.gameObject, -1, 112, -1, 112, 1, 0);
            var slider = BuildSlider(timeline, "Progress");
            Stretch((RectTransform)slider.transform);
            var tickRoot = NewRect("Ticks", timeline);
            Place(tickRoot, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(24, -8), new Vector2(-24, 8));
            tickRoot.SetSiblingIndex(1);   // above the track, below the handle

            var chipRow = HStack(pinned.transform, "Outcome", 10, 0, 0, 0, 0, TextAnchor.MiddleLeft, false);
            Elem(chipRow.gameObject, -1, 56, -1, 56, 1, 0);
            var chip = Chip(chipRow.transform, "OutcomeChip", "Diseased", "triangle-alert");
            chip.gameObject.SetActive(false);

            // Scrolling below: the treatment steps the user can mark as done.
            var body2 = NewRect("Body", sheet);
            Place(body2, Vector2.zero, Vector2.one, new Vector2(0, CtaHeight), new Vector2(0, -700));
            var (scroll, content) = VScroll(body2, "Scroll", 22, -1, 8, 30);
            Stretch((RectTransform)scroll.transform);
            var stepsTitle = Heading(content.transform, "StepsTitle", "Your treatment steps", T.fontH3);
            var stepsHint = Text(content.transform, "StepsHint", "", T.fontSmall - 1, T.textSecondary, T.fontRegular);
            var list = VStack(content.transform, "Steps", 22, 0, 0, 0, 0);
            var template = StepCard(list.transform);
            var disclaimer = Text(content.transform, "Disclaimer", "", T.fontMeta, T.textTertiary, T.fontRegular);

            var (bar, cta, ctaLabel) = CtaBar(sheet, "Prevention tips", "shield-check");

            Wire(view, ("backButton", back), ("leafInset", insetRaw), ("phaseTabs", phaseTabs), ("stageMeta", meta), ("stageTitle", title),
                ("stageBody", body), ("playButton", play), ("playIcon", playIcon), ("progress", slider), ("tickRoot", tickRoot),
                ("outcomeChip", chip), ("stepsRoot", (RectTransform)list.transform), ("stepTemplate", template), ("stepsTitle", stepsTitle),
                ("stepsHint", stepsHint), ("disclaimer", disclaimer), ("nextButton", cta), ("scroll", scroll));
        }
    }
}
#endif
