namespace CGame
{
    public interface IWorldSession
    {
        GameSessionId Id { get; }

        bool IsActive { get; }

        void Shutdown();
    }
}
