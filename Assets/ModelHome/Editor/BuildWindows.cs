using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace ModelHome.EditorTools
{
    /// <summary>Windows x64 standalone build that renders on the GPU (plan Task 13, user requirement).
    /// Direct3D 12 with a Direct3D 11 fallback; the Unity Windows player already exports
    /// NvOptimusEnablement / AmdPowerXpressRequestHighPerformance so hybrid laptops use the dedicated GPU.</summary>
    public static class BuildWindows
    {
        public const string OutputPath = "Builds/Windows/ModelHome.exe";

        [MenuItem("Model Home/Apply Windows Build Settings")]
        public static void ApplySettings()
        {
            PlayerSettings.productName = "Model Home";
            PlayerSettings.companyName = "FohYooo";
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.runInBackground = true;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.vSyncCount = 1;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            Debug.Log("[ModelHome] Windows build settings applied (D3D12 -> D3D11, Mono, 1080p fullscreen window, VSync).");
        }

        [MenuItem("Model Home/4. Build Windows x64")]
        public static void Build()
        {
            ApplySettings();
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ModelHomeSceneBuilder.ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[ModelHome] Windows build {s.result}: {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalTime}");
            if (s.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Windows build {s.result} ({s.totalErrors} errors)");
        }

        /// <summary>Batchmode entry point: -executeMethod ModelHome.EditorTools.BuildWindows.BuildCli</summary>
        public static void BuildCli()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
                EditorApplication.Exit(1);
            }
        }
    }
}
