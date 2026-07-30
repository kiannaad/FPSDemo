namespace CGame
{
    public enum CharacterSpawnState
    {
        Requested,
        ResolvingDefinition,
        ResolvingInitialWeapon,
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
