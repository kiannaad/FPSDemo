using System;
using UnityEngine;
using YooAsset;

namespace CGame
{
    public sealed class AssetService
    {
        private readonly ResourceService resources;

        public AssetService(ResourceService resources)
        {
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
        }

        public AssetHandle LoadAsset<T>(string location) where T : UnityEngine.Object
        {
            return resources.GetPackage().LoadAssetAsync<T>(location);
        }

        public bool CheckLocation(string location)
        {
            return resources.GetPackage().CheckLocationValid(location);
        }

        public void UnloadUnusedAssets()
        {
            resources.GetPackage().UnloadUnusedAssetsAsync();
        }
    }
}
