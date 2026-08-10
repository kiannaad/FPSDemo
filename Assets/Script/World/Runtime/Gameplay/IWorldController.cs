namespace CGame
{
    public interface IWorldController
    {
        GameSessionId SessionId { get; }

        bool IsActive { get; }

        void Shutdown();
    }
}
