using System.Threading.Tasks;

namespace CGame
{
    public sealed class DefaultGameState : GameState
    {
        private readonly ExperienceManagerComponent experienceManager;

        public DefaultGameState(World world, ExperienceDefinition experience)
        {
            experienceManager = new ExperienceManagerComponent(world, experience);
        }

        public ExperienceManagerComponent ExperienceManager => experienceManager;

        protected override void OnInitialize()
        {
            AddComponent(experienceManager);
        }

        public override Task LoadExperienceAsync() => experienceManager.LoadAsync();

        public override Task ShutdownExperienceAsync() => experienceManager.ShutdownAsync();
    }
}
