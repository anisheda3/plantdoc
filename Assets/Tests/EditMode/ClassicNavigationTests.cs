using NUnit.Framework;
using PlantDoctor.Core;
using PlantDoctor.UI.Classic;

namespace PlantDoctor.Tests
{
    /// <summary>Where the back arrow goes from every step, so the user can always step back or start over.</summary>
    public class ClassicNavigationTests
    {
        [TestCase(SimState.Scan, SimState.Welcome)]
        [TestCase(SimState.ScanRetry, SimState.Welcome)]
        [TestCase(SimState.AiAnalysis, SimState.Scan)]
        [TestCase(SimState.PlantIdentified, SimState.Scan)]
        [TestCase(SimState.FeatureAnalysis, SimState.PlantIdentified)]
        [TestCase(SimState.Diagnosis, SimState.FeatureAnalysis)]
        [TestCase(SimState.LowConfidence, SimState.Diagnosis)]
        [TestCase(SimState.FocusPart, SimState.Diagnosis)]
        [TestCase(SimState.DiseaseSimulation, SimState.FocusPart)]
        [TestCase(SimState.TreatmentIntro, SimState.DiseaseSimulation)]
        [TestCase(SimState.TreatmentStep, SimState.TreatmentIntro)]
        [TestCase(SimState.RecoveryTimeline, SimState.TreatmentStep)]
        [TestCase(SimState.Prevention, SimState.RecoveryTimeline)]
        [TestCase(SimState.FinalReport, SimState.Prevention)]
        [TestCase(SimState.Error, SimState.Welcome)]
        public void BackGoesToThePreviousStep(SimState from, SimState expected)
        {
            Assert.AreEqual(expected, ClassicNavigation.BackTarget(from, healthy: false));
        }

        [Test]
        public void TheFirstPageHasNothingToGoBackTo()
        {
            Assert.IsNull(ClassicNavigation.BackTarget(SimState.Welcome, false));
        }

        [Test]
        public void AHealthyPlantSkipsTheTreatmentOnTheWayBack()
        {
            Assert.AreEqual(SimState.Diagnosis, ClassicNavigation.BackTarget(SimState.Prevention, healthy: true));
        }

        [Test]
        public void EveryStepEventuallyLeadsBackToTheStart()
        {
            foreach (SimState start in System.Enum.GetValues(typeof(SimState)))
            {
                var state = start;
                for (int hops = 0; hops < 30 && state != SimState.Welcome; hops++)
                {
                    var next = ClassicNavigation.BackTarget(state, healthy: false);
                    Assert.IsNotNull(next, $"{state} must have a way back.");
                    state = next.Value;
                }
                Assert.AreEqual(SimState.Welcome, state, $"Going back from {start} must reach the starting page.");
            }
        }

        [Test]
        public void LeavingOnlyAsksWhenResultsWouldBeLost()
        {
            Assert.IsFalse(ClassicNavigation.WouldLoseProgress(SimState.Scan));
            Assert.IsFalse(ClassicNavigation.WouldLoseProgress(SimState.AiAnalysis));
            Assert.IsTrue(ClassicNavigation.WouldLoseProgress(SimState.Diagnosis));
            Assert.IsTrue(ClassicNavigation.WouldLoseProgress(SimState.FinalReport));
        }
    }
}
