using NUnit.Framework;
using PlantDoctor.AI;
using PlantDoctor.AR;
using PlantDoctor.Core;
using UnityEngine;

namespace PlantDoctor.Tests
{
    /// <summary>
    /// The phone camera path: orientation / aspect-fill mapping (what you see is what is
    /// analysed) and the pre-inference quality gate on realistic frames.
    /// </summary>
    public class CameraPipelineTests
    {
        const float Eps = 0.0001f;

        [Test]
        public void IdentityMappingLeavesCoordinatesAlone()
        {
            var m = ViewportMapping.ForRotatedTexture(640, 480, 0, false, 640f / 480f);
            AssertUv(new Vector2(0.25f, 0.75f), m.Map(0.25f, 0.75f));
        }

        [Test]
        public void QuarterTurnMapsCornersToTheRotatedSensorImage()
        {
            // A landscape sensor frame shown upright on a portrait screen of the same shape.
            var m = ViewportMapping.ForRotatedTexture(4, 2, 90, false, 2f / 4f);
            // Rotating clockwise moves the texture's bottom-left to the screen's top-left,
            // and its top-left to the screen's top-right.
            AssertUv(new Vector2(0f, 0f), m.Map(0f, 1f));
            AssertUv(new Vector2(0f, 1f), m.Map(1f, 1f));
            AssertUv(new Vector2(1f, 0f), m.Map(0f, 0f));
        }

        [Test]
        public void HalfTurnAndThreeQuarterTurnAreConsistent()
        {
            var half = ViewportMapping.ForRotatedTexture(4, 4, 180, false, 1f);
            AssertUv(new Vector2(1f, 1f), half.Map(0f, 0f));

            var threeQuarter = ViewportMapping.ForRotatedTexture(4, 2, 270, false, 0.5f);
            // 270 cw == 90 ccw: texture bottom-left ends at the screen's bottom-right.
            AssertUv(new Vector2(0f, 0f), threeQuarter.Map(1f, 0f));
        }

        [Test]
        public void VerticalMirrorFlipsTextureV()
        {
            var m = ViewportMapping.ForRotatedTexture(4, 4, 0, true, 1f);
            AssertUv(new Vector2(0.5f, 1f), m.Map(0.5f, 0f));
        }

        [Test]
        public void AspectFillCropsTheSidesOfAWideFrameOnAPortraitScreen()
        {
            // 16:9 landscape frame, no rotation, on a 9:16 screen: only the centre band is visible.
            var m = ViewportMapping.ForRotatedTexture(1600, 900, 0, false, 9f / 16f);
            float visible = (9f / 16f) / (16f / 9f);
            Assert.AreEqual(0.5f - visible * 0.5f, m.Map(0f, 0.5f).x, Eps, "Left screen edge must sit inside the frame.");
            Assert.AreEqual(0.5f + visible * 0.5f, m.Map(1f, 0.5f).x, Eps);
            Assert.AreEqual(0f, m.Map(0.5f, 0f).y, Eps, "Full height stays visible.");
        }

        [Test]
        public void ResampleProducesTheUprightImage()
        {
            // 2x1 texture: red on the left, blue on the right. Rotated 90 cw, red is on top.
            var src = new[] { new Color32(255, 0, 0, 255), new Color32(0, 0, 255, 255) };
            var m = ViewportMapping.ForRotatedTexture(2, 1, 90, false, 0.5f);
            var dst = new Color32[1 * 2];
            m.Resample(src, 2, 1, dst, 1, 2);
            Assert.Greater(dst[1].r, 200, "Top output pixel should be red.");
            Assert.Greater(dst[0].b, 200, "Bottom output pixel should be blue.");
        }

        [Test]
        public void OutputSizeKeepsTheViewportAspect()
        {
            var portrait = ViewportMapping.OutputSize(9f / 19.5f, 960);
            Assert.AreEqual(960, portrait.y);
            Assert.AreEqual(Mathf.RoundToInt(960 * 9f / 19.5f), portrait.x);
            Assert.AreEqual(new Vector2Int(960, 540), ViewportMapping.OutputSize(16f / 9f, 960));
        }

