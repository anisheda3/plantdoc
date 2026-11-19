#if UNITY_EDITOR
using PlantDoctor.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// Factory for the reusable UI components, all styled from <see cref="UITheme"/>: cards,
    /// buttons, chips, headers, tabs, icons and photos. Screens are assembled from these, so a
    /// change to a token or a component changes every screen.
    /// </summary>
    public static class UIKit
    {
        public static UITheme T;

        // ------------------------------------------------------------------
        // Geometry
        // ------------------------------------------------------------------

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Stretch(Transform t, float left = 0, float bottom = 0, float right = 0, float top = 0) =>
            Stretch((RectTransform)t, left, bottom, right, top);

        public static RectTransform Place(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
            return rt;
        }

        /// <summary>Fixed-size element anchored to one point (anchor = pivot unless given).</summary>
        public static RectTransform Pin(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        public static LayoutElement Elem(GameObject go, float minW = -1, float minH = -1, float prefW = -1, float prefH = -1,
            float flexW = -1, float flexH = -1)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.minWidth = minW; le.minHeight = minH; le.preferredWidth = prefW; le.preferredHeight = prefH;
            le.flexibleWidth = flexW; le.flexibleHeight = flexH;
            return le;
        }

        public static LayoutElement Fixed(GameObject go, float w, float h) => Elem(go, w, h, w, h, 0, 0);

        public static LayoutElement Ignore(GameObject go)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            return le;
        }

        // ------------------------------------------------------------------
        // Layout groups
        // ------------------------------------------------------------------

        public static VerticalLayoutGroup VStack(Transform parent, string name, float spacing = 0, int l = 0, int t = 0, int r = 0, int b = 0,
            TextAnchor align = TextAnchor.UpperLeft, bool expandWidth = true, bool controlHeight = true)
        {
            var rt = NewRect(name, parent);
            var g = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing; g.padding = new RectOffset(l, r, t, b); g.childAlignment = align;
            g.childControlWidth = true; g.childControlHeight = controlHeight;
            g.childForceExpandWidth = expandWidth; g.childForceExpandHeight = false;
            return g;
        }

        public static HorizontalLayoutGroup HStack(Transform parent, string name, float spacing = 0, int l = 0, int t = 0, int r = 0, int b = 0,
            TextAnchor align = TextAnchor.MiddleLeft, bool expandHeight = false, bool controlWidth = true)
        {
            var rt = NewRect(name, parent);
            var g = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing; g.padding = new RectOffset(l, r, t, b); g.childAlignment = align;
            g.childControlWidth = controlWidth; g.childControlHeight = true;
            g.childForceExpandWidth = false; g.childForceExpandHeight = expandHeight;
            return g;
        }

        public static ContentSizeFitter FitHeight(GameObject go)
        {
            var f = go.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            return f;
        }

        // ------------------------------------------------------------------
        // Visuals
        // ------------------------------------------------------------------

        /// <summary>A flat or rounded box. radius 0 = square corners.</summary>
        public static Image Box(Transform parent, string name, Color color, float radius = 0f, bool raycast = false)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            if (radius > 0f && T.roundedSprite != null)
            {
                img.sprite = T.roundedSprite;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = UIShapeLibrary.RoundedRadius / radius;
            }
            return img;
        }

        public static Image Circle(Transform parent, string name, Color color, bool raycast = false)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = T.circleSprite; img.color = color; img.raycastTarget = raycast;
            return img;
        }

        public static Image Outline(Transform parent, string name, Color color, float radius)
        {
            var img = Box(parent, name, color, radius);
            img.sprite = T.outlineSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UIShapeLibrary.RoundedRadius / radius;
            return img;
        }

        /// <summary>Soft drop shadow behind a card; ignored by layout groups.</summary>
        public static Image Shadow(RectTransform target, float spread = 26f, float alpha = 0.10f)
        {
            var shadow = Box(target, "Shadow", new Color(T.forest.r, T.forest.g, T.forest.b, alpha));
            shadow.sprite = T.softShadowSprite;
            shadow.type = Image.Type.Sliced;
            shadow.pixelsPerUnitMultiplier = 1.15f;
            Place(shadow.rectTransform, Vector2.zero, Vector2.one, new Vector2(-spread, -spread * 1.35f), new Vector2(spread, spread * 0.55f));
            shadow.transform.SetAsFirstSibling();
            Ignore(shadow.gameObject);
            return shadow;
        }

        /// <summary>A frosted-glass card: faint white fill and a hairline border, readable over the plant backdrop.</summary>
        public static Image Card(Transform parent, string name, Color? color = null, float? radius = null, bool shadow = false)
        {
            float r = radius ?? T.cardRadius;
            var card = Box(parent, name, color ?? T.white, r, raycast: true);
            if (shadow) Shadow(card.rectTransform);
            if (color == null || color.Value.a < 0.5f)
            {
                var stroke = Outline(card.transform, "Stroke", T.glassStroke, r);
                Stretch(stroke.rectTransform);
                Ignore(stroke.gameObject);
            }
            return card;
        }

        /// <summary>A soft coloured glow behind a button (ignored by layout groups).</summary>
        public static Image Glow(RectTransform target, Color color, float spread = 34f)
        {
            var glow = Box(target, "Glow", color);
            glow.sprite = T.softShadowSprite;
            glow.type = Image.Type.Sliced;
            glow.pixelsPerUnitMultiplier = 1.15f;
            Place(glow.rectTransform, Vector2.zero, Vector2.one, new Vector2(-spread, -spread * 1.1f), new Vector2(spread, spread * 0.7f));
            glow.transform.SetAsFirstSibling();
            Ignore(glow.gameObject);
            return glow;
        }

        public static Image Gradient(Transform parent, string name, Color color, bool flipped = false)
        {
            var img = Box(parent, name, color);
            img.sprite = T.gradientSprite;
            if (flipped) img.rectTransform.localScale = new Vector3(1f, -1f, 1f);
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
            TMP_FontAsset font = null, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, bool wrap = true)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font != null ? font : T.fontRegular;
            t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.enableWordWrapping = wrap;
            t.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        public static TextMeshProUGUI Heading(Transform parent, string name, string text, float? size = null, Color? color = null,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft) =>
            Text(parent, name, text, size ?? T.fontH2, color ?? T.textPrimary, T.fontBold, align);

        public static TextMeshProUGUI Body(Transform parent, string name, string text, Color? color = null, float? size = null,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var t = Text(parent, name, text, size ?? T.fontBody, color ?? T.textSecondary, T.fontRegular, align);
            t.lineSpacing = 8f;
            return t;
        }

        public static Image Icon(Transform parent, string name, string icon, float size, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = T.Icon(icon);
            img.preserveAspect = true;
            img.color = color;
            img.raycastTarget = false;
            if (img.sprite == null) Debug.LogWarning($"[AI Plant Doctor] Missing icon '{icon}'");
            rt.sizeDelta = new Vector2(size, size);
            Fixed(rt.gameObject, size, size);
            return img;
        }

        /// <summary>A photo clipped to rounded corners and cropped to fill ("cover").</summary>
        public static (RectTransform frame, RawImage raw, CoverImage cover) Photo(Transform parent, string name, Texture texture, float radius)
        {
            var frame = Box(parent, name, Color.white, radius);
            var mask = frame.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var raw = NewRect("Photo", frame.transform).gameObject.AddComponent<RawImage>();
            Stretch(raw.rectTransform);
            raw.raycastTarget = false;
            var cover = raw.gameObject.AddComponent<CoverImage>();
            raw.texture = texture;
            raw.color = texture != null ? Color.white : new Color(1, 1, 1, 0);
            return (frame.rectTransform, raw, cover);
        }

        // ------------------------------------------------------------------
        // Buttons
        // ------------------------------------------------------------------

        public static Button MakeButton(GameObject go, Graphic target = null)
        {
            var b = go.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            if (target != null) b.targetGraphic = target;
            go.AddComponent<PressFeedback>();
            return b;
        }

        public static (Button button, TextMeshProUGUI label, Image icon, Image background) ButtonBase(Transform parent, string name, string label,
            string iconName, Color fill, Color textColor, float height, float radius, bool outlined = false, Color? outline = null)
        {
            var bg = Box(parent, name, fill, radius, raycast: true);
            var btn = MakeButton(bg.gameObject, bg);
            Fixed(bg.gameObject, -1, height); Elem(bg.gameObject, -1, height, -1, height, 1, 0);
            if (outlined) { var o = Outline(bg.transform, "Outline", outline ?? T.divider, radius); Stretch(o.rectTransform); }

            var row = HStack(bg.transform, "Row", 18, 28, 0, 28, 0, TextAnchor.MiddleCenter, true);
            Stretch(row.transform);
            row.childForceExpandWidth = false;
            Image icon = null;
            if (!string.IsNullOrEmpty(iconName)) icon = Icon(row.transform, "Icon", iconName, 48, textColor);
            var text = Text(row.transform, "Label", label, T.fontButton, textColor, T.fontSemiBold, TextAlignmentOptions.Center, wrap: false);
            Elem(text.gameObject, -1, -1, -1, -1, 0, 0);
            return (btn, text, icon, bg);
        }

        public static (Button, TextMeshProUGUI) Primary(Transform parent, string name, string label, string icon = null, bool onDark = false)
        {
            var (b, t, _, bg) = ButtonBase(parent, name, label, icon, T.forest, T.textOnDark, T.buttonHeight, T.buttonRadius);
            Glow(bg.rectTransform, new Color(T.forest.r, T.forest.g, T.forest.b, 0.40f), 26f);
            return (b, t);
        }

        public static (Button, TextMeshProUGUI) Secondary(Transform parent, string name, string label, string icon = null)
        {
            var (b, t, _, _) = ButtonBase(parent, name, label, icon, T.white, T.textPrimary, T.buttonHeight - 8, T.buttonRadius, true, T.glassStroke);
            return (b, t);
        }

        public static (Button, TextMeshProUGUI) Subtle(Transform parent, string name, string label, string icon = null)
        {
            var (b, t, _, _) = ButtonBase(parent, name, label, icon, T.sageSoft, T.leaf, T.buttonHeight - 24, T.buttonRadius);
            return (b, t);
        }

        public static (Button button, Image background, Image icon) CircleIcon(Transform parent, string name, string iconName, float size,
            Color bg, Color fg, float iconSize = 0f)
        {
            var circle = Circle(parent, name, bg, raycast: true);
            circle.rectTransform.sizeDelta = new Vector2(size, size);
            Fixed(circle.gameObject, size, size);
            var btn = MakeButton(circle.gameObject, circle);
            var icon = Icon(circle.transform, "Icon", iconName, iconSize > 0 ? iconSize : size * 0.5f, fg);
            Pin(icon.rectTransform, new Vector2(0.5f, 0.5f), icon.rectTransform.sizeDelta, Vector2.zero);
            return (btn, circle, icon);
        }

        // ------------------------------------------------------------------
        // Chips, tabs, header
        // ------------------------------------------------------------------

        /// <summary>A status chip template; the screen binds its text and tone at runtime.</summary>
        public static StatusChipView Chip(Transform parent, string name, string text = "Status", string iconName = null, float height = 56f)
        {
            var bg = Box(parent, name, T.sageSoft, height * 0.5f);
            var view = bg.gameObject.AddComponent<StatusChipView>();
            // The chip itself is the layout group, so its fitter can measure the content.
            var row = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 10; row.padding = new RectOffset(22, 24, 0, 0); row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true; row.childControlHeight = true; row.childForceExpandWidth = false; row.childForceExpandHeight = true;
            var icon = Icon(bg.transform, "Icon", iconName ?? "circle", 30, T.leaf);
            var label = Text(bg.transform, "Label", text, T.fontMeta, T.leaf, T.fontSemiBold, TextAlignmentOptions.Center, wrap: false);
            Elem(label.gameObject, -1, -1, -1, -1, 0, 0);
            if (iconName == null) icon.gameObject.SetActive(false);
            Elem(bg.gameObject, -1, height, -1, height, 0, 0);
            var fitter = bg.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            Wire(view, ("background", bg), ("icon", icon), ("label", label));
            return view;
        }

        public static SegmentedTabs Tabs(Transform parent, string name, string[] labels, SegmentedTabs.Style style, float height = 96f)
        {
            bool pills = style == SegmentedTabs.Style.Pills;
            var track = pills ? Box(parent, name, T.ivoryDeep, height * 0.5f, true) : Box(parent, name, Color.clear, 0f, false);
            Elem(track.gameObject, -1, height, -1, height, 1, 0);
            var row = HStack(track.transform, "Row", pills ? 6 : 0, pills ? 8 : 0, pills ? 8 : 0, pills ? 8 : 0, pills ? 8 : 0, TextAnchor.MiddleCenter, true);
            Stretch(row.transform);
            row.childForceExpandWidth = true;

            var buttons = new Button[labels.Length];
            var backgrounds = new Image[labels.Length];
            var texts = new TextMeshProUGUI[labels.Length];
            var underlines = new Image[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                var item = Box(row.transform, "Tab" + i, Color.clear, pills ? height * 0.5f - 8f : 0f, true);
                Elem(item.gameObject, -1, -1, -1, -1, 1, 1);
                buttons[i] = MakeButton(item.gameObject, item);
                backgrounds[i] = item;
                texts[i] = Text(item.transform, "Label", labels[i], T.fontSmall + 1, T.textSecondary, T.fontSemiBold, TextAlignmentOptions.Center, wrap: false);
                Stretch(texts[i].transform);
                if (!pills)
                {
                    var u = Box(item.transform, "Underline", Color.clear, 4f);
                    Place(u.rectTransform, new Vector2(0.15f, 0f), new Vector2(0.85f, 0f), new Vector2(0, 0), new Vector2(0, 8));
                    underlines[i] = u;
                }
            }
            var tabs = track.gameObject.AddComponent<SegmentedTabs>();
            var so = new SerializedObject(tabs);
            so.FindProperty("style").enumValueIndex = (int)style;
            so.ApplyModifiedPropertiesWithoutUndo();
            WireArray(tabs, "buttons", buttons);
            WireArray(tabs, "backgrounds", backgrounds);
            WireArray(tabs, "labels", texts);
            if (!pills) WireArray(tabs, "underlines", underlines);
            return tabs;
        }

        /// <summary>Back arrow + title (+ optional right action). Returns the pieces for wiring.</summary>
        public static (RectTransform header, Button back, TextMeshProUGUI title, Button right) AppHeader(Transform parent, string title,
            bool onDark = true, string rightIcon = null)
        {
            var header = NewRect("Header", parent);
            Place(header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -T.headerHeight), new Vector2(0, 0));

            var (back, backBg, _) = CircleIcon(header, "Back", "chevron-left", 100, T.glassDark, T.textPrimary, 54);
            Pin(((RectTransform)back.transform), new Vector2(0, 0.5f), new Vector2(100, 100), new Vector2(T.pagePadding - 10f, 0));
            var backStroke = Outline(backBg.transform, "Stroke", T.glassStroke, 50);
            backStroke.sprite = T.ringSprite; backStroke.type = Image.Type.Simple;
            Stretch(backStroke.rectTransform); Ignore(backStroke.gameObject);

            var text = Text(header, "Title", title, T.fontH3 + 2, T.textPrimary, T.fontSemiBold, TextAlignmentOptions.Center, wrap: false);
            Place(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(T.pagePadding + 120f, 0), new Vector2(-(T.pagePadding + 120f), 0));

            Button right = null;
            if (!string.IsNullOrEmpty(rightIcon))
            {
                var (rb, rbg, _) = CircleIcon(header, "Right", rightIcon, 100, T.glassDark, T.textPrimary, 50);
                Pin((RectTransform)rb.transform, new Vector2(1, 0.5f), new Vector2(100, 100), new Vector2(-(T.pagePadding - 10f), 0));
                var rs = Outline(rbg.transform, "Stroke", T.glassStroke, 50);
                rs.sprite = T.ringSprite; rs.type = Image.Type.Simple;
                Stretch(rs.rectTransform); Ignore(rs.gameObject);
                right = rb;
            }
            return (header, back, text, right);
        }

        // ------------------------------------------------------------------
        // Pages
        // ------------------------------------------------------------------

        /// <summary>Full-screen page root with a background colour and a safe-area child.</summary>
        public static (RectTransform root, RectTransform safe, CanvasGroup group) PageRoot(Transform parent, string name, Color background)
        {
            var root = NewRect(name, parent);
            Stretch(root);
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = background;
            bg.raycastTarget = background.a > 0.01f;   // a see-through page must not swallow touches meant for AR
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var safe = NewRect("Safe", root);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            return (root, safe, group);
        }

        /// <summary>A vertical scroll area. Add children to content (a VStack).</summary>
        public static (ScrollRect scroll, VerticalLayoutGroup content) VScroll(Transform parent, string name, float spacing = 28f, int padH = -1, int padTop = 8, int padBottom = 40)
        {
            int ph = padH < 0 ? (int)T.pagePadding : padH;
            var area = NewRect(name, parent);
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            var viewport = NewRect("Viewport", area);
            Stretch(viewport);
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0);          // catches drags anywhere in the area
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = VStack(viewport, "Content", spacing, ph, padTop, ph, padBottom);
            var rt = (RectTransform)content.transform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(0, 0); rt.offsetMax = new Vector2(0, 0);
            FitHeight(rt.gameObject);

            scroll.viewport = viewport;
            scroll.content = rt;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.scrollSensitivity = 40f;
            scroll.decelerationRate = 0.12f;
            return (scroll, content);
        }

        /// <summary>The bar at the bottom of a sheet holding the page's next recommended action.</summary>
        public static (RectTransform bar, Button button, TextMeshProUGUI label) CtaBar(Transform parent, string label, string icon = null,
            bool onDark = true)
        {
            var bar = NewRect("CtaBar", parent);
            Place(bar, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 236));
            var holder = NewRect("Holder", bar);
            Place(holder, Vector2.zero, Vector2.one, new Vector2(T.pagePadding, 50), new Vector2(-T.pagePadding, -22));
            var (btn, text, _, bg) = ButtonBase(holder, "Primary", label, icon, T.forest, T.textOnDark, T.buttonHeight, T.buttonRadius);
            Stretch(bg.rectTransform);
            Glow(bg.rectTransform, new Color(T.forest.r, T.forest.g, T.forest.b, 0.40f), 24f);
            return (bar, btn, text);
        }

        /// <summary>
        /// The bottom sheet of a page: a dark translucent panel with a drag handle, anchored to the bottom
        /// of the safe area and reaching past the screen edge so its lower corners never show.
        /// Returns the background and a content rect of exactly the sheet's visible height.
        /// </summary>
        public static (RectTransform bg, RectTransform content) Sheet(RectTransform safe, float height)
        {
            var bg = Box(safe, "SheetBackground", T.sheet, 64, raycast: true);
            Place(bg.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, -420), new Vector2(0, height));
            var stroke = Outline(bg.transform, "Stroke", T.glassStroke, 64);
            Stretch(stroke.rectTransform);
            var handle = Box(bg.transform, "Handle", new Color(1, 1, 1, 0.26f), 6);
            Pin(handle.rectTransform, new Vector2(0.5f, 1), new Vector2(120, 10), new Vector2(0, -22), new Vector2(0.5f, 1));

            var content = NewRect("SheetContent", safe);
            Place(content, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, height));
            return (bg.rectTransform, content);
        }

        /// <summary>Tells a page how much of the screen its sheet covers, so the plant can take the rest.</summary>
        public static void Backdrop(Object view, float sheetHeight, float topInset = 170f, float plantOpacity = 1f, bool show = true)
        {
            var so = new SerializedObject(view);
            so.FindProperty("sheetHeight").floatValue = sheetHeight;
            so.FindProperty("heroTopInset").floatValue = topInset;
            so.FindProperty("plantOpacity").floatValue = plantOpacity;
            so.FindProperty("showBackdrop").boolValue = show;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------
        // Wiring helpers
        // ------------------------------------------------------------------

        public static void Wire(Object target, params (string field, Object value)[] assignments)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            foreach (var (field, value) in assignments)
            {
                var prop = so.FindProperty(field);
                if (prop == null) { Debug.LogWarning($"[AI Plant Doctor] Field '{field}' not found on {target.GetType().Name}."); continue; }
                prop.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[AI Plant Doctor] Array '{field}' not found on {target.GetType().Name}."); return; }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[AI Plant Doctor] Field '{field}' not found on {target.GetType().Name}."); return; }
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[AI Plant Doctor] Field '{field}' not found on {target.GetType().Name}."); return; }
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[AI Plant Doctor] Field '{field}' not found on {target.GetType().Name}."); return; }
            prop.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static Texture2D Tex(string path)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null) Debug.LogWarning($"[AI Plant Doctor] Missing texture {path}");
            return t;
        }
    }
}
#endif
