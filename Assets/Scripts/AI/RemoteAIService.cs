using System;
using System.Threading;
using System.Threading.Tasks;
using PlantDoctor.Utilities;
using UnityEngine;
using UnityEngine.Networking;

namespace PlantDoctor.AI
{
    /// <summary>
    /// Posts the preprocessed image to the FastAPI inference server.
    /// Uses UnityWebRequest wrapped in a Task so callers can simply await.
    /// </summary>
    public class RemoteAIService : IPlantDiseaseAI
    {
        public string DisplayName => "Remote AI (FastAPI)";
        public AnalysisOrigin Origin => AnalysisOrigin.Remote;

        readonly string _endpoint;
        readonly int _timeoutSeconds;
        readonly int _jpegQuality;
        readonly string _apiKey;

        public RemoteAIService(string endpoint, int timeoutSeconds = 20, int jpegQuality = 85, string apiKey = null)
        {
            _endpoint = endpoint;
            _timeoutSeconds = timeoutSeconds;
            _jpegQuality = Mathf.Clamp(jpegQuality, 40, 100);
            _apiKey = apiKey;
        }

        public async Task<PlantAnalysisResult> AnalyzePlant(Texture2D image, CancellationToken token = default)
        {
            if (image == null)
                return PlantAnalysisResult.Failure("INVALID_IMAGE", "No image supplied.");

            byte[] jpeg;
            try { jpeg = image.EncodeToJPG(_jpegQuality); }
            catch (Exception e)
            {
                Log.Error(Log.Channel.AI, $"Encode failed: {e.Message}");
                return PlantAnalysisResult.Failure("INVALID_IMAGE", "The captured image could not be encoded.");
            }

            var form = new WWWForm();
            form.AddBinaryData("image", jpeg, "scan.jpg", "image/jpeg");
            form.AddField("client_version", Application.version ?? "dev");

            using var request = UnityWebRequest.Post(_endpoint, form);
            request.timeout = _timeoutSeconds;

            // Sent whenever the server has PLANTDOCTOR_API_KEY configured. Harmless to
            // send even against a server running with authentication disabled (unset key).
            if (!string.IsNullOrEmpty(_apiKey))
                request.SetRequestHeader("X-API-Key", _apiKey);

            try
            {
                var op = request.SendWebRequest();
                while (!op.isDone)
                {
                    if (token.IsCancellationRequested)
                    {
                        request.Abort();
                        token.ThrowIfCancellationRequested();
                    }
                    await Task.Yield();
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Log.Error(Log.Channel.AI, $"Request error: {e.Message}");
                return PlantAnalysisResult.Failure("AI_UNAVAILABLE", "Could not reach the AI server.");
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Log.Warn(Log.Channel.AI, $"HTTP {request.responseCode}: {request.error}");

                if (request.responseCode == 401)
                    return PlantAnalysisResult.Failure("UNAUTHORIZED",
                        "The AI server rejected this request (missing or invalid API key). Check the app's configured key.");

                if (request.responseCode == 429)
                    return PlantAnalysisResult.Failure("RATE_LIMITED",
                        "Too many requests right now. Please wait a moment and try again.");

                if (request.result == UnityWebRequest.Result.ConnectionError)
                    return PlantAnalysisResult.Failure("NETWORK_UNAVAILABLE",
                        $"No connection to the AI server at {_endpoint}. Check the address in Settings, or continue in Demo Mode.");

                // The server reports *why* it failed (MODEL_UNAVAILABLE, INVALID_IMAGE, ...)
                // in a JSON body. Surfacing that beats a bare status code.
                return ErrorFromBody(request.downloadHandler?.text, request.responseCode);
            }

            return AIResponseParser.Parse(request.downloadHandler.text, AnalysisOrigin.Remote);
        }

        /// <summary>Turns an HTTP error body into a failure, keeping the server's own code if present.</summary>
        public static PlantAnalysisResult ErrorFromBody(string body, long statusCode)
        {
            if (!string.IsNullOrWhiteSpace(body) && body.Contains("error_code"))
            {
                var parsed = AIResponseParser.Parse(body, AnalysisOrigin.Remote);
                if (parsed.HasError && parsed.ErrorCode != "MALFORMED_RESPONSE")
                {
                    if (parsed.ErrorCode == "MODEL_UNAVAILABLE")
                        parsed.ErrorMessage = "The AI server is running but has no trained model installed yet. " +
                                              "Use Demo Mode, or install a model on the server.";
                    return parsed;
                }
            }
            return PlantAnalysisResult.Failure("MODEL_ERROR", $"AI server error ({statusCode}).");
        }
    }
}
