using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlantDoctor.Diseases;
using PlantDoctor.Plants;
using PlantDoctor.Utilities;
using UnityEngine;

namespace PlantDoctor.AI
{
    /// <summary>
    /// DEVELOPMENT / DEMO ONLY. Produces a deterministic result from an explicitly
    /// chosen demo scenario — it does NOT invent a prediction from the image and its
    /// output is always flagged Origin = Mock so the UI can watermark "DEMO / MOCK AI".
    /// </summary>
    public class MockAIService : IPlantDiseaseAI
    {
        public string DisplayName => "DEMO / MOCK AI";
        public AnalysisOrigin Origin => AnalysisOrigin.Mock;

        readonly PlantDatabase _database;
        readonly int _simulatedLatencyMs;

        public string SelectedPlantId { get; set; } = "tomato";
        public string SelectedDiseaseId { get; set; } = "early_blight";
        public float SelectedSeverity { get; set; } = 0.68f;
        public float SelectedConfidence { get; set; } = 0.94f;

        public MockAIService(PlantDatabase database, int simulatedLatencyMs = 1200)
        {
            _database = database;
            _simulatedLatencyMs = simulatedLatencyMs;
        }

        public async Task<PlantAnalysisResult> AnalyzePlant(Texture2D image, CancellationToken token = default)
        {
            // Simulated latency so the async UI path is exercised exactly as with a real server.
            await Task.Delay(_simulatedLatencyMs, token);

            if (_database == null || !_database.TryResolve(SelectedPlantId, out var plant))
                return PlantAnalysisResult.Failure("UNSUPPORTED_PLANT",
                    $"No demo data registered for plant '{SelectedPlantId}'.");

            DiseaseData disease = plant.FindDisease(SelectedDiseaseId);
            bool healthy = string.IsNullOrEmpty(SelectedDiseaseId) || SelectedDiseaseId == "healthy";

            var result = new PlantAnalysisResult
            {
                origin = AnalysisOrigin.Mock,
                modelVersion = "mock-scenario",
                plantId = plant.plantId,
                plantName = plant.plantName,
                diseaseId = healthy ? "healthy" : disease?.diseaseId ?? SelectedDiseaseId,
                diseaseName = healthy ? "Healthy" : disease?.diseaseName ?? SelectedDiseaseId,
                confidence = Mathf.Clamp01(SelectedConfidence),
                severity = healthy ? 0f : Mathf.Clamp01(SelectedSeverity),
                isHealthy = healthy,
                recommendedTreatmentId = healthy ? null : disease?.treatmentPlan?.treatmentId,
                warnings = new List<string> { "Result produced by DEMO / MOCK AI — not a real model prediction." }
            };

            result.features = BuildFeatures(plant, result.severity);
            result.regions = BuildRegions(result.severity);

            Log.Info(Log.Channel.AI, $"[MOCK] Plant: {result.plantName}");
            Log.Info(Log.Channel.AI, $"[MOCK] Disease: {result.diseaseName}");
            Log.Info(Log.Channel.AI, $"[MOCK] Confidence: {result.confidence:0.00}");
            return result;
        }

        /// <summary>Feature values are derived from the chosen scenario severity, not random noise.</summary>
        static List<FeatureReading> BuildFeatures(PlantData plant, float severity)
        {
            var list = new List<FeatureReading>();
            if (plant.features == null) return list;

            foreach (var f in plant.features)
            {
                if (f == null) continue;
                float value = f.higherIsBetter
                    ? Mathf.Clamp01(f.optimalValue - severity * f.optimalValue * 0.4f)
                    : Mathf.Clamp01(f.optimalValue + severity * (1f - f.optimalValue));

                list.Add(new FeatureReading
                {
                    featureId = f.featureId,
                    value = value,
                    optimal = f.optimalValue,
                    status = f.Evaluate(value),
                    source = f.source
                });
            }
            return list;
        }

        static List<DetectedRegion> BuildRegions(float severity)
        {
            var regions = new List<DetectedRegion>();
            if (severity <= 0.01f) return regions;
            regions.Add(new DetectedRegion { type = "lesion", bounds = new Rect(0.31f, 0.22f, 0.18f, 0.15f), score = severity });
            regions.Add(new DetectedRegion { type = "yellowing", bounds = new Rect(0.55f, 0.48f, 0.24f, 0.20f), score = severity * 0.8f });
            return regions;
        }
    }
}
