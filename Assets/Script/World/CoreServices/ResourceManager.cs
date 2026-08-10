using System.Threading;
using System.Threading.Tasks;

namespace CGame
{
    public sealed class ResourceManager : WorldSubSystem
    {
        private readonly string packageName;
        private readonly ResourceService resources = new ResourceService();

        public ResourceManager(string packageName = "DefaultPackage")
        {
            this.packageName = string.IsNullOrWhiteSpace(packageName) ? "DefaultPackage" : packageName;
        }

        public bool IsReady => resources.IsReady;

        public ResourceService Resources => resources;

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await resources.InitializeAsync(packageName);
            cancellationToken.ThrowIfCancellationRequested();
        }

        protected override Task OnShutdownAsync() => resources.ShutdownAsync();
    }
}
