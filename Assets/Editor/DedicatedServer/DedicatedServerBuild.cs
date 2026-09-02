using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CGame.Editor
{
    public static class DedicatedServerBuild
    {
        public const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        public const string OutputPath = "E:/UnityProgram/FPS/Build/DedicatedServer/Windows/FPSResearchServer.exe";

        [MenuItem("CGame/Network/Build Windows Dedicated Server")]
        public static void BuildWindows()
        {
            if (!File.Exists(DedicatedServerProjectSetup.BootstrapScenePath))
            {
                DedicatedServerProjectSetup.Setup();
            }

            if (!File.Exists(SampleScenePath))
            {
                throw new FileNotFoundException("Formal SampleScene is missing.", SampleScenePath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { DedicatedServerProjectSetup.BootstrapScenePath, SampleScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Dedicated Server build failed: {report.summary.result}, Errors={report.summary.totalErrors}");
            }

            Debug.Log(
                $"[DedicatedServer][037] BuildSucceeded Output={OutputPath} Size={report.summary.totalSize} Duration={report.summary.totalTime}");
        }
    }
}
