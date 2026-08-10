namespace CGame
{
    public interface IGameSessionFactory
    {
        IWorldSession Create(World world, GameSessionId sessionId, GameStartRequest request);
    }
}
