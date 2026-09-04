using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Network
{
    [CreateAssetMenu(fileName = "EnemyArchetypeCombatCatalog", menuName = "CGame/Network/Enemy Archetype Combat Catalog")]
    public sealed class EnemyArchetypeCombatCatalog : ScriptableObject
    {
        [SerializeField] private EnemyArchetypeCombatDefinition[] definitions = Array.Empty<EnemyArchetypeCombatDefinition>();

        public IReadOnlyList<EnemyArchetypeCombatDefinition> Definitions => definitions;

        public void Configure(params EnemyArchetypeCombatDefinition[] values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            ValidateValues(values);
            definitions = (EnemyArchetypeCombatDefinition[])values.Clone();
        }

        public EnemyArchetypeCombatDefinition GetRequired(string archetypeId)
        {
            ValidateValues(definitions);
            for (int index = 0; index < definitions.Length; index++)
            {
                EnemyArchetypeCombatDefinition definition = definitions[index];
                if (string.Equals(definition.ArchetypeId, archetypeId, StringComparison.Ordinal)) return definition;
            }

            throw new InvalidOperationException($"Enemy archetype combat definition is missing: {archetypeId}.");
        }

        public void Validate() => ValidateValues(definitions);

        private static void ValidateValues(IReadOnlyList<EnemyArchetypeCombatDefinition> values)
        {
            if (values == null || values.Count != 3)
                throw new InvalidOperationException("Enemy archetype combat catalog requires exactly three definitions.");

            var archetypeIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Count; index++)
            {
                EnemyArchetypeCombatDefinition definition = values[index];
                if (definition == null) throw new InvalidOperationException("Enemy archetype combat catalog contains a null definition.");
                definition.Validate();
                if (!archetypeIds.Add(definition.ArchetypeId))
                    throw new InvalidOperationException($"Enemy archetype combat definition is duplicated: {definition.ArchetypeId}.");
            }
        }
    }
}
