namespace CGame
{
    public enum CharacterSpawnState
    {
        Requested,
        ResolvingDefinition,
        Assembling,
        Registering,
        Possessing,
        CharacterReady,
        CancelRequested,
        Cancelled,
        Released,
        Failed,
    }
}
