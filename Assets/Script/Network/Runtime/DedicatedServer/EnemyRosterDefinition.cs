using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Network
{
    [CreateAssetMenu(fileName = "EnemyRoster", menuName = "CGame/Network/Dedicated Enemy Roster")]
    public sealed class EnemyRosterDefinition : ScriptableObject
    {
        [SerializeField] private EnemyRosterEntryData[] entries = Array.Empty<EnemyRosterEntryData>();

        public IReadOnlyList<EnemyRosterEntry> Entries
        {
            get
            {
                var values = new EnemyRosterEntry[entries.Length];
                for (int index = 0; index < entries.Length; index++) values[index] = entries[index].ToValue();
                return values;
            }
        }

        public void Configure(params EnemyRosterEntry[] values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            Validate(values);
            entries = new EnemyRosterEntryData[values.Length];
            for (int index = 0; index < values.Length; index++) entries[index] = EnemyRosterEntryData.FromValue(values[index]);
        }

        public void Validate()
        {
            var values = new EnemyRosterEntry[entries.Length];
            for (int index = 0; index < entries.Length; index++) values[index] = entries[index].ToValue();
            Validate(values);
        }

        private static void Validate(IReadOnlyList<EnemyRosterEntry> values)
        {
            if (values.Count != 3) throw new InvalidOperationException("Enemy roster definition requires exactly three entries.");
            var enemyIds = new HashSet<long>();
            var archetypeIds = new HashSet<string>(StringComparer.Ordinal);
            var spawnPointIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Count; index++)
            {
                EnemyRosterEntry entry = values[index];
                if (!enemyIds.Add(entry.EnemyId)) throw new InvalidOperationException($"EnemyId is duplicated: {entry.EnemyId}.");
                if (!archetypeIds.Add(entry.ArchetypeId)) throw new InvalidOperationException($"Enemy ArchetypeId is duplicated: {entry.ArchetypeId}.");
                if (!spawnPointIds.Add(entry.SpawnPointId)) throw new InvalidOperationException($"Enemy SpawnPointId is duplicated: {entry.SpawnPointId}.");
            }
        }

        [Serializable]
        private sealed class EnemyRosterEntryData
        {
            [SerializeField] private long enemyId;
            [SerializeField] private string archetypeId;
            [SerializeField] private string spawnPointId;

            public EnemyRosterEntry ToValue() => new EnemyRosterEntry(enemyId, archetypeId, spawnPointId);

            public static EnemyRosterEntryData FromValue(EnemyRosterEntry value) => new EnemyRosterEntryData
            {
                enemyId = value.EnemyId,
                archetypeId = value.ArchetypeId,
                spawnPointId = value.SpawnPointId
            };
        }
    }
}
