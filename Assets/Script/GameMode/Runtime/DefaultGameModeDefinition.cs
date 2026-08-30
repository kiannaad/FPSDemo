using System;
using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "GameModeDefinition", menuName = "CGame/Gameplay/Game Mode")]
    public sealed class DefaultGameModeDefinition : GameModeDefinition
    {
        public override GameMode CreateGameMode(World world, Player player)
        {
            ValidateRequiredReferences();
            return new DefaultGameMode(world, player, PlayerStateDefinition);
        }
    }
}
