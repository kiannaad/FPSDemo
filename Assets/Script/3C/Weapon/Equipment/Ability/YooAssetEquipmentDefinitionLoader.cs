using System;
using CGame.Animation;
using YooAsset;

namespace CGame
{
    public sealed class YooAssetEquipmentDefinitionLoader :
        IEquipmentDefinitionLoader
    {
        private readonly IWeaponAnimationDefinitionLocationResolver
            locationResolver;

        public YooAssetEquipmentDefinitionLoader(
            IWeaponAnimationDefinitionLocationResolver locationResolver)
        {
            this.locationResolver = locationResolver
                ?? throw new ArgumentNullException(nameof(locationResolver));
        }

        public IEquipmentDefinitionLoadOperation BeginLoad(WeaponId weaponId)
        {
            if (!locationResolver.TryResolveLocation(
                    weaponId,
                    out string location)
                || string.IsNullOrWhiteSpace(location))
            {
                return new LoadOperation(weaponId, null);
            }

            AssetHandle handle;
            try
            {
                handle = YooAssets.LoadAssetAsync<WeaponAnimationDefinition>(
                    location);
            }
            catch
            {
                handle = null;
            }

            return new LoadOperation(weaponId, handle);
        }

        private sealed class LoadOperation :
            IEquipmentDefinitionLoadOperation
        {
            private readonly WeaponId expectedWeaponId;
            private AssetHandle handle;
            private bool leaseTaken;

            public LoadOperation(
                WeaponId expectedWeaponId,
                AssetHandle handle)
            {
                this.expectedWeaponId = expectedWeaponId;
                this.handle = handle;
            }

            public bool IsDone => handle == null || handle.IsDone;

            public bool TryTakeLease(
                out IEquipmentDefinitionLease definitionLease)
            {
                definitionLease = null;
                if (leaseTaken || !IsDone)
                {
                    return false;
                }

                leaseTaken = true;
                AssetHandle completed = handle;
                handle = null;
                if (!YooAssetWeaponAnimationDefinitionLease.TryTakeOwnership(
                        completed,
                        expectedWeaponId,
                        out YooAssetWeaponAnimationDefinitionLease lease))
                {
                    return false;
                }

                definitionLease = lease;
                return true;
            }

            public void Dispose()
            {
                AssetHandle current = handle;
                handle = null;
                current?.Release();
            }
        }
    }
}
