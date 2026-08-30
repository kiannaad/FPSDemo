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
#if UNITY_EDITOR
            if (defaultPackage.InitializeStatus == EOperationStatus.Succeed)
            {
                IsReady = true;
                return;
            }
#endif
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
#if UNITY_EDITOR
            // PlayMode tests and scene reloads share YooAsset's editor simulation package.
            // Destroying it here is asynchronous and can race the next GameInstance startup.
            await Task.CompletedTask;
            return;
#else
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
#endif
        }

        public async Task PreloadAssetsAsync(IEnumerable<string> locations)
        {
            using (await LoadRequiredAssetsAsync(locations)) { }
        }

        public async Task<IDisposable> LoadRequiredAssetsAsync(IEnumerable<string> locations)
        {
            if (locations == null) throw new ArgumentNullException(nameof(locations));
            var handles = new List<AssetHandle>();
            try
            {
                foreach (string location in locations)
                {
                    if (string.IsNullOrWhiteSpace(location))
                    {
                        throw new InvalidOperationException("Required asset location cannot be empty.");
                    }

                    handles.Add(GetPackage().LoadAssetAsync<UnityEngine.Object>(location));
                }

                foreach (AssetHandle handle in handles)
                {
                    await handle.Task;
                    if (handle.Status != EOperationStatus.Succeed)
                    {
                        throw new InvalidOperationException(handle.LastError);
                    }
                }

                return new AssetHandleLease(handles);
            }
            catch
            {
                ReleaseHandles(handles);
                throw;
            }
        }

        private static void ReleaseHandles(IReadOnlyList<AssetHandle> handles)
        {
            for (int index = handles.Count - 1; index >= 0; index--)
            {
                handles[index]?.Release();
            }
        }

        private sealed class AssetHandleLease : IDisposable
        {
            private List<AssetHandle> handles;

            public AssetHandleLease(List<AssetHandle> handles)
            {
                this.handles = handles;
            }

            public void Dispose()
            {
                List<AssetHandle> current = handles;
                handles = null;
                if (current != null) ReleaseHandles(current);
            }
        }
    }
}
