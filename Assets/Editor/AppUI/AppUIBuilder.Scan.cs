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
        static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);

        // ==================================================================
        // 1. Splash
        // ==================================================================


        // ==================================================================
        // 2. Home dashboard (+ My Plants rows)
        // ==================================================================

        static (RectTransform card, ScanRowView row) BuildScanRow(Transform parent, string name)
        {
            var card = Card(parent, name, null, 32);
            Elem(card.gameObject, -1, 176, -1, 176, 1, 0);
            var btn = MakeButton(card.gameObject, card);

            var row = HStack(card.transform, "Row", 24, 22, 22, 26, 22, TextAnchor.MiddleLeft, true);
            Stretch(row.transform);
            var (frame, raw, cover) = Photo(row.transform, "Thumb", null, 26);
            Fixed(frame.gameObject, 132, 132);
            raw.color = T.sageSoft;

            var info = VStack(row.transform, "Info", 6, 0, 0, 0, 0, TextAnchor.MiddleLeft, true);
            Elem(info.gameObject, -1, -1, -1, -1, 1, 0);
            var plant = Heading(info.transform, "Plant", "Tomato", T.fontH3 - 2);
            var cond = Text(info.transform, "Condition", "Early Blight", T.fontSmall + 2, T.textSecondary, T.fontRegular);
            var time = Text(info.transform, "Time", "Today, 10:24 AM", T.fontMeta + 1, T.textTertiary, T.fontRegular);

            var right = VStack(row.transform, "Right", 8, 0, 0, 0, 0, TextAnchor.MiddleRight, false);
            right.childForceExpandWidth = false;
            var chip = Chip(right.transform, "Chip", "Moderate", "info", 50);
            Icon(right.transform, "Chevron", "chevron-right", 46, T.textTertiary);

            var view = card.gameObject.AddComponent<ScanRowView>();
            Wire(view, ("photo", raw), ("cover", cover), ("title", plant), ("subtitle", cond), ("time", time), ("chip", chip), ("button", btn));
            return (card.rectTransform, view);
        }



        // ==================================================================
        // 3. Scan
        // ==================================================================

        static QualityIndicatorView QualityIndicator(Transform parent, string name, string label, string icon)
        {
            var col = VStack(parent, name, 8, 0, 0, 0, 0, TextAnchor.MiddleCenter, false);
            Elem(col.gameObject, -1, -1, -1, -1, 1, 1);
            col.childForceExpandWidth = false;
            var ringHolder = NewRect("Holder", col.transform);
            Fixed(ringHolder.gameObject, 104, 104);
            var ring = Icon(ringHolder, "Ring", "circle", 104, Color.white);
            Pin(ring.rectTransform, Mid, new Vector2(104, 104), Vector2.zero);
            var ic = Icon(ringHolder, "Icon", icon, 52, Color.white);
            Pin(ic.rectTransform, Mid, new Vector2(52, 52), Vector2.zero);
            var l = Text(col.transform, "Label", label, T.fontSmall, T.textOnDark, T.fontSemiBold, TextAlignmentOptions.Center, wrap: false);
            var s = Text(col.transform, "Status", "Checking", T.fontMeta, T.textOnDarkMuted, T.fontRegular, TextAlignmentOptions.Center, wrap: false);
            var view = col.gameObject.AddComponent<QualityIndicatorView>();
            Wire(view, ("ring", ring), ("icon", ic), ("label", l), ("status", s));
            return view;
        }

        static void BuildScan(Transform parent)
        {
            var (root, safe, _) = PageRoot(parent, "Scan", Color.clear);
            var view = AddScreen<ScanScreen>(root);
            Backdrop(view, 0, 170, 0f, false);   // the live camera is the backdrop here

            var topFade = Gradient(root, "TopFade", new Color(T.forestDeep.r, T.forestDeep.g, T.forestDeep.b, 0.82f));
            Place(topFade.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -560), Vector2.zero);
            topFade.transform.SetAsFirstSibling();
            var bottomFade = Gradient(root, "BottomFade", new Color(T.forestDeep.r, T.forestDeep.g, T.forestDeep.b, 0.9f), flipped: true);
            Place(bottomFade.rectTransform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 900));
            bottomFade.transform.SetSiblingIndex(1);

            // Top controls
            var (back, _, _) = CircleIcon(safe, "Back", "chevron-left", 100, T.glassDark, Color.white, 56);
            Pin((RectTransform)back.transform, new Vector2(0, 1), new Vector2(100, 100), new Vector2(34, -26));
            var (demo, demoLabel, _, _) = ButtonBase(safe, "Demo", "Try demo", "play", T.glassDark, Color.white, 84, 42);
            Fixed(demo.gameObject, 310, 84);
            demoLabel.fontSize = T.fontSmall;
            Pin((RectTransform)demo.transform, new Vector2(1, 1), new Vector2(310, 84), new Vector2(-34, -34));

            var instrBg = Box(safe, "InstructionBg", T.glassDark, 44);
            Pin(instrBg.rectTransform, new Vector2(0.5f, 1), new Vector2(900, 92), new Vector2(0, -160));
            var instr = Text(instrBg.transform, "Instruction", "Position a single leaf within the frame", T.fontH3 - 4, T.textOnDark, T.fontMedium, TextAlignmentOptions.Center, wrap: false);
            Stretch(instr.rectTransform, 20, 0, 20, 0);

            // The framing guide: this rectangle is the region that is checked and analysed.
            var area = NewRect("FrameArea", safe);
            Place(area, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 660), new Vector2(0, -310));
            var reticle = NewRect("Reticle", area);
            Pin(reticle, Mid, new Vector2(800, 960), Vector2.zero);
            var cornerSprite = Sprite(UIShapeLibrary.CornerPath);
            var corners = new Image[4];
            var anchors = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            var flips = new[] { new Vector3(1, 1, 1), new Vector3(-1, 1, 1), new Vector3(1, -1, 1), new Vector3(-1, -1, 1) };
            for (int i = 0; i < 4; i++)
            {
                var c = NewRect("Corner" + i, reticle);
                var img = c.gameObject.AddComponent<Image>();
                img.sprite = cornerSprite; img.color = Color.white; img.raycastTarget = false;
                Pin(c, anchors[i], new Vector2(160, 160), Vector2.zero, anchors[i]);
                c.localScale = flips[i];
                corners[i] = img;
            }

            // Live guidance under the frame (the real validation message, never invented).
            var bannerBg = Box(safe, "StatusBanner", new Color(T.amber.r, T.amber.g, T.amber.b, 0.92f), 40);
            Pin(bannerBg.rectTransform, new Vector2(0.5f, 0), new Vector2(820, 80), new Vector2(0, 560), new Vector2(0.5f, 0));
            var banner = Text(bannerBg.transform, "Text", "Checking the view…", T.fontSmall + 3, Color.white, T.fontSemiBold, TextAlignmentOptions.Center, wrap: false);
            Stretch(banner.rectTransform, 24, 0, 24, 0);

            // Three live checks
            var panel = Box(safe, "QualityPanel", T.glassDark, 44);
            Pin(panel.rectTransform, new Vector2(0.5f, 0), new Vector2(984, 240), new Vector2(0, 300), new Vector2(0.5f, 0));
            var qRow = HStack(panel.transform, "Row", 0, 20, 10, 20, 10, TextAnchor.MiddleCenter, true);
            Stretch(qRow.transform);
            qRow.childForceExpandWidth = true;
            var light = QualityIndicator(qRow.transform, "Light", "Light", "sun");
            var focus = QualityIndicator(qRow.transform, "Focus", "Focus", "scan");
            var leaf = QualityIndicator(qRow.transform, "Leaf", "Leaf", "leaf");

            // Shutter
            var shutterHolder = NewRect("Shutter", safe);
            Pin(shutterHolder, new Vector2(0.5f, 0), new Vector2(200, 200), new Vector2(0, 70), new Vector2(0.5f, 0));
            var ring = Circle(shutterHolder, "Ring", Color.white, true);
            ring.sprite = T.ringSprite;
            Stretch(ring.rectTransform);
            var core = Circle(shutterHolder, "Core", Color.white);
            Pin(core.rectTransform, Mid, new Vector2(150, 150), Vector2.zero);
            var leafMark = Icon(core.transform, "Leaf", "leaf", 70, T.forest);
            Pin(leafMark.rectTransform, Mid, new Vector2(70, 70), Vector2.zero);
            var shutter = MakeButton(shutterHolder.gameObject, ring);

            Wire(view, ("reticle", reticle), ("instruction", instr), ("statusBanner", banner), ("statusBannerBackground", bannerBg),
                ("lightIndicator", light), ("focusIndicator", focus), ("leafIndicator", leaf),
                ("backButton", back), ("shutter", shutter), ("shutterRing", ring), ("shutterCore", core), ("demoButton", demo));
            WireArray(view, "corners", corners);
        }

        // ==================================================================
        // 4. Analysis
        // ==================================================================



        static Color Hex(string hex) => UITheme.Hex(hex);
    }
}
#endif
