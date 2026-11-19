using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlantDoctor.Diseases;
using PlantDoctor.Utilities;
using UnityEngine;
using UnityEngine.Networking;

namespace PlantDoctor.AI
{
    /// <summary>
    /// The CropScan XR API (e.g. https://cropscan-api-ovmb.onrender.com, the backend of crop-scan-xr-frontend.vercel.app). It is not
    /// the app's own FastAPI server, so it gets its own client:
    ///   * POST {base}/api/scans, multipart field "file" (not "image") -> ScanOut {status, disease_id, confidence, top3[], looks_like_crop};
    ///   * GET {base}/healthz -> {status, model_loaded, model_version, ...};
    ///   * class names are PlantVillage-style ("Tomato___Late_blight", "Cotton___Fusarium_wilt"), resolved to the app's plant and
    ///     disease through the content database's label table (dataset CropScan) and the diseases' own aliases;
    ///   * a free Render service sleeps when idle and takes up to about a minute to wake, so requests wait 90 s.
    /// CropScan reports what the disease is, not how severe it is; the simulation starts at a moderate severity and says so.
    /// </summary>
    public class CropScanAIService : IPlantDiseaseAI
    {
        public const int DefaultTimeoutSeconds = 90;
        /// <summary>The simulation's starting severity when the server reports none.</summary>
        public const float AssumedSeverity = 0.5f;

        public string DisplayName => "CropScan XR server";
        public AnalysisOrigin Origin => AnalysisOrigin.Remote;

        readonly string _scanUrl;
        readonly int _timeoutSeconds;
        readonly int _jpegQuality;

        public CropScanAIService(string endpoint, int timeoutSeconds = DefaultTimeoutSeconds, int jpegQuality = 88)
        {
            _scanUrl = ScanUrl(endpoint);
            _timeoutSeconds = Mathf.Max(timeoutSeconds, DefaultTimeoutSeconds);
            _jpegQuality = Mathf.Clamp(jpegQuality, 40, 100);
        }

