using System;

namespace CGame
{
    public class Controller : Actor
    {
        protected Controller(Player player)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public Player Player { get; }

        public float ControlYaw { get; protected set; }

        public float ControlPitch { get; protected set; }

        public Actor PossessedActor { get; private set; }

        protected void AttachPossessedActor(Actor actor)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (PossessedActor != null)
            {
                throw new InvalidOperationException("Controller already possesses an Actor.");
            }

            PossessedActor = actor;
        }

        protected void DetachPossessedActor(Actor expectedActor)
        {
            if (ReferenceEquals(PossessedActor, expectedActor))
            {
                PossessedActor = null;
            }
        }

        public void NotifyPossessedActorUnregistered(Actor actor)
        {
            if (!ReferenceEquals(PossessedActor, actor))
            {
                return;
            }

            PossessedActor = null;
            OnPossessedActorUnregistered(actor);
        }

        protected virtual void OnPossessedActorUnregistered(Actor actor)
        {
        }
    }
}
