using System.IO;
using NUnit.Framework;
using PlantDoctor.AI;
using PlantDoctor.Plants;
using UnityEditor;
using UnityEngine;

namespace PlantDoctor.Tests
{
    /// <summary>The CropScan XR client (cropscan-api-ovmb.onrender.com): addresses, response parsing, and its 68 class names.</summary>
    public class CropScanTests
    {
        // A real response (19 Nov 2026, a PlantSeg celery early-blight photo the model does not know): no verdict, top-3 guesses.
        const string Uncertain = "{\"id\":\"1652332a\",\"created_at\":\"2026-11-19T08:12:18Z\",\"status\":\"uncertain\",\"disease_id\":null," +
            "\"confidence\":0.294,\"top3\":[{\"disease_id\":\"Tomato___Late_blight\",\"confidence\":0.294,\"name\":\"Tomato Late Blight\"}," +
            "{\"disease_id\":\"Potato___Early_blight\",\"confidence\":0.193,\"name\":\"Potato Early Blight\"}],\"model_version\":\"bioclip_vit_b16@v6\"," +
            "\"image_url\":null,\"gradcam_url\":null,\"info\":null,\"has_cycle\":false,\"can_enter_field\":false,\"looks_like_crop\":null}";

        static string Confident(string id, float c) =>
            $"{{\"id\":\"x\",\"status\":\"ok\",\"disease_id\":\"{id}\",\"confidence\":{c.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
            $"\"top3\":[{{\"disease_id\":\"{id}\",\"confidence\":{c.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"name\":\"N\"}}],\"model_version\":\"m\",\"looks_like_crop\":true}}";

        [TestCase("https://cropscan-api-ovmb.onrender.com", true)]
        [TestCase("https://cropscan-api-ovmb.onrender.com/", true)]
        [TestCase("https://example.org/api/scans", true)]
        [TestCase("http://192.168.1.20:8000/v1/analyze", false)]
        public void RecognisesCropScanAddresses(string url, bool expected) => Assert.AreEqual(expected, CropScanAIService.IsCropScan(url));

        [Test]
        public void BuildsTheScanAndHealthAddresses()
        {
            Assert.AreEqual("https://cropscan-api-ovmb.onrender.com/api/scans?lang=en", CropScanAIService.ScanUrl("https://cropscan-api-ovmb.onrender.com/"));
            Assert.AreEqual("https://cropscan-api-ovmb.onrender.com/api/scans?lang=en", CropScanAIService.ScanUrl("https://cropscan-api-ovmb.onrender.com/api/scans"));
            Assert.AreEqual("https://cropscan-api-ovmb.onrender.com/healthz", CropScanAIService.HealthUrlFor("https://cropscan-api-ovmb.onrender.com/docs"));
            Assert.AreEqual("http://192.168.1.20:8000/v1/health", CropScanAIService.HealthUrlFor("http://192.168.1.20:8000/v1/analyze"));
            Assert.IsNull(CropScanAIService.HealthUrlFor("cropscan"));
        }

        [Test]
        public void AnUncertainScanBecomesAPossibilityNotADiagnosis()
        {
            var r = CropScanAIService.Parse(Uncertain);
            Assert.IsFalse(r.HasError, r.ErrorMessage);
            Assert.AreEqual("tomato", r.plantId);
            Assert.AreEqual("late_blight", r.diseaseId);
            Assert.IsTrue(r.forceUncertain);
            Assert.AreEqual(0.294f, r.confidence, 1e-3f);
            Assert.AreEqual(CropScanAIService.AssumedSeverity, r.severity);
        }

        [Test]
        public void AConfidentScanMapsToTheAppsDisease()
        {
            var r = CropScanAIService.Parse(Confident("Cotton___Bacterial_blight", 0.93f));
            Assert.IsFalse(r.HasError, r.ErrorMessage);
            Assert.AreEqual(("cotton", "bacterial_blight"), (r.plantId, r.diseaseId));
            Assert.IsFalse(r.forceUncertain);
            Assert.IsFalse(r.isHealthy);
        }

        [Test]
        public void HealthyAndNonCropScans()
        {
            var h = CropScanAIService.Parse(Confident("Mango___healthy", 0.9f));
            Assert.IsTrue(h.isHealthy);
            Assert.AreEqual(0f, h.severity);
            var notCrop = CropScanAIService.Parse("{\"status\":\"rejected\",\"disease_id\":null,\"confidence\":0.1,\"top3\":[],\"looks_like_crop\":false}");
            Assert.AreEqual("NOT_A_PLANT", notCrop.ErrorCode);
            Assert.AreEqual("NO_DIAGNOSIS", CropScanAIService.Parse("{\"status\":\"uncertain\",\"disease_id\":null,\"top3\":[]}").ErrorCode);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Failed to parse CropScan response"));
            Assert.AreEqual("MALFORMED_RESPONSE", CropScanAIService.Parse("not json").ErrorCode);
        }

        /// <summary>Every class the deployed CropScan model can report reaches a plant and a disease (or healthy) the app teaches.</summary>
        [Test]
        public void EveryCropScanClassReachesAnAuthoredDisease()
        {
            var database = AssetDatabase.LoadAssetAtPath<PlantDatabase>("Assets/Data/Plants/PlantDatabase.asset");
            database.Rebuild();
            var path = Path.Combine(Application.dataPath, "..", "tools", "data", "cropscan_labels.json");
            var json = File.ReadAllText(path);
            int n = 0;
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(json,
                         "\"([^\"]+___[^\"]+)\":\\s*\\{\\s*\"plant\":\\s*\"([^\"]+)\",\\s*\"disease\":\\s*\"([^\"]+)\""))
            {
                string label = m.Groups[1].Value, plantId = m.Groups[2].Value, diseaseId = m.Groups[3].Value;
                var r = CropScanAIService.Parse(Confident(label, 0.9f));
                Assert.IsFalse(r.HasError, $"{label}: {r.ErrorMessage}");
                Assert.AreEqual(plantId, r.plantId, label);
                Assert.IsTrue(database.TryResolve(r.plantId, out var plant), $"{label}: no plant {r.plantId}");
                if (diseaseId == "healthy") Assert.IsTrue(r.isHealthy, label);
                else
                {
                    var d = plant.FindDisease(r.diseaseId);
                    Assert.IsNotNull(d, $"{label}: {plantId} has no disease {r.diseaseId}");
                    Assert.AreEqual(diseaseId, d.diseaseId, label);
                    Assert.IsNotNull(plant.FindDisease(label), $"{label} is not an alias of {plantId}/{diseaseId}");
                }
                n++;
            }
            Assert.AreEqual(68, n);
        }
    }
}
