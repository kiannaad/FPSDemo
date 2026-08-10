using System;
using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "GameModeDefinition", menuName = "CGame/Gameplay/Game Mode")]
    public sealed class DefaultGameModeDefinition : GameModeDefinition
    {
        [SerializeField] private ControllerDefinition localPlayerController;
        [SerializeField] private PawnData pawnData;
        [SerializeField] private InitialInventorySet initialInventorySet;

        public override PawnData ResolvePawnData(GameStartRequest request)
        {
            return pawnData != null
                ? pawnData
                : throw new InvalidOperationException("GameModeDefinition has no PawnData.");
        }

        public override InitialInventorySet ResolveInitialInventorySet(GameStartRequest request)
        {
            return initialInventorySet;
        }

        public override GameMode CreateRuntime(GameModeCreationContext context, PawnData resolvedPawnData)
        {
            if (localPlayerController == null)
            {
                throw new InvalidOperationException("GameModeDefinition has no ControllerDefinition.");
            }

            return new GameMode(context, localPlayerController, resolvedPawnData);
        }
    }
}
