using PlantDoctor.Core;
using UnityEngine;

namespace PlantDoctor.AI
{
    /// <summary>
    /// Pre-inference gate: rejects obviously unusable frames before they reach the classifier.
    /// Uses cheap CPU metrics on small samples of the region the user framed, so it is
    /// mobile-friendly and runs on the live preview several times a second.
    /// </summary>
    [CreateAssetMenu(menuName = "PlantDoctor/AI/Image Quality Settings", fileName = "ImageQualitySettings")]
    public class ImageQualityAnalyzer : ScriptableObject
    {
        [Range(0f, 1f)] public float minBrightness = 0.16f;
        [Range(0f, 1f)] public float maxBrightness = 0.94f;

        [Tooltip("Mean absolute Laplacian of the framed region, sampled near native resolution. " +
                 "Leaves are smooth surfaces, so this is deliberately lenient.")]
        [Range(0f, 1f)] public float minSharpness = 0.018f;

        [Tooltip("Fraction of the framed region that looks like foliage (green, yellow or brown). " +
                 "Diseased leaves are not green, so a green-only test would reject the very images we need.")]
        [Range(0f, 1f)] public float minFoliageCoverage = 0.25f;

        [Range(0f, 1f)] public float maxMotion = 0.35f;

        [Tooltip("Side length of the brightness / coverage sample grid.")]
        [Min(8)] public int sampleWidth = 48;

        [Tooltip("Side length of the sharpness sample grid. Larger = closer to native resolution.")]
        [Min(16)] public int sharpnessSampleSize = 128;

        // Reused across calls. Analyze() runs on the live-preview loop several
        // times a second, and a fresh Color[] each time caused a steady GC
        // sawtooth that showed up as preview stutter on mobile.
        Color[] _sampleBuffer;
        float[] _lumBuffer;

        public ScanQualityReport Analyze(Texture2D image, float cameraMotion = 0f) =>
            Analyze(image, cameraMotion, new Rect(0f, 0f, 1f, 1f));

        /// <param name="regionOfInterest">Normalised 0..1 texture-space rect the user framed.</param>
        public ScanQualityReport Analyze(Texture2D image, float cameraMotion, Rect regionOfInterest)
        {
            var report = new ScanQualityReport { Motion = cameraMotion };
            if (image == null)
            {
                report.Issues.Add(ScanIssue.Occlusion);
                return report;
            }

            if (regionOfInterest.width <= 0f || regionOfInterest.height <= 0f)
                regionOfInterest = new Rect(0f, 0f, 1f, 1f);

            int n = Mathf.Max(8, sampleWidth);
            var pixels = Sample(image, regionOfInterest, n);

            float luminanceSum = 0f, foliage = 0f;
            for (int i = 0; i < n * n; i++)
            {
                var c = pixels[i];
                luminanceSum += Lum(c);
                if (IsFoliage(c)) foliage++;
            }

            report.Brightness = luminanceSum / (n * n);
            report.Coverage = foliage / (n * n);
            report.Sharpness = EstimateSharpness(image, regionOfInterest);

            if (report.Brightness < minBrightness || report.Brightness > maxBrightness)
                report.Issues.Add(ScanIssue.InsufficientLighting);
            if (report.Sharpness < minSharpness)
                report.Issues.Add(ScanIssue.ExcessiveBlur);
            if (report.Coverage < minFoliageCoverage)
                report.Issues.Add(ScanIssue.LeafTooSmall);
            if (cameraMotion > maxMotion)
                report.Issues.Add(ScanIssue.ExcessiveMotion);

            return report;
        }

        /// <summary>
        /// Foliage test in HSV: hue between orange-brown (~20°) and teal (~170°), with enough
        /// saturation and value to exclude grey walls, white paper, sky and shadow.
        /// </summary>
        public static bool IsFoliage(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            float hue = h * 360f;
            return hue >= 20f && hue <= 170f && s >= 0.18f && v >= 0.12f;
        }

        Color[] Sample(Texture2D source, Rect roi, int n)
        {
            if (_sampleBuffer == null || _sampleBuffer.Length < n * n)
                _sampleBuffer = new Color[n * n];

            for (int y = 0; y < n; y++)
            {
                float v = roi.y + (y + 0.5f) / n * roi.height;
                for (int x = 0; x < n; x++)
                {
                    float u = roi.x + (x + 0.5f) / n * roi.width;
                    _sampleBuffer[y * n + x] = source.GetPixelBilinear(u, v);
                }
            }
            return _sampleBuffer;
        }

        /// <summary>
        /// Normalised mean absolute Laplacian over the centre of the framed region.
        /// Sampled close to native resolution: measuring blur on a heavily
        /// downsampled copy hides the very detail that distinguishes sharp from soft.
        /// </summary>
        float EstimateSharpness(Texture2D source, Rect roi)
        {
            // Never sample finer than the real pixels, or interpolation fakes smoothness.
            int nativeW = Mathf.Max(3, Mathf.RoundToInt(source.width * roi.width));
            int nativeH = Mathf.Max(3, Mathf.RoundToInt(source.height * roi.height));
            int n = Mathf.Clamp(Mathf.Min(nativeW, nativeH), 3, Mathf.Max(16, sharpnessSampleSize));

            if (_lumBuffer == null || _lumBuffer.Length < n * n)
                _lumBuffer = new float[n * n];

            // Centre square of the ROI, one sample per (approximate) source pixel.
            float spanU = n / (float)source.width;
            float spanV = n / (float)source.height;
            float u0 = roi.center.x - spanU * 0.5f;
            float v0 = roi.center.y - spanV * 0.5f;

            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    _lumBuffer[y * n + x] = Lum(source.GetPixelBilinear(
                        u0 + (x + 0.5f) / source.width, v0 + (y + 0.5f) / source.height));

            float sum = 0f; int count = 0;
            for (int y = 1; y < n - 1; y++)
            {
                for (int x = 1; x < n - 1; x++)
                {
                    int i = y * n + x;
                    float lap = 4f * _lumBuffer[i] - _lumBuffer[i - 1] - _lumBuffer[i + 1]
                                - _lumBuffer[i - n] - _lumBuffer[i + n];
                    sum += Mathf.Abs(lap);
                    count++;
                }
            }
            return count == 0 ? 0f : Mathf.Clamp01(sum / count);
        }

        static float Lum(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
    }
}
