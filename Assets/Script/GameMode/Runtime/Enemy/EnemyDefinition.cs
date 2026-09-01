using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "EnemyDefinition", menuName = "CGame/Gameplay/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField] private EnemyPlayerStateDefinition playerStateDefinition;

        public EnemyPlayerStateDefinition PlayerStateDefinition => playerStateDefinition;

        public void Configure(EnemyPlayerStateDefinition definition)
        {
            playerStateDefinition = definition ?? throw new System.ArgumentNullException(nameof(definition));
        }
    }
}
