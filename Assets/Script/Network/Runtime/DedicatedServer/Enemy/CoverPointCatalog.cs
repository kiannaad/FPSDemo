using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CGame.Network
{
    [CreateAssetMenu(fileName = "CoverPointCatalog", menuName = "CGame/Network/Dedicated Cover Point Catalog")]
    public sealed class CoverPointCatalog : ScriptableObject
    {
        [SerializeField] private List<CoverPointDefinition> definitions = new List<CoverPointDefinition>();

        public IReadOnlyList<CoverPointDefinition> Definitions => definitions;

        public void Configure(params CoverPointDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0) throw new InvalidOperationException("Cover point catalog requires at least one definition.");
            if (definitions.Any(definition => definition == null)) throw new InvalidOperationException("Cover point catalog cannot contain a null definition.");
            if (definitions.GroupBy(definition => definition.CoverPointId, StringComparer.Ordinal).Any(group => group.Count() > 1))
                throw new InvalidOperationException("Cover point catalog requires unique CoverPointId values.");
            this.definitions = new List<CoverPointDefinition>(definitions);
        }

        public IReadOnlyList<CoverPointDefinition> ValidateForLevel(string levelId, IEnemyNavPathQuery navigation)
        {
            if (string.IsNullOrWhiteSpace(levelId)) throw new ArgumentException("LevelId is required.", nameof(levelId));
            if (navigation == null) throw new ArgumentNullException(nameof(navigation));
            if (definitions == null || definitions.Count == 0) throw new InvalidOperationException("Cover point catalog requires at least one definition.");

            var accepted = new List<CoverPointDefinition>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CoverPointDefinition definition in definitions)
            {
                if (definition == null) throw new InvalidOperationException("Cover point catalog cannot contain a null definition.");
                if (!seenIds.Add(definition.CoverPointId)) throw new InvalidOperationException($"Cover point catalog has duplicate CoverPointId: {definition.CoverPointId}.");
                if (!string.Equals(definition.LevelId, levelId, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Cover point {definition.CoverPointId} belongs to level {definition.LevelId}, not {levelId}.");
                definition.ValidateStatic(navigation);
                accepted.Add(definition);
            }

            return accepted;
        }
    }
}
