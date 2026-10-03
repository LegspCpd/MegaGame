using UnityEditor;
using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;

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
            PlayerSettings.msaa = 4;
            PlayerSettings.gpuSkinning = true;
            PlayerSettings.graphicsJobs = true;
            PlayerSettings.graphicsJobMode = GraphicsJobMode.Optimized;

            // Scripting
            PlayerSettings.scriptingBackend = ScriptingImplementation.IL2CPP;
            PlayerSettings.apiCompatibilityLevel = ApiCompatibilityLevel.NET_Standard_2_1;
            PlayerSettings.allowUnsafeCode = true;

            // IL2CPP
            PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Standalone, Il2CppCompilerConfiguration.Release);

            // Strip engine code
            PlayerSettings.managedStrippingLevel = ManagedStrippingLevel.High;
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
            PlayerSettings.SetGraphicsAPIs(target, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11, UnityEngine.Rendering.GraphicsDeviceType.Vulkan, UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
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

        private static string[] GetEnabledScenes()
        {
            return System.Array.FindAll(EditorBuildSettings.scenes, scene => scene.enabled)
                .Select(scene => scene.path).ToArray();
        }
    }
}