#if UNITY_EDITOR
using PlantDoctor.AI;
using PlantDoctor.AR;
using PlantDoctor.Core;
using PlantDoctor.Diseases;
using PlantDoctor.Plants;
using PlantDoctor.Simulation;
using PlantDoctor.Treatment;
using PlantDoctor.UI;
using PlantDoctor.Utilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
#if PLANTDOCTOR_AR_FOUNDATION
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;
#endif

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// Builds the complete, runnable app scene in one click — no manual wiring.
    /// Menu: Tools > AI Plant Doctor > Build App Scene.
    ///
    /// One scene serves every device:
    ///  • AR rig (AR Session + XR Origin + AR camera)  — ARCore phones
    ///  • Studio rig (3D camera, lights, floor)          — phones without ARCore, and the Editor
    /// AppModeController picks one at startup.
    /// </summary>
    public static class SceneBootstrapper
    {
        public const string ScenePath = "Assets/Scenes/PlantDoctor.unity";
        const string DataRoot = "Assets/Data";
        const string PrefabFolder = "Assets/Prefabs/AR";

        /// <summary>Which interface the scene gets. Classic is the step-by-step dark-glass interface; Dashboard is the
        /// card-and-sheet interface with the Cure journey page. Both share every system, the plant and the leaf.</summary>
        public enum UiStyle { Classic, Dashboard }

        public const UiStyle DefaultStyle = UiStyle.Classic;

        /// <summary>Command line: -uiStyle classic | dashboard.</summary>
        static UiStyle StyleFromCommandLine()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-uiStyle")
                    return args[i + 1].ToLowerInvariant() == "dashboard" ? UiStyle.Dashboard : UiStyle.Classic;
            return DefaultStyle;
        }

        [MenuItem("Tools/AI Plant Doctor/Build App Scene (Classic UI)", priority = 1)]
        static void BuildClassicMenu() => BuildAndReport(UiStyle.Classic);

        [MenuItem("Tools/AI Plant Doctor/Build App Scene (Dashboard UI)", priority = 2)]
        static void BuildDashboardMenu() => BuildAndReport(UiStyle.Dashboard);

        [MenuItem("Tools/AI Plant Doctor/Build App Scene", priority = 0)]
        public static void BuildSceneMenu() => BuildAndReport(DefaultStyle);

        static void BuildAndReport(UiStyle style)
        {
            if (!BuildScene(style)) return;
            EditorUtility.DisplayDialog("Plant Doctor scene ready",
                "PlantDoctor.unity was created and fully wired.\n\n" +
                "Press Play to try it in the Editor (studio mode + webcam), or use " +
                "Tools > AI Plant Doctor > Build Android APK for your phone.", "OK");
        }

        /// <summary>Kept for muscle memory: the old menu now builds the new scene.</summary>
        [MenuItem("Tools/AI Plant Doctor/Build Demo Scene")]
        public static void BuildDemoSceneAlias() => BuildSceneMenu();

        public static bool BuildScene() => BuildScene(StyleFromCommandLine());

        public static bool BuildScene(UiStyle style)
        {
            // ---------- Data and generated assets ----------
            AssetDatabase.Refresh();
            // Textures first: the species data references the generated disease masks.
            DiseaseTextureBuilder.BuildAll();
            DemoDataBuilder.Build();
            var database = AssetDatabase.LoadAssetAtPath<PlantDatabase>($"{DataRoot}/Plants/PlantDatabase.asset");
            if (database == null)
            {
                Debug.LogError("[AI Plant Doctor] Could not create the plant database.");
                return false;
            }
            var policy = AssetDatabase.LoadAssetAtPath<ConfidencePolicy>($"{DataRoot}/Config/ConfidencePolicy.asset");
            var quality = AssetDatabase.LoadAssetAtPath<ImageQualityAnalyzer>($"{DataRoot}/Config/ImageQualitySettings.asset");
            var generalPrevention = AssetDatabase.LoadAssetAtPath<PreventionPlan>($"{DataRoot}/Treatments/PreventionPlan_General.asset");

            AssignDiseaseMasks();
            UIShapeLibrary.BuildAll();
            if (style == UiStyle.Dashboard) AppUIBuilder.LoadOrCreateTheme();
            int viewerLayer = ProjectLayers.Ensure(ProjectLayers.Viewer);
            var leafMaterial = PlantBuilder.CreateLeafSpecimenMaterial();

            EnsureFolder("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------- Lighting shared by both modes ----------
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.97f, 0.9f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.82f, 0.86f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.60f, 0.62f, 0.54f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.32f, 0.26f);
            RenderSettings.skybox = null;

            // ---------- Systems (one shared health state drives every plant) ----------
            // NewScene unloads unused assets, so objects loaded before it may be destroyed:
            // load the default species fresh.
            var tomato = AssetDatabase.LoadAssetAtPath<PlantData>($"{DataRoot}/Plants/Plant_Tomato.asset");
            if (tomato == null || tomato.species == null)
                Debug.LogError("[AI Plant Doctor] Tomato species profile missing: the twin will be empty until a scan.");
            var species = tomato != null ? tomato.species : null;

            var systems = new GameObject("_Systems");
            var binder = systems.AddComponent<PlantVisualBinder>();
            var stateManager = systems.AddComponent<SimulationStateManager>();
            var aiManager = systems.AddComponent<AIManager>();
            var featureAnalysis = systems.AddComponent<FeatureAnalysisManager>();
            var diseaseSim = systems.AddComponent<DiseaseSimulationManager>();
            var recovery = systems.AddComponent<RecoveryManager>();
            var treatment = systems.AddComponent<TreatmentManager>();
            var prevention = systems.AddComponent<PreventionManager>();
            var flow = systems.AddComponent<AppFlowController>();
            var demoMode = systems.AddComponent<DemoModeController>();
            var history = style == UiStyle.Dashboard ? systems.AddComponent<ScanHistory>() : null;
            systems.AddComponent<AudioSource>();
            systems.AddComponent<FeedbackDirector>();
            var overlays = systems.AddComponent<AROverlayRenderer>();
            var scan = systems.AddComponent<ARScanController>();
            var webcam = systems.AddComponent<WebCamFrameSource>();
            var mode = systems.AddComponent<AppModeController>();

            Wire(aiManager, ("plantDatabase", database), ("confidencePolicy", policy));
            Wire(diseaseSim, ("visualBinder", binder));
            Wire(recovery, ("visualBinder", binder));
            Wire(treatment, ("visualBinder", binder), ("recoveryManager", recovery));
            Wire(prevention, ("genericFallbackPlan", generalPrevention));
            Wire(overlays, ("plantDatabase", database));
            Wire(demoMode, ("flow", flow), ("plantDatabase", database));
            if (history != null) Wire(history, ("flow", flow));
            Wire(scan, ("flow", flow), ("qualitySettings", quality));
            Wire(flow,
                ("plantDatabase", database), ("qualitySettings", quality),
                ("stateManager", stateManager), ("aiManager", aiManager),
                ("featureAnalysis", featureAnalysis), ("diseaseSimulation", diseaseSim),
                ("treatmentManager", treatment), ("recoveryManager", recovery),
                ("preventionManager", prevention));

            // ---------- The AR twin: placed on a real surface, shown only on the plant (AR) view ----------
            var arTwin = PlantBuilder.BuildTwin("PlantTwin (AR)", Vector3.zero, species, binder, "");
            Wire(arTwin.Presenter, ("flow", flow), ("visualBinder", binder), ("twinBuilder", arTwin.Builder),
                ("visualRoot", arTwin.Visual), ("growRoot", arTwin.PlantRoot));
            arTwin.Visual.SetActive(false);

            // ---------- The viewer stage: its own layer, far from the AR scene ----------
            var viewerRoot = new GameObject("_Viewer");
            viewerRoot.transform.position = new Vector3(400f, 0f, 0f);
            SetLayer(viewerRoot, viewerLayer);

            var viewerTwin = PlantBuilder.BuildTwin("PlantTwin (Viewer)", viewerRoot.transform.position, species, binder, ProjectLayers.Viewer);
            viewerTwin.Twin.transform.SetParent(viewerRoot.transform, true);
            Wire(viewerTwin.Presenter, ("flow", flow), ("visualBinder", binder), ("twinBuilder", viewerTwin.Builder),
                ("visualRoot", viewerTwin.Visual), ("growRoot", viewerTwin.PlantRoot));

            var leafStation = new GameObject("LeafStation");
            leafStation.transform.SetParent(viewerRoot.transform, false);
            leafStation.transform.localPosition = new Vector3(40f, 0f, 0f);
            SetLayer(leafStation, viewerLayer);
            var leaf = leafStation.AddComponent<LeafSpecimen>();
            Wire(leaf, ("material", leafMaterial), ("stalkMaterial", PlantBuilder.CreateLeafStalkMaterial()), ("outlineTexture", AssetDatabase.LoadAssetAtPath<Texture2D>(PlantBuilder.LeafTexturePath)),
                ("visualBinder", binder));
            var leafSo = new SerializedObject(leaf);
            leafSo.FindProperty("layerName").stringValue = ProjectLayers.Viewer;
            leafSo.ApplyModifiedPropertiesWithoutUndo();

            var plantView = BuildViewerCamera("PlantCamera", viewerRoot.transform, viewerLayer, 26f, 20f, 9f, 0f, new Vector2(-6f, 38f), 0.78f);
            var leafView = BuildViewerCamera("LeafCamera", leafStation.transform, viewerLayer, 22f, 14f, 6f, 0f, new Vector2(-35f, 35f));
            var stage = viewerRoot.AddComponent<ViewerStage>();
            Wire(stage, ("plantBuilder", viewerTwin.Builder), ("plantRoot", viewerTwin.PlantRoot),
                ("plantView", plantView), ("leafView", leafView), ("leaf", leaf));

            // ---------- Main camera (camera mode / UI) ----------
            var studio = BuildStudioRig(viewerLayer);

            // ---------- AR rig ----------
            GameObject[] arObjects = System.Array.Empty<GameObject>();
            MonoBehaviour arFrameSource = null, placer = null, probe = null;
#if PLANTDOCTOR_AR_FOUNDATION
            arObjects = BuildARRig(sun, arTwin, flow, viewerLayer, out arFrameSource, out placer);
            probe = systems.AddComponent<ARAvailabilityProbe>();
#else
            Debug.LogWarning("[AI Plant Doctor] AR Foundation is not installed: the scene will run in camera mode only.");
#endif

            Wire(mode, ("arAvailabilityProbe", probe), ("scanController", scan), ("arFrameSource", arFrameSource),
                ("webCamFrameSource", webcam), ("twinPresenter", arTwin.Presenter), ("arPlacer", placer));
            WireArray(mode, "arOnlyObjects", arObjects);
            WireArray(mode, "cameraModeOnlyObjects", new Object[] { studio });

            // ---------- UI ----------
            if (style == UiStyle.Classic)
            {
                // The classic interface continues stage by stage and keeps the placed plant in the world.
                SetBool(flow, "guidedStages", true);
                if (placer != null) SetBool(placer, "twinInAllStages", true);
                ClassicUIBuilder.Build(new ClassicUIBuilder.Context
                {
                    Flow = flow, Demo = demoMode, Mode = mode, Scan = scan, TwinPlacement = placer, Binder = binder, Stage = stage
                });
            }
            else
            {
                AppUIBuilder.Build(new AppUIBuilder.Context
                {
                    Flow = flow, Demo = demoMode, Mode = mode, Scan = scan, TwinPlacement = placer, Binder = binder,
                    Stage = stage, Database = database, ArTwinBuilder = arTwin.Builder, History = history,
                    Simulation = diseaseSim, Recovery = recovery
                });
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AI Plant Doctor] {ScenePath} built and wired.");
            return true;
        }

        static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[AI Plant Doctor] Field '{field}' not found on {target.GetType().Name}."); return; }
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetLayer(GameObject go, int layer)
        {
            if (layer >= 0) go.layer = layer;
        }

        /// <summary>A camera that renders only the viewer layer, switched off until a screen needs it.</summary>
        static Viewport3D BuildViewerCamera(string name, Transform parent, int layer, float fov, float yaw, float pitch, float yawLimit, Vector2 pitchRange, float margin = 1.08f)
        {
            var go = new GameObject(name, typeof(Camera));
            go.transform.SetParent(parent, false);
            var cam = go.GetComponent<Camera>();
            cam.cullingMask = layer >= 0 ? 1 << layer : 0;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 40f;
            cam.fieldOfView = fov;
            cam.enabled = false;
            var view = go.AddComponent<Viewport3D>();
            var so = new SerializedObject(view);
            so.FindProperty("fieldOfView").floatValue = fov;
            so.FindProperty("startYaw").floatValue = yaw;
            so.FindProperty("startPitch").floatValue = pitch;
            so.FindProperty("yawLimit").floatValue = yawLimit;
            so.FindProperty("pitchRange").vector2Value = pitchRange;
            so.FindProperty("margin").floatValue = margin;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // ------------------------------------------------------------------
        // Main camera
        // ------------------------------------------------------------------

        /// <summary>
        /// The main camera for devices without AR: it only clears to the page colour and carries
        /// the audio listener. Every 3D picture is drawn by the viewer cameras into textures.
        /// </summary>
        static GameObject BuildStudioRig(int viewerLayer)
        {
            var rig = new GameObject("StudioRig");
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rig.transform, false);
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.965f, 0.953f, 0.914f);
            cam.cullingMask = LayerMask.GetMask("UI");   // nothing in the world; the UI layer is needed when the canvas is drawn by this camera
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 30f;
            return rig;
        }