        // ------------------------------------------------------------------ addresses
        /// <summary>True for a CropScan address: one ending in /api/scans, or a host named cropscan.</summary>
        public static bool IsCropScan(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint)) return false;
            string e = endpoint.Trim().ToLowerInvariant();
            return e.Contains("/api/scans") || (Uri.TryCreate(e, UriKind.Absolute, out var u) && u.Host.Contains("cropscan"));
        }

        /// <summary>The server's root: everything before /api/, /healthz or /docs.</summary>
        public static string BaseUrl(string endpoint)
        {
            string e = (endpoint ?? "").Trim().TrimEnd('/');
            foreach (var cut in new[] { "/api/", "/healthz", "/docs" })
            {
                int i = e.IndexOf(cut, StringComparison.OrdinalIgnoreCase);
                if (i > 0) return e.Substring(0, i);
            }
            return e;
        }

        public static string ScanUrl(string endpoint) => BaseUrl(endpoint) + "/api/scans?lang=en";
        public static string HealthUrl(string endpoint) => BaseUrl(endpoint) + "/healthz";

        /// <summary>
        /// The health address for whichever server the endpoint names: CropScan's /healthz, or the app's own server
        /// (http://host:8000/v1/analyze -> /v1/health; the legacy /analyze -> /health). Null when the address is not a full URL.
        /// </summary>
        public static string HealthUrlFor(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint) || !endpoint.Trim().StartsWith("http")) return null;
            if (IsCropScan(endpoint)) return HealthUrl(endpoint);
            string e = endpoint.Trim().TrimEnd('/');
            if (e.EndsWith("/analyze")) e = e.Substring(0, e.Length - "/analyze".Length);
            return e + "/health";
        }

        // ------------------------------------------------------------------ request
        public async Task<PlantAnalysisResult> AnalyzePlant(Texture2D image, CancellationToken token = default)
        {
            if (image == null) return PlantAnalysisResult.Failure("INVALID_IMAGE", "No image supplied.");
            byte[] jpeg;
            try { jpeg = image.EncodeToJPG(_jpegQuality); }
            catch (Exception e)
            {
                Log.Error(Log.Channel.AI, $"Encode failed: {e.Message}");
                return PlantAnalysisResult.Failure("INVALID_IMAGE", "The captured image could not be encoded.");
            }

            var form = new WWWForm();
            form.AddBinaryData("file", jpeg, "scan.jpg", "image/jpeg");
            using var request = UnityWebRequest.Post(_scanUrl, form);
            request.timeout = _timeoutSeconds;
            try
            {
                var op = request.SendWebRequest();
                while (!op.isDone)
                {
                    if (token.IsCancellationRequested) { request.Abort(); token.ThrowIfCancellationRequested(); }
                    await Task.Yield();
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Log.Error(Log.Channel.AI, $"Request error: {e.Message}");
                return PlantAnalysisResult.Failure("AI_UNAVAILABLE", "Could not reach the CropScan server.");
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Log.Warn(Log.Channel.AI, $"CropScan HTTP {request.responseCode}: {request.error}");
                return HttpFailure(request.responseCode, request.result == UnityWebRequest.Result.ConnectionError, request.error,
                                   request.downloadHandler?.text);
            }
            return Parse(request.downloadHandler.text);
        }

        public static PlantAnalysisResult HttpFailure(long code, bool connectionError, string error, string body)
        {
            if (code == 429) return PlantAnalysisResult.Failure("RATE_LIMITED", "The CropScan server is busy. Please wait a moment and try again.");
            if (code == 413) return PlantAnalysisResult.Failure("INVALID_IMAGE", "The photo is too large for the CropScan server.");
            if (code == 415 || code == 422 || code == 400)
                return PlantAnalysisResult.Failure("INVALID_IMAGE", $"The CropScan server could not use this photo ({Detail(body) ?? code.ToString()}).");
            if (code == 503 || code == 502 || code == 504)
                return PlantAnalysisResult.Failure("AI_UNAVAILABLE", "The CropScan server is starting up or unavailable. Try again in a minute.");
            if (connectionError || code == 0)
                return PlantAnalysisResult.Failure("NETWORK_UNAVAILABLE",
                    error != null && error.ToLowerInvariant().Contains("timeout")
                        ? "The CropScan server did not answer in time. A sleeping server can take up to a minute to wake: try again."
                        : "No connection to the CropScan server. Check the internet connection, or continue in Demo Mode.");
            return PlantAnalysisResult.Failure("MODEL_ERROR", $"CropScan server error ({code}{(Detail(body) is string d ? ": " + d : "")}).");
        }

        static string Detail(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try { return MiniJson.Parse(body) is Dictionary<string, object> n && n.TryGetValue("detail", out var d) && d is string s ? s : null; }
            catch { return null; }
        }

        // ------------------------------------------------------------------ response
        /// <summary>A CropScan ScanOut -> the app's result. Never throws.</summary>
        public static PlantAnalysisResult Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return PlantAnalysisResult.Failure("MALFORMED_RESPONSE", "Empty response from the CropScan server.");
            try
            {
                if (!(MiniJson.Parse(json) is Dictionary<string, object> n))
                    return PlantAnalysisResult.Failure("MALFORMED_RESPONSE", "The CropScan response was not a JSON object.");

                if (n.TryGetValue("looks_like_crop", out var crop) && crop is bool isCrop && !isCrop)
                    return PlantAnalysisResult.Failure("NOT_A_PLANT", "The CropScan server says this photo does not look like a crop leaf. Fill the frame with one leaf and try again.");

                string status = MiniJson.GetString(n, "status", "");
                string classId = MiniJson.GetString(n, "disease_id", null);
                // CropScan leaves disease_id empty (status "uncertain") when it is not confident enough for a verdict.
                bool uncertain = string.IsNullOrEmpty(classId) || status.IndexOf("uncertain", StringComparison.OrdinalIgnoreCase) >= 0;
                float confidence = MiniJson.GetFloat(n, "confidence", 0f);
                string name = null;
                var top = n.TryGetValue("top3", out var t) ? t as List<object> : null;
                if (top != null && top.Count > 0 && top[0] is Dictionary<string, object> first)
                {
                    name = MiniJson.GetString(first, "name", null);
                    if (string.IsNullOrEmpty(classId))
                    {
                        // Not confident enough for a verdict: the best guess, shown as a possibility only.
                        classId = MiniJson.GetString(first, "disease_id", null);
                        confidence = MiniJson.GetFloat(first, "confidence", confidence);
                    }
                }
                if (string.IsNullOrEmpty(classId))
                    return PlantAnalysisResult.Failure("NO_DIAGNOSIS", "The CropScan server could not identify a disease in this photo. Try a closer, sharper photo of one leaf.");

                if (!Resolve(classId, out var plantId, out var diseaseId))
                    return PlantAnalysisResult.Failure("UNSUPPORTED_PLANT", $"CropScan reported '{classId}', which this app has no lesson for yet.");

                bool healthy = diseaseId == "healthy";
                var r = new PlantAnalysisResult
                {
                    origin = AnalysisOrigin.Remote,
                    schemaVersion = "cropscan",
                    modelVersion = MiniJson.GetString(n, "model_version", "cropscan"),
                    plantId = plantId,
                    diseaseId = diseaseId,
                    diseaseName = healthy ? "Healthy" : name ?? classId,
                    confidence = Mathf.Clamp01(confidence),
                    severity = healthy ? 0f : AssumedSeverity,
                    isHealthy = healthy,
                    forceUncertain = uncertain
                };
                if (uncertain)
                    r.warnings.Add("The CropScan server was not confident about this photo: treat this as a possibility, not a diagnosis.");
                if (!healthy)
                    r.warnings.Add("CropScan identifies the disease but not its severity: the simulation starts at a moderate level.");
                return r;
            }
            catch (Exception e)
            {
                Log.Error(Log.Channel.AI, $"Failed to parse CropScan response: {e.Message}");
                return PlantAnalysisResult.Failure("MALFORMED_RESPONSE", "The CropScan response could not be read.");
            }
        }

        /// <summary>
        /// A CropScan class name -> the app's plant and disease ("healthy" for healthy classes): first the content database's label
        /// table, then the PlantVillage naming ("Crop___Disease"), leaving the class name as the disease id for the aliases to match.
        /// </summary>
        public static bool Resolve(string classId, out string plantId, out string diseaseId)
        {
            if (ContentDatabase.TryResolveLabel(classId, null, out plantId, out diseaseId)) return true;
            plantId = diseaseId = null;
            int sep = classId.IndexOf("___", StringComparison.Ordinal);
            if (sep <= 0) return false;
            string crop = classId.Substring(0, sep);
            int cut = crop.IndexOfAny(new[] { '(', ',' });
            if (cut > 0) crop = crop.Substring(0, cut);
            plantId = crop.Trim('_', ' ').ToLowerInvariant();
            string rest = classId.Substring(sep + 3);
            diseaseId = rest.Equals("healthy", StringComparison.OrdinalIgnoreCase) ? "healthy" : classId;
            return true;
        }
    }
}
