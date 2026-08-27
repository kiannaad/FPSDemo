namespace CGame
{
    public enum ActorState
    {
        Constructed,
        Registered,
        Initializing,
        Initialized,
        ActivationPending,
        Playing,
        InitializationFaulted,
        TickFaulted,
        EndingPlay,
        Ended,
        Unregistered
    }
}
