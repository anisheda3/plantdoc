using NUnit.Framework;
using PlantDoctor.Diseases;
using PlantDoctor.Plants;
using UnityEngine;

namespace PlantDoctor.Tests
{
    public class DiseaseDataTests
    {
        static DiseaseData MakeDisease()
        {
            var d = ScriptableObject.CreateInstance<DiseaseData>();
            d.diseaseId = "early_blight";
            d.diseaseName = "Early Blight";
            d.aiLabelAliases = new[] { "Tomato___Early_blight" };
            d.moderateSeverityThreshold = 0.35f;
            d.severeSeverityThreshold = 0.70f;
            return d;
        }

        [Test]
        public void MatchesCanonicalIdAndModelAlias()
        {
            var d = MakeDisease();
            Assert.IsTrue(d.Matches("early_blight"));
            Assert.IsTrue(d.Matches("EARLY_BLIGHT"), "Matching must be case-insensitive.");
            Assert.IsTrue(d.Matches("Tomato___Early_blight"), "Raw model labels must resolve via aliases.");
            Assert.IsFalse(d.Matches("late_blight"));
            Assert.IsFalse(d.Matches(null));
        }

        [Test]
        public void SeverityBandsRespectThresholds()
        {
            var d = MakeDisease();
            Assert.AreEqual(SeverityBand.Mild, d.BandFor(0.10f));
            Assert.AreEqual(SeverityBand.Moderate, d.BandFor(0.50f));
            Assert.AreEqual(SeverityBand.Severe, d.BandFor(0.85f));
        }

        [Test]
        public void DatabaseResolvesAndFailsGracefully()
        {
            var plant = ScriptableObject.CreateInstance<PlantData>();
            plant.plantId = "tomato";
            plant.plantName = "Tomato";
            plant.diseases = new[] { MakeDisease() };

            var db = ScriptableObject.CreateInstance<PlantDatabase>();
            db.plants = new[] { plant };
            db.Rebuild();

            Assert.IsTrue(db.TryResolve("tomato", out var found));
            Assert.AreEqual("Tomato", found.plantName);
            Assert.IsFalse(db.TryResolve("banana", out _), "Unknown plants must fail cleanly.");
            Assert.IsFalse(db.TryResolve(null, out _));

            Assert.IsTrue(db.TryResolveDisease("tomato", "Tomato___Early_blight", out _, out var disease));
            Assert.AreEqual("Early Blight", disease.diseaseName);
            Assert.IsFalse(db.TryResolveDisease("tomato", "nonexistent", out _, out _));
        }

        [Test]
        public void FeatureEvaluationRespectsDirection()
        {
            var color = ScriptableObject.CreateInstance<FeatureData>();
            color.optimalValue = 0.95f; color.tolerance = 0.10f; color.higherIsBetter = true;
            Assert.AreEqual(AI.FeatureStatus.Normal, color.Evaluate(0.90f));
            Assert.AreEqual(AI.FeatureStatus.Abnormal, color.Evaluate(0.60f));

            var spots = ScriptableObject.CreateInstance<FeatureData>();
            spots.optimalValue = 0.05f; spots.tolerance = 0.10f; spots.higherIsBetter = false;
            Assert.AreEqual(AI.FeatureStatus.Normal, spots.Evaluate(0.10f));
            Assert.AreEqual(AI.FeatureStatus.Abnormal, spots.Evaluate(0.88f));
        }
    }
}
