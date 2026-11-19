using NUnit.Framework;
using PlantDoctor.Core;
using PlantDoctor.Treatment;
using UnityEngine;

namespace PlantDoctor.Tests
{
    public class TreatmentFlowTests
    {
        static TreatmentStep MakeStep(string id, TreatmentInteractionMode mode, float severityDelta)
        {
            var s = ScriptableObject.CreateInstance<TreatmentStep>();
            s.stepId = id;
            s.title = id;
            s.interactionMode = mode;
            s.simulatedDaysAdvanced = 2;
            s.effect = new StateDelta { diseaseSeverity = severityDelta };
            return s;
        }

        static TreatmentPlan MakePlan()
        {
            var p = ScriptableObject.CreateInstance<TreatmentPlan>();
            p.treatmentId = "t";
            p.planName = "Test Plan";
            p.steps = new[]
            {
                MakeStep("a", TreatmentInteractionMode.TapRegion, -0.1f),
                MakeStep("b", TreatmentInteractionMode.SprayApply, -0.2f)
            };
            return p;
        }

        [Test]
        public void PlanExposesStepsSafely()
        {
            var plan = MakePlan();
            Assert.AreEqual(2, plan.StepCount);
            Assert.IsNotNull(plan.GetStep(0));
            Assert.IsNull(plan.GetStep(5), "Out-of-range step access must return null, not throw.");
            Assert.IsNull(plan.GetStep(-1));
        }

        [Test]
        public void EmptyPlanReportsZeroSteps()
        {
            var p = ScriptableObject.CreateInstance<TreatmentPlan>();
            Assert.AreEqual(0, p.StepCount);
            Assert.IsNull(p.GetStep(0));
        }

        [Test]
        public void PreventionPlanCountsMeasures()
        {
            var plan = ScriptableObject.CreateInstance<PreventionPlan>();
            Assert.AreEqual(0, plan.MeasureCount, "Null measures array must report zero, not throw.");
            plan.measures = new[]
            {
                new PreventionMeasure { category = PreventionCategory.CropHygiene, title = "x", detail = "y" }
            };
            Assert.AreEqual(1, plan.MeasureCount);
        }

        [Test]
        public void ChemicalInfoCarriesSafetyFieldsByDefault()
        {
            var info = new ChemicalTreatmentInfo();
            Assert.IsFalse(info.involvesChemicalProduct, "Steps must not imply chemical use unless explicitly set.");
            Assert.IsNotEmpty(info.localRegulationsNote, "Regulatory note must always be present.");
        }
    }
}
