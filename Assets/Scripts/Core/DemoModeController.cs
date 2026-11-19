using System.Threading.Tasks;
using PlantDoctor.AI;
using PlantDoctor.Plants;
using PlantDoctor.Utilities;
using UnityEngine;

namespace PlantDoctor.Core
{
    /// <summary>
    /// DEMO MODE: lets a presenter pick plant + condition and run the whole loop with no
    /// camera and no server. Results are produced by MockAIService and are always
    /// labelled "DEMO / MOCK AI" — they are never presented as real predictions.
    ///
    /// Demo Mode uses its own mock backend, so it stays available even when the app is
    /// configured for a remote server (e.g. the server is down during a presentation).
    /// </summary>
    public class DemoModeController : MonoBehaviour
    {
        [SerializeField] AppFlowController flow;
        [SerializeField] PlantDatabase plantDatabase;
        [SerializeField] Texture2D placeholderScanImage;

        public const string DemoBannerText = "DEMO / MOCK AI — simulated result, not a real model prediction";

        public bool IsAvailable => flow != null && flow.AI != null;

        public string[] AvailablePlantIds()
        {
            if (plantDatabase == null) return System.Array.Empty<string>();
            var all = plantDatabase.All;
            var ids = new string[all.Count];
            for (int i = 0; i < all.Count; i++) ids[i] = all[i].plantId;
            return ids;
        }

        public string[] AvailableConditionIds(string plantId)
        {
            if (plantDatabase == null || !plantDatabase.TryResolve(plantId, out var plant) || plant.diseases == null)
                return new[] { "healthy" };

            var ids = new string[plant.diseases.Length + 1];
            ids[0] = "healthy";
            for (int i = 0; i < plant.diseases.Length; i++)
                ids[i + 1] = plant.diseases[i] != null ? plant.diseases[i].diseaseId : "unknown";
            return ids;
        }

        /// <summary>Runs the scenario currently selected in Settings.</summary>
        public Task RunSelectedScenario() =>
            flow != null && flow.AI != null
                ? RunScenario(flow.AI.DemoPlant, flow.AI.DemoScenario)
                : RunScenario("tomato", "early_blight");

        /// <summary>Runs the full pipeline for a chosen scenario.</summary>
        public async Task RunScenario(string plantId, string conditionId, float severity = 0.68f, float confidence = 0.94f)
        {
            if (!IsAvailable)
            {
                Log.Warn(Log.Channel.Core, "Demo Mode requires an AppFlowController with an AIManager.");
                return;
            }

            var mock = flow.AI.CreateMock();
            mock.SelectedPlantId = plantId;
            mock.SelectedDiseaseId = conditionId;
            mock.SelectedSeverity = severity;
            mock.SelectedConfidence = confidence;

            Log.Info(Log.Channel.Core, $"[DEMO MODE] scenario {plantId}/{conditionId} severity {severity:0.00}");

            // Bypasses the quality gate: there is no real camera frame in demo mode.
            flow.ClearLastScan();
            flow.GoTo(SimState.AiAnalysis);
            var result = await flow.AI.Analyze(
                placeholderScanImage != null ? placeholderScanImage : Texture2D.whiteTexture, mock);
            flow.ApplyAnalysis(result);
        }
    }
}
