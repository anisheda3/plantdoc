using System;
using System.Threading;
using System.Threading.Tasks;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using PlantDoctor.Utilities;
using UnityEngine;

namespace PlantDoctor.AI
{
    public enum InferenceMode { Mock, Remote, Local }

    /// <summary>
    /// Owns the active IPlantDiseaseAI and runs inference off the critical path.
    /// Switching backends changes nothing in UI or simulation code.
    ///
    /// The backend can also be switched at runtime (in-app settings), because on a
    /// phone the inference server's LAN address is only known on the day. The choice
    /// is persisted in PlayerPrefs and overrides the Inspector defaults on next launch.
    /// </summary>
    public class AIManager : MonoBehaviour
    {
        const string PrefMode = "plantdoctor.ai.mode";
        const string PrefEndpoint = "plantdoctor.ai.endpoint";
        const string PrefApiKey = "plantdoctor.ai.apikey";
        const string PrefScenario = "plantdoctor.ai.demoScenario";
        const string PrefDemoPlant = "plantdoctor.ai.demoPlant";

        [SerializeField] InferenceMode inferenceMode = InferenceMode.Mock;
        [SerializeField] PlantDatabase plantDatabase;
        [SerializeField] ConfidencePolicy confidencePolicy;
        [SerializeField] string remoteEndpoint = "http://localhost:8000/v1/analyze";
        [SerializeField] int requestTimeoutSeconds = 20;
        [Tooltip("Sent as the X-API-Key header when calling the remote server. Leave blank " +
                 "for a server running with PLANTDOCTOR_API_KEY unset (auth disabled).")]
        [SerializeField] string remoteApiKey = "";
        [Tooltip("Load the backend choice saved by the in-app settings sheet.")]
        [SerializeField] bool usePersistedSettings = true;

        IPlantDiseaseAI _service;
        CancellationTokenSource _cts;
        string _demoScenario = "early_blight";
        string _demoPlant = "tomato";

        public IPlantDiseaseAI Service => _service ??= CreateService();
        public ConfidencePolicy Policy => confidencePolicy;
        public PlantDatabase Database => plantDatabase;
        public InferenceMode Mode => inferenceMode;
        public string RemoteEndpoint => remoteEndpoint;
        public string RemoteApiKey => remoteApiKey;
        public bool IsBusy { get; private set; }

        /// <summary>Raised after SetBackend so settings UI and banners can refresh.</summary>
        public event Action BackendChanged;

        /// <summary>Exposed so Demo Mode UI can pick a scenario when running on mock.</summary>
        public MockAIService Mock => Service as MockAIService;

        /// <summary>Plant the demo backend reports: any plantId in the database (the 14 PlantVillage crops).</summary>
        public string DemoPlant
        {
            get => _demoPlant;
            set
            {
                _demoPlant = string.IsNullOrEmpty(value) ? "tomato" : value;
                if (Mock != null) Mock.SelectedPlantId = _demoPlant;
                PlayerPrefs.SetString(PrefDemoPlant, _demoPlant);
            }
        }

        /// <summary>Condition the demo backend reports: a disease id of the demo plant, or "healthy".</summary>
        public string DemoScenario
        {
            get => _demoScenario;
            set
            {
                _demoScenario = string.IsNullOrEmpty(value) ? "early_blight" : value;
                if (Mock != null) Mock.SelectedDiseaseId = _demoScenario;
                PlayerPrefs.SetString(PrefScenario, _demoScenario);
            }
        }

        void Awake()
        {
            if (!usePersistedSettings) return;
            if (PlayerPrefs.HasKey(PrefMode)) inferenceMode = (InferenceMode)PlayerPrefs.GetInt(PrefMode);
            remoteEndpoint = PlayerPrefs.GetString(PrefEndpoint, remoteEndpoint);
            remoteApiKey = PlayerPrefs.GetString(PrefApiKey, remoteApiKey);
            _demoScenario = PlayerPrefs.GetString(PrefScenario, _demoScenario);
            _demoPlant = PlayerPrefs.GetString(PrefDemoPlant, _demoPlant);
        }

