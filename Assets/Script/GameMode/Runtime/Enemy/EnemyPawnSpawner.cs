using System;
using System.Threading.Tasks;

namespace CGame
{
    public class EnemyPawnSpawner
    {
        private readonly EnemyTargetPawnFactory pawnFactory;

        public EnemyPawnSpawner(EnemyTargetPawnFactory pawnFactory = null)
        {
            this.pawnFactory = pawnFactory ?? new EnemyTargetPawnFactory();
        }

        public virtual async Task<EnemySpawnHandle> SpawnAsync(
            World world,
            EnemyDefinition definition,
            SpawnPointReservation reservation)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (definition?.PlayerStateDefinition == null) throw new ArgumentNullException(nameof(definition));
            if (reservation == null || reservation.Kind != SpawnPointKind.Enemy)
                throw new ArgumentException("Enemy reservation is required.", nameof(reservation));

            var playerState = new EnemyPlayerState(definition.PlayerStateDefinition);
            var controller = new IdleAIController(playerState);
            Pawn pawn = null;
            ActorRegistration controllerRegistration = null;
            ActorRegistration pawnRegistration = null;
            try
            {
                pawn = await pawnFactory.CreateAsync(
                    definition.PlayerStateDefinition,
                    reservation.Transform.position,
                    reservation.Transform.rotation);
                controllerRegistration = world.RegisterActor(controller, critical: true);
                pawnRegistration = world.RegisterActor(pawn, critical: true);
                controller.Possess(pawn);
                reservation.Commit(pawnRegistration.RegistrationId);
                if (world.State == WorldState.Playing)
                {
                    world.ActivateActor(controllerRegistration);
                    world.ActivateActor(pawnRegistration);
                }
                return new EnemySpawnHandle(
                    world,
                    controllerRegistration,
                    pawnRegistration,
                    world.LevelRuntime,
                    reservation.PointId);
            }
            catch
            {
                if (pawnRegistration != null && !pawnRegistration.IsDisposed) world.UnregisterActor(pawnRegistration);
                else if (pawn != null && pawn.State == ActorState.Constructed) pawn.DestroyCandidate();
                if (controllerRegistration != null && !controllerRegistration.IsDisposed) world.UnregisterActor(controllerRegistration);
                throw;
            }
        }
    }
}
