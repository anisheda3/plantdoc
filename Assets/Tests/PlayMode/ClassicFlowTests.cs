using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PlantDoctor.AR;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using PlantDoctor.UI;
using PlantDoctor.UI.Classic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PlantDoctor.Tests
{
    /// <summary>
    /// Drives the classic, step-by-step interface end to end in the real app scene with a synthetic leaf
    /// camera: scan, analysis, identification, health check, diagnosis, zoom in (the new leaf), spread,
    /// treatment, recovery, prevention and the report. The explicit variant also writes a screenshot of every
    /// stage to docs/screenshots.
    /// </summary>
    public class ClassicFlowTests
    {
        const string SceneName = "PlantDoctor";
        static readonly Vector2Int ShotSize = new Vector2Int(1080, 2340);

        [UnityTest]
        [Timeout(600000)]   // the walk through every plant takes ~3 min, past the default 180 s
        public IEnumerator ClassicFlowRunsEndToEnd() => Drive(capture: false);

        [UnityTest, Explicit("Writes PNGs to docs/screenshots; run on demand."), Timeout(900000)]
        public IEnumerator CaptureClassicScreenshots() => Drive(capture: true);

        IEnumerator Drive(bool capture)
        {
            if (!Application.CanStreamedLevelBeLoaded(SceneName))
                Assert.Ignore("PlantDoctor scene is not in Build Settings. Run Tools > AI Plant Doctor > Build App Scene.");

            SceneManager.LoadScene(SceneName);
            yield return null;

            var flow = UnityEngine.Object.FindObjectOfType<AppFlowController>();
            if (UnityEngine.Object.FindObjectOfType<MainScreenView>() == null)
                Assert.Ignore("This scene was built with the dashboard interface; this test is for the classic one.");

            var mode = UnityEngine.Object.FindObjectOfType<AppModeController>();
            var scan = UnityEngine.Object.FindObjectOfType<ARScanController>();
            var demo = UnityEngine.Object.FindObjectOfType<DemoModeController>();
            var stage = UnityEngine.Object.FindObjectOfType<ViewerStage>();
            var classicStage = UnityEngine.Object.FindObjectOfType<ClassicStage>();
            Assert.IsNotNull(flow, "AppFlowController missing from the scene.");
            Assert.IsTrue(flow.GuidedStages, "The classic interface runs the flow stage by stage.");
            Assert.IsNotNull(classicStage, "ClassicStage missing from the scene.");
            Assert.IsNotNull(stage, "ViewerStage missing from the scene.");

            yield return WaitUntil(() => mode == null || mode.Mode != AppMode.Resolving, 5f);
            Assert.AreEqual(AppMode.Camera, mode.Mode, "Without ARCore (Editor) the app must fall back to camera mode.");
            flow.AI.SetBackend(AI.InferenceMode.Mock);
            flow.AI.DemoPlant = "tomato";
            flow.AI.DemoScenario = "early_blight";

            var fake = new GameObject("FakeLeafCamera").AddComponent<FakeLeafCamera>();
            scan.SetFrameSource(fake);
            if (capture) BeginCapture();

            yield return Settle(1.0f);
            Assert.AreEqual(SimState.Welcome, flow.CurrentState);
            if (capture) yield return Shot("01_welcome");

            flow.BeginScan();
            yield return WaitUntil(() => scan.HasLiveQuality, 5f);
            Assert.IsTrue(scan.LiveQuality.IsAcceptable, $"Synthetic leaf should pass the live quality gate: {string.Join(", ", scan.LiveQuality.Issues)}");
            yield return Settle(0.8f);
            if (capture) yield return Shot("02_scan");

            scan.CaptureAndSubmit();
            yield return WaitUntil(() => flow.CurrentState == SimState.AiAnalysis, 3f);
            yield return Settle(0.5f);
            if (capture) yield return Shot("03_analyzing");

            // The classic flow stops at each stage until the user continues.
            yield return WaitUntil(() => flow.CurrentState == SimState.PlantIdentified, 20f);
            Assert.AreEqual(SimState.PlantIdentified, flow.CurrentState, "A good camera frame must reach identification and wait there.");
            Assert.AreEqual("tomato", flow.Context.Plant.plantId);

            // The spots on the scanned leaf are traced from its pixels, outlined on the photo and planted on the twin.
            var trace = flow.Context.ScanTrace;
            Assert.IsNotNull(trace, "A scan of a spotted leaf must be traced.");
            Assert.GreaterOrEqual(trace.LesionCount, 3, $"The scanned leaf has several lesions; traced: {trace.Summary()}");
            Assert.IsNotNull(trace.Overlay);
            Assert.AreEqual(trace.LesionCount + trace.YellowCount, flow.Context.Analysis.regions.Count, "The AI's coarse boxes are replaced by the traced spots.");
            yield return Settle(1.4f);
            if (capture) yield return Shot("04_identified");

            flow.ContinueFromPlantIdentified();
            yield return Settle(1.2f);
            Assert.AreEqual(SimState.FeatureAnalysis, flow.CurrentState);
            if (capture) yield return Shot("05_health_check");

            flow.ContinueFromFeatures();
            yield return Settle(1.2f);
            Assert.AreEqual(SimState.Diagnosis, flow.CurrentState);
            if (capture) yield return Shot("06_diagnosis");

            // The back arrow steps back one stage, the house button asks before starting over.
            var nav = UnityEngine.Object.FindObjectOfType<ClassicNavigation>();
            Assert.IsNotNull(nav, "ClassicNavigation missing from the scene.");
            nav.GoBack();
            yield return Settle(0.4f);
            Assert.AreEqual(SimState.FeatureAnalysis, flow.CurrentState, "Back from the diagnosis returns to the health check.");
            flow.ContinueFromFeatures();
            yield return Settle(0.6f);
            Assert.AreEqual(SimState.Diagnosis, flow.CurrentState);
            nav.GoHome();
            yield return null;
            Assert.IsTrue(nav.ConfirmOpen, "Leaving a finished diagnosis must be confirmed.");
            nav.CloseConfirm();
            yield return Settle(0.3f);
            Assert.AreEqual(SimState.Diagnosis, flow.CurrentState, "Choosing 'Stay' keeps the result.");

            // Zoom in: the new leaf, in the free area above the sheet, turned by dragging.
            flow.ContinueFromDiagnosis();
            // Record the leaf on its own as the step opens: nothing may move it but the user's finger.
            var frames = new System.Collections.Generic.List<Color32[]>();
            for (int f = 0; f < 12; f++)
            {
                yield return Settle(0.2f);
                frames.Add(LeafFrame(stage, capture ? $"leaf_{f:00}" : null));
            }
            for (int f = 1; f < frames.Count; f++)
                Debug.Log($"[LeafMotion] frame {f}: {MovedShare(frames[f - 1], frames[f]):P2} of the pixels changed");
            Assert.AreEqual(SimState.FocusPart, flow.CurrentState);
            Assert.IsTrue(stage.LeafView.Active, "The leaf is drawn at the zoom-in step.");
            Assert.Less(MovedShare(frames[frames.Count - 3], frames[frames.Count - 1]), 0.002f, "The zoomed leaf must stay still on its own.");
            Assert.IsFalse(stage.PlantView.Active, "Only the leaf is shown at the zoom-in step, not the plant.");
            if (capture) yield return Shot("07_zoom_in");

            float yawBefore = stage.LeafView.Yaw;
            stage.LeafView.BeginDrag();
            stage.LeafView.Drag(new Vector2(120f, 0f));
            stage.LeafView.EndDrag();
            Assert.AreNotEqual(yawBefore, stage.LeafView.Yaw, "Dragging must turn the leaf.");
            yield return Settle(2.0f);
            float settled = stage.LeafView.Yaw;
            yield return Settle(0.5f);
            Assert.AreEqual(settled, stage.LeafView.Yaw, 0.01f, "After the flick the leaf must come to rest and stay still.");

            flow.ContinueFromFocus();
            yield return Settle(3.5f);
            Assert.AreEqual(SimState.DiseaseSimulation, flow.CurrentState);
            Assert.IsFalse(stage.LeafView.Active, "The leaf is only drawn at its own step.");
            if (capture) yield return Shot("08_how_it_spread");

            flow.ContinueFromSimulation();
            yield return Settle(1.0f);
            Assert.AreEqual(SimState.TreatmentIntro, flow.CurrentState);
            if (capture) yield return Shot("09_treatment_plan");

            flow.BeginTreatmentSteps();
            yield return Settle(1.0f);
            if (capture) yield return Shot("10_treatment_step");

            float before = flow.Context.CurrentState.diseaseSeverity;
            int steps = flow.Treatment.Plan.StepCount;
            for (int i = 0; i < steps; i++)
            {
                flow.PerformTreatmentStep();
                yield return Settle(i == 0 ? 1.0f : 0.3f);
                if (capture && i == 0) yield return Shot("11_step_done");
                flow.NextTreatmentStep();
                yield return null;
            }
            Assert.Less(flow.Context.CurrentState.diseaseSeverity, before, "Treatment must reduce severity.");
            Assert.AreEqual(SimState.RecoveryTimeline, flow.CurrentState);
            yield return Settle(4f);
            if (capture) yield return Shot("12_recovery");

            flow.ContinueFromRecovery();
            yield return Settle(1.0f);
            Assert.AreEqual(SimState.Prevention, flow.CurrentState);
            if (capture) yield return Shot("13_prevention");

            for (int i = 0; i < 12 && flow.CurrentState == SimState.Prevention; i++)
            {
                flow.NextPreventionMeasure();
                yield return null;
            }
            Assert.AreEqual(SimState.FinalReport, flow.CurrentState);
            var report = flow.Context.BuildReport();
            Assert.Greater(report.RecoveryPercent, 0f);
            Assert.Less(report.RecoveryPercent, 1f, "Treatment is never a guaranteed full cure.");
            yield return Settle(1.4f);
            if (capture) yield return Shot("14_report");

            nav.GoHome();
            yield return null;
            Assert.IsTrue(nav.ConfirmOpen);
            nav.ConfirmLeave();
            yield return Settle(0.8f);
            Assert.AreEqual(SimState.Welcome, flow.CurrentState, "'Start over' returns to the starting page.");
            flow.BeginScan();
            yield return WaitUntil(() => scan.HasLiveQuality, 5f);
            nav.GoBack();
            yield return Settle(0.4f);
            Assert.AreEqual(SimState.Welcome, flow.CurrentState, "Back from the scan returns to the starting page.");

            // Healthy plants take the short path and are never shown as an error.
            var healthyRun = demo.RunScenario("tomato", "healthy", 0f, 0.9f);
            yield return WaitUntil(() => healthyRun.IsCompleted, 5f);
            flow.ContinueFromPlantIdentified();
            flow.ContinueFromFeatures();
            yield return Settle(1.2f);
            Assert.AreEqual(SimState.Diagnosis, flow.CurrentState, "A healthy result must not land on the error screen.");
            if (capture) yield return Shot("15_healthy");
            flow.ContinueFromDiagnosis();
            yield return null;
            Assert.AreEqual(SimState.Prevention, flow.CurrentState);

            // Every species: the twin is rebuilt for the identified plant and the zoomed leaf shows that disease.
            var sweep = new[] { ("potato", "late_blight", "16_potato"), ("pepper", "bacterial_spot", "17_pepper"),
                                ("apple", "cedar_apple_rust", "18_apple_rust"), ("apple", "apple_scab", "19_apple_scab") };
            foreach (var (plantId, diseaseId, shot) in sweep)
            {
                var run = demo.RunScenario(plantId, diseaseId, 0.7f, 0.93f);
                yield return WaitUntil(() => run.IsCompleted, 5f);
                Assert.AreEqual(SimState.PlantIdentified, flow.CurrentState, $"{plantId} demo should reach identification.");
                Assert.AreEqual(diseaseId, flow.Context.Disease.diseaseId);
                flow.ContinueFromPlantIdentified();
                flow.ContinueFromFeatures();
                flow.ContinueFromDiagnosis();
                Assert.AreEqual(SimState.FocusPart, flow.CurrentState);
                yield return Settle(2.0f);
                if (capture) yield return Shot(shot);
            }

            // The rest of the PlantVillage crops: each is identified with its own twin, and diseased ones show their leaf.
            var crops = new[] { ("blueberry", "healthy"), ("cherry", "powdery_mildew"), ("corn", "northern_leaf_blight"), ("grape", "esca"),
                                ("orange", "citrus_greening"), ("peach", "bacterial_spot"), ("raspberry", "healthy"), ("soybean", "healthy"),
                                ("squash", "powdery_mildew"), ("strawberry", "leaf_scorch"), ("tomato", "yellow_leaf_curl_virus"),
                                // PlantSeg plants and organ diseases (the zoom shows the fruit, head or root)
                                ("pepper", "blossom_end_rot"), ("banana", "bunchy_top"), ("cabbage", "black_rot"), ("carrot", "cavity_spot"),
                                ("coffee", "leaf_rust"), ("maple", "tar_spot"), ("wheat", "stripe_rust"), ("wheat", "head_scab"),
                                ("cucumber", "angular_leaf_spot"), ("rice", "blast"), ("lettuce", "downy_mildew"),
                                ("mango", "anthracnose"), ("sugarcane", "red_rot"), ("chilli", "leaf_curl"), ("chilli", "anthracnose"),
                                ("cotton", "bacterial_blight"), ("cotton", "fusarium_wilt"), ("rice", "tungro"), ("wheat", "blast") };
            int number = 20;
            foreach (var (plantId, diseaseId) in crops)
            {
                var run = demo.RunScenario(plantId, diseaseId, 0.7f, 0.93f);
                yield return WaitUntil(() => run.IsCompleted, 5f);
                Assert.AreEqual(SimState.PlantIdentified, flow.CurrentState, $"{plantId} demo should reach identification.");
                Assert.AreEqual(plantId, flow.Context.Plant.plantId);
                Assert.AreEqual(plantId, stage.PlantBuilder.Current.speciesId, $"The twin is rebuilt as {plantId}.");
                yield return Settle(1.4f);
                if (capture) yield return Shot($"{number++}_{plantId}");
                if (diseaseId == "healthy") continue;
                Assert.AreEqual(diseaseId, flow.Context.Disease.diseaseId);
                flow.ContinueFromPlantIdentified();
                flow.ContinueFromFeatures();
                flow.ContinueFromDiagnosis();
                Assert.AreEqual(SimState.FocusPart, flow.CurrentState);
                yield return Settle(2.0f);
                if (capture) yield return Shot($"{number++}_{plantId}_zoom");
            }

            // The demo picker holds all 36 plants and up to ten conditions.
            var settings = UnityEngine.Object.FindObjectOfType<SettingsView>(true);
            Assert.IsNotNull(settings, "SettingsView missing from the scene.");
            settings.Open();
            yield return Settle(1.0f);
            if (capture) yield return Shot($"{number++}_settings");
            settings.Close();
            yield return Settle(0.4f);

            if (capture) EndCapture();
            UnityEngine.Object.Destroy(fake.gameObject);
        }

        // ------------------------------------------------------------------

        static IEnumerator Settle(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>The leaf's own picture (its RenderTexture) as pixels; also saved to docs/screenshots/motion when a name is given.</summary>
        static Color32[] LeafFrame(ViewerStage stage, string name)
        {
            var rt = stage.LeafView.Texture;
            if (rt == null) return new Color32[0];
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            var pixels = tex.GetPixels32();
            if (name != null)
            {
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "screenshots", "motion"));
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), tex.EncodeToPNG());
            }
            UnityEngine.Object.Destroy(tex);
            return pixels;
        }

        /// <summary>Share of pixels whose colour changed noticeably between two frames (1 when the sizes differ).</summary>
        static float MovedShare(Color32[] a, Color32[] b)
        {
            if (a.Length == 0 || a.Length != b.Length) return 1f;
            int moved = 0;
            for (int i = 0; i < a.Length; i++)
                if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) + Mathf.Abs(a[i].a - b[i].a) > 24) moved++;
            return moved / (float)a.Length;
        }

        static RenderTexture _target;

        /// <summary>Renders the run into a phone-sized target with the UI on a camera canvas, so layout, text and animation are as on the device.</summary>
        static void BeginCapture()
        {
            var cam = Camera.main;
            var canvas = GameObject.Find("UI_Canvas").GetComponent<Canvas>();
            _target = new RenderTexture(ShotSize.x, ShotSize.y, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = _target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.5f;   // a tiny distance shrinks the canvas so far that TMP's mobile SDF shader loses precision
        }

        static void EndCapture()
        {
            var cam = Camera.main;
            if (cam != null) cam.targetTexture = null;
            if (_target != null) { _target.Release(); UnityEngine.Object.Destroy(_target); _target = null; }
        }

        static IEnumerator Shot(string name)
        {
            yield return null;
            yield return null;
            if (_target == null) yield break;

            var prev = RenderTexture.active;
            RenderTexture.active = _target;
            var tex = new Texture2D(ShotSize.x, ShotSize.y, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, ShotSize.x, ShotSize.y), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "screenshots"));
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }
    }
}
