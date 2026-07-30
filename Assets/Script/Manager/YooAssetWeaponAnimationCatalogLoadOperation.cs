using System;
using CGame.Animation;
using YooAsset;

internal sealed class YooAssetWeaponAnimationCatalogLoadOperation :
    IWeaponAnimationCatalogLoadOperation
{
    private AssetHandle handle;

    public YooAssetWeaponAnimationCatalogLoadOperation(AssetHandle handle)
    {
        this.handle =
            handle ?? throw new ArgumentNullException(nameof(handle));
    }

    public bool IsCompleted => handle == null || handle.IsDone;
    public bool IsSuccessful =>
        handle != null && handle.Status == EOperationStatus.Succeed;
    public WeaponAnimationCatalog Asset =>
        handle?.GetAssetObject<WeaponAnimationCatalog>();
    public string Error => handle?.LastError ?? string.Empty;

    public void Dispose()
    {
        if (handle == null)
        {
            return;
        }

        handle.Release();
        handle = null;
    }
}
