namespace CGame
{
    public abstract class PlayerSubSystem : SubSystem
    {
        public Player Player => Owner as Player;
    }
}
