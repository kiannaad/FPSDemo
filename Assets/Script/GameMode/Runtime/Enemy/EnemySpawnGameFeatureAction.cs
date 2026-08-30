using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "EnemySpawnGameFeatureAction", menuName = "CGame/Gameplay/Enemy Spawn Feature Action")]
    public sealed class EnemySpawnGameFeatureAction : GameFeatureAction
    {
        [SerializeField] private EnemyDefinition enemyDefinition;
        [SerializeField] private int initialSpawnCount = 3;

        public void Configure(EnemyDefinition definition, int count)
        {
            enemyDefinition = definition ?? throw new System.ArgumentNullException(nameof(definition));
            initialSpawnCount = count > 0 ? count : throw new System.ArgumentOutOfRangeException(nameof(count));
        }

        public override GameFeatureActivationReceipt Activate(GameFeatureActivationContext context)
        {
            if (!(context.Owner is ExperienceManagerComponent manager))
                throw new System.InvalidOperationException("Enemy spawn action requires ExperienceManagerComponent.");
            return context.InstallComponent(
                new EnemySpawnGameComponent(manager.World, enemyDefinition, initialSpawnCount));
        }
    }
}
