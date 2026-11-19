using NUnit.Framework;
using PlantDoctor.Core;
using PlantDoctor.Treatment;
using UnityEngine;

namespace PlantDoctor.Tests
{
    /// <summary>
    /// Guards the realism constraints from the design brief: the simulation must never
    /// imply that treatment guarantees a complete cure.
    /// </summary>
    public class RealismTests
    {
        [Test]
        public void SeverityNeverFallsBelowResidualFloor()
        {
            var state = new PlantHealthState { diseaseSeverity = 0.68f, leafDamage = 0.5f };
            for (int i = 0; i < 20; i++)
            {
                state.Apply(new StateDelta { diseaseSeverity = -0.2f, leafDamage = -0.2f });
                state.ApplyFloors(0.15f, 0.10f);
            }
            Assert.GreaterOrEqual(state.diseaseSeverity, 0.15f, "Treatment must not model a guaranteed cure.");
            Assert.GreaterOrEqual(state.leafDamage, 0.10f, "Necrotic tissue does not regenerate.");
        }

        [Test]
        public void RecoveryPercentStaysBelowOneHundred()
        {
            var ctx = new SessionContext
            {
                InitialState = new PlantHealthState { diseaseSeverity = 0.68f },
                CurrentState = new PlantHealthState { diseaseSeverity = 0.15f }
            };
            Assert.Less(ctx.BuildReport().RecoveryPercent, 1.0f);
        }

        [Test]
        public void PlanShipsWithNonZeroFloorsAndDisclaimer()
        {
            var plan = ScriptableObject.CreateInstance<TreatmentPlan>();
            Assert.Greater(plan.residualSeverityFloor, 0f, "Default plan must not promise a full cure.");
            Assert.IsNotEmpty(plan.disclaimer);
            StringAssert.Contains("does not guarantee", plan.disclaimer);
        }

        [Test]
        public void MockResultsAreAlwaysFlaggedAsSimulated()
        {
            var mock = new AI.PlantAnalysisResult { origin = AI.AnalysisOrigin.Mock };
            Assert.IsTrue(mock.IsSimulatedResult, "Mock output must be watermarkable by the UI.");
            var real = new AI.PlantAnalysisResult { origin = AI.AnalysisOrigin.Remote };
            Assert.IsFalse(real.IsSimulatedResult);
        }
    }
}
