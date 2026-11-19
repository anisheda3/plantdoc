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
        // 9. Treatment plan
        // ==================================================================

        static StepCardView StepCard(Transform parent)
        {
            var card = Card(parent, "StepTemplate");
            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(32, 32, 32, 32); v.spacing = 20;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            FitHeight(card.gameObject);

            var top = HStack(card.transform, "Top", 24, 0, 0, 0, 0, TextAnchor.UpperLeft, false);
            var circle = Circle(top.transform, "Number", T.botanical);
            Fixed(circle.gameObject, 80, 80);
            var num = Text(circle.transform, "Text", "1", T.fontH3, T.textOnDark, T.fontBold, TextAlignmentOptions.Center, wrap: false);
            Stretch(num.rectTransform);
            var done = Icon(circle.transform, "Done", "check", 46, Color.white);
            Pin(done.rectTransform, Mid, new Vector2(46, 46), Vector2.zero);
            var textCol = VStack(top.transform, "Text", 6, 0, 0, 0, 0, TextAnchor.UpperLeft, true);
            Elem(textCol.gameObject, -1, -1, -1, -1, 1, 0);
            var title = Heading(textCol.transform, "Title", "Step", T.fontH3);
            var body = Body(textCol.transform, "Body", "");

            var (frame, raw, cover) = Photo(card.transform, "Photo", null, 28);
            Elem(frame.gameObject, -1, 210, -1, 210, 1, 0);
            raw.color = T.sageSoft;

            var safety = Box(card.transform, "Safety", T.amberSoft, 26);
            var sRow = safety.gameObject.AddComponent<HorizontalLayoutGroup>();
            sRow.padding = new RectOffset(26, 26, 22, 22); sRow.spacing = 20; sRow.childAlignment = TextAnchor.UpperLeft;
            sRow.childControlWidth = true; sRow.childControlHeight = true; sRow.childForceExpandWidth = false; sRow.childForceExpandHeight = false;
            FitHeight(safety.gameObject);
            Icon(safety.transform, "Icon", "triangle-alert", 46, T.amber);
            var safetyText = Text(safety.transform, "Text", "", T.fontMeta + 2, T.amber, T.fontMedium);
            Elem(safetyText.gameObject, -1, -1, -1, -1, 1, 0);

            var chip = Chip(card.transform, "DoneChip", "Done", "circle-check");
            var (action, actionLabel, _, actionBg) = ButtonBase(card.transform, "Action", "Mark as done", null, T.forest, T.textOnDark, 108, 54);

            var view = card.gameObject.AddComponent<StepCardView>();
            Wire(view, ("numberCircle", circle), ("numberText", num), ("doneIcon", done), ("title", title), ("body", body),
                ("photo", cover), ("photoFrame", frame.gameObject), ("safetyBox", safety.gameObject), ("safetyText", safetyText),
                ("actionButton", action), ("actionBackground", actionBg), ("actionLabel", actionLabel), ("statusChip", chip));
            return view;
        }


        // ==================================================================
        // 10. Recovery simulation
        // ==================================================================

        static Slider BuildSlider(Transform parent, string name)
        {
            var go = NewRect(name, parent);
            Elem(go.gameObject, -1, 112, -1, 112, 1, 0);
            var slider = go.gameObject.AddComponent<Slider>();

            var bg = Box(go, "Background", T.ivoryDeep, 8);
            Place(bg.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -8), new Vector2(0, 8));
            var fillArea = NewRect("FillArea", go);
            Place(fillArea, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -8), new Vector2(0, 8));
            var fill = Box(fillArea, "Fill", T.botanical, 8);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0, 1);
            fill.rectTransform.sizeDelta = new Vector2(10, 0);
            var handleArea = NewRect("HandleArea", go);
            // The slider stretches its handle over the full height of this area, so the area is exactly one handle tall.
            Place(handleArea, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(26, -26), new Vector2(-26, 26));
            var handle = Circle(handleArea, "Handle", T.textOnDark, true);
            handle.rectTransform.sizeDelta = new Vector2(52, 0);
            handle.rectTransform.anchoredPosition = Vector2.zero;

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0; slider.maxValue = 1; slider.value = 0;
            return slider;
        }


        // ==================================================================
        // 11. Prevention
        // ==================================================================

        static ChecklistItemView ChecklistItem(Transform parent)
        {
            var card = Card(parent, "ItemTemplate", null, 30);
            var btn = MakeButton(card.gameObject, card);
            var row = card.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(26, 30, 26, 26); row.spacing = 26; row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true; row.childControlHeight = true; row.childForceExpandWidth = false; row.childForceExpandHeight = false;
            FitHeight(card.gameObject);
            var tile = Box(card.transform, "Tile", T.sageSoft, 26);
            Fixed(tile.gameObject, 100, 100);
            var icon = Icon(tile.transform, "Icon", "sprout", 54, T.botanical);
            Pin(icon.rectTransform, Mid, new Vector2(54, 54), Vector2.zero);
            var text = VStack(card.transform, "Text", 6, 0, 0, 0, 0, TextAnchor.MiddleLeft, true);
            Elem(text.gameObject, -1, -1, -1, -1, 1, 0);
            var title = Text(text.transform, "Title", "Measure", T.fontBody, T.textPrimary, T.fontSemiBold);
            var detail = Text(text.transform, "Detail", "", T.fontSmall - 1, T.textSecondary, T.fontRegular);
            var tick = Icon(card.transform, "Tick", "circle", 60, T.textTertiary);

            var view = card.gameObject.AddComponent<ChecklistItemView>();
            Wire(view, ("iconTile", tile), ("icon", icon), ("title", title), ("detail", detail), ("tick", tick), ("button", btn));
            return view;
        }

        static void BuildPrevention(Transform parent)
        {
            var (root, safe, scroll, content, back, right, cta, ctaLabel) = SheetPage(parent, "Prevention", "Prevention", null, "Final report", "file-text", 1320f);
            var view = AddScreen<PreventionScreen>(root);
            Backdrop(view, 1320f);

            var intro = Text(content.transform, "Intro", "", T.fontBody - 2, T.textSecondary, T.fontRegular);
            var list = VStack(content.transform, "Items", 20, 0, 0, 0, 0);
            var template = ChecklistItem(list.transform);
            var progress = Text(content.transform, "Progress", "", T.fontSmall, T.textTertiary, T.fontMedium, TextAlignmentOptions.Center);

            Wire(view, ("backButton", back), ("intro", intro), ("listRoot", (RectTransform)list.transform), ("itemTemplate", template),
                ("progress", progress), ("nextButton", cta));
        }

        // ==================================================================
        // 12. Final report
        // ==================================================================

        static CheckRowView CheckRow(Transform parent, string name)
        {
            var row = HStack(parent, name, 20, 0, 0, 0, 0, TextAnchor.UpperLeft, false);
            var iconHolder = NewRect("IconHolder", row.transform);
            Fixed(iconHolder.gameObject, 48, 46);
            var icon = Icon(iconHolder, "Icon", "circle-check", 44, T.botanical);
            Pin(icon.rectTransform, Mid, new Vector2(44, 44), Vector2.zero);
            var text = Body(row.transform, "Text", "Item", T.textPrimary, T.fontBody - 2);
            Elem(text.gameObject, -1, -1, -1, -1, 1, 0);
            var view = row.gameObject.AddComponent<CheckRowView>();
            Wire(view, ("icon", icon), ("text", text));
            return view;
        }

        static RectTransform ReportSection(Transform parent, string title, string name)
        {
            var section = VStack(parent, name, 14, 0, 0, 0, 0);
            SectionTitle(section.transform, title);
            var (card, layout) = PaddedCard(section.transform, "Card", 32, 16);
            layout.childForceExpandWidth = true;
            return (RectTransform)section.transform;
        }

        static void BuildReport(Transform parent)
        {
            var (root, safe, scroll, content, back, right, _, _) = SheetPage(parent, "Report", "Health report", "file-down", null, null, 1760f);
            var view = AddScreen<ReportScreen>(root);
            Backdrop(view, 1760f);
            // Leave room at the bottom for the save bar.
            var body = (RectTransform)scroll.transform.parent;
            body.offsetMin = new Vector2(0, 330);

            // Summary card
            var summary = Card(content.transform, "Summary");
            var sRow = summary.gameObject.AddComponent<HorizontalLayoutGroup>();
            sRow.padding = new RectOffset(30, 30, 30, 30); sRow.spacing = 30; sRow.childAlignment = TextAnchor.UpperLeft;
            sRow.childControlWidth = true; sRow.childControlHeight = true; sRow.childForceExpandWidth = false; sRow.childForceExpandHeight = false;
            FitHeight(summary.gameObject);
            var (frame, raw, cover) = Photo(summary.transform, "ScanPhoto", null, 30);
            Fixed(frame.gameObject, 230, 280);
            raw.color = T.sageSoft;
            var info = VStack(summary.transform, "Info", 6, 0, 0, 0, 0, TextAnchor.UpperLeft, true);
            Elem(info.gameObject, -1, -1, -1, -1, 1, 0);
            Text(info.transform, "PlantCaption", "Plant", T.fontMeta, T.textTertiary, T.fontMedium);
            var plantName = Text(info.transform, "PlantName", "Tomato", T.fontH2 + 4, T.textPrimary, T.fontBold, TextAlignmentOptions.TopLeft, wrap: false);
            var sci = Text(info.transform, "Scientific", "", T.fontSmall - 1, T.textSecondary, T.fontRegular, TextAlignmentOptions.TopLeft, wrap: false);
            sci.fontStyle = FontStyles.Italic;
            Spacer(info.transform, 8);
            Text(info.transform, "ConditionCaption", "Condition", T.fontMeta, T.textTertiary, T.fontMedium);
            var cond = Text(info.transform, "Condition", "Early Blight", T.fontH3 + 2, T.textPrimary, T.fontBold, TextAlignmentOptions.TopLeft);
            var chips = HStack(info.transform, "Chips", 10, 0, 8, 0, 0, TextAnchor.UpperLeft, false);
            var severity = Chip(chips.transform, "Severity", "Moderate", "info");
            var chips2 = HStack(info.transform, "Chips2", 10, 0, 0, 0, 0, TextAnchor.UpperLeft, false);
            var source = Chip(chips2.transform, "Source", "DEMO AI", "flask-conical");

            // Summary paragraph
            var sumSection = VStack(content.transform, "SummarySection", 14, 0, 0, 0, 0);
            SectionTitle(sumSection.transform, "Summary");
            var (sumCard, _) = PaddedCard(sumSection.transform, "Card", 34, 10);
            var summaryText = Body(sumCard.transform, "Text", "", T.textPrimary);

            var symptoms = ReportSection(content.transform, "Observed symptoms", "SymptomsSection");
            var treatment = ReportSection(content.transform, "Recommended treatment", "TreatmentSection");
            var prevention = ReportSection(content.transform, "Prevention checklist", "PreventionSection");
            var actions = ReportSection(content.transform, "Actions Taken", "ActionsSection");

            // A hidden holder for the row template (instantiated into the sections).
            var holder = NewRect("Templates", content.transform);
            Ignore(holder.gameObject);
            var template = CheckRow(holder, "RowTemplate");
            holder.gameObject.SetActive(false);

            var notes = Text(content.transform, "Notes", "", T.fontMeta, T.textTertiary, T.fontRegular);
            var (again, againLabel) = Secondary(content.transform, "Again", "Scan another plant", "scan-line");

            // Save bar, at the bottom of the sheet
            var sheet = body.parent;
            var bar = NewRect("SaveBar", sheet);
            Place(bar, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 330));
            var holder2 = VStack(bar, "Holder", 14, (int)T.pagePadding, 20, (int)T.pagePadding, 40, TextAnchor.LowerCenter, true);
            Stretch(holder2.transform);
            var status = Text(holder2.transform, "Status", "", T.fontSmall - 1, T.textSecondary, T.fontMedium, TextAlignmentOptions.Center);
            Elem(status.gameObject, -1, 76, -1, -1, 1, 0);
            var (save, saveLabel, _, saveBg) = ButtonBase(holder2.transform, "Save", "Save Report", "download", T.forest, T.textOnDark, T.buttonHeight, T.buttonRadius);
            Glow(saveBg.rectTransform, new Color(T.forest.r, T.forest.g, T.forest.b, 0.40f), 24f);

            Wire(view, ("backButton", back), ("exportIconButton", right), ("photo", cover), ("plantName", plantName), ("scientificName", sci),
                ("conditionName", cond), ("severityChip", severity), ("sourceChip", source), ("summary", summaryText),
                ("symptomsRoot", (RectTransform)symptoms.GetChild(1)), ("treatmentRoot", (RectTransform)treatment.GetChild(1)),
                ("preventionRoot", (RectTransform)prevention.GetChild(1)), ("actionsRoot", (RectTransform)actions.GetChild(1)),
                ("rowTemplate", template), ("notes", notes), ("saveButton", save), ("saveLabel", saveLabel), ("saveStatus", status),
                ("againButton", again), ("scroll", scroll));
        }
    }
}
#endif
