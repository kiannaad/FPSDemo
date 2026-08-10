using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace CGame
{
    public sealed class ResourceService
    {
        private ResourcePackage defaultPackage;

        public bool IsReady { get; private set; }

        public async Task InitializeAsync(string packageName = "DefaultPackage")
        {
            IsReady = false;
            YooAssets.Initialize();
            defaultPackage = YooAssets.TryGetPackage(packageName) ?? YooAssets.CreatePackage(packageName);
            YooAssets.SetDefaultPackage(defaultPackage);
            InitializeParameters parameters;
#if UNITY_EDITOR
            PackageInvokeBuildResult buildResult = EditorSimulateModeHelper.SimulateBuild(packageName);
            if (buildResult == null || string.IsNullOrWhiteSpace(buildResult.PackageRootDirectory))
            {
                throw new InvalidOperationException(
                    $"Failed to build the YooAsset editor simulation manifest for {packageName}.");
            }

            parameters = new EditorSimulateModeParameters
            {
                EditorFileSystemParameters =
                    FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory)
            };
#else
            parameters = new OfflinePlayModeParameters
            {
                BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters()
            };
#endif
            InitializationOperation initialization = defaultPackage.InitializeAsync(parameters);
            await initialization.Task;
            if (initialization.Status != EOperationStatus.Succeed)
            {
                throw new InvalidOperationException(initialization.Error);
            }

            RequestPackageVersionOperation version = defaultPackage.RequestPackageVersionAsync();
            await version.Task;
            if (version.Status != EOperationStatus.Succeed)
            {
                throw new InvalidOperationException(version.Error);
            }

            UpdatePackageManifestOperation manifest =
                defaultPackage.UpdatePackageManifestAsync(version.PackageVersion);
            await manifest.Task;
            if (manifest.Status != EOperationStatus.Succeed)
            {
                throw new InvalidOperationException(manifest.Error);
            }

            IsReady = true;
        }

        public ResourcePackage GetPackage()
        {
            return defaultPackage
                ?? throw new InvalidOperationException("Resource service is not initialized.");
        }

        public async Task ShutdownAsync()
        {
            IsReady = false;
            ResourcePackage package = defaultPackage;
            defaultPackage = null;
            if (package == null || !YooAssets.Initialized)
            {
                return;
            }

            DestroyOperation destruction = package.DestroyAsync();
            await destruction.Task;
            if (destruction.Status != EOperationStatus.Succeed)
            {
                throw new InvalidOperationException(destruction.Error);
            }

            YooAssets.RemovePackage(package);
        }

        public async Task PreloadAssetsAsync(IEnumerable<string> locations)
        {
            var handles = new List<AssetHandle>();
            foreach (string location in locations)
            {
                handles.Add(GetPackage().LoadAssetAsync<UnityEngine.Object>(location));
            }

            foreach (AssetHandle handle in handles)
            {
                await handle.Task;
            }
        }
    }
}
