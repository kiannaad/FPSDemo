namespace CGame
{
    public enum CharacterDefinitionResolveError
    {
        None,
        InvalidDefinitionId,
        DefinitionNotFound,
        AssetLoadFailed,
        DefinitionIdMismatch,
        MissingVisualPrefab,
        InvalidAnimationConfig,
        MissingSupportedControlKind,
        InvalidInitialWeaponId,
    }
}
