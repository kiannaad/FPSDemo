namespace CGame
{
    public interface IPawnInitStateParticipant
    {
        string Name { get; }

        bool CanEnterState(PawnInitState nextState, PawnInitContext context);

        void EnterState(PawnInitState nextState, PawnInitContext context);

        void Shutdown();
    }
}
