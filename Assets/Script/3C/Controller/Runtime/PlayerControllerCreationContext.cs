using System;

namespace CGame
{
    public sealed class PlayerControllerCreationContext
    {
        public PlayerControllerCreationContext(World world, GameSessionId sessionId, PawnData pawnData)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            if (!sessionId.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(sessionId));
            }

            SessionId = sessionId;
            PawnData = pawnData ?? throw new ArgumentNullException(nameof(pawnData));
        }

        public World World { get; }

        public GameSessionId SessionId { get; }

        public PawnData PawnData { get; }
    }
}
