using System.Threading;
using System.Threading.Tasks;
using PlantDoctor.Utilities;
using UnityEngine;

namespace PlantDoctor.AI
{
    /// <summary>
    /// On-device inference seam (Unity Sentis / ONNX Runtime / TFLite).
    /// Intentionally NOT faking predictions: until a real model asset is imported it
    /// reports MODEL_UNAVAILABLE so the app can fall back rather than invent a diagnosis.
    /// TODO[AI]: import the exported .onnx model, run the worker here and map outputs
    /// through AIResponseParser-equivalent logic into PlantAnalysisResult.
    /// </summary>
    public class LocalAIService : IPlantDiseaseAI
    {
        public string DisplayName => "Local AI (on-device)";
        public AnalysisOrigin Origin => AnalysisOrigin.Local;

        public bool IsModelLoaded { get; private set; }

        public Task<PlantAnalysisResult> AnalyzePlant(Texture2D image, CancellationToken token = default)
        {
            if (!IsModelLoaded)
            {
                Log.Warn(Log.Channel.AI, "Local model not loaded — no on-device inference performed.");
                return Task.FromResult(PlantAnalysisResult.Failure(
                    "MODEL_UNAVAILABLE",
                    "No on-device model is installed. Use the remote server or Demo Mode."));
            }

            // Real implementation slots in here without changing any caller.
            return Task.FromResult(PlantAnalysisResult.Failure("MODEL_UNAVAILABLE", "Inference pipeline not yet implemented."));
        }
    }
}