        IPlantDiseaseAI CreateService()
        {
            switch (inferenceMode)
            {
                case InferenceMode.Remote:
                    // The CropScan XR API speaks its own protocol (/api/scans, field "file", ScanOut); anything else is the app's own server.
                    if (CropScanAIService.IsCropScan(remoteEndpoint))
                        return new CropScanAIService(remoteEndpoint);
                    return new RemoteAIService(remoteEndpoint, requestTimeoutSeconds, apiKey: remoteApiKey);
                case InferenceMode.Local:
                    return new LocalAIService();
                default:
                    Log.Warn(Log.Channel.AI, "Running with MOCK AI. Results are simulated, not real predictions.");
                    return CreateMock();
            }
        }

        /// <summary>A fresh demo backend, independent of the active one (used by "Try demo").</summary>
        public MockAIService CreateMock() =>
            new MockAIService(plantDatabase) { SelectedPlantId = _demoPlant, SelectedDiseaseId = _demoScenario };

        public void SetService(IPlantDiseaseAI service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            Log.Info(Log.Channel.AI, $"Inference backend set to {service.DisplayName}");
            BackendChanged?.Invoke();
        }

        /// <summary>Switches backend at runtime and persists the choice.</summary>
        public void SetBackend(InferenceMode mode, string endpoint = null, string apiKey = null)
        {
            if (IsBusy) CancelActiveRequest();

            inferenceMode = mode;
            if (endpoint != null) remoteEndpoint = endpoint.Trim();
            if (apiKey != null) remoteApiKey = apiKey.Trim();

            PlayerPrefs.SetInt(PrefMode, (int)inferenceMode);
            PlayerPrefs.SetString(PrefEndpoint, remoteEndpoint);
            PlayerPrefs.SetString(PrefApiKey, remoteApiKey);
            PlayerPrefs.Save();

            SetService(CreateService());
        }

        public Task<PlantAnalysisResult> Analyze(Texture2D image) => Analyze(image, null);

        /// <param name="overrideService">Run this backend instead of the active one (Demo Mode).</param>
        public async Task<PlantAnalysisResult> Analyze(Texture2D image, IPlantDiseaseAI overrideService)
        {
            if (IsBusy)
                return PlantAnalysisResult.Failure("BUSY", "An analysis is already running.");
            if (image == null)
                return PlantAnalysisResult.Failure("INVALID_IMAGE", "No image was supplied to the analyzer.");

            var service = overrideService ?? Service;
            IsBusy = true;
            _cts = new CancellationTokenSource();
            GameEvents.RaiseAIAnalysisStarted();
            Log.Info(Log.Channel.AI, $"Image submitted ({image.width}x{image.height}) via {service.DisplayName}");

            try
            {
                var result = await service.AnalyzePlant(image, _cts.Token);
                if (result == null)
                    result = PlantAnalysisResult.Failure("MALFORMED_RESPONSE", "The AI returned no result.");

                if (result.HasError) GameEvents.RaiseAIAnalysisFailed(result.ErrorMessage);
                else GameEvents.RaiseAIAnalysisCompleted(result);
                return result;
            }
            catch (OperationCanceledException)
            {
                var cancelled = PlantAnalysisResult.Failure("CANCELLED", "Analysis was cancelled.");
                GameEvents.RaiseAIAnalysisFailed(cancelled.ErrorMessage);
                return cancelled;
            }
            catch (Exception e)
            {
                Log.Error(Log.Channel.AI, $"Inference failed: {e.Message}");
                var failure = PlantAnalysisResult.Failure("AI_UNAVAILABLE", "The AI service is unavailable. Check the server or switch to Demo Mode.");
                GameEvents.RaiseAIAnalysisFailed(failure.ErrorMessage);
                return failure;
            }
            finally
            {
                IsBusy = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        public void CancelActiveRequest() => _cts?.Cancel();
        void OnDestroy() => CancelActiveRequest();

        /// <summary>
        /// Drop any in-flight inference when the app is backgrounded. A remote
        /// request would otherwise sit open burning battery and mobile data, and
        /// could resolve into a destroyed UI on resume.
        /// </summary>
        void OnApplicationPause(bool paused)
        {
            if (paused && IsBusy) CancelActiveRequest();
        }
    }
}
