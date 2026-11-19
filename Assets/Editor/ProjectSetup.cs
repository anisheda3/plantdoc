#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
#if PLANTDOCTOR_AR_FOUNDATION
using UnityEngine.XR.ARFoundation;
#endif
#if PLANTDOCTOR_ARCORE
using UnityEditor.XR.ARCore;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.Management;
#endif

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// Configures the project for an Android AR build in one step:
    ///  • Player settings ARCore requires (OpenGL ES 3, API 24+, IL2CPP / ARM64, portrait)
    ///  • Plain-HTTP allowed so the phone can reach a dev inference server on the LAN
    ///  • XR Plug-in Management with the ARCore loader enabled for Android
    ///  • ARCore marked "Optional", so phones without ARCore still install and use camera mode
    ///  • URP: the AR background renderer feature (without it the camera feed is black)
    ///  • Shadow settings tuned for a small object on a table
    /// Menu: Tools > AI Plant Doctor > Configure Project for Android AR.
    /// </summary>
    public static class ProjectSetup
    {
        const string RendererDataPath = "Assets/New Universal Render Pipeline Asset_Renderer.asset";
        const string PipelineAssetPath = "Assets/New Universal Render Pipeline Asset.asset";

        [MenuItem("Tools/AI Plant Doctor/Configure Project for Android AR", priority = 20)]
        public static void ApplyMenu()
        {
            var report = Apply();
            EditorUtility.DisplayDialog("Project configured", string.Join("\n", report), "OK");
        }

        public static List<string> Apply()
        {
            var report = new List<string>();
            ConfigurePlayer(report);
            ConfigureXR(report);
            ConfigureRenderer(report);
            AssetDatabase.SaveAssets();
            foreach (var line in report) Debug.Log($"[AI Plant Doctor] {line}");
            return report;
        }

        static void ConfigurePlayer(List<string> report)
        {
            PlayerSettings.companyName = "PlantDoctor";
            PlayerSettings.productName = "Plant Doctor";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = Mathf.Max(2, PlayerSettings.Android.bundleVersionCode);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.plantdoctor.app");

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;   // ARCore minimum
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;          // Play + ARCore requirement

            // ARCore's camera background shader is GLES3-only.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, false);

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // A development inference server on the LAN speaks plain HTTP.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            PlayerSettings.iOS.cameraUsageDescription = "Plant Doctor uses the camera to scan plant leaves.";

            report.Add("Player: com.plantdoctor.app, API 24+, IL2CPP ARM64, OpenGL ES 3, portrait, LAN HTTP allowed.");
        }

        static void ConfigureXR(List<string> report)
        {
#if PLANTDOCTOR_ARCORE
            const string settingsKey = "com.unity.xr.management.loader_settings";
            EditorBuildSettings.TryGetConfigObject(settingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/XR")) AssetDatabase.CreateFolder("Assets", "XR");
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(settingsKey, perTarget, true);
            }

            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);

            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            general.InitManagerOnStart = true;
            bool assigned = XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.ARCore.ARCoreLoader", BuildTargetGroup.Android);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(general.Manager);
            EditorUtility.SetDirty(perTarget);

            var arcore = ARCoreSettings.GetOrCreateSettings();
            arcore.requirement = ARCoreSettings.Requirement.Optional;
            EditorUtility.SetDirty(arcore);

            report.Add($"XR: ARCore loader for Android {(assigned ? "enabled" : "already enabled")}, ARCore = Optional (non-AR phones use camera mode).");
#else
            report.Add("XR: ARCore XR Plugin not installed — skipped (the app will run in camera mode).");
#endif
        }

        static void ConfigureRenderer(List<string> report)
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>(RendererDataPath);
            if (rendererData == null)
            {
                report.Add($"Renderer: {RendererDataPath} not found — add ARBackgroundRendererFeature manually.");
                return;
            }

#if PLANTDOCTOR_AR_FOUNDATION
            bool has = false;
            foreach (var f in rendererData.rendererFeatures) if (f is ARBackgroundRendererFeature) has = true;
            if (!has)
            {
                var feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
                feature.name = "ARBackgroundRendererFeature";
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

                var so = new SerializedObject(rendererData);
                var features = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rendererData);
                report.Add("URP: ARBackgroundRendererFeature added (camera feed renders behind the twin).");
            }
            else report.Add("URP: ARBackgroundRendererFeature already present.");
#endif

            var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipeline != null)
            {
                var so = new SerializedObject(pipeline);
                SetFloat(so, "m_ShadowDistance", 6f);         // tight distance = sharp shadows on a small plant
                SetInt(so, "m_SoftShadowsSupported", 1);
                SetInt(so, "m_MainLightShadowmapResolution", 2048);
                SetInt(so, "m_MSAA", 2);                      // cleaner thin leaf edges
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pipeline);
                report.Add("URP: shadows 6 m / soft / 2048, MSAA 2x.");
            }
        }

        static void SetFloat(SerializedObject so, string name, float v) { var p = so.FindProperty(name); if (p != null) p.floatValue = v; }
        static void SetInt(SerializedObject so, string name, int v)
        {
            var p = so.FindProperty(name);
            if (p == null) return;
            if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = v != 0;
            else p.intValue = v;
        }
    }
}
#endif
