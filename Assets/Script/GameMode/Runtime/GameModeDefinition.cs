using UnityEngine;

namespace CGame
{
    public abstract class GameModeDefinition : ScriptableObject
    {
        public abstract GameMode CreateGameMode(World world, Player player);
    }
}
