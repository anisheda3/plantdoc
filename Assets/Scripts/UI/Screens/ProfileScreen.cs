using System;
using System.Collections;
using System.Collections.Generic;
using PlantDoctor.AI;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using PlantDoctor.Treatment;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace PlantDoctor.UI
{
    /// <summary>Profile and settings: name, AI backend, demo scenario, accessibility and credits.</summary>
    public class ProfileScreen : ScreenView
    {
        public override AppScreenId Id => AppScreenId.Profile;

        [SerializeField] TMP_InputField nameInput;
        [SerializeField] Button demoTab;
        [SerializeField] Button serverTab;
        [SerializeField] Image[] backendBackgrounds = new Image[2];
        [SerializeField] TMP_Text[] backendLabels = new TMP_Text[2];
        [SerializeField] GameObject serverFields;
        [SerializeField] TMP_InputField endpointInput;
        [SerializeField] TMP_InputField apiKeyInput;
        [SerializeField] Button testButton;
        [SerializeField] TMP_Text statusText;
        [SerializeField] GameObject demoFields;
        [SerializeField] Button[] plantButtons = new Button[4];
        [SerializeField] Image[] plantBackgrounds = new Image[4];
        [SerializeField] TMP_Text[] plantLabels = new TMP_Text[4];
        [SerializeField] string[] plantIds = { "tomato", "potato", "pepper", "apple" };
        [SerializeField] Button[] scenarioButtons = new Button[3];
        [SerializeField] Image[] scenarioBackgrounds = new Image[3];
        [SerializeField] TMP_Text[] scenarioLabels = new TMP_Text[3];
        [SerializeField] Button motionButton;
        [SerializeField] Image motionTick;
        [SerializeField] Button creditsButton;
        [SerializeField] GameObject creditsPanel;
        [SerializeField] TMP_Text creditsText;
        [SerializeField] TextAsset creditsAsset;
        [SerializeField] Button saveButton;
        [SerializeField] TMP_Text versionText;
        [SerializeField] ScrollRect scroll;

        [Header("Header and scan history")]
        [SerializeField] Button backButton;
        [SerializeField] RectTransform historyRoot;
        [SerializeField] ScanRowView historyTemplate;
        [SerializeField] GameObject historyEmpty;

        InferenceMode _mode;
        int _plant, _scenario;
        string[] _scenarioIds = { "early_blight", "late_blight", "healthy" };
        Coroutine _test;

        protected override void OnBind()
        {
            if (historyTemplate != null) historyTemplate.gameObject.SetActive(false);
            Hook(backButton, () => Ctx.Router.Back());
            if (Ctx.History != null) Ctx.History.Changed += RefreshHistory;
            Hook(demoTab, () => SelectMode(InferenceMode.Mock));
            Hook(serverTab, () => SelectMode(InferenceMode.Remote));
            Hook(testButton, TestConnection);
            Hook(saveButton, Save);
            Hook(motionButton, () => { PlayerPrefs.SetInt(UITheme.ReducedMotionPref, UITheme.ReducedMotion ? 0 : 1); PaintMotion(); });
            Hook(creditsButton, () => SetActive(creditsPanel, creditsPanel != null && !creditsPanel.activeSelf));
            for (int i = 0; i < plantButtons.Length; i++) { int k = i; Hook(plantButtons[i], () => { _plant = k; LoadConditions(); _scenario = 0; Paint(); }); }
            for (int i = 0; i < scenarioButtons.Length; i++) { int k = i; Hook(scenarioButtons[i], () => { _scenario = k; Paint(); }); }
            SetText(versionText, $"Plant Doctor {Application.version}");
            if (creditsText != null && creditsAsset != null) creditsText.text = creditsAsset.text;
        }

        protected override void OnShown()
        {
            var ai = Ctx.Flow != null ? Ctx.Flow.AI : null;
            _mode = ai != null && ai.Mode == InferenceMode.Remote ? InferenceMode.Remote : InferenceMode.Mock;
            if (nameInput != null) nameInput.text = UIText.UserName;
            if (endpointInput != null) endpointInput.text = ai != null ? ai.RemoteEndpoint : "";
            if (apiKeyInput != null) apiKeyInput.text = ai != null ? ai.RemoteApiKey : "";
            _plant = Mathf.Max(0, Array.IndexOf(plantIds, ai != null ? ai.DemoPlant : "tomato"));
            LoadConditions();
            _scenario = Mathf.Max(0, Array.IndexOf(_scenarioIds, ai != null ? ai.DemoScenario : "early_blight"));
            SetActive(creditsPanel, false);
            SetText(statusText, _mode == InferenceMode.Mock
                ? "Demo AI returns a simulated, clearly labelled result. Your camera scan still runs through the real quality checks."
                : "CropScan: https://cropscan-api-ovmb.onrender.com. Your own server: e.g. http://192.168.1.20:8000/v1/analyze");
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
            Paint();
            PaintMotion();
            RefreshHistory();
        }

        void OnDestroy()
        {
            if (Ctx?.History != null) Ctx.History.Changed -= RefreshHistory;
        }

        /// <summary>Past scans, kept on this phone; tapping one shows its summary.</summary>
        void RefreshHistory()
        {
            if (historyRoot == null || historyTemplate == null || Theme == null || !gameObject.activeInHierarchy) return;
            Clear(historyRoot, historyTemplate.transform, historyEmpty != null ? historyEmpty.transform : null);
            var records = Ctx.History != null ? Ctx.History.Records : null;
            bool any = records != null && records.Count > 0;
            SetActive(historyEmpty, !any);
            if (!any) return;
            for (int i = 0; i < Mathf.Min(12, records.Count); i++)
            {
                var record = records[i];
                var row = Instantiate(historyTemplate, historyRoot);
                row.gameObject.SetActive(true);
                row.Bind(Theme, record, Ctx.History.Thumbnail(record), () => Ctx.Router.ShowScanDetail(record));
            }
        }

        void SelectMode(InferenceMode mode) { _mode = mode; Paint(); }

        void LoadConditions()
        {
            var ai = Ctx.Flow != null ? Ctx.Flow.AI : null;
            string id = plantIds[Mathf.Clamp(_plant, 0, plantIds.Length - 1)];
            var ids = new List<string>(); var names = new List<string>();
            if (ai != null && ai.Database != null && ai.Database.TryResolve(id, out var plant) && plant.diseases != null)
                foreach (var d in plant.diseases) if (d != null) { ids.Add(d.diseaseId); names.Add(d.diseaseName); }
            ids.Add("healthy"); names.Add("Healthy");
            _scenarioIds = ids.ToArray();
            for (int i = 0; i < scenarioButtons.Length; i++)
            {
                bool used = i < names.Count;
                if (scenarioButtons[i] != null) scenarioButtons[i].gameObject.SetActive(used);
                if (used && scenarioLabels[i] != null) scenarioLabels[i].text = names[i];
            }
        }

        void Paint()
        {
            if (Theme == null) return;
            bool demo = _mode == InferenceMode.Mock;
            SetActive(serverFields, !demo);
            SetActive(demoFields, demo);
            Style(backendBackgrounds, backendLabels, demo ? 0 : 1);
            Style(plantBackgrounds, plantLabels, _plant);
            Style(scenarioBackgrounds, scenarioLabels, _scenario);
        }

        void Style(Image[] backgrounds, TMP_Text[] labels, int selected)
        {
            for (int i = 0; i < backgrounds.Length; i++)
            {
                bool on = i == selected;
                if (backgrounds[i] != null) backgrounds[i].color = on ? Theme.forest : Theme.ivoryDeep;
                if (labels[i] != null) labels[i].color = on ? Theme.textOnDark : Theme.textSecondary;
            }
        }

        void PaintMotion()
        {
            if (motionTick == null || Theme == null) return;
            bool on = UITheme.ReducedMotion;
            motionTick.sprite = Theme.Icon(on ? "circle-check" : "circle");
            motionTick.color = on ? Theme.botanical : Theme.textTertiary;
        }

        void Save()
        {
            var ai = Ctx.Flow != null ? Ctx.Flow.AI : null;
            if (nameInput != null) UIText.UserName = nameInput.text.Trim();
            if (ai == null) return;

            if (_mode == InferenceMode.Remote && endpointInput != null && !endpointInput.text.Trim().StartsWith("http"))
            {
                SetText(statusText, "The server address must start with http:// or https://");
                if (statusText != null) statusText.color = Theme.red;
                return;
            }
            ai.SetBackend(_mode, endpointInput != null ? endpointInput.text : null, apiKeyInput != null ? apiKeyInput.text : null);
            ai.DemoPlant = plantIds[Mathf.Clamp(_plant, 0, plantIds.Length - 1)];
            if (_scenario < _scenarioIds.Length) ai.DemoScenario = _scenarioIds[_scenario];
            GameEvents.RaiseStageContentChanged();
            Ctx.Router.Toast("Settings saved", Tone.Good);
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
                SetStatus("Enter a full address first, e.g. https://cropscan-api-ovmb.onrender.com or http://192.168.1.20:8000/v1/analyze", Theme.amber);
                yield break;
            }
            SetStatus(cropScan ? $"Contacting {url} … (a sleeping CropScan server can take up to a minute to wake)" : $"Contacting {url} …", Theme.textSecondary);
            using var request = UnityWebRequest.Get(url);
            request.timeout = cropScan ? PlantDoctor.AI.CropScanAIService.DefaultTimeoutSeconds : 6;   // a sleeping CropScan server takes up to a minute to wake
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                SetStatus(cropScan ? $"Could not reach the CropScan server ({request.error}). Check the internet connection and the address." : $"Could not reach the server ({request.error}). Same Wi-Fi? Firewall open on port 8000?", Theme.red);
                yield break;
            }
            string body = request.downloadHandler.text ?? "";
            bool loaded = body.Contains("\"model_loaded\":true") || body.Contains("\"model_loaded\": true");
            SetStatus(loaded ? "Connected. The server has a trained model loaded." : "Connected, but the server has no trained model yet: scans will report MODEL_UNAVAILABLE.",
                loaded ? Theme.botanical : Theme.amber);
        }

        void SetStatus(string text, Color color)
        {
            SetText(statusText, text);
            if (statusText != null) statusText.color = color;
        }

        /// <summary>The server's health address: CropScan's /healthz, or http://host:8000/v1/analyze -> /v1/health.</summary>
        public static string HealthUrl(string analyzeEndpoint) => PlantDoctor.AI.CropScanAIService.HealthUrlFor(analyzeEndpoint);
    }
}
