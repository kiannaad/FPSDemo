using System;
using System.Threading;
using System.Threading.Tasks;

namespace CGame
{
    public sealed class AssetManager : WorldSubSystem
    {
        public AssetManager()
        {
            AddDependency<ResourceManager>();
        }

        public AssetService Assets { get; private set; }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResourceManager resources = World.GetSubSystem<ResourceManager>();
            if (resources == null || !resources.IsReady)
            {
                throw new InvalidOperationException("AssetManager requires an initialized ResourceManager.");
            }

            Assets = new AssetService(resources.Resources);
            return Task.CompletedTask;
        }

        protected override Task OnShutdownAsync()
        {
            Assets?.UnloadUnusedAssets();
            Assets = null;
            return Task.CompletedTask;
        }
    }
}
