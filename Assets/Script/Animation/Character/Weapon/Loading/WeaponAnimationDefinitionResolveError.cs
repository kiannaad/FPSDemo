namespace CGame.Animation
{
    public enum WeaponAnimationDefinitionResolveError
    {
        None,
        InvalidWeaponId,
        CatalogLoadFailed,
        CatalogInvalid,
        DefinitionNotFound,
        DefinitionLoadFailed,
        DefinitionIdMismatch,
        InvalidDefinition,
        ProviderDisposed,
    }
}
