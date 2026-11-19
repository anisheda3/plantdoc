using NUnit.Framework;
using PlantDoctor.AI;
using PlantDoctor.Core;
using PlantDoctor.Diseases;
using PlantDoctor.Treatment;
using PlantDoctor.UI;
using UnityEngine;

namespace PlantDoctor.Tests
{
    /// <summary>Regression tests for the defects fixed in the October 2026 review.</summary>
    public class FlowFixTests
    {
        [Test]
        public void HealthyResultIsReportedAsHealthyNotAsAnError()
        {
            var ctx = new SessionContext
            {
                Analysis = new PlantAnalysisResult { plantId = "tomato", isHealthy = true, confidence = 0.9f, origin = AnalysisOrigin.Remote }
            };
            Assert.IsTrue(ctx.IsHealthy);
            var report = ctx.BuildReport();
            Assert.IsTrue(report.IsHealthy);
            Assert.AreEqual("No disease detected", report.DiseaseName);
        }

        [Test]
        public void TreatmentStepAppliesItsEffectOnlyOnce()
        {
            var step = ScriptableObject.CreateInstance<TreatmentStep>();
            step.title = "remove";
            step.simulatedDaysAdvanced = 0;
            step.effect = new StateDelta { diseaseSeverity = -0.2f };

            var plan = ScriptableObject.CreateInstance<TreatmentPlan>();
            plan.steps = new[] { step };
            plan.residualSeverityFloor = 0f;

            var disease = ScriptableObject.CreateInstance<DiseaseData>();
            disease.treatmentPlan = plan;

            var ctx = new SessionContext { Disease = disease, CurrentState = new PlantHealthState { diseaseSeverity = 0.8f } };
            var go = new GameObject("treatment");
            var manager = go.AddComponent<TreatmentManager>();

            Assert.IsTrue(manager.Begin(ctx));
            manager.PerformCurrentStep();
            manager.PerformCurrentStep();   // a second tap must not stack the same treatment
            manager.PerformCurrentStep();

            Assert.AreEqual(0.6f, ctx.CurrentState.diseaseSeverity, 0.0001f);
            Assert.IsTrue(manager.CurrentStepPerformed);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ServerErrorBodyKeepsTheServersCode()
        {
            var r = RemoteAIService.ErrorFromBody(@"{""error_code"":""MODEL_UNAVAILABLE"",""message"":""No trained model is installed on this server.""}", 503);
            Assert.AreEqual("MODEL_UNAVAILABLE", r.ErrorCode);
            StringAssert.Contains("Demo Mode", r.ErrorMessage);

            var invalid = RemoteAIService.ErrorFromBody(@"{""error_code"":""INVALID_IMAGE"",""message"":""Upload a JPEG or PNG image.""}", 415);
            Assert.AreEqual("INVALID_IMAGE", invalid.ErrorCode);

            Assert.AreEqual("MODEL_ERROR", RemoteAIService.ErrorFromBody(@"{""detail"":""boom""}", 500).ErrorCode);
            Assert.AreEqual("MODEL_ERROR", RemoteAIService.ErrorFromBody(null, 502).ErrorCode);
        }

        [Test]
        public void HealthUrlIsDerivedFromTheAnalyzeEndpoint()
        {
            Assert.AreEqual("http://192.168.1.20:8000/v1/health", ProfileScreen.HealthUrl("http://192.168.1.20:8000/v1/analyze"));
            Assert.AreEqual("http://host:8000/health", ProfileScreen.HealthUrl("http://host:8000/analyze/"));
            Assert.IsNull(ProfileScreen.HealthUrl("192.168.1.20:8000"));
            Assert.IsNull(ProfileScreen.HealthUrl(""));
        }

        [Test]
        public void EveryFlowStateHasAPageAndInfectionTreatmentAndRecoveryShareOne()
        {
            foreach (SimState state in System.Enum.GetValues(typeof(SimState)))
                Assert.DoesNotThrow(() => ScreenRouter.ScreenForState(state), state.ToString());

            Assert.AreEqual(AppScreenId.Home, ScreenRouter.ScreenForState(SimState.Welcome));
            Assert.AreEqual(AppScreenId.Scan, ScreenRouter.ScreenForState(SimState.Scan));
            Assert.AreEqual(AppScreenId.Analysis, ScreenRouter.ScreenForState(SimState.AiAnalysis));
            Assert.AreEqual(AppScreenId.Diagnosis, ScreenRouter.ScreenForState(SimState.Diagnosis));
            Assert.AreEqual(AppScreenId.ArView, ScreenRouter.ScreenForState(SimState.ArView));
            Assert.AreEqual(AppScreenId.Zoom, ScreenRouter.ScreenForState(SimState.FocusPart));
            Assert.AreEqual(AppScreenId.Prevention, ScreenRouter.ScreenForState(SimState.Prevention));
            Assert.AreEqual(AppScreenId.Report, ScreenRouter.ScreenForState(SimState.FinalReport));

            // One page, one animation: the lifecycle, the treatment and the recovery are not separate screens.
            Assert.AreEqual(AppScreenId.Journey, ScreenRouter.ScreenForState(SimState.DiseaseSimulation));
            Assert.AreEqual(AppScreenId.Journey, ScreenRouter.ScreenForState(SimState.TreatmentIntro));
            Assert.AreEqual(AppScreenId.Journey, ScreenRouter.ScreenForState(SimState.TreatmentStep));
            Assert.AreEqual(AppScreenId.Journey, ScreenRouter.ScreenForState(SimState.RecoveryTimeline));
        }

        [Test]
        public void ThereIsNoGuidePage()
        {
            foreach (var name in System.Enum.GetNames(typeof(AppScreenId)))
                Assert.AreNotEqual("Guide", name, "The disease guide was removed on purpose.");
        }

        [Test]
        public void DemoReportIsMarkedAndNeverCarriesAnInventedConfidence()
        {
            var d = new ReportData { PlantName = "Tomato", Condition = "Early Blight", IsDemo = true, SourceText = "DEMO AI (simulated)", SeverityLabel = "Moderate" };
            d.Symptoms.Add("Dark rings on older leaves");
            d.Treatment.Add(("Remove infected leaves", true));
            d.Notes.Add("DEMO AI: simulated.");
            var pdf = PdfReport.Build(d);
            var text = System.Text.Encoding.ASCII.GetString(pdf);
            StringAssert.StartsWith("%PDF-", text);
            StringAssert.Contains("%%EOF", text);
            StringAssert.Contains("DEMO AI", text);
            StringAssert.DoesNotContain("Confidence", text);
        }

        [Test]
        public void PdfTextIsMadeSafeForTheBuiltInFont()
        {
            Assert.AreEqual("a - b", PdfReport.Ascii("a \u2013 b"));
            Assert.AreEqual("it's", PdfReport.Ascii("it\u2019s"));
        }

        [Test]
        public void ScanHistoryKeepsSavesAndDeletesRecords()
        {
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plantdoctor-history-" + System.Guid.NewGuid().ToString("N"));
            var go = new GameObject("history");
            go.SetActive(false);
            var history = go.AddComponent<ScanHistory>();
            history.Folder = folder;
            go.SetActive(true);
            try
            {
                var ctx = new SessionContext
                {
                    Analysis = new PlantAnalysisResult { plantId = "tomato", plantName = "Tomato", diseaseName = "Early Blight", severity = 0.4f, confidence = 0.8f, origin = AnalysisOrigin.Remote }
                };
                var photo = new Texture2D(8, 8, TextureFormat.RGB24, false);
                var record = history.Add(ctx, photo);
                Assert.AreEqual(1, history.Records.Count);
                Assert.IsFalse(record.IsDemo);
                Assert.IsTrue(System.IO.File.Exists(System.IO.Path.Combine(folder, "history.json")));

                var demo = history.Add(new SessionContext { Analysis = new PlantAnalysisResult { plantId = "tomato", origin = AnalysisOrigin.Mock } }, null);
                Assert.IsTrue(demo.IsDemo);
                Assert.AreEqual(demo.id, history.Records[0].id, "Newest first.");

                history.Delete(record.id);
                Assert.AreEqual(1, history.Records.Count);
                Object.DestroyImmediate(photo);
            }
            finally
            {
                Object.DestroyImmediate(go);
                if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true);
            }
        }

        [Test]
        public void FreshAnalysisStartsAFreshSession()
        {
            var ctx = new SessionContext { CompletedTreatmentSteps = 3, SimulatedDay = 9, PreventionMeasuresReviewed = 4 };
            ctx.Reset();
            Assert.AreEqual(0, ctx.CompletedTreatmentSteps);
            Assert.AreEqual(0, ctx.SimulatedDay);
            Assert.AreEqual(0, ctx.PreventionMeasuresReviewed);
            Assert.IsFalse(ctx.IsHealthy);
        }
    }
}
