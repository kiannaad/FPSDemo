using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CGame
{
    public sealed class DefaultGameMode : GameMode
    {
        private readonly PlayerStateDefinition playerStateDefinition;
        private readonly PawnFactory pawnFactory;
        private ActorRegistration pawnRegistration;
        private Pawn defaultPawn;
        private string occupiedPlayerPointId;
        private Guid occupiedRegistrationId;

        public DefaultGameMode(
            World world,
            Player player,
            PlayerStateDefinition playerStateDefinition,
            PawnFactory pawnFactory = null)
            : base(world, player)
        {
            this.playerStateDefinition = playerStateDefinition ?? throw new ArgumentNullException(nameof(playerStateDefinition));
            this.pawnFactory = pawnFactory ?? new PawnFactory();
        }

        public Pawn DefaultPawn => defaultPawn != null && defaultPawn.State != ActorState.Unregistered
            ? defaultPawn
            : null;

        public ActorRegistration DefaultPawnRegistration =>
            pawnRegistration != null && !pawnRegistration.IsDisposed ? pawnRegistration : null;

        public string OccupiedPlayerPointId => occupiedPlayerPointId;

        public PlayerStateDefinition PlayerStateDefinition => playerStateDefinition;

        protected override Controller CreatePlayerController(Player player)
        {
            return CGame.PlayerController.Create(
                player,
                playerStateDefinition,
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
            if (DefaultPawn != null)
            {
                throw new InvalidOperationException("DefaultGameMode already spawned its default Pawn.");
            }

            LevelRuntime levelRuntime = World.LevelRuntime
                ?? throw new InvalidOperationException("DefaultGameMode requires LevelRuntime.");
            using SpawnPointReservation reservation = levelRuntime.ReserveFirstPlayerPoint();
            Vector3 spawnPosition = reservation.Transform.position;
            Quaternion spawnRotation = reservation.Transform.rotation;
            Pawn candidate = await pawnFactory.CreateAsync(
                playerStateDefinition.PawnData,
                playerStateDefinition.InputProfile,
                spawnPosition,
                spawnRotation,
                cancellationToken);
            ActorRegistration candidateRegistration = null;
            Pawn previousPawn = DefaultPawn;
            ActorRegistration previousRegistration = pawnRegistration;
            try
            {
                candidateRegistration = World.RegisterActor(candidate, critical: true);
                candidateRegistration.Disposed += OnPawnRegistrationDisposed;
                occupiedPlayerPointId = reservation.PointId;
                occupiedRegistrationId = candidateRegistration.RegistrationId;
                reservation.Commit(candidateRegistration.RegistrationId);
                controller.Unpossess();
                if (previousRegistration != null && !previousRegistration.IsDisposed)
                {
                    World.UnregisterActor(previousRegistration);
                }

                controller.Possess(candidate);
                if (World.State == WorldState.Playing)
                {
                    World.ActivateActor(candidateRegistration);
                }

                defaultPawn = candidate;
                pawnRegistration = candidateRegistration;
            }
            catch
            {
                if (ReferenceEquals(controller.PossessedPawn, candidate))
                {
                    controller.Unpossess();
                }

                if (candidateRegistration != null && !candidateRegistration.IsDisposed)
                {
                    candidateRegistration.Disposed -= OnPawnRegistrationDisposed;
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

                ReleasePlayerPoint();

                throw;
            }
        }

        protected override void OnShutdown()
        {
            ReleasePlayerPoint();
            defaultPawn = null;
            pawnRegistration = null;
        }

        private void OnPawnRegistrationDisposed(ActorRegistration registration)
        {
            registration.Disposed -= OnPawnRegistrationDisposed;
            if (ReferenceEquals(pawnRegistration, registration))
            {
                PlayerController?.NotifyPossessedActorUnregistered(registration.Actor);
                ReleasePlayerPoint();
                pawnRegistration = null;
                defaultPawn = null;
            }
        }

        private void ReleasePlayerPoint()
        {
            if (!string.IsNullOrEmpty(occupiedPlayerPointId) && occupiedRegistrationId != Guid.Empty)
            {
                World.LevelRuntime?.ReleaseOccupied(occupiedPlayerPointId, occupiedRegistrationId);
            }
            occupiedPlayerPointId = null;
            occupiedRegistrationId = Guid.Empty;
        }
    }
}
