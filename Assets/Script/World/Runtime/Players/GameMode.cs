using System;
using System.Threading;
using System.Threading.Tasks;

namespace CGame
{
    public abstract class GameMode : Actor
    {
        protected GameMode(World world, Player player)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            Player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public World World { get; }

        public Player Player { get; }

        public Controller PlayerController { get; private set; }

        internal void CreateAndAttachPlayerController()
        {
            if (PlayerController != null)
            {
                throw new InvalidOperationException("GameMode already created a PlayerController.");
            }

            Controller controller = CreatePlayerController(Player)
                ?? throw new InvalidOperationException("GameMode returned no PlayerController.");
            World.RegisterActor(controller, critical: true);
            Player.AttachController(controller);
            PlayerController = controller;
        }

        protected abstract Controller CreatePlayerController(Player player);

        public virtual Task SpawnDefaultPawn(
            Controller playerController,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
