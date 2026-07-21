namespace CGame
{
    public enum CharacterSpawnError
    {
        None,
        InvalidDefinitionId,
        DefinitionNotFound,
        InvalidDefinition,
        InvalidPlacement,
        UnsupportedControlKind,
        ControlKindNotSupportedByDefinition,
        CommitFailed,
        DuplicateRequestId,
    }
}
