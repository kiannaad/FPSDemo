using System;
using System.Threading;
using System.Threading.Tasks;

namespace CGame
{
    public sealed class ResourceWorldCoreService : IWorldCoreService
    {
        private readonly string packageName;
        private readonly ResourceService resources = new ResourceService();

        public ResourceWorldCoreService(string packageName)
        {
            this.packageName = string.IsNullOrWhiteSpace(packageName) ? "DefaultPackage" : packageName;
        }

        public string Name => "Resource";

        public bool IsReady => resources.IsReady;

        public ResourceService Resources => resources;

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await resources.InitializeAsync(packageName);
            cancellationToken.ThrowIfCancellationRequested();
            if (!resources.IsReady)
            {
                throw new InvalidOperationException($"Resource package {packageName} did not become ready.");
            }
        }

        public async Task ShutdownAsync()
        {
            await resources.ShutdownAsync();
        }
    }
}
