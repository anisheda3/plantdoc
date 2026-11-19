using NUnit.Framework;
using PlantDoctor.AI;
using PlantDoctor.AR;
using PlantDoctor.Core;
using UnityEngine;

namespace PlantDoctor.Tests
{
    public class ARLayerTests
    {
        static AROverlayRenderer NewRenderer()
        {
            var go = new GameObject("overlay");
            return go.AddComponent<AROverlayRenderer>();
        }

        static PlantAnalysisResult ResultWithRegions()
        {
            var r = new PlantAnalysisResult { plantId = "tomato", confidence = 0.9f, severity = 0.6f };
            r.regions.Add(new DetectedRegion { type = "lesion", bounds = new Rect(0.3f, 0.2f, 0.2f, 0.2f), score = 0.87f });
            r.regions.Add(new DetectedRegion { type = "yellowing", bounds = new Rect(0.6f, 0.5f, 0.2f, 0.2f), score = 0.55f });
            r.regions.Add(new DetectedRegion { type = "lesion", bounds = new Rect(0f, 0f, 0.1f, 0.1f), score = 0.02f }); // below threshold
            return r;
        }

        [Test]
        public void OverlayFiltersLowScoringRegions()
        {
            var renderer = NewRenderer();
            renderer.Rebuild(ResultWithRegions());
            Assert.AreEqual(2, renderer.Markers.Count, "Regions under the score threshold must be hidden.");
        }

        [Test]
        public void OverlayMapsRegionTypeToFeatureId()
        {
            var renderer = NewRenderer();
            renderer.Rebuild(ResultWithRegions());
            Assert.AreEqual("leaf_spots", renderer.Markers[0].FeatureId);
            Assert.AreEqual("yellowing", renderer.Markers[1].FeatureId);
        }

        [Test]
        public void OverlayHitTestFindsTappedRegion()
        {
            var renderer = NewRenderer();
            renderer.Rebuild(ResultWithRegions());
            var hit = renderer.MarkerAt(new Vector2(0.35f, 0.25f));
            Assert.IsNotNull(hit);
            Assert.AreEqual("lesion", hit.RegionType);
            Assert.IsNull(renderer.MarkerAt(new Vector2(0.95f, 0.95f)), "Empty space must return null, not throw.");
        }

        [Test]
        public void OverlayHandlesErroredResultSafely()
        {
            var renderer = NewRenderer();
            renderer.Rebuild(PlantAnalysisResult.Failure("AI_UNAVAILABLE", "down"));
            Assert.AreEqual(0, renderer.Markers.Count);
            Assert.DoesNotThrow(() => renderer.Rebuild(null));
        }

        [Test]
        public void CameraFailuresAllProduceActionableText()
        {
            foreach (CameraFailure f in System.Enum.GetValues(typeof(CameraFailure)))
            {
                var text = CameraFailureText.Describe(f);
                if (f == CameraFailure.None) Assert.IsEmpty(text);
                else Assert.IsNotEmpty(text, $"{f} needs user-facing copy.");
            }
        }

        [Test]
        public void QualityGateRejectsDarkFrame()
        {
            var settings = ScriptableObject.CreateInstance<ImageQualityAnalyzer>();
            var report = settings.Analyze(null);
            Assert.IsFalse(report.IsAcceptable, "A null frame must never be considered scannable.");
            Assert.IsNotEmpty(report.UserMessage());
        }
    }
}
