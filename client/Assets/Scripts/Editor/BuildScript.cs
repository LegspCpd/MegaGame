using UnityEditor;
using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System;
using System.IO;
using System.Linq;

namespace Megame.Client
{
    public class BuildScript : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Set build version from command line
            string version = GetBuildVersion();
            if (!string.IsNullOrEmpty(version))
            {
                PlayerSettings.bundleVersion = version;
                Debug.Log($"Build version set to: {version}");
            }

            // Configure player settings
            ConfigurePlayerSettings(report.summary.platform);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Build succeeded: {report.summary.outputPath}");
                Debug.Log($"Build size: {report.summary.totalSize} bytes");
                Debug.Log($"Build time: {report.summary.totalTime}");
            }
            else
            {
                Debug.LogError($"Build failed: {report.summary.outputPath}");
                foreach (var step in report.steps)
                {
                    if (step.depth == 0)
                    {
                        Debug.Log($"Step: {step.name} - {step.duration}");
                    }
                }
            }
        }

        private string GetBuildVersion()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-buildVersion" && i + 1 < args.Length)
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        private void ConfigurePlayerSettings(BuildTarget target)
        {
            // General
            PlayerSettings.productName = "MegaGame";
            PlayerSettings.companyName = "MegaGame Studios";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.resizableWindow = true;

            // Graphics
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.gpuSkinning = true;
            PlayerSettings.graphicsJobs = true;
            // PlayerSettings.graphicsJobMode not available in Unity 2022.3

            // Scripting
            // PlayerSettings.scriptingBackend not available in Unity 2022.3
            PlayerSettings.apiCompatibilityLevel = ApiCompatibilityLevel.NET_Standard;
            PlayerSettings.allowUnsafeCode = true;

            // IL2CPP
            PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Standalone, Il2CppCompilerConfiguration.Release);

            // Strip engine code
            // PlayerSettings.managedStrippingLevel not available in Unity 2022.3
            PlayerSettings.stripEngineCode = true;

            // Platform specific
            switch (target)
            {
                case BuildTarget.StandaloneLinux64:
                case BuildTarget.StandaloneWindows64:
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(
                        BuildTargetGroup.Standalone,
                        "MEGAME_CLIENT;UNITY_STANDALONE;NET_STANDARD_2_1"
                    );
                    break;
            }

            // Rendering
            PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
            if (target == BuildTarget.StandaloneLinux64)
            {
                PlayerSettings.SetGraphicsAPIs(target, new[] {
                    UnityEngine.Rendering.GraphicsDeviceType.Vulkan,
                    UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            }
            else
            {
                // OpenGLES3 does not exist as a desktop backend on Windows;
                // leaving it in the list makes the player fail to start.
                PlayerSettings.SetGraphicsAPIs(target, new[] {
                    UnityEngine.Rendering.GraphicsDeviceType.Direct3D11,
                    UnityEngine.Rendering.GraphicsDeviceType.Vulkan });
            }
        }

        [MenuItem("MegaGame/Build/Linux")]
        public static void BuildLinux()
        {
            string path = "build/StandaloneLinux64/MegaGame";
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = path,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None
            };
            BuildPipeline.BuildPlayer(options);
        }

        [MenuItem("MegaGame/Build/Windows")]
        public static void BuildWindows()
        {
            string path = "build/StandaloneWindows64/MegaGame.exe";
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildPipeline.BuildPlayer(options);
        }

        [MenuItem("MegaGame/Build/All")]
        public static void BuildAll()
        {
            BuildLinux();
            BuildWindows();
        }

        /// <summary>
        /// Entry point used by CI (game-ci/unity-builder -buildMethod).
        /// Honours -outputPath and -buildVersion, and fails the build when
        /// Unity reports an error so CI cannot pass on a broken player.
        /// </summary>
        public static void PerformClientBuild()
        {
            string outputPath = GetArg("-outputPath", "build");
            string version = GetArg("-buildVersion", null);

            // Prefer the explicit -buildTarget flag: unity-builder runs Unity in
            // a container and does not forward host env vars into the Editor, so
            // UNITY_BUILD_TARGET is not visible here even when CI sets it.
            // -buildTarget is the documented Unity CLI switch and always wins.
            string flagTarget = GetArg("-buildTarget", null);
            string envTarget = Environment.GetEnvironmentVariable("UNITY_BUILD_TARGET");

            string targetName = flagTarget ?? envTarget;
            if (!string.IsNullOrEmpty(flagTarget))
            {
                Debug.Log($"BuildScript: using -buildTarget {flagTarget}");
            }
            else if (!string.IsNullOrEmpty(envTarget))
            {
                Debug.Log($"BuildScript: using UNITY_BUILD_TARGET {envTarget}");
            }
            else
            {
                Debug.Log("BuildScript: no build target specified, defaulting to Linux");
            }

            BuildTarget target =
                targetName != null && targetName.ToLowerInvariant() == "windows"
                    ? BuildTarget.StandaloneWindows64
                    : BuildTarget.StandaloneLinux64;

            string exe = target == BuildTarget.StandaloneWindows64 ? "MegaGame.exe" : "MegaGame";
            Directory.CreateDirectory(outputPath);
            string locationPath = Path.Combine(outputPath, exe);

            if (!string.IsNullOrEmpty(version))
            {
                PlayerSettings.bundleVersion = version;
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = locationPath,
                target = target,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception(
                    $"Unity build failed for {target}: {report.summary.result} " +
                    $"({report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings)");
            }

            Debug.Log($"Build succeeded: {locationPath}");
        }

        /// <summary>
        /// Dedicated server build (headless Linux player), also used by CI.
        /// </summary>
        public static void PerformHeadlessBuild()
        {
            string outputPath = GetArg("-outputPath", "build/server");
            string version = GetArg("-buildVersion", null);

            Directory.CreateDirectory(outputPath);
            string locationPath = Path.Combine(outputPath, "MegaGameServer");

            if (!string.IsNullOrEmpty(version))
            {
                PlayerSettings.bundleVersion = version;
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = locationPath,
                target = BuildTarget.StandaloneLinux64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception(
                    $"Unity headless build failed: {report.summary.result} " +
                    $"({report.summary.totalErrors} errors)");
            }

            Debug.Log($"Headless build succeeded: {locationPath}");
        }

        private static string GetArg(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }
            return fallback;
        }

        private const string DefaultScenePath = "Assets/Scenes/Boot.unity";

        private static string[] GetEnabledScenes()
        {
            string[] enabled = System.Array.FindAll(
                EditorBuildSettings.scenes,
                scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            // .meta GUIDs are generated on first import, so a checked-in
            // EditorBuildSettings can end up pointing at nothing. Fall back to
            // the boot scene rather than building with zero scenes.
            if (enabled.Length == 0 || !File.Exists(enabled[0]))
            {
                Debug.Log($"No usable build scenes configured; falling back to {DefaultScenePath}");
                return new[] { DefaultScenePath };
            }

            return enabled;
        }
    }
}