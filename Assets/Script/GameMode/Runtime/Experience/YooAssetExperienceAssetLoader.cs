using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CGame
{
    public sealed class YooAssetExperienceAssetLoader : IExperienceAssetLoader
    {
        private readonly AssetService assets;

        public YooAssetExperienceAssetLoader(AssetService assets)
        {
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
        }

        public Task<IDisposable> LoadAsync(IReadOnlyList<string> locations) =>
            assets.LoadRequiredAssetsAsync(locations);
    }
}
