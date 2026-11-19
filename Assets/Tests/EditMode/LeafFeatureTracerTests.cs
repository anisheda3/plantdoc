using NUnit.Framework;
using PlantDoctor.AI;
using UnityEngine;

namespace PlantDoctor.Tests
{
    /// <summary>The scan's spots are traced from the pixels and planted on the 3D leaves.</summary>
    public class LeafFeatureTracerTests
    {
        const int Size = 224;
        static readonly Color32 Soil = new Color32(60, 40, 25, 255);
        static readonly Color32 Green = new Color32(60, 150, 50, 255);
        static readonly Color32 Brown = new Color32(90, 55, 25, 255);
        static readonly Color32 Yellow = new Color32(210, 190, 50, 255);

        TraceResult _trace;

        [TearDown]
        public void TearDown() => _trace?.Dispose();

        /// <summary>A green leaf (ellipse, 70 x 95 px) on dark soil, with the given brown spots and yellow patches (x, y, radius; y from the top).</summary>
        static Color32[] Leaf(Vector3[] lesions, Vector3[] yellow)
        {
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float ex = (x - 112f) / 70f, ey = (y - 112f) / 95f;
                    var c = ex * ex + ey * ey <= 1f ? Green : Soil;
                    foreach (var p in yellow) if ((x - p.x) * (x - p.x) + (y - p.y) * (y - p.y) <= p.z * p.z && c.Equals(Green)) c = Yellow;
                    foreach (var p in lesions) if ((x - p.x) * (x - p.x) + (y - p.y) * (y - p.y) <= p.z * p.z) c = Brown;
                    px[(Size - 1 - y) * Size + x] = c;   // texture rows run from the bottom
                }
            return px;
        }

        [Test]
        public void FindsTheSpotsAndPatchesOnTheLeafButNotTheSoilAroundIt()
        {
            _trace = LeafFeatureTracer.Trace(Leaf(
                new[] { new Vector3(100, 90, 9), new Vector3(130, 130, 7), new Vector3(95, 140, 6) },
                new[] { new Vector3(135, 80, 14) }), Size, Size);

            Assert.IsTrue(_trace.IsUsable);
            Assert.AreEqual(0.416f, _trace.LeafCoverage, 0.04f, "The leaf covers about 42% of the picture.");
            Assert.AreEqual(3, _trace.LesionCount, "Three brown spots; the dark soil outside the leaf is not a lesion.");
            Assert.AreEqual(1, _trace.YellowCount);

            var biggest = _trace.Features[0];
            Assert.AreEqual(TracedKind.Lesion, biggest.Kind);
            Assert.AreEqual(100f / Size, biggest.Centroid.x, 0.02f);
            Assert.AreEqual(90f / Size, biggest.Centroid.y, 0.02f);
            Assert.AreEqual(Mathf.PI * 81f / (Mathf.PI * 70f * 95f), biggest.AreaOfLeaf, 0.004f);

            var regions = _trace.ToRegions();
            Assert.AreEqual(4, regions.Count);
            Assert.IsTrue(regions.Exists(r => r.type == "yellowing"));
            Assert.AreEqual("3 lesions, 1 yellow area", _trace.Summary());

            // Affected share of the leaf: three discs (r 9, 7, 6) and a yellow disc (r 14) on an ellipse of 70 x 95 px.
            float expected = (Mathf.PI * (81f + 49f + 36f + 196f)) / (Mathf.PI * 70f * 95f);
            Assert.AreEqual(expected, _trace.AffectedFraction, 0.02f);
            StringAssert.Contains("about 5% of the leaf", _trace.Caption());   // rounded to the nearest 5%
        }

