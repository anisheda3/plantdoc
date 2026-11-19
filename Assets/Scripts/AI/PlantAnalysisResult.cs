using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlantDoctor.AI
{
    public enum MeasurementSource { VisualAiEstimate, SensorMeasurement, UserInput, Unknown }
    public enum FeatureStatus { Normal, Abnormal, Unknown }
    public enum ConfidenceBand { High, Moderate, Uncertain }
    public enum AnalysisOrigin { Remote, Local, Mock }

    /// <summary>One measured/estimated plant feature returned by the AI.</summary>
    [Serializable]
    public class FeatureReading
    {
        public string featureId;
        public float value;
        public float optimal;
        public FeatureStatus status = FeatureStatus.Unknown;
        public MeasurementSource source = MeasurementSource.VisualAiEstimate;

        /// <summary>UI must never show an AI estimate as if it were a sensor reading.</summary>
        public string SourceLabel => source switch
        {
            MeasurementSource.VisualAiEstimate => "VISUAL AI ESTIMATE",
            MeasurementSource.SensorMeasurement => "SENSOR MEASUREMENT",
            MeasurementSource.UserInput => "USER INPUT",
            _ => "UNKNOWN SOURCE"
        };
    }

    /// <summary>Normalised (0..1) image-space region of interest.</summary>
    [Serializable]
    public class DetectedRegion
    {
        public string type;   // lesion | yellowing | healthy | leaf
        public Rect bounds;
        public float score;
    }

    /// <summary>
    /// The ONLY contract between the AI layer and the simulation layer.
    /// Contains no Unity scene references by design.
    /// </summary>
    [Serializable]
    public class PlantAnalysisResult
    {
        public string schemaVersion = "1.0";
        public string modelVersion = "unknown";
        public AnalysisOrigin origin = AnalysisOrigin.Mock;

        public string plantId;
        public string plantName;
        public string diseaseId;
        public string diseaseName;

        [Range(0f, 1f)] public float confidence;
        [Range(0f, 1f)] public float severity;
        public bool isHealthy;
        /// <summary>The server itself judged the result uncertain (CropScan's "uncertain"): shown as a possibility whatever the confidence.</summary>
        public bool forceUncertain;

        public List<FeatureReading> features = new List<FeatureReading>();
        public List<DetectedRegion> regions = new List<DetectedRegion>();
        public string recommendedTreatmentId;
        public List<string> warnings = new List<string>();

        public bool HasError => !string.IsNullOrEmpty(ErrorCode);
        public string ErrorCode;
        public string ErrorMessage;

        /// <summary>True when results came from MockAIService and must be watermarked in UI.</summary>
        public bool IsSimulatedResult => origin == AnalysisOrigin.Mock;

        public FeatureReading GetFeature(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < features.Count; i++)
                if (string.Equals(features[i].featureId, id, StringComparison.OrdinalIgnoreCase))
                    return features[i];
            return null;
        }

        public static PlantAnalysisResult Failure(string code, string message) =>
            new PlantAnalysisResult { ErrorCode = code, ErrorMessage = message };
    }
}
