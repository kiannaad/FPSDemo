using UnityEngine;

namespace CGame
{
    public abstract class GameModeDefinition : ScriptableObject
    {
        [SerializeField] private PlayerStateDefinition playerStateDefinition;
        [SerializeField] private GameStateDefinition gameStateDefinition;
        [SerializeField] private ExperienceDefinition experienceDefinition;

        public PlayerStateDefinition PlayerStateDefinition => playerStateDefinition;
        public GameStateDefinition GameStateDefinition => gameStateDefinition;
        public ExperienceDefinition ExperienceDefinition => experienceDefinition;

        public void ConfigureAssembly(
            PlayerStateDefinition playerState,
            GameStateDefinition gameState,
            ExperienceDefinition experience)
        {
            playerStateDefinition = playerState ?? throw new System.ArgumentNullException(nameof(playerState));
            gameStateDefinition = gameState ?? throw new System.ArgumentNullException(nameof(gameState));
            experienceDefinition = experience ?? throw new System.ArgumentNullException(nameof(experience));
        }

        public void ValidateRequiredReferences()
        {
            if (playerStateDefinition == null || gameStateDefinition == null || experienceDefinition == null)
            {
                throw new System.InvalidOperationException(
                    "GameModeDefinition requires PlayerStateDefinition, GameStateDefinition and ExperienceDefinition.");
            }

            playerStateDefinition.ValidateRequiredReferences();
            experienceDefinition.ValidateConfiguration();
        }

        public abstract GameMode CreateGameMode(World world, Player player);
    }
}
