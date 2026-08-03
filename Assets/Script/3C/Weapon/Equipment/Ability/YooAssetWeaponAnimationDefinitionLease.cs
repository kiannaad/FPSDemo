using CGame.Animation;
using YooAsset;

namespace CGame
{
    public sealed class YooAssetWeaponAnimationDefinitionLease :
        IEquipmentDefinitionLease
    {
        private AssetHandle handle;

        private YooAssetWeaponAnimationDefinitionLease(
            AssetHandle handle,
            WeaponAnimationDefinition definition)
        {
            this.handle = handle;
            Definition = definition;
        }

        public WeaponAnimationDefinition Definition { get; private set; }
        public WeaponId WeaponId => Definition == null ? default : Definition.WeaponId;
        public bool IsValid =>
            !IsDisposed &&
            handle != null &&
            handle.IsValid &&
            Definition != null &&
            WeaponId.IsValid;
        public bool IsDisposed { get; private set; }

        public static bool TryTakeOwnership(
            AssetHandle handle,
            WeaponId expectedWeaponId,
            out YooAssetWeaponAnimationDefinitionLease lease)
        {
            lease = null;
            if (handle == null)
            {
                return false;
            }

            if (!handle.IsValid)
            {
                return false;
            }

            if (!handle.IsDone ||
                handle.Status != EOperationStatus.Succeed)
            {
                handle.Release();
                return false;
            }

            WeaponAnimationDefinition definition =
                handle.GetAssetObject<WeaponAnimationDefinition>();
            if (definition == null ||
                !expectedWeaponId.IsValid ||
                definition.Validate(expectedWeaponId) !=
                WeaponAnimationDefinitionError.None)
            {
                handle.Release();
                return false;
            }

            lease = new YooAssetWeaponAnimationDefinitionLease(
                handle,
                definition);
            return true;
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            AssetHandle current = handle;
            handle = null;
            Definition = null;
            current?.Release();
        }
    }
}
