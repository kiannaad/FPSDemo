using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace CGame
{
    public sealed class ExperienceManagerComponent : ActorComponent, IGameFeatureActivationHost
    {
        private readonly World world;
        private readonly ExperienceDefinition experience;
        private readonly IExperienceAssetLoader assetLoader;
        private readonly GameFeatureComponentRegistry components = new GameFeatureComponentRegistry();
        private ExperienceActivationTransaction transaction;
        private readonly List<IDisposable> assetLeases = new List<IDisposable>();
        private Task currentLoadTask;

        public ExperienceManagerComponent(
            World world,
            ExperienceDefinition experience,
            IExperienceAssetLoader assetLoader = null)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.experience = experience ?? throw new ArgumentNullException(nameof(experience));
            this.assetLoader = assetLoader;
        }

        public ExperienceLoadState LoadState { get; private set; } = ExperienceLoadState.NotStarted;

        public string Failure { get; private set; } = string.Empty;

        public int ReadyWriteCount { get; private set; }

        public int FailedWriteCount { get; private set; }

        public GameFeatureComponentRegistry Components => components;

        public World World => world;

        public GameFeatureActivationReceipt InstallComponent(object component, Guid ownerId) =>
            components.Install(component, ownerId);

        public Task LoadAsync()
        {
            if (currentLoadTask != null) return currentLoadTask;
            if (LoadState != ExperienceLoadState.NotStarted)
            {
                throw new InvalidOperationException("Experience cannot be switched or loaded twice in one World.");
            }

            currentLoadTask = LoadInternalAsync();
            return currentLoadTask;
        }

        public async Task ShutdownAsync()
        {
            if (LoadState == ExperienceLoadState.Shutdown) return;
            LoadState = ExperienceLoadState.ShuttingDown;
            Task loadTask = currentLoadTask;
            if (loadTask != null)
            {
                try { await loadTask; }
                catch { }
            }

            transaction?.Dispose();
            transaction = null;
            ReleaseAssets();
            components.Dispose();
            LoadState = ExperienceLoadState.Shutdown;
            Debug.Log("[Experience] Phase=Shutdown Result=Complete");
        }

        protected override void OnShutdown()
        {
            if (LoadState != ExperienceLoadState.Shutdown)
            {
                ShutdownAsync().GetAwaiter().GetResult();
            }
        }

        private async Task LoadInternalAsync()
        {
            LoadState = ExperienceLoadState.Loading;
            Debug.Log("[Experience] Phase=Load Result=Started");
            try
            {
                transaction = new ExperienceActivationTransaction(this);
                IReadOnlyList<GameFeatureConfig> order = transaction.BuildActivationOrder(experience);
                IExperienceAssetLoader loader = assetLoader;
                for (int index = 0; index < order.Count; index++)
                {
                    GameFeatureConfig feature = order[index];
                    if (feature.RequiredAssetLocations.Count > 0)
                    {
                        if (loader == null)
                        {
                            AssetManager assetManager = world.GetSubSystem<AssetManager>();
                            if (assetManager == null)
                            {
                                throw new InvalidOperationException($"Feature {feature.FeatureId} requires AssetManager.");
                            }

                            loader = new YooAssetExperienceAssetLoader(assetManager.Assets);
                        }

                        assetLeases.Add(await loader.LoadAsync(feature.RequiredAssetLocations));
                    }
                }

                if (LoadState == ExperienceLoadState.ShuttingDown)
                {
                    ReleaseAssets();
                    return;
                }

                transaction.Activate(experience);
                if (LoadState == ExperienceLoadState.ShuttingDown)
                {
                    transaction.Dispose();
                    transaction = null;
                    ReleaseAssets();
                    return;
                }

                LoadState = ExperienceLoadState.Ready;
                ReadyWriteCount++;
                Debug.Log("[Experience] Phase=Activate Result=Ready");
            }
            catch (Exception exception)
            {
                transaction?.Dispose();
                transaction = null;
                ReleaseAssets();
                if (LoadState != ExperienceLoadState.ShuttingDown)
                {
                    Failure = exception.Message;
                    LoadState = ExperienceLoadState.Failed;
                    FailedWriteCount++;
                    Debug.LogError($"[Experience] Phase=Rollback Result=Failed Error={exception.Message}");
                }
                throw;
            }
        }

        private void ReleaseAssets()
        {
            for (int index = assetLeases.Count - 1; index >= 0; index--) assetLeases[index].Dispose();
            assetLeases.Clear();
        }
    }
}
