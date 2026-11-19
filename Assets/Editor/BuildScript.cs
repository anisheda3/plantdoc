#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PlantDoctor.EditorTools
{
    /// <summary>
    /// One-click (or command-line) Android build.
    ///
    /// Editor:  Tools > AI Plant Doctor > Build Android APK
    /// CLI:     Unity.exe -batchmode -quit -projectPath . -buildTarget Android
    ///                    -executeMethod PlantDoctor.EditorTools.BuildScript.CommandLineBuild
    /// Output:  Builds/Android/PlantDoctor.apk
    /// </summary>
    public static class BuildScript
    {
        public const string ApkPath = "Builds/Android/PlantDoctor.apk";

        [MenuItem("Tools/AI Plant Doctor/Build Android APK", priority = 40)]
        public static void BuildAndroidMenu()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUtility.DisplayDialog("Switch to Android?",
                    "The active platform is not Android. Switch now? (Unity will reimport assets.)", "Switch and build", "Cancel"))
                return;

            bool ok = BuildAndroid();
            EditorUtility.DisplayDialog(ok ? "Build succeeded" : "Build failed",
                ok ? $"APK written to {Path.GetFullPath(ApkPath)}\n\nInstall with:\nadb install -r \"{ApkPath}\""
                   : "See the Console for details.", "OK");
            if (ok) EditorUtility.RevealInFinder(ApkPath);
        }

        /// <summary>Configures the project, regenerates the scene if missing, and builds.</summary>
        public static bool BuildAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            ProjectSetup.Apply();
            if (!File.Exists(SceneBootstrapper.ScenePath) && !SceneBootstrapper.BuildScene())
                return false;

            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SceneBootstrapper.ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[AI Plant Doctor] Build {summary.result}: {summary.totalErrors} errors, " +
                      $"{summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalSeconds:0}s -> {ApkPath}");
            return summary.result == BuildResult.Succeeded;
        }

        /// <summary>Batch-mode entry point: regenerates the scene, then builds. Exit code 0 = success.</summary>
        public static void CommandLineBuild()
        {
            bool ok = false;
            try
            {
                ProjectSetup.Apply();
                ok = SceneBootstrapper.BuildScene() && BuildAndroid();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Batch-mode entry point: configure + generate everything, no build.</summary>
        public static void CommandLineSetup()
        {
            bool ok = false;
            try
            {
                ProjectSetup.Apply();
                ok = SceneBootstrapper.BuildScene();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
#endif
