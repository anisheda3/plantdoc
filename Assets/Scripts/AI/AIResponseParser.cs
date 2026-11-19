using System;
using System.Collections.Generic;
using PlantDoctor.Utilities;
using UnityEngine;

namespace PlantDoctor.AI
{
    // Wire DTOs mirroring the FastAPI schema. Kept separate from the domain model
    // so the server contract can evolve without touching the simulation.
    [Serializable] public class WireFeature { public float value; public float optimal; public string status; public string source; }
    [Serializable] public class WireRegion  { public string type; public float[] bbox; public float score; }
    [Serializable] public class WireRecommendation { public string treatment_id; }

    /// <summary>
    /// Tolerant JSON parser. Malformed or partial payloads degrade to a safe result
    /// with an error code instead of throwing.
    /// </summary>
    public static class AIResponseParser
    {
        public static PlantAnalysisResult Parse(string json, AnalysisOrigin origin)
        {
            if (string.IsNullOrWhiteSpace(json))
                return PlantAnalysisResult.Failure("MALFORMED_RESPONSE", "Empty response from AI service.");

            try
            {
                var node = MiniJson.Parse(json) as Dictionary<string, object>;
                if (node == null)
                    return PlantAnalysisResult.Failure("MALFORMED_RESPONSE", "Response was not a JSON object.");

                if (node.TryGetValue("error_code", out var errCode) && errCode != null)
                    return PlantAnalysisResult.Failure(errCode.ToString(), MiniJson.GetString(node, "message", "The AI service reported an error."));

                var r = new PlantAnalysisResult
                {
                    origin = origin,
                    schemaVersion = MiniJson.GetString(node, "schema_version", "1.0"),
                    modelVersion = MiniJson.GetString(node, "model_version", "unknown"),
                    plantId = MiniJson.GetString(node, "plant_id", null),
                    plantName = MiniJson.GetString(node, "plant_name", null),
                    diseaseId = MiniJson.GetString(node, "disease_id", null),
                    diseaseName = MiniJson.GetString(node, "disease_name", null),
                    confidence = Mathf.Clamp01(MiniJson.GetFloat(node, "confidence", 0f)),
                    severity = Mathf.Clamp01(MiniJson.GetFloat(node, "severity", 0f)),
                    isHealthy = MiniJson.GetBool(node, "is_healthy", false)
                };

                if (string.IsNullOrEmpty(r.plantId))
                    return PlantAnalysisResult.Failure("MALFORMED_RESPONSE", "Response is missing 'plant_id'.");

                if (node.TryGetValue("features", out var fObj) && fObj is Dictionary<string, object> fMap)
                {
                    foreach (var kv in fMap)
                    {
                        if (kv.Value is not Dictionary<string, object> fv) continue;
                        r.features.Add(new FeatureReading
                        {
                            featureId = kv.Key,
                            value = Mathf.Clamp01(MiniJson.GetFloat(fv, "value", 0f)),
                            optimal = Mathf.Clamp01(MiniJson.GetFloat(fv, "optimal", 0f)),
                            status = ParseStatus(MiniJson.GetString(fv, "status", null)),
                            source = ParseSource(MiniJson.GetString(fv, "source", "visual_ai"))
                        });
                    }
                }

                if (node.TryGetValue("regions", out var regObj) && regObj is List<object> regList)
                {
                    foreach (var item in regList)
                    {
                        if (item is not Dictionary<string, object> rd) continue;
                        var bbox = MiniJson.GetFloatArray(rd, "bbox");
                        r.regions.Add(new DetectedRegion
                        {
                            type = MiniJson.GetString(rd, "type", "region"),
                            score = MiniJson.GetFloat(rd, "score", 0f),
                            bounds = bbox != null && bbox.Length == 4
                                ? new Rect(bbox[0], bbox[1], bbox[2], bbox[3]) : new Rect()
                        });
                    }
                }

                if (node.TryGetValue("recommendation", out var recObj) && recObj is Dictionary<string, object> rec)
                    r.recommendedTreatmentId = MiniJson.GetString(rec, "treatment_id", null);

                if (node.TryGetValue("warnings", out var wObj) && wObj is List<object> wList)
                    foreach (var w in wList) if (w != null) r.warnings.Add(w.ToString());

                return r;
            }
            catch (Exception e)
            {
                Log.Error(Log.Channel.AI, $"Failed to parse AI response: {e.Message}");
                return PlantAnalysisResult.Failure("MALFORMED_RESPONSE", "The AI response could not be read.");
            }
        }

        static FeatureStatus ParseStatus(string s) => s?.ToLowerInvariant() switch
        {
            "abnormal" => FeatureStatus.Abnormal,
            "normal" => FeatureStatus.Normal,
            _ => FeatureStatus.Unknown
        };

        static MeasurementSource ParseSource(string s) => s?.ToLowerInvariant() switch
        {
            "sensor" or "sensor_measurement" => MeasurementSource.SensorMeasurement,
            "user" or "user_input" => MeasurementSource.UserInput,
            "visual_ai" or "visual_ai_estimate" => MeasurementSource.VisualAiEstimate,
            _ => MeasurementSource.Unknown
        };
    }
}
