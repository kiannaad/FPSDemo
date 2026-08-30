using System;

namespace CGame
{
    public sealed class EnemySpawnHandle : IDisposable
    {
        private readonly World world;
        private ActorRegistration controllerRegistration;
        private ActorRegistration pawnRegistration;
        private readonly LevelRuntime levelRuntime;
        private readonly string pointId;
        private readonly Guid registrationId;
        private HealthDeathComponent health;

        internal EnemySpawnHandle(
            World world,
            ActorRegistration controllerRegistration,
            ActorRegistration pawnRegistration,
            LevelRuntime levelRuntime,
            string pointId)
        {
            this.world = world;
            this.controllerRegistration = controllerRegistration;
            this.pawnRegistration = pawnRegistration;
            this.levelRuntime = levelRuntime;
            this.pointId = pointId;
            registrationId = pawnRegistration.RegistrationId;
            Controller = (IdleAIController)controllerRegistration.Actor;
            Pawn = (Pawn)pawnRegistration.Actor;
            health = Pawn.GetComponent<HealthDeathComponent>();
            health.DeathFinished += Dispose;
            pawnRegistration.Disposed += OnPawnUnregistered;
        }

        public IdleAIController Controller { get; }
        public Pawn Pawn { get; }
        public string PointId => pointId;
        public Guid RegistrationId => registrationId;
        public bool IsDisposed => pawnRegistration == null;
        public ActorRegistration PawnRegistration => pawnRegistration;

        public void Dispose()
        {
            ActorRegistration pawn = pawnRegistration;
            ActorRegistration controller = controllerRegistration;
            pawnRegistration = null;
            controllerRegistration = null;
            if (health != null)
            {
                health.DeathFinished -= Dispose;
                health = null;
            }
            if (pawn != null)
            {
                pawn.Disposed -= OnPawnUnregistered;
                if (!pawn.IsDisposed) world.UnregisterActor(pawn);
            }
            if (controller != null && !controller.IsDisposed) world.UnregisterActor(controller);
            levelRuntime.ReleaseOccupied(pointId, registrationId);
        }

        private void OnPawnUnregistered(ActorRegistration registration)
        {
            registration.Disposed -= OnPawnUnregistered;
            Dispose();
        }
    }
}
