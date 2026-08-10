using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CGame.GameplayTags;

namespace CGame
{
    public sealed class GameplayTagWorldCoreService : WorldSubSystem
    {
        private readonly GameplayTagSource[] sources;

        public GameplayTagWorldCoreService(IEnumerable<GameplayTagSource> sources)
        {
            this.sources = sources == null ? Array.Empty<GameplayTagSource>() : new List<GameplayTagSource>(sources).ToArray();
        }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GameplayTagRegistryBuildResult result = GameplayTagManager.Instance.Initialize(sources);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors));
            }

            return Task.CompletedTask;
        }

        protected override Task OnShutdownAsync()
        {
            GameplayTagManager.Instance.Shutdown();
            return Task.CompletedTask;
        }
    }
}
