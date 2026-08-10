using System;

namespace CGame
{
    public sealed class GameplaySessionFactory : IGameSessionFactory
    {
        private readonly GameModeDefinition gameModeDefinition;

        public GameplaySessionFactory(GameModeDefinition gameModeDefinition)
        {
            this.gameModeDefinition = gameModeDefinition
                ?? throw new ArgumentNullException(nameof(gameModeDefinition));
        }

        public IWorldSession Create(World world, GameSessionId sessionId, GameStartRequest request)
        {
            return GameManager.Create(world, sessionId, request, gameModeDefinition);
        }
    }
}
