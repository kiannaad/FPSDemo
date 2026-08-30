using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "GameStateDefinition", menuName = "CGame/Gameplay/Game State Definition")]
    public sealed class GameStateDefinition : ScriptableObject
    {
        public GameState CreateGameState(World world, ExperienceDefinition experience)
        {
            if (world == null) throw new System.ArgumentNullException(nameof(world));
            if (experience == null) throw new System.ArgumentNullException(nameof(experience));
            return new DefaultGameState(world, experience);
        }
    }
}
