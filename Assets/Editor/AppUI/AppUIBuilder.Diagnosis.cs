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
        // 5. Diagnosis result
        // ==================================================================

        static BulletRowView BulletRow(Transform parent, string name, float size = -1)
        {
            var row = HStack(parent, name, 22, 0, 0, 0, 0, TextAnchor.UpperLeft, false);
            // The dot sits in a holder as tall as one line, so it lines up with the first line of text.
            var dotHolder = NewRect("DotHolder", row.transform);
            Fixed(dotHolder.gameObject, 22, 46);
            var dot = Circle(dotHolder, "Dot", T.amber);
            Pin(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(20, 20), Vector2.zero);
            var text = Body(row.transform, "Text", "Bullet", T.textPrimary, size > 0 ? size : T.fontBody - 1);
            Elem(text.gameObject, -1, -1, -1, -1, 1, 0);
            var view = row.gameObject.AddComponent<BulletRowView>();
            Wire(view, ("dot", dot), ("text", text));
            row.childAlignment = TextAnchor.UpperLeft;
            return view;
        }

        static void BuildDiagnosis(Transform parent)
        {
            var (root, safe, scroll, content, back, right, cta, ctaLabel) = SheetPage(parent, "Diagnosis", "Diagnosis", "file-text", "View in AR", "box", 1320f);
            var view = AddScreen<DiagnosisScreen>(root);
            Backdrop(view, 1320f);
            var ctaIcon = cta.transform.Find("Row/Icon").GetComponent<Image>();

            // Summary: scan photo, names and status chips
            var summary = Card(content.transform, "Summary");
            var sRow = summary.gameObject.AddComponent<HorizontalLayoutGroup>();
            sRow.padding = new RectOffset(30, 30, 30, 30); sRow.spacing = 30; sRow.childAlignment = TextAnchor.UpperLeft;
            sRow.childControlWidth = true; sRow.childControlHeight = true; sRow.childForceExpandWidth = false; sRow.childForceExpandHeight = false;
            FitHeight(summary.gameObject);
            var (frame, raw, cover) = Photo(summary.transform, "ScanPhoto", null, 30);
            Fixed(frame.gameObject, 240, 300);
            raw.color = T.sageSoft;

            var info = VStack(summary.transform, "Info", 6, 0, 0, 0, 0, TextAnchor.UpperLeft, true);
            Elem(info.gameObject, -1, -1, -1, -1, 1, 0);
            Text(info.transform, "PlantCaption", "Plant", T.fontMeta, T.textTertiary, T.fontMedium);
            var plantName = Text(info.transform, "PlantName", "Tomato", 52, T.textPrimary, T.fontBold, TextAlignmentOptions.TopLeft, wrap: false);
            var sci = Text(info.transform, "Scientific", "(Solanum lycopersicum)", T.fontSmall, T.textSecondary, T.fontRegular, TextAlignmentOptions.TopLeft, wrap: false);
            sci.fontStyle = FontStyles.Italic;
            Spacer(info.transform, 12);
            Text(info.transform, "ConditionCaption", "Condition", T.fontMeta, T.textTertiary, T.fontMedium);
            var condName = Text(info.transform, "Condition", "Early Blight", T.fontH2 - 2, T.textPrimary, T.fontBold, TextAlignmentOptions.TopLeft);
            var chips = HStack(info.transform, "Chips", 10, 0, 8, 0, 0, TextAnchor.UpperLeft, false);
            var severity = Chip(chips.transform, "Severity", "Moderate", "info");
            var chips2 = HStack(info.transform, "Chips2", 10, 0, 0, 0, 0, TextAnchor.UpperLeft, false);
            var source = Chip(chips2.transform, "Source", "DEMO AI", "flask-conical");
            var confidence = Chip(chips2.transform, "Confidence", "94% confidence", "circle-check");

            // Uncertainty
            var banner = Box(content.transform, "Uncertain", T.amberSoft, 30);
            var bRow = banner.gameObject.AddComponent<HorizontalLayoutGroup>();
            bRow.padding = new RectOffset(28, 28, 26, 26); bRow.spacing = 22; bRow.childAlignment = TextAnchor.UpperLeft;
            bRow.childControlWidth = true; bRow.childControlHeight = true; bRow.childForceExpandWidth = false; bRow.childForceExpandHeight = false;
            FitHeight(banner.gameObject);
            Icon(banner.transform, "Icon", "triangle-alert", 56, T.amber);
            var bText = Text(banner.transform, "Text", "", T.fontSmall + 1, T.amber, T.fontMedium);
            Elem(bText.gameObject, -1, -1, -1, -1, 1, 0);

            // Tabs
            var tabs = Tabs(content.transform, "Tabs", new[] { "About", "Symptoms", "Impact" }, SegmentedTabs.Style.Pills, 100);

            // About
            var about = VStack(content.transform, "AboutPanel", 24, 0, 0, 0, 0);
            var (aboutCard, _) = PaddedCard(about.transform, "Card", 36, 14);
            Heading(aboutCard.transform, "WhatTitle", "What is it?", T.fontH3);
            var what = Body(aboutCard.transform, "What", "");
            Spacer(aboutCard.transform, 10);
            Heading(aboutCard.transform, "CausesTitle", "What causes it?", T.fontH3);
            var causes = Body(aboutCard.transform, "Causes", "");

            // Symptoms
            var sym = VStack(content.transform, "SymptomsPanel", 24, 0, 0, 0, 0);
            SectionTitle(sym.transform, "Symptoms Detected");
            var photoBlock = VStack(sym.transform, "PhotoBlock", 14, 0, 0, 0, 0);
            var photoRow = HStack(photoBlock.transform, "Photos", 18, 0, 0, 0, 0, TextAnchor.UpperLeft, false);
            Elem(photoRow.gameObject, -1, 290, -1, 290, 1, 0);
            photoRow.childForceExpandWidth = true; photoRow.childForceExpandHeight = true;
            var covers = new CoverImage[3];
            for (int i = 0; i < 3; i++)
            {
                var (pf, pr, pc) = Photo(photoRow.transform, "Photo" + i, null, 30);
                Elem(pf.gameObject, -1, -1, -1, -1, 1, 1);
                pr.color = T.sageSoft;
                covers[i] = pc;
            }
            var photoCaption = Text(photoBlock.transform, "Caption", "", T.fontMeta, T.textTertiary, T.fontRegular);

            var (bulletCard, _) = PaddedCard(sym.transform, "Bullets", 36, 18);
            var bulletRoot = bulletCard.transform;
            var bulletTemplate = BulletRow(bulletRoot, "BulletTemplate");

            var featureBlock = VStack(sym.transform, "FeatureBlock", 18, 0, 0, 0, 0);
            SectionTitle(featureBlock.transform, "Compared with a healthy plant");
            var featureList = VStack(featureBlock.transform, "Rows", 16, 0, 0, 0, 0);
            var featureTemplate = FeatureBar(featureList.transform);
            var featureCaption = Text(featureBlock.transform, "Caption", "", T.fontMeta, T.textTertiary, T.fontRegular);

            // Impact
            var impact = VStack(content.transform, "ImpactPanel", 24, 0, 0, 0, 0);
            var (impactCard, _) = PaddedCard(impact.transform, "Card", 36, 16);
            Heading(impactCard.transform, "Title", "What it does to the plant", T.fontH3);
            var effect = Body(impactCard.transform, "Effect", "");
            var untreated = Body(impactCard.transform, "Untreated", "", T.textPrimary);
            untreated.fontStyle = FontStyles.Bold;
            var sevCard = Box(impactCard.transform, "SeverityNote", T.ivoryDeep, 24);
            var sevText = Text(sevCard.transform, "Text", "", T.fontSmall + 1, T.textPrimary, T.fontMedium);
            var sevLayout = sevCard.gameObject.AddComponent<VerticalLayoutGroup>();
            sevLayout.padding = new RectOffset(26, 26, 22, 22); sevLayout.childControlWidth = true; sevLayout.childControlHeight = true;
            sevLayout.childForceExpandWidth = true; sevLayout.childForceExpandHeight = false;
            FitHeight(sevCard.gameObject);
            sevText.rectTransform.SetParent(sevCard.transform, false);

            var (secondary, secondaryLabel) = Secondary(content.transform, "Secondary", "View the possible match in AR", "box");

            Wire(view, ("backButton", back), ("reportButton", right), ("scanCover", cover), ("plantName", plantName), ("scientificName", sci),
                ("conditionName", condName), ("severityChip", severity), ("sourceChip", source), ("confidenceChip", confidence),
                ("uncertaintyBanner", banner.gameObject), ("uncertaintyText", bText), ("tabs", tabs),
                ("aboutPanel", about.gameObject), ("symptomsPanel", sym.gameObject), ("impactPanel", impact.gameObject), ("scroll", scroll),
                ("aboutWhat", what), ("aboutCauses", causes),
                ("photoBlock", photoBlock.gameObject), ("photoCaption", photoCaption), ("bulletRoot", (RectTransform)bulletRoot),
                ("bulletTemplate", bulletTemplate), ("featureBlock", featureBlock.gameObject), ("featureRoot", (RectTransform)featureList.transform),
                ("featureTemplate", featureTemplate), ("featureCaption", featureCaption),
                ("impactEffect", effect), ("impactUntreated", untreated), ("impactSeverity", sevText),
                ("primaryButton", cta), ("primaryLabel", ctaLabel), ("primaryIcon", ctaIcon),
                ("secondaryButton", secondary), ("secondaryLabel", secondaryLabel));
            WireArray(view, "photos", covers);
        }

        static void Spacer(Transform parent, float height)
        {
            var s = NewRect("Spacer", parent);
            Elem(s.gameObject, -1, height, -1, height, 0, 0);
        }

        static FeatureBarRow FeatureBar(Transform parent)
        {
            var card = Card(parent, "FeatureTemplate", null, 28);
            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(30, 30, 26, 28); v.spacing = 16;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            FitHeight(card.gameObject);

            var top = HStack(card.transform, "Top", 14, 0, 0, 0, 0, TextAnchor.MiddleLeft, false);
            Elem(top.gameObject, -1, 56, -1, 56, 1, 0);
            var name = Text(top.transform, "Name", "Leaf Color", T.fontBody - 2, T.textPrimary, T.fontSemiBold, TextAlignmentOptions.MidlineLeft, wrap: false);
            Elem(name.gameObject, -1, -1, -1, -1, 1, 0);
            var value = Text(top.transform, "Value", "72%", T.fontBody, T.textPrimary, T.fontBold, TextAlignmentOptions.MidlineRight, wrap: false);
            Elem(value.gameObject, 110, -1, 110, -1, 0, 0);
            var chip = Chip(top.transform, "Status", "Normal", "circle-check", 50);

            var track = Box(card.transform, "Track", T.ivoryDeep, 12);
            Elem(track.gameObject, -1, 22, -1, 22, 1, 0);
            var fill = Box(track.transform, "Fill", T.botanical, 12);
            Place(fill.rectTransform, Vector2.zero, new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            var marker = Box(track.transform, "HealthyMarker", T.textPrimary, 3);
            Place(marker.rectTransform, new Vector2(0.95f, -0.3f), new Vector2(0.95f, 1.3f), new Vector2(-3, 0), new Vector2(3, 0));
            marker.rectTransform.anchoredPosition = Vector2.zero;

            var view = card.gameObject.AddComponent<FeatureBarRow>();
            Wire(view, ("nameText", name), ("valueText", value), ("fill", fill), ("marker", marker.rectTransform), ("status", chip));
            return view;
        }

        // ==================================================================
        // 6. AR / plant view
        // ==================================================================

        static (Button button, Image bg, TextMeshProUGUI label, Image icon) ToolButton(Transform parent, string name, string icon, string label)
        {
            var box = Box(parent, name, T.glassDark, 36, true);
            Fixed(box.gameObject, 150, 150);
            var btn = MakeButton(box.gameObject, box);
            var col = VStack(box.transform, "Col", 6, 0, 16, 0, 12, TextAnchor.MiddleCenter, false);
            Stretch(col.transform);
            col.childForceExpandWidth = false;
            var ic = Icon(col.transform, "Icon", icon, 58, Color.white);
            var text = Text(col.transform, "Label", label, T.fontMeta, T.textOnDark, T.fontMedium, TextAlignmentOptions.Center, wrap: false);
            Elem(text.gameObject, -1, -1, -1, -1, 0, 0);
            return (btn, box, text, ic);
        }


        // ==================================================================
        // 7. Zoomed leaf
        // ==================================================================

        static CalloutView Callout(Transform parent, string name)
        {
            var go = NewRect(name, parent);
            Stretch(go);
            var line = Box(go, "Line", new Color(1, 1, 1, 0.85f));
            line.rectTransform.pivot = new Vector2(0, 0.5f);
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = Mid;
            line.rectTransform.sizeDelta = new Vector2(100, 3);
            var dot = Circle(go, "Dot", Color.white);
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = Mid;
            dot.rectTransform.sizeDelta = new Vector2(28, 28);
            var ring = Circle(dot.transform, "Core", T.red);
            Pin(ring.rectTransform, Mid, new Vector2(14, 14), Vector2.zero);
            var card = Box(go, "Card", new Color(T.forestDeep.r, T.forestDeep.g, T.forestDeep.b, 0.82f), 26);
            card.rectTransform.anchorMin = card.rectTransform.anchorMax = Mid;
            card.rectTransform.sizeDelta = new Vector2(380, 96);
            var text = Text(card.transform, "Text", "Label", T.fontSmall - 1, Color.white, T.fontMedium, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 16, 6, 16, 6);
            var view = go.gameObject.AddComponent<CalloutView>();
            Wire(view, ("card", card.rectTransform), ("text", text), ("dot", dot.rectTransform), ("line", line.rectTransform));
            return view;
        }


        // ==================================================================
        // 8. How it spreads
        // ==================================================================


    }
}
#endif
