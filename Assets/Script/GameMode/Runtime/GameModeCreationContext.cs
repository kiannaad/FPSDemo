using System;

namespace CGame
{
    public sealed class GameModeCreationContext
    {
        public GameModeCreationContext(World world, GameSessionId sessionId, GameStartRequest request)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            if (!sessionId.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(sessionId));
            }

            SessionId = sessionId;
            Request = request;
        }

        public World World { get; }

        public GameSessionId SessionId { get; }

        public GameStartRequest Request { get; }
    }
}