        [Test]
        public void PlantsTheSpotsWhereTheyAreOnTheLeaf()
        {
            _trace = LeafFeatureTracer.Trace(Leaf(new[] { new Vector3(100, 90, 9) }, new Vector3[0]), Size, Size);
            var mask = _trace.HeroLesionMask;

            // The leaf's box in the photo is x 42..182, y 17..207; the spot sits at (0.41, 0.62) of the leaf, base at the bottom.
            float lu = (100f - 42f) / 141f, lv = 1f - (90f - 17f) / 191f;
            float centre = mask.GetPixelBilinear(0.05f + 0.9f * lu, 0.05f + 0.9f * lv).r;
            float elsewhere = mask.GetPixelBilinear(0.05f + 0.9f * 0.8f, 0.05f + 0.9f * 0.2f).r;
            Assert.Greater(centre, 0.9f, "The traced spot's centre is the first thing the shader shows.");
            Assert.AreEqual(0f, elsewhere, 0.01f, "Healthy parts of the leaf have no lesion.");

            // The spot grows with severity: its rim is lower in the mask than its centre.
            float rim = mask.GetPixelBilinear(0.05f + 0.9f * (lu + 8f / 141f), 0.05f + 0.9f * lv).r;
            Assert.That(rim, Is.InRange(0.25f, 0.8f));
            Assert.Less(rim, centre);

            Assert.AreEqual(TraceTextures.MaskSize, _trace.TwinLesionMask.width);
            Assert.AreEqual(TextureFormat.R8, mask.format);
            Assert.IsTrue(mask.isReadable, "The callouts read the mask to find the spots.");
        }

        [Test]
        public void TheOverlayOutlinesTheLeafAndItsSpots()
        {
            _trace = LeafFeatureTracer.Trace(Leaf(new[] { new Vector3(100, 90, 9) }, new Vector3[0]), Size, Size);
            var overlay = _trace.Overlay;
            Assert.AreEqual(Size, overlay.width);
            // Texture rows run from the bottom: the spot's edge at (109, 90) is row Size - 1 - 90.
            Assert.Greater(overlay.GetPixel(109, Size - 1 - 90).a, 0.9f, "The edge of the spot is drawn.");
            Assert.AreEqual(0f, overlay.GetPixel(100, Size - 1 - 90).a, 0.01f, "Inside the spot stays clear so the photo shows.");
            Assert.AreEqual(0f, overlay.GetPixel(5, 5).a, 0.01f, "Far from the leaf nothing is drawn.");
        }

        [Test]
        public void ASidewaysLeafIsTurnedSoItsLongSideRunsAlongTheLeaf()
        {
            // The same leaf turned a quarter turn: its base is now at the left.
            var upright = Leaf(new[] { new Vector3(112, 180, 8) }, new Vector3[0]);
            var sideways = new Color32[upright.Length];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    sideways[(Size - 1 - x) * Size + (Size - 1 - y)] = upright[(Size - 1 - y) * Size + x];   // (x, y) -> (Size-1-y, x): turned a quarter turn

            using (var a = LeafFeatureTracer.Trace(upright, Size, Size))
            using (var b = LeafFeatureTracer.Trace(sideways, Size, Size))
            {
                Assert.AreEqual(1, a.LesionCount);
                Assert.AreEqual(1, b.LesionCount);
                // Both end up with the lesion at the same height along the leaf in the mask.
                float va = HighestRow(a.HeroLesionMask), vb = HighestRow(b.HeroLesionMask);
                Assert.AreEqual(va, vb, 0.08f);
            }
        }

        static float HighestRow(Texture2D mask)
        {
            float best = -1f, at = 0f;
            for (int y = 0; y < mask.height; y++)
                for (int x = 0; x < mask.width; x++)
                {
                    float v = mask.GetPixel(x, y).r;
                    if (v > best) { best = v; at = y / (float)mask.height; }
                }
            return at;
        }

        [Test]
        public void ANormalLeafOrNoLeafTracesNothing()
        {
            using (var healthy = LeafFeatureTracer.Trace(Leaf(new Vector3[0], new Vector3[0]), Size, Size))
            {
                Assert.AreEqual(0, healthy.LesionCount);
                Assert.IsFalse(healthy.IsUsable, "Nothing traced: the app keeps its authored look.");
            }

            var soil = new Color32[Size * Size];
            for (int i = 0; i < soil.Length; i++) soil[i] = Soil;
            using (var empty = LeafFeatureTracer.Trace(soil, Size, Size))
                Assert.IsFalse(empty.IsUsable, "No leaf in the picture.");

            Assert.IsNull(LeafFeatureTracer.Trace((Texture2D)null));
        }

        [Test]
        public void TinySpecklesAreNotTreatedAsLesions()
        {
            _trace = LeafFeatureTracer.Trace(Leaf(new[] { new Vector3(100, 90, 1.5f), new Vector3(130, 130, 1.5f) }, new Vector3[0]), Size, Size);
            Assert.AreEqual(0, _trace.LesionCount, "A pixel or two of noise is not a lesion.");
        }
    }
}
