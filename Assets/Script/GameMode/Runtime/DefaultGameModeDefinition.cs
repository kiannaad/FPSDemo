using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace CGame
{
    [CreateAssetMenu(fileName = "GameModeDefinition", menuName = "CGame/Gameplay/Game Mode")]
    public sealed class DefaultGameModeDefinition : GameModeDefinition
    {
        [FormerlySerializedAs("pawnData")]
        [SerializeField] private PawnDefinition pawnDefinition;
        [SerializeField] private InitialInventorySet initialInventorySet;

        public override GameMode CreateGameMode(World world, Player player)
        {
            if (pawnDefinition == null)
            {
                throw new InvalidOperationException("GameModeDefinition has no PawnData.");
            }

            return new DefaultGameMode(world, player, pawnDefinition, initialInventorySet);
        }
    }
}
