using NUnit.Framework;
using UnityEngine.TestTools;
using PlantDoctor.AI;
using PlantDoctor.Core;

namespace PlantDoctor.Tests
{
    public class AIParsingTests
    {
        const string ValidJson = @"{
            ""schema_version"":""1.0"",""model_version"":""plantdoc-v0.3.1"",
            ""plant_id"":""tomato"",""plant_name"":""Tomato"",
            ""disease_id"":""early_blight"",""disease_name"":""Early Blight"",
            ""confidence"":0.94,""severity"":0.68,""is_healthy"":false,
            ""features"":{""leaf_color"":{""value"":0.72,""optimal"":0.95,""status"":""abnormal"",""source"":""visual_ai""},
                          ""leaf_spots"":{""value"":0.88,""optimal"":0.05,""status"":""abnormal"",""source"":""visual_ai""}},
            ""regions"":[{""type"":""lesion"",""bbox"":[0.31,0.22,0.18,0.15],""score"":0.87}],
            ""recommendation"":{""treatment_id"":""tomato_early_blight_treatment""},
            ""warnings"":[]}";

        [Test]
        public void ParsesValidResponse()
        {
            var r = AIResponseParser.Parse(ValidJson, AnalysisOrigin.Remote);
            Assert.IsFalse(r.HasError);
            Assert.AreEqual("tomato", r.plantId);
            Assert.AreEqual("early_blight", r.diseaseId);
            Assert.AreEqual(0.94f, r.confidence, 0.001f);
            Assert.AreEqual(2, r.features.Count);
            Assert.AreEqual(0.88f, r.GetFeature("leaf_spots").value, 0.001f);
            Assert.AreEqual(1, r.regions.Count);
            Assert.AreEqual("tomato_early_blight_treatment", r.recommendedTreatmentId);
        }

        [Test]
        public void MalformedJsonReturnsErrorNotException()
        {
            // The parser is expected to log an error here. Unity's Test Runner fails
            // any test that emits Debug.LogError unless it is declared in advance,
            // so declare it: the log IS the correct behaviour under test.
            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(
                @"\[AI\] Failed to parse AI response"));

            var r = AIResponseParser.Parse("{ this is not json", AnalysisOrigin.Remote);
            Assert.IsTrue(r.HasError);
            Assert.AreEqual("MALFORMED_RESPONSE", r.ErrorCode);
        }

        [Test]
        public void EmptyResponseHandled()
        {
            Assert.IsTrue(AIResponseParser.Parse("", AnalysisOrigin.Remote).HasError);
        }

        [Test]
        public void MissingPlantIdIsRejected()
        {
            var r = AIResponseParser.Parse(@"{""confidence"":0.9}", AnalysisOrigin.Remote);
            Assert.IsTrue(r.HasError);
        }

        [Test]
        public void ServerErrorPayloadSurfacesCode()
        {
            var r = AIResponseParser.Parse(@"{""error_code"":""UNSUPPORTED_PLANT"",""message"":""nope""}", AnalysisOrigin.Remote);
            Assert.AreEqual("UNSUPPORTED_PLANT", r.ErrorCode);
        }

        [Test]
        public void MissingDiseaseYieldsNullDiseaseNotCrash()
        {
            var r = AIResponseParser.Parse(@"{""plant_id"":""tomato"",""is_healthy"":true,""confidence"":0.9}", AnalysisOrigin.Remote);
            Assert.IsFalse(r.HasError);
            Assert.IsTrue(r.isHealthy);
            Assert.IsNull(r.diseaseId);
        }

        [Test]
        public void ConfidenceBandingRespectsThresholds()
        {
            var policy = UnityEngine.ScriptableObject.CreateInstance<ConfidencePolicy>();
            Assert.AreEqual(ConfidenceBand.High, policy.Evaluate(0.94f));
            Assert.AreEqual(ConfidenceBand.Moderate, policy.Evaluate(0.61f));
            Assert.AreEqual(ConfidenceBand.Uncertain, policy.Evaluate(0.42f));
        }
    }
}
