using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PlantDoctor.AR;
using PlantDoctor.Core;
using PlantDoctor.Plants;
using PlantDoctor.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PlantDoctor.Tests
{
    /// <summary>
    /// Loads the real app scene and drives a complete phone-camera scan with a synthetic
    /// leaf "camera" (so no real webcam is ever opened): capture -> quality gate -> AI ->
    /// identification -> features -> diagnosis -> treatment -> recovery -> prevention -> report.
    ///
    /// The explicit screenshot variant also renders every stage to docs/screenshots.
    /// </summary>
    public class PhoneScanFlowTests
    {
        const string SceneName = "PlantDoctor";
        static readonly Vector2Int ShotSize = new Vector2Int(1080, 2340);   // 19.5:9 phone

        [UnityTest]
        public IEnumerator PhoneScanFlowRunsEndToEnd() => Drive(capture: false);

        [UnityTest, Explicit("Writes PNGs to docs/screenshots; run on demand.")]
        public IEnumerator CaptureStageScreenshots() => Drive(capture: true);

        IEnumerator Drive(bool capture)
        {
            if (!Application.CanStreamedLevelBeLoaded(SceneName))
                Assert.Ignore("PlantDoctor scene is not in Build Settings. Run Tools > AI Plant Doctor > Build App Scene.");

            SceneManager.LoadScene(SceneName);
            yield return null;

            var flow = UnityEngine.Object.FindObjectOfType<AppFlowController>();
            if (UnityEngine.Object.FindObjectOfType<ScreenRouter>() == null)
                Assert.Ignore("This scene was built with the classic interface; this test is for the dashboard one.");
            var mode = UnityEngine.Object.FindObjectOfType<AppModeController>();
            var scan = UnityEngine.Object.FindObjectOfType<ARScanController>();
            var demo = UnityEngine.Object.FindObjectOfType<DemoModeController>();
            var router = UnityEngine.Object.FindObjectOfType<ScreenRouter>();
            var history = UnityEngine.Object.FindObjectOfType<ScanHistory>();
            Assert.IsNotNull(flow, "AppFlowController missing from the scene.");
            Assert.IsNotNull(scan, "ARScanController missing from the scene.");
            Assert.IsNotNull(router, "ScreenRouter missing from the scene.");
            Assert.IsNotNull(history, "ScanHistory missing from the scene.");

            // Tests never touch the real scan history of the machine.
            string historyFolder = Path.Combine(Path.GetTempPath(), "plantdoctor-test-" + Guid.NewGuid().ToString("N"));
            history.Folder = historyFolder;

            yield return WaitUntil(() => mode == null || mode.Mode != AppMode.Resolving, 5f);
            Assert.AreEqual(AppMode.Camera, mode.Mode, "Without ARCore (Editor) the app must fall back to camera mode.");
            flow.AI.SetBackend(AI.InferenceMode.Mock);
            flow.AI.DemoPlant = "tomato";   // PlayerPrefs may hold another plant from a manual run
            flow.AI.DemoScenario = "early_blight";

            // Swap in a synthetic camera before the scan stage starts the real one.
            var fake = new GameObject("FakeLeafCamera").AddComponent<FakeLeafCamera>();
            scan.SetFrameSource(fake);
            if (capture) BeginCapture();

            // The splash is timed, and the first frames of a cold test run are slow, so only shoot it if still up.
            if (router.Booting) Assert.AreEqual(AppScreenId.Splash, router.Current, "The app opens on the splash screen.");
            if (capture && router.Booting) { yield return Settle(0.3f); if (router.Booting) yield return Shot("01_splash"); }
            router.FinishBoot();
            yield return Settle(0.9f);
            Assert.AreEqual(AppScreenId.Home, router.Current);
            if (capture) yield return Shot("02_home");

            flow.BeginScan();
            yield return WaitUntil(() => scan.HasLiveQuality, 5f);
            Assert.AreEqual(AppScreenId.Scan, router.Current);
            Assert.IsTrue(scan.LiveQuality.IsAcceptable,
                $"Synthetic leaf should pass the live quality gate: {string.Join(", ", scan.LiveQuality.Issues)}");
            yield return Settle(0.8f);
            if (capture) yield return Shot("03_scan");

            scan.CaptureAndSubmit();
            yield return WaitUntil(() => flow.CurrentState == SimState.AiAnalysis, 3f);
            yield return Settle(0.4f);
            Assert.AreEqual(AppScreenId.Analysis, router.Current);
            if (capture) yield return Shot("04_analysis");

            yield return WaitUntil(() => flow.CurrentState == SimState.Diagnosis, 20f);
            Assert.AreEqual(SimState.Diagnosis, flow.CurrentState, "A good camera frame must reach the diagnosis.");
            Assert.IsNotNull(flow.LastScanImage, "The scanned leaf should be kept for the results screen.");
            Assert.AreEqual("tomato", flow.Context.Plant.plantId);
            yield return Settle(1.6f);
            Assert.AreEqual(AppScreenId.Diagnosis, router.Current);
            Assert.AreEqual(1, history.Records.Count, "A finished scan is saved to the history exactly once.");
            if (capture) yield return Shot("05_diagnosis");

            // The plant view is the hub; each step can be opened from it, in any order.
            Assert.IsNotNull(UnityEngine.Object.FindObjectOfType<ViewerStage>(), "ViewerStage missing from the scene.");
            flow.ContinueFromDiagnosis();
            yield return Settle(1.6f);
            Assert.AreEqual(AppScreenId.ArView, router.Current);
            if (capture) yield return Shot("06_plant_view");

            flow.OpenZoom();
            yield return Settle(1.6f);
            Assert.AreEqual(AppScreenId.Zoom, router.Current);
            if (capture) yield return Shot("07_magnified_leaf");

            // The leaf turns with a finger drag (and keeps drifting a moment after the flick), nothing else moves it.
            var leafView = UnityEngine.Object.FindObjectOfType<ViewerStage>().LeafView;
            float yawBefore = leafView.Yaw;
            leafView.BeginDrag();
            leafView.Drag(new Vector2(120f, 0f));
            leafView.EndDrag();
            Assert.AreNotEqual(yawBefore, leafView.Yaw, "Dragging must turn the leaf.");
            Assert.IsTrue(leafView.Interacting, "Callouts hide while the leaf is turning.");
            yield return Settle(2.0f);
            float settled = leafView.Yaw;
            yield return Settle(0.5f);
            Assert.AreEqual(settled, leafView.Yaw, 0.01f, "After the flick the leaf must come to rest and stay still.");
            leafView.AnimateReset();
            yield return Settle(1.2f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(leafView.Yaw, 14f), 1f, "A double tap returns the leaf to its start view.");

            // One page, one animation: the cure journey starts on the diseased plant and plays to recovery.
            float before = flow.Context.CurrentState.diseaseSeverity;
            flow.OpenJourney();
            yield return Settle(0.9f);
            Assert.AreEqual(AppScreenId.Journey, router.Current);
            Assert.IsNotNull(flow.Treatment.Plan, "The treatment plan is ready on the journey page.");
            if (capture) yield return Shot("08_journey_diseased");
            yield return Settle(4.5f);
            if (capture) yield return Shot("09_journey_treatment");
            yield return Settle(8.5f);
            if (capture) yield return Shot("10_journey_recovered");

            // Steps are marked as done by the user; playing the animation does not do it for them.
            Assert.AreEqual(0, flow.Context.CompletedTreatmentSteps, "Watching the animation must not mark steps as done.");
            int steps = flow.Treatment.Plan.StepCount;
            for (int i = 0; i < steps; i++)
            {
                flow.PerformNextTreatmentStep(false);
                yield return null;
            }
            Assert.AreEqual(steps, flow.Context.CompletedTreatmentSteps);
            Assert.Less(flow.Context.CurrentState.diseaseSeverity, before, "Treatment must reduce severity.");
            yield return Settle(0.5f);
            if (capture) yield return Shot("11_journey_steps_done");

            flow.OpenPrevention();
            yield return Settle(1.0f);
            Assert.AreEqual(AppScreenId.Prevention, router.Current);
            if (capture) yield return Shot("12_prevention");
            flow.SetPreventionReviewed(2);

            flow.OpenReport();
            yield return Settle(1.2f);
            Assert.AreEqual(AppScreenId.Report, router.Current);
            var report = flow.Context.BuildReport();
            Assert.Greater(report.RecoveryPercent, 0f);
            Assert.Less(report.RecoveryPercent, 1f, "Treatment is never a guaranteed full cure.");
            if (capture) yield return Shot("13_report");

            // The profile (settings and past scans) opens from the home screen's gear.
            flow.GoTo(SimState.Welcome);
            yield return Settle(0.8f);
            Assert.AreEqual(AppScreenId.Home, router.Current);
            if (capture) yield return Shot("14_home");
            router.ShowProfile();
            yield return Settle(0.7f);
            Assert.AreEqual(AppScreenId.Profile, router.Current);
            if (capture) yield return Shot("15_profile");
            router.Back();
            yield return Settle(0.5f);
            Assert.AreEqual(AppScreenId.Home, router.Current);

            // Healthy plants take the short path and are never shown as an error.
            var healthyRun = demo.RunScenario("tomato", "healthy", 0f, 0.9f);
            yield return WaitUntil(() => healthyRun.IsCompleted, 5f);
            yield return WaitUntil(() => flow.CurrentState == SimState.Diagnosis, 5f);
            yield return Settle(1.6f);
            Assert.AreEqual(SimState.Diagnosis, flow.CurrentState, "A healthy result must not land on the error screen.");
            if (capture) yield return Shot("16_healthy");
            flow.ContinueFromDiagnosis();
            yield return null;
            Assert.AreEqual(SimState.Prevention, flow.CurrentState);

            // One twin generator for every species: the body is rebuilt for each identified
            // plant and that disease's own look (orange rust, dark late-blight blotches) lands on it.
            var builder = UnityEngine.Object.FindObjectsOfType<PlantTwinBuilder>(true).First(b => b.gameObject.layer != 0);
            var sweep = new[] { ("potato", "late_blight", "17_potato"), ("pepper", "bacterial_spot", "18_pepper"),
                                ("apple", "cedar_apple_rust", "19_apple_rust"), ("apple", "apple_scab", "20_apple_scab") };
            foreach (var (plantId, diseaseId, shot) in sweep)
            {
                var run = demo.RunScenario(plantId, diseaseId, 0.7f, 0.93f);
                yield return WaitUntil(() => run.IsCompleted, 5f);
                yield return WaitUntil(() => flow.CurrentState == SimState.Diagnosis, 5f);
                Assert.AreEqual(SimState.Diagnosis, flow.CurrentState, $"{plantId} demo should reach the diagnosis.");
                Assert.AreEqual(plantId, builder.Current.speciesId, $"The twin should be rebuilt as {plantId}.");
                Assert.AreEqual(diseaseId, flow.Context.Disease.diseaseId);
                Assert.Greater(builder.DiseaseRenderers.Count, 0);

                flow.OpenArView();
                yield return Settle(1.0f);
                flow.OpenZoom();
                yield return Settle(1.4f);
                Assert.AreEqual(AppScreenId.Zoom, router.Current);
                if (capture) yield return Shot(shot);
            }

            if (capture) EndCapture();
            UnityEngine.Object.Destroy(fake.gameObject);
            try { if (Directory.Exists(historyFolder)) Directory.Delete(historyFolder, true); } catch (IOException) { }
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

        static RenderTexture _target;

        /// <summary>
        /// Renders the whole run into a phone-sized target with the UI on a camera canvas, so
        /// layout, text meshes and animations are exactly as they would be on the device
        /// (switching canvas mode for a single frame leaves text unbuilt).
        /// </summary>
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
