namespace CGame
{
    public interface IWorldTickCoreService
    {
        TickGroup TickGroup { get; }

        void Tick(float deltaTime);
    }
}
