using System;

namespace CGame
{
    public sealed class IdleAIController : Controller
    {
        public IdleAIController(EnemyPlayerState playerState)
        {
            PlayerState = playerState ?? throw new ArgumentNullException(nameof(playerState));
        }

        public EnemyPlayerState PlayerState { get; }
        public Pawn PossessedPawn => PossessedActor as Pawn;

        public void Possess(Pawn pawn)
        {
            if (pawn == null) throw new ArgumentNullException(nameof(pawn));
            AttachPossessedActor(pawn);
            pawn.SettingController(this);
            pawn.ClearingControlIntent();
        }

        protected override void OnShutdown()
        {
            Pawn pawn = PossessedPawn;
            if (pawn != null)
            {
                DetachPossessedActor(pawn);
                pawn.ClearingController(this);
            }
        }

        protected override void OnPossessedActorUnregistered(Actor actor)
        {
            if (actor is Pawn pawn) pawn.ClearingController(this);
        }
    }
}