        [Test]
        public void FoliageTestAcceptsDiseasedLeafColoursAndRejectsBackgrounds()
        {
            Assert.IsTrue(ImageQualityAnalyzer.IsFoliage(new Color(0.25f, 0.55f, 0.20f)), "healthy green");
            Assert.IsTrue(ImageQualityAnalyzer.IsFoliage(new Color(0.80f, 0.75f, 0.25f)), "chlorotic yellow");
            Assert.IsTrue(ImageQualityAnalyzer.IsFoliage(new Color(0.45f, 0.30f, 0.12f)), "brown lesion");
            Assert.IsFalse(ImageQualityAnalyzer.IsFoliage(new Color(0.5f, 0.5f, 0.5f)), "grey wall");
            Assert.IsFalse(ImageQualityAnalyzer.IsFoliage(new Color(0.3f, 0.5f, 0.9f)), "blue sky");
            Assert.IsFalse(ImageQualityAnalyzer.IsFoliage(new Color(0.05f, 0.08f, 0.04f)), "deep shadow");
        }

        [Test]
        public void SharpLeafFrameIsAcceptedAndBlankFrameIsRejected()
        {
            var gate = ScriptableObject.CreateInstance<ImageQualityAnalyzer>();

            var leaf = Frame(240, 320, (x, y) =>
            {
                // Mid-green with vein-like stripes and fine texture.
                float vein = (x % 9 == 0) ? 0.18f : 0f;
                float grain = ((x * 7 + y * 13) % 5) * 0.03f;
                return new Color(0.22f + vein, 0.52f + grain + vein, 0.18f);
            });
            var report = gate.Analyze(leaf, 0f, new Rect(0.15f, 0.3f, 0.7f, 0.4f));
            Assert.IsTrue(report.IsAcceptable, $"A sharp, well-lit leaf must pass. Issues: {string.Join(",", report.Issues)} " +
                                               $"(sharp {report.Sharpness:0.000}, cover {report.Coverage:0.00}, bright {report.Brightness:0.00})");

            var wall = Frame(240, 320, (x, y) => new Color(0.6f, 0.6f, 0.6f));
            var wallReport = gate.Analyze(wall, 0f, new Rect(0.15f, 0.3f, 0.7f, 0.4f));
            Assert.Contains(ScanIssue.LeafTooSmall, wallReport.Issues);
            Assert.Contains(ScanIssue.ExcessiveBlur, wallReport.Issues);

            Object.DestroyImmediate(leaf);
            Object.DestroyImmediate(wall);
        }

        [Test]
        public void QualityGateOnlyLooksInsideTheFramedRegion()
        {
            var gate = ScriptableObject.CreateInstance<ImageQualityAnalyzer>();
            // Leaf texture in the centre, grey everywhere else.
            var frame = Frame(200, 200, (x, y) =>
                x > 60 && x < 140 && y > 60 && y < 140
                    ? new Color(0.2f + ((x + y) % 4) * 0.05f, 0.55f, 0.18f)
                    : new Color(0.55f, 0.55f, 0.55f));

            var whole = gate.Analyze(frame, 0f, new Rect(0f, 0f, 1f, 1f));
            var centre = gate.Analyze(frame, 0f, new Rect(0.32f, 0.32f, 0.36f, 0.36f));
            Assert.Greater(centre.Coverage, whole.Coverage);
            Assert.Greater(centre.Coverage, 0.9f);
            Object.DestroyImmediate(frame);
        }

        [Test]
        public void MotionAboveThresholdIsRejected()
        {
            var gate = ScriptableObject.CreateInstance<ImageQualityAnalyzer>();
            var frame = Frame(64, 64, (x, y) => new Color(0.2f, 0.5f + (x % 3) * 0.1f, 0.2f));
            Assert.Contains(ScanIssue.ExcessiveMotion, gate.Analyze(frame, 0.9f).Issues);
            Object.DestroyImmediate(frame);
        }

        static Texture2D Frame(int w, int h, System.Func<int, int, Color> pixel)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = pixel(x, y);
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static void AssertUv(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Eps, $"u: expected {expected}, got {actual}");
            Assert.AreEqual(expected.y, actual.y, Eps, $"v: expected {expected}, got {actual}");
        }
    }
}
