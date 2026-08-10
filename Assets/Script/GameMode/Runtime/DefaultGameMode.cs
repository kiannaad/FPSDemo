using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CGame
{
    public sealed class DefaultGameMode : GameMode
    {
        private readonly PawnDefinition pawnDefinition;
        private readonly InitialInventorySet initialInventorySet;
        private readonly PawnFactory pawnFactory;
        private ActorRegistration pawnRegistration;
        private Pawn defaultPawn;

        public DefaultGameMode(
            World world,
            Player player,
            PawnDefinition pawnDefinition,
            InitialInventorySet initialInventorySet,
            PawnFactory pawnFactory = null)
            : base(world, player)
        {
            this.pawnDefinition = pawnDefinition ?? throw new ArgumentNullException(nameof(pawnDefinition));
            this.initialInventorySet = initialInventorySet;
            this.pawnFactory = pawnFactory ?? new PawnFactory();
        }

        public Pawn DefaultPawn => defaultPawn != null && defaultPawn.State != ActorState.Unregistered
            ? defaultPawn
            : null;

        protected override Controller CreatePlayerController(Player player)
        {
            return CGame.PlayerController.Create(
                player,
                pawnDefinition,
                initialInventorySet,
                player.GetSubSystem<InputSubSystem>(),
                new DefaultPlayerControllerComponentFactory());
        }

        public override async Task SpawnDefaultPawn(
            Controller playerController,
            CancellationToken cancellationToken = default)
        {
            if (!(playerController is PlayerController controller))
            {
                throw new InvalidOperationException("DefaultGameMode requires a PlayerController.");
            }

            ResolveSpawnPose(out Vector3 spawnPosition, out Quaternion spawnRotation);
            Pawn candidate = await pawnFactory.CreateAsync(
                pawnDefinition,
                spawnPosition,
                spawnRotation,
                cancellationToken);
            ActorRegistration candidateRegistration = null;
            Pawn previousPawn = DefaultPawn;
            ActorRegistration previousRegistration = pawnRegistration;
            try
            {
                candidateRegistration = World.RegisterActor(candidate, critical: true);
                controller.Unpossess();
                controller.Possess(candidate);
                if (World.State == WorldState.Playing)
                {
                    World.ActivateActor(candidateRegistration);
                }

                defaultPawn = candidate;
                pawnRegistration = candidateRegistration;
                if (previousRegistration != null && !previousRegistration.IsDisposed)
                {
                    World.UnregisterActor(previousRegistration);
                }
            }
            catch
            {
                if (ReferenceEquals(controller.PossessedPawn, candidate))
                {
                    controller.Unpossess();
                }

                if (candidateRegistration != null && !candidateRegistration.IsDisposed)
                {
                    World.UnregisterActor(candidateRegistration);
                }
                else if (candidate.State == ActorState.Constructed)
                {
                    candidate.DestroyCandidate();
                }

                if (previousPawn != null && previousPawn.State != ActorState.Unregistered)
                {
                    controller.Possess(previousPawn);
                }

                throw;
            }
        }

        protected override void OnShutdown()
        {
            defaultPawn = null;
            pawnRegistration = null;
        }

        private static void ResolveSpawnPose(out Vector3 position, out Quaternion rotation)
        {
            PlayerStart playerStart = UnityEngine.Object.FindObjectOfType<PlayerStart>();
            if (playerStart == null)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                return;
            }

            PlayerStartInfo start = playerStart.GetInfo();
            position = start.Position;
            rotation = start.Rotation;
        }
    }
}
