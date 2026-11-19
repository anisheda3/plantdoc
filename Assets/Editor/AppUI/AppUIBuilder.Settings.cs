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
        // Guide
        // ==================================================================



        // ==================================================================
        // My Plants
        // ==================================================================


        // ==================================================================
        // Profile and settings
        // ==================================================================

        static TMP_InputField InputField(Transform parent, string name, string placeholder, TMP_InputField.ContentType type = TMP_InputField.ContentType.Standard)
        {
            var bg = Box(parent, name, T.ivoryDeep, 24, true);
            Elem(bg.gameObject, -1, 104, -1, 104, 1, 0);
            var input = bg.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = bg;
            var area = NewRect("Text Area", bg.transform);
            Stretch(area, 30, 10, 30, 10);
            area.gameObject.AddComponent<RectMask2D>();
            var ph = Text(area, "Placeholder", placeholder, T.fontSmall + 2, T.textTertiary, T.fontRegular, TextAlignmentOptions.MidlineLeft, wrap: false);
            Stretch(ph.rectTransform);
            var text = Text(area, "Text", "", T.fontSmall + 2, T.textPrimary, T.fontMedium, TextAlignmentOptions.MidlineLeft, wrap: false);
            Stretch(text.rectTransform);
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = ph;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = type;
            input.caretColor = T.botanical;
            input.selectionColor = new Color(T.botanical.r, T.botanical.g, T.botanical.b, 0.3f);
            input.fontAsset = T.fontMedium;
            input.pointSize = T.fontSmall + 2;
            return input;
        }

        /// <summary>A row of pill choices (one selected at a time).</summary>
        static void PillRow(Transform parent, string name, string[] labels, out Button[] buttons, out Image[] backgrounds, out TextMeshProUGUI[] texts, float height = 92)
        {
            var row = HStack(parent, name, 12, 0, 0, 0, 0, TextAnchor.MiddleCenter, true);
            Elem(row.gameObject, -1, height, -1, height, 1, 0);
            row.childForceExpandWidth = true;
            buttons = new Button[labels.Length]; backgrounds = new Image[labels.Length]; texts = new TextMeshProUGUI[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                var item = Box(row.transform, "Pill" + i, T.ivoryDeep, height * 0.5f - 6, true);
                Elem(item.gameObject, -1, -1, -1, -1, 1, 1);
                buttons[i] = MakeButton(item.gameObject, item);
                backgrounds[i] = item;
                texts[i] = Text(item.transform, "Label", labels[i], T.fontSmall, T.textSecondary, T.fontSemiBold, TextAlignmentOptions.Center, wrap: false);
                Stretch(texts[i].rectTransform, 8, 0, 8, 0);
                texts[i].enableAutoSizing = true; texts[i].fontSizeMin = 20; texts[i].fontSizeMax = T.fontSmall;
            }
        }

        static void BuildProfile(Transform parent)
        {
            var (root, safe, scroll, content, back, right, cta, ctaLabel) = SheetPage(parent, "Profile", "Profile", null, null, null, 1900f);
            var view = AddScreen<ProfileScreen>(root);
            Backdrop(view, 1900f, 170, 0.30f);

            // Past scans (kept on this phone)
            var (historyCard, _) = PaddedCard(content.transform, "HistoryCard", 32, 18);
            Heading(historyCard.transform, "Title", "Your scans", T.fontH3);
            Body(historyCard.transform, "Hint", "Every scan stays on this phone. Tap one to see its summary.", T.textSecondary, T.fontSmall - 1);
            var historyList = VStack(historyCard.transform, "List", 16, 0, 0, 0, 0);
            var (rowCard, rowView) = BuildScanRow(historyList.transform, "RowTemplate");
            var historyEmpty = VStack(historyList.transform, "Empty", 10, 0, 20, 0, 20, TextAnchor.MiddleCenter, true);
            Icon(historyEmpty.transform, "Icon", "sprout", 64, T.leaf);
            Text(historyEmpty.transform, "Text", "No scans yet. Scan a leaf and it will appear here.", T.fontSmall, T.textSecondary, T.fontRegular, TextAlignmentOptions.Center);

            // Name
            var (nameCard, _) = PaddedCard(content.transform, "NameCard", 32, 16);
            Heading(nameCard.transform, "Title", "Your name", T.fontH3);
            Body(nameCard.transform, "Hint", "Used for the greeting on the home screen. It stays on this phone.", T.textSecondary, T.fontSmall);
            var nameInput = InputField(nameCard.transform, "Name", "Your name (optional)");

            // Backend
            var (aiCard, _) = PaddedCard(content.transform, "AiCard", 32, 18);
            Heading(aiCard.transform, "Title", "AI backend", T.fontH3);
            PillRow(aiCard.transform, "Backend", new[] { "Demo AI", "Server AI" }, out var backendButtons, out var backendBgs, out var backendLabels);
            var status = Text(aiCard.transform, "Status", "", T.fontSmall - 1, T.textSecondary, T.fontRegular);

            var server = VStack(aiCard.transform, "ServerFields", 16, 0, 0, 0, 0);
            var endpoint = InputField(server.transform, "Endpoint", "http://192.168.1.20:8000/v1/analyze", TMP_InputField.ContentType.Standard);
            endpoint.keyboardType = TouchScreenKeyboardType.URL;
            var apiKey = InputField(server.transform, "ApiKey", "API key (optional)");
            var (test, _) = Subtle(server.transform, "Test", "Test connection", "refresh-cw");

            var demo = VStack(aiCard.transform, "DemoFields", 16, 0, 0, 0, 0);
            Text(demo.transform, "PlantLabel", "DEMO PLANT", T.fontMeta, T.textTertiary, T.fontSemiBold);
            PillRow(demo.transform, "Plants", new[] { "Tomato", "Potato", "Pepper", "Apple" }, out var plantButtons, out var plantBgs, out var plantLabels);
            Text(demo.transform, "ConditionLabel", "DEMO CONDITION", T.fontMeta, T.textTertiary, T.fontSemiBold);
            PillRow(demo.transform, "Conditions", new[] { "Early Blight", "Late Blight", "Healthy" }, out var scenarioButtons, out var scenarioBgs, out var scenarioLabels);

            // Accessibility
            var (accCard, _) = PaddedCard(content.transform, "AccessibilityCard", 32, 16);
            Heading(accCard.transform, "Title", "Accessibility", T.fontH3);
            var motionRow = Box(accCard.transform, "Motion", Color.clear, 0, true);
            Elem(motionRow.gameObject, -1, 100, -1, 100, 1, 0);
            var motionBtn = MakeButton(motionRow.gameObject, motionRow);
            var mRow = HStack(motionRow.transform, "Row", 20, 0, 0, 0, 0, TextAnchor.MiddleLeft, false);
            Stretch(mRow.transform);
            var mText = VStack(mRow.transform, "Text", 2, 0, 0, 0, 0, TextAnchor.MiddleLeft, true);
            Elem(mText.gameObject, -1, -1, -1, -1, 1, 0);
            Text(mText.transform, "Title", "Reduce motion", T.fontBody - 1, T.textPrimary, T.fontSemiBold);
            Text(mText.transform, "Sub", "Fewer animations and transitions", T.fontSmall - 2, T.textSecondary, T.fontRegular);
            var motionTick = Icon(mRow.transform, "Tick", "circle", 60, T.textTertiary);

            // About and credits
            var (aboutCard, _) = PaddedCard(content.transform, "AboutCard", 32, 16);
            Heading(aboutCard.transform, "Title", "About", T.fontH3);
            var version = Text(aboutCard.transform, "Version", "Plant Doctor", T.fontSmall, T.textSecondary, T.fontMedium);
            Body(aboutCard.transform, "Note", "Plant Doctor gives educational guidance. It is not a substitute for a local agricultural expert.", T.textSecondary, T.fontSmall - 1);
            var (creditsBtn, _) = Subtle(aboutCard.transform, "CreditsButton", "Photo, icon and font credits", "info");
            var creditsPanel = Box(aboutCard.transform, "Credits", T.ivoryDeep, 24);
            var cl = creditsPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            cl.padding = new RectOffset(26, 26, 22, 22); cl.childControlWidth = true; cl.childControlHeight = true;
            cl.childForceExpandWidth = true; cl.childForceExpandHeight = false;
            FitHeight(creditsPanel.gameObject);
            var creditsText = Text(creditsPanel.transform, "Text", "", T.fontMeta - 2, T.textSecondary, T.fontRegular);
            creditsPanel.gameObject.SetActive(false);

            var (save, _) = Primary(content.transform, "Save", "Save settings", "check");

            Wire(view, ("nameInput", nameInput), ("demoTab", backendButtons[0]), ("serverTab", backendButtons[1]), ("serverFields", server.gameObject),
                ("endpointInput", endpoint), ("apiKeyInput", apiKey), ("testButton", test), ("statusText", status), ("demoFields", demo.gameObject),
                ("motionButton", motionBtn), ("motionTick", motionTick), ("creditsButton", creditsBtn), ("creditsPanel", creditsPanel.gameObject),
                ("creditsText", creditsText), ("creditsAsset", AssetDatabase.LoadAssetAtPath<TextAsset>(CreditsPath)),
                ("saveButton", save), ("versionText", version), ("scroll", scroll), ("backButton", back),
                ("historyRoot", (RectTransform)historyList.transform), ("historyTemplate", rowView), ("historyEmpty", historyEmpty.gameObject));
            WireArray(view, "backendBackgrounds", backendBgs);
            WireArray(view, "backendLabels", backendLabels);
            WireArray(view, "plantButtons", plantButtons);
            WireArray(view, "plantBackgrounds", plantBgs);
            WireArray(view, "plantLabels", plantLabels);
            WireArray(view, "scenarioButtons", scenarioButtons);
            WireArray(view, "scenarioBackgrounds", scenarioBgs);
            WireArray(view, "scenarioLabels", scenarioLabels);
        }

        // ==================================================================
        // Error
        // ==================================================================

        static void BuildError(Transform parent)
        {
            var (root, safe, _) = PageRoot(parent, "Error", Color.clear);
            var view = AddScreen<ErrorScreen>(root);
            Backdrop(view, 0, 170, 0.25f);
            var scrim = Box(root, "Scrim", new Color(0.03f, 0.09f, 0.06f, 0.55f));
            Stretch(scrim.rectTransform); scrim.transform.SetSiblingIndex(0);

            var stack = VStack(safe, "Stack", 24, 60, 0, 60, 0, TextAnchor.MiddleCenter, true);
            Stretch(stack.transform);
            stack.childForceExpandWidth = true;
            var circle = Circle(stack.transform, "Icon", T.redSoft);
            Fixed(circle.gameObject, 200, 200);
            var icon = Icon(circle.transform, "Alert", "triangle-alert", 100, T.red);
            Pin(icon.rectTransform, Mid, new Vector2(100, 100), Vector2.zero);
            var title = Heading(stack.transform, "Title", "That didn't work", T.fontH1 - 6, T.textPrimary, TextAlignmentOptions.Center);
            var message = Body(stack.transform, "Message", "", T.textSecondary, T.fontBody, TextAlignmentOptions.Center);
            Spacer(stack.transform, 24);
            var (retry, _) = Primary(stack.transform, "Retry", "Scan again", "scan-line");
            var (home, _) = Secondary(stack.transform, "Home", "Back to home", "house");

            Wire(view, ("message", message), ("retryButton", retry), ("homeButton", home));
        }
    }
}
#endif
