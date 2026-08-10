namespace CGame
{
    public abstract class WorldSubSystem : SubSystem
    {
        public World World => Owner as World;
    }
}
