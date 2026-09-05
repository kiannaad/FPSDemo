using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Network
{
    [CreateAssetMenu(fileName = "EnemyPresentationCatalog", menuName = "CGame/Network/Enemy Presentation Catalog")]
    public sealed class EnemyPresentationCatalog : ScriptableObject
    {
        [SerializeField] private EnemyArchetypeSpec[] archetypes = Array.Empty<EnemyArchetypeSpec>();

        public IReadOnlyList<EnemyArchetypeSpec> Archetypes => archetypes;

        public void Configure(params EnemyArchetypeSpec[] values)
        {
            archetypes = values ?? throw new ArgumentNullException(nameof(values));
        }

        public bool TryGet(string archetypeId, out EnemyArchetypeSpec archetype)
        {
            for (int index = 0; index < archetypes.Length; index++)
            {
                EnemyArchetypeSpec candidate = archetypes[index];
                if (candidate != null && string.Equals(candidate.ArchetypeId, archetypeId, StringComparison.Ordinal))
                {
                    archetype = candidate;
                    return true;
                }
            }

            archetype = null;
            return false;
        }
    }
}
