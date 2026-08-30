using System.Threading.Tasks;

namespace CGame
{
    public abstract class GameState : Actor
    {
        public abstract Task LoadExperienceAsync();

        public abstract Task ShutdownExperienceAsync();
    }
}
