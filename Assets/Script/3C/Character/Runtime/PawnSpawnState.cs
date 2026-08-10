namespace CGame
{
    public enum PawnSpawnState
    {
        None,
        Preparing,
        DataAvailable,
        Committing,
        WaitingForGameplayReady,
        GameplayReady,
        Failed
    }
}
