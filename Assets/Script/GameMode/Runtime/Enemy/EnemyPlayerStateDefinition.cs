using UnityEngine;

namespace CGame
{
    [CreateAssetMenu(fileName = "EnemyPlayerStateDefinition", menuName = "CGame/Gameplay/Enemy Player State Definition")]
    public sealed class EnemyPlayerStateDefinition : ScriptableObject
    {
        [SerializeField] private GameObject pawnPrefab;
        [SerializeField] private float maximumHealth = 100f;

        public GameObject PawnPrefab => pawnPrefab;
        public float MaximumHealth => maximumHealth;

        public void Configure(GameObject prefab, float health = 100f)
        {
            pawnPrefab = prefab ?? throw new System.ArgumentNullException(nameof(prefab));
            maximumHealth = health > 0f ? health : throw new System.ArgumentOutOfRangeException(nameof(health));
        }
    }
}
