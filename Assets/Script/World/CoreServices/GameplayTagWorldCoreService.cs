using System;
using System.Threading;
using System.Threading.Tasks;
using CGame.GameplayTags;

namespace CGame
{
    public sealed class GameplayTagWorldCoreService : IWorldCoreService
    {
        private readonly GameplayTagConfig config;
        private bool initialized;

        public GameplayTagWorldCoreService(GameplayTagConfig config)
        {
            this.config = config;
        }

        public string Name => "GameplayTag";

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (config == null)
            {
                throw new InvalidOperationException("GameBootstrap requires a GameplayTagConfig.");
            }

            GameplayTagManager.Instance.Shutdown();
            GameplayTagRegistryBuildResult result = GameplayTagManager.Instance.Initialize(
                config.Sources,
                config.Redirects);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors));
            }

            initialized = true;
            return Task.CompletedTask;
        }

        public Task ShutdownAsync()
        {
            if (initialized)
            {
                GameplayTagManager.Instance.Shutdown();
                initialized = false;
            }

            return Task.CompletedTask;
        }
    }
}
