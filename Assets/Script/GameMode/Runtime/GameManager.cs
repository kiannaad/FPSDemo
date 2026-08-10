using System;

namespace CGame
{
    public sealed class GameManager : IWorldSession
    {
        private GameManager(
            GameSessionId id,
            GameStartRequest request,
            GameMode gameMode)
        {
            Id = id;
            Request = request;
            CurrentGameMode = gameMode ?? throw new ArgumentNullException(nameof(gameMode));
            IsActive = true;
        }

        public GameSessionId Id { get; }

        public GameStartRequest Request { get; }

        public GameMode CurrentGameMode { get; private set; }

        public bool IsActive { get; private set; }

        public static GameManager Create(
            World world,
            GameSessionId sessionId,
            GameStartRequest request,
            GameModeDefinition definition)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            PawnData resolvedPawnData = definition.ResolvePawnData(request)
                ?? throw new InvalidOperationException("GameModeDefinition resolved no PawnData.");
            var context = new GameModeCreationContext(world, sessionId, request);
            GameMode gameMode = definition.CreateRuntime(context, resolvedPawnData)
                ?? throw new InvalidOperationException("GameModeDefinition returned no runtime GameMode.");
            gameMode.InitializeInventory(definition.ResolveInitialInventorySet(request));
            return new GameManager(sessionId, request, gameMode);
        }

        public void Shutdown()
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;
            CurrentGameMode?.Shutdown();
            CurrentGameMode = null;
        }
    }
}
