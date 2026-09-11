using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CGame.Editor
{
    /// <summary>Builds two independently named clients for the formal PvE capture.</summary>
    public static class PveAcceptancePlayerBuild
    {
        public static void BuildWindowsPair()
        {
            string embeddedRoot = BuildEmbeddedPackage();
            string originalProduct = PlayerSettings.productName;
            bool originalBackground = PlayerSettings.runInBackground;
            try
            {
                PlayerSettings.runInBackground = true;
                foreach (string role in new[] { "Lead", "Support" })
                {
                    PlayerSettings.productName = "FPSPvE" + role;
                    var options = new BuildPlayerOptions
                    {
                        scenes = new[] { DedicatedServerBuild.SampleScenePath },
                        locationPathName = "E:/UnityProgram/FPS/Build/PveAcceptance/" + role + "/FPSPvE" + role + ".exe",
                        target = BuildTarget.StandaloneWindows64,
                        targetGroup = BuildTargetGroup.Standalone,
                        subtarget = (int)StandaloneBuildSubtarget.Player,
                        options = BuildOptions.Development
                    };
                    BuildReport report = BuildPipeline.BuildPlayer(options);
                    if (report.summary.result != BuildResult.Succeeded)
                        throw new InvalidOperationException($"{role} player build failed: {report.summary.result}, errors={report.summary.totalErrors}");
                    string streamingRoot = Path.Combine(Path.GetDirectoryName(options.locationPathName),
                        "FPSPvE" + role + "_Data", "StreamingAssets", "yoo", "DefaultPackage");
                    Directory.CreateDirectory(streamingRoot);
                    foreach (string source in Directory.GetFiles(embeddedRoot))
                        File.Copy(source, Path.Combine(streamingRoot, Path.GetFileName(source)), true);
                    Debug.Log($"[Network][070] StandaloneBuild Role={role} Result={report.summary.result} Size={report.summary.totalSize}");
                }
            }
            finally
            {
                PlayerSettings.productName = originalProduct;
                PlayerSettings.runInBackground = originalBackground;
            }
        }

        private static string BuildEmbeddedPackage()
        {
            // Stage generated bundles outside Assets: a verification clone must
            // not overwrite the source project's checked-in StreamingAssets.
            var parameters = new YooAsset.Editor.BuiltinBuildParameters
            {
                BuildOutputRoot = "E:/UnityProgram/FPS/Build/PveAcceptance/Bundles",
                BuildinFileRoot = "E:/UnityProgram/FPS/Build/PveAcceptance/Embedded/yoo",
                BuildPipeline = YooAsset.Editor.EBuildPipeline.BuiltinBuildPipeline.ToString(),
                BuildBundleType = (int)YooAsset.EBuildBundleType.AssetBundle,
                BuildTarget = BuildTarget.StandaloneWindows64,
                PackageName = "DefaultPackage",
                PackageVersion = "Pve070-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                EnableSharePackRule = true,
                VerifyBuildingResult = true,
                FileNameStyle = YooAsset.EFileNameStyle.HashName,
                BuildinFileCopyOption = YooAsset.Editor.EBuildinFileCopyOption.OnlyCopyAll,
                CompressOption = YooAsset.Editor.ECompressOption.LZ4
            };
            var result = new YooAsset.Editor.BuiltinBuildPipeline().Run(parameters, true);
            if (!result.Success)
                throw new InvalidOperationException("PvE resource package failed: " + result.ErrorInfo);
            Debug.Log($"[Network][070] EmbeddedPackage Version={parameters.PackageVersion} Result=Succeeded");
            return parameters.GetBuildinRootDirectory();
        }
    }
}
