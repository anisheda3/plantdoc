using System.Collections;
using PlantDoctor.AI;
using PlantDoctor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace PlantDoctor.UI.Classic
{
    /// <summary>
    /// In-app settings sheet. On a phone the inference server's LAN address is only known
    /// on the day, so it is entered here (and tested) rather than baked into the build.
    /// Also picks the plant and condition Demo Mode reports. Changes persist via AIManager.
    /// </summary>
    public class SettingsView : MonoBehaviour
    {
        [SerializeField] AppFlowController flow;
        [SerializeField] ClassicTheme theme;
        [SerializeField] FadeSlide group;
        [SerializeField] Button closeButton;
        [SerializeField] Button backdropButton;

        [Header("Backend")]
        [SerializeField] Button demoTab;
        [SerializeField] Button serverTab;
        [SerializeField] GameObject serverFields;
        [SerializeField] TMP_InputField endpointInput;
        [SerializeField] TMP_InputField apiKeyInput;
        [SerializeField] Button testButton;
        [SerializeField] TMP_Text statusText;
        [Tooltip("Everything below the server fields; slides up when they are hidden so there is no gap.")]
        [SerializeField] RectTransform lowerBlock;
        [SerializeField] float serverFieldsHeight = 340f;
        [Tooltip("The sheet itself; shortened by serverFieldsHeight when the server fields are hidden.")]
        [SerializeField] RectTransform card;

        [Header("Demo scenario")]
        [Tooltip("The demo plant and condition pickers; hidden while Server AI is chosen, which shortens the sheet by demoBlockHeight.")]
        [SerializeField] GameObject demoBlock;
        [SerializeField] float demoBlockHeight = 760f;
        [SerializeField] Button[] plantButtons;
        [SerializeField] string[] plantIds = { "tomato", "potato", "pepper", "apple", "blueberry", "cherry", "corn",
                                               "grape", "orange", "peach", "raspberry", "soybean", "squash", "strawberry",
                                               "banana", "basil", "bean", "broccoli", "cabbage", "carrot", "cauliflower", "celery",
                                               "coffee", "cucumber", "eggplant", "garlic", "ginger", "lettuce", "maple", "plum",
                                               "rice", "tobacco", "wheat", "mango", "sugarcane", "chilli", "cotton" };
        [Tooltip("Relabelled per plant: one button per disease, then Healthy.")]
        [SerializeField] Button[] scenarioButtons;

        [SerializeField] Button saveButton;

        InferenceMode _mode;
        int _plant;
        int _scenario;
        string[] _scenarioIds = { "early_blight", "late_blight", "healthy" };
        Coroutine _test;
        float _cardTop = float.NaN;

        void Awake()
        {
            if (card != null) _cardTop = card.offsetMax.y;
            group?.Hide(true);
        }

        void OnEnable()
        {
            Hook(closeButton, Close);
            Hook(backdropButton, Close);
            Hook(demoTab, () => SelectMode(InferenceMode.Mock));
            Hook(serverTab, () => SelectMode(InferenceMode.Remote));
            Hook(testButton, TestConnection);
            Hook(saveButton, Save);
            if (scenarioButtons != null)
                for (int i = 0; i < scenarioButtons.Length; i++)
                {
                    int index = i;
                    Hook(scenarioButtons[i], () => SelectScenario(index));
                }
            if (plantButtons != null)
                for (int i = 0; i < plantButtons.Length; i++)
                {
                    int index = i;
                    Hook(plantButtons[i], () => SelectPlant(index));
                }
        }

        void OnDisable()
        {
            Unhook(closeButton); Unhook(backdropButton); Unhook(demoTab); Unhook(serverTab);
            Unhook(testButton); Unhook(saveButton);
            if (scenarioButtons != null) foreach (var b in scenarioButtons) Unhook(b);
            if (plantButtons != null) foreach (var b in plantButtons) Unhook(b);
        }

        public bool IsOpen => group != null && group.IsShown;

        public void Open()
        {
            var ai = flow != null ? flow.AI : null;
            _mode = ai != null && ai.Mode == InferenceMode.Remote ? InferenceMode.Remote : InferenceMode.Mock;
            if (endpointInput != null) endpointInput.text = ai != null ? ai.RemoteEndpoint : "";
            if (apiKeyInput != null) apiKeyInput.text = ai != null ? ai.RemoteApiKey : "";
            _plant = Mathf.Max(0, System.Array.IndexOf(plantIds, ai != null ? ai.DemoPlant : "tomato"));
            LoadConditions();
            _scenario = Mathf.Max(0, System.Array.IndexOf(_scenarioIds, ai != null ? ai.DemoScenario : "early_blight"));
            SetStatus(_mode == InferenceMode.Mock
                ? "Demo AI returns a simulated result for the chosen scenario. Your camera scan still runs through the real quality checks."
                : "CropScan: https://cropscan-api-ovmb.onrender.com. Your own server: your PC's LAN address, e.g. http://192.168.1.20:8000/v1/analyze", theme != null ? theme.textSecondary : Color.white);
            RefreshVisuals();
            group?.Show();
        }

        public void Close() => group?.Hide();

        void SelectMode(InferenceMode mode)
        {
            _mode = mode;
            RefreshVisuals();
        }

        void SelectScenario(int index)
        {
            if (index >= _scenarioIds.Length) return;
            _scenario = index;
            RefreshVisuals();
        }

        void SelectPlant(int index)
        {
            if (index == _plant) return;
            _plant = index;
            LoadConditions();
            _scenario = 0;   // the first disease of the newly chosen plant
            RefreshVisuals();
        }

        /// <summary>Names the condition buttons after the chosen plant's diseases (plus Healthy).</summary>
        void LoadConditions()
        {
            var ai = flow != null ? flow.AI : null;
            string plantId = plantIds != null && _plant < plantIds.Length ? plantIds[_plant] : "tomato";
            var ids = new System.Collections.Generic.List<string>();
            var names = new System.Collections.Generic.List<string>();
            if (ai != null && ai.Database != null && ai.Database.TryResolve(plantId, out var plant) && plant.diseases != null)
                foreach (var d in plant.diseases)
                {
                    if (d == null) continue;
                    ids.Add(d.diseaseId);
                    names.Add(d.diseaseName);
                }
            ids.Add("healthy");
            names.Add("Healthy");
            _scenarioIds = ids.ToArray();

            if (scenarioButtons == null) return;
            for (int i = 0; i < scenarioButtons.Length; i++)
            {
                if (scenarioButtons[i] == null) continue;
                bool used = i < names.Count;
                scenarioButtons[i].gameObject.SetActive(used);
                var label = scenarioButtons[i].GetComponentInChildren<TMP_Text>(true);
                if (used && label != null) label.text = names[i];
            }
        }

        void Save()
        {
            var ai = flow != null ? flow.AI : null;
            if (ai == null) { Close(); return; }

            if (_mode == InferenceMode.Remote && endpointInput != null &&
                !endpointInput.text.Trim().StartsWith("http"))
            {
                SetStatus("The server address must start with http:// or https://", theme.danger);
                return;
            }

            ai.SetBackend(_mode, endpointInput != null ? endpointInput.text : null, apiKeyInput != null ? apiKeyInput.text : null);
            if (plantIds != null && _plant < plantIds.Length) ai.DemoPlant = plantIds[_plant];
            if (_scenario < _scenarioIds.Length) ai.DemoScenario = _scenarioIds[_scenario];
            GameEvents.RaiseStageContentChanged();
            Close();
        }

        void TestConnection()
        {
            if (_test != null) StopCoroutine(_test);
            _test = StartCoroutine(TestRoutine(endpointInput != null ? endpointInput.text.Trim() : ""));
        }

        IEnumerator TestRoutine(string endpoint)
        {
            string url = HealthUrl(endpoint);
            bool cropScan = PlantDoctor.AI.CropScanAIService.IsCropScan(endpoint);
            if (url == null)
            {
                SetStatus("Enter a full address first, e.g. https://cropscan-api-ovmb.onrender.com or http://192.168.1.20:8000/v1/analyze", theme.warning);
                yield break;
            }

            SetStatus(cropScan ? $"Contacting {url} … (a sleeping CropScan server can take up to a minute to wake)" : $"Contacting {url} …", theme.textSecondary);
            using var request = UnityWebRequest.Get(url);
            request.timeout = cropScan ? PlantDoctor.AI.CropScanAIService.DefaultTimeoutSeconds : 6;   // a sleeping CropScan server takes up to a minute to wake
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                SetStatus(cropScan ? $"Could not reach the CropScan server ({request.error}). Check the internet connection and the address." : $"Could not reach the server ({request.error}). Same Wi-Fi? Firewall open on port 8000?", theme.danger);
                yield break;
            }

            string body = request.downloadHandler.text ?? "";
            bool modelLoaded = body.Contains("\"model_loaded\":true") || body.Contains("\"model_loaded\": true");
            SetStatus(modelLoaded
                ? "Connected. The server has a trained model loaded."
                : "Connected, but the server has no trained model yet — scans will report MODEL_UNAVAILABLE.",
                modelLoaded ? theme.accent : theme.warning);
        }

        /// <summary>The server's health address: CropScan's /healthz, or http://host:8000/v1/analyze -> /v1/health.</summary>
        public static string HealthUrl(string analyzeEndpoint) => PlantDoctor.AI.CropScanAIService.HealthUrlFor(analyzeEndpoint);

        void RefreshVisuals()
        {
            if (theme == null) return;
            Style(demoTab, _mode == InferenceMode.Mock);
            Style(serverTab, _mode == InferenceMode.Remote);
            if (serverFields != null) serverFields.SetActive(_mode == InferenceMode.Remote);
            if (lowerBlock != null) lowerBlock.anchoredPosition = new Vector2(0f, _mode == InferenceMode.Remote ? 0f : serverFieldsHeight);
            bool remote = _mode == InferenceMode.Remote;
            if (demoBlock != null) demoBlock.SetActive(!remote);
            if (card != null && !float.IsNaN(_cardTop))
                card.offsetMax = new Vector2(card.offsetMax.x, _cardTop - (remote ? (demoBlock != null ? demoBlockHeight : 0f) : serverFieldsHeight));
            if (scenarioButtons != null)
                for (int i = 0; i < scenarioButtons.Length; i++) Style(scenarioButtons[i], i == _scenario);
            if (plantButtons != null)
                for (int i = 0; i < plantButtons.Length; i++) Style(plantButtons[i], i == _plant);
        }

        void Style(Button button, bool selected)
        {
            if (button == null) return;
            var img = button.targetGraphic as Image;
            if (img != null) img.color = selected ? theme.accent : theme.panelSunken;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.color = selected ? theme.textOnAccent : theme.textSecondary;
        }

        void SetStatus(string text, Color color)
        {
            if (statusText == null) return;
            statusText.text = text;
            statusText.color = color;
        }

        static void Hook(Button b, UnityEngine.Events.UnityAction action)
        {
            if (b == null) return;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(action);
        }

        static void Unhook(Button b)
        {
            if (b != null) b.onClick.RemoveAllListeners();
        }
    }
}