#if PLANTDOCTOR_AR_FOUNDATION
        // ------------------------------------------------------------------
        // AR rig
        // ------------------------------------------------------------------

        static GameObject[] BuildARRig(Light sun, PlantBuilder.Result twin, AppFlowController flow, int viewerLayer,
            out MonoBehaviour frameSource, out MonoBehaviour placerOut)
        {
            var sessionGo = new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager));

            var originGo = new GameObject("XR Origin", typeof(XROrigin));
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(originGo.transform, false);

            var camGo = new GameObject("AR Camera", typeof(Camera), typeof(AudioListener),
                typeof(ARCameraManager), typeof(ARCameraBackground), typeof(TrackedPoseDriver));
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(offset.transform, false);
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 20f;
            cam.cullingMask = ProjectLayers.Without(viewerLayer);   // the viewer stage is never part of the AR scene

            // Same bindings as AR Foundation's own "XR Origin (Mobile AR)" menu item.
            var tpd = camGo.GetComponent<TrackedPoseDriver>();
            var position = new InputAction("Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            position.AddBinding("<HandheldARInputDevice>/devicePosition");
            var rotation = new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            rotation.AddBinding("<HandheldARInputDevice>/deviceRotation");
            tpd.positionInput = new InputActionProperty(position);
            tpd.rotationInput = new InputActionProperty(rotation);

            var source = camGo.AddComponent<ARCameraFrameSource>();
            Wire(source, ("cameraManager", camGo.GetComponent<ARCameraManager>()),
                ("cameraBackground", camGo.GetComponent<ARCameraBackground>()));

            var origin = originGo.GetComponent<XROrigin>();
            origin.Camera = cam;
            origin.CameraFloorOffsetObject = offset;

            var planes = originGo.AddComponent<ARPlaneManager>();
            planes.planePrefab = PlanePrefab();
            planes.requestedDetectionMode = UnityEngine.XR.ARSubsystems.PlaneDetectionMode.Horizontal;
            var raycasts = originGo.AddComponent<ARRaycastManager>();
            originGo.AddComponent<ARAnchorManager>();

            // Placement reticle: the holo ring, laid on the detected surface.
            var reticle = new GameObject("PlacementReticle", typeof(MeshFilter), typeof(MeshRenderer));
            reticle.GetComponent<MeshFilter>().sharedMesh = PlantBuilder.GroundQuad();
            var reticleRenderer = reticle.GetComponent<MeshRenderer>();
            reticleRenderer.sharedMaterial = ReticleMaterial();
            reticleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            reticle.transform.localScale = Vector3.one * 0.28f;
            reticle.SetActive(false);

            // Placement + gestures live on their own object so they deactivate with the AR rig.
            var arSystems = new GameObject("_AR");
            var placer = arSystems.AddComponent<PlantTwinPlacer>();
            Wire(placer, ("raycastManager", raycasts), ("planeManager", planes), ("arCamera", cam),
                ("twinRoot", twin.Twin.transform), ("presenter", twin.Presenter),
                ("placementReticle", reticle.transform));
            placer.enabled = false;   // AppModeController enables it in AR mode

            var estimation = sun.gameObject.AddComponent<ARLightEstimation>();
            Wire(estimation, ("cameraManager", camGo.GetComponent<ARCameraManager>()));

            // AR objects start inactive; AppModeController turns them on once AR is confirmed.
            sessionGo.SetActive(false);
            originGo.SetActive(false);
            arSystems.SetActive(false);
            reticle.transform.SetParent(arSystems.transform, true);

            frameSource = source;
            placerOut = placer;
            return new[] { sessionGo, originGo, arSystems };
        }

        static GameObject PlanePrefab()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(PrefabFolder);
            string path = $"{PrefabFolder}/ARPlane_Dots.prefab";

            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_ARPlaneDots.mat");
            if (mat == null)
            {
                mat = new Material(Shader.Find("PlantDoctor/ARPlaneDots")) { name = "M_ARPlaneDots" };
                AssetDatabase.CreateAsset(mat, "Assets/Materials/M_ARPlaneDots.mat");
            }

            var go = new GameObject("ARPlane_Dots", typeof(ARPlane), typeof(ARPlaneMeshVisualizer),
                typeof(MeshFilter), typeof(MeshRenderer));
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static Material ReticleMaterial()
        {
            const string path = "Assets/Materials/M_PlacementReticle.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("PlantDoctor/HoloRing")) { name = "M_PlacementReticle" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_Dashes", 24f);
            mat.SetFloat("_Spin", 0.6f);
            mat.SetFloat("_Width", 0.035f);
            mat.SetColor("_Color", new Color(0.86f, 0.83f, 0.74f, 1f));
            return mat;
        }
#endif

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>Gives each disease's simulation profile its own masks (spots vs blotches).</summary>
        static void AssignDiseaseMasks()
        {
            var early = AssetDatabase.LoadAssetAtPath<SimulationProfile>($"{DataRoot}/Diseases/SimProfile_TomatoEarlyBlight.asset");
            var late = AssetDatabase.LoadAssetAtPath<SimulationProfile>($"{DataRoot}/Diseases/SimProfile_TomatoLateBlight.asset");
            var yellow = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Generated/T_YellowingMask.png");
            if (early != null)
            {
                early.lesionMask = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Generated/T_LesionMask_EarlyBlight.png");
                early.yellowingMask = yellow;
                EditorUtility.SetDirty(early);
            }
            if (late != null)
            {
                late.lesionMask = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Generated/T_LesionMask_LateBlight.png");
                late.yellowingMask = yellow;
                EditorUtility.SetDirty(late);
            }
        }

        static void RegisterInBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(path, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != path) scenes.Add(new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void Wire(Object target, params (string field, Object value)[] assignments) => UIKit.Wire(target, assignments);

        static void WireArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
