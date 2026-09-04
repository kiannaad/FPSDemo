using System;
using UnityEngine;

namespace CGame.Network
{
    [CreateAssetMenu(fileName = "EnemyArchetypeSpec", menuName = "CGame/Network/Enemy Archetype Spec")]
    public sealed class EnemyArchetypeSpec : ScriptableObject
    {
        [SerializeField] private string archetypeId;
        [SerializeField] private GameObject presentationPrefab;

        public string ArchetypeId => archetypeId;
        public GameObject PresentationPrefab => presentationPrefab;

        public void Configure(string id, GameObject prefab)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Archetype ID is required.", nameof(id));
            archetypeId = id;
            presentationPrefab = prefab ?? throw new ArgumentNullException(nameof(prefab));
        }
    }
}
