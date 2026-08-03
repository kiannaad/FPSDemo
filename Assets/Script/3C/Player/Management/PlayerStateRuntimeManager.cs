using CGame.Ability;

namespace CGame
{
    public sealed class PlayerStateRuntimeManager : IManager
    {
        private PlayerStateManager stateManager;

        public override int Priority => 70;
        public PlayerStateManager StateManager => stateManager;
        public PlayerState PlayerState => stateManager?.PlayerState;

        public override void Init()
        {
            stateManager = new PlayerStateManager();
            stateManager.Initialize(new AbilitySet(), this);
        }

        public override void Update(float elapseSeconds)
        {
        }

        public override void Shutdown()
        {
            stateManager?.Shutdown();
            stateManager = null;
        }
    }
}
