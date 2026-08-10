using System;
using System.Threading;
using System.Threading.Tasks;

namespace CGame
{
    public sealed class AssetWorldCoreService : IWorldCoreService
    {
        private readonly ResourceWorldCoreService resourceService;
        private AssetService assets;

        public AssetWorldCoreService(ResourceWorldCoreService resourceService)
        {
            this.resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
        }

        public string Name => "Asset";

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!resourceService.IsReady)
            {
                throw new InvalidOperationException("Asset service requires an initialized Resource service.");
            }

            assets = new AssetService(resourceService.Resources);
            return Task.CompletedTask;
        }

        public Task ShutdownAsync()
        {
            assets?.UnloadUnusedAssets();
            assets = null;
            return Task.CompletedTask;
        }
    }
}
