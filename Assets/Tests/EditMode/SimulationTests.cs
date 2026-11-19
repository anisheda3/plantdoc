using NUnit.Framework;
using PlantDoctor.AI;
using PlantDoctor.Core;
using PlantDoctor.Simulation;

namespace PlantDoctor.Tests
{
    public class SimulationTests
    {
        [Test]
        public void TreatmentEffectsReduceSeverityAndClamp()
        {
            var state = new PlantHealthState { diseaseSeverity = 0.68f, lesionDensity = 0.88f };
            state.Apply(new StateDelta { diseaseSeverity = -0.12f, lesionDensity = -0.25f });
            Assert.AreEqual(0.56f, state.diseaseSeverity, 0.001f);
            Assert.AreEqual(0.63f, state.lesionDensity, 0.001f);

            state.Apply(new StateDelta { diseaseSeverity = -5f });
            Assert.AreEqual(0f, state.diseaseSeverity, 0.0001f, "State must clamp to 0..1");
        }

        [Test]
        public void StateSeededFromAnalysis()
        {
            var analysis = new PlantAnalysisResult { severity = 0.68f };
            analysis.features.Add(new FeatureReading { featureId = "leaf_spots", value = 0.88f });
            analysis.features.Add(new FeatureReading { featureId = "yellowing", value = 0.61f });
            analysis.features.Add(new FeatureReading { featureId = "leaf_color", value = 0.72f });

            var state = PlantStateEvaluator.FromAnalysis(analysis, null);
            Assert.AreEqual(0.68f, state.diseaseSeverity, 0.001f);
            Assert.AreEqual(0.88f, state.lesionDensity, 0.001f);
            Assert.AreEqual(0.61f, state.yellowing, 0.001f);
        }

        [Test]
        public void ReportComputesRecoveryPercent()
        {
            var ctx = new SessionContext
            {
                InitialState = new PlantHealthState { diseaseSeverity = 0.68f },
                CurrentState = new PlantHealthState { diseaseSeverity = 0.18f },
                CompletedTreatmentSteps = 4,
                TotalTreatmentSteps = 4,
                PreventionMeasuresReviewed = 4
            };
            var report = ctx.BuildReport();
            Assert.AreEqual(0.735f, report.RecoveryPercent, 0.01f);
            Assert.AreEqual("Good", report.PreventionScore);
        }

        [Test]
        public void ZeroInitialSeverityDoesNotDivideByZero()
        {
            var ctx = new SessionContext();
            Assert.AreEqual(0f, ctx.BuildReport().RecoveryPercent, 0.0001f);
        }
    }
}
