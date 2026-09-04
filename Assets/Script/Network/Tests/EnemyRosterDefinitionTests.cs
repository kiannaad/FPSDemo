using NUnit.Framework;
using UnityEngine;

namespace CGame.Network.Tests
{
    public sealed class EnemyRosterDefinitionTests
    {
        [Test]
        public void Configure_RequiresExactlyThreeUniqueStableEnemyIdsAndSpawnPoints()
        {
            EnemyRosterDefinition definition = ScriptableObject.CreateInstance<EnemyRosterDefinition>();
            try
            {
                definition.Configure(
                    new EnemyRosterEntry(501, "Enemy.Pistol", "EnemyPoint 1"),
                    new EnemyRosterEntry(502, "Enemy.Rifle", "EnemyPoint 2"),
                    new EnemyRosterEntry(503, "Enemy.Ak", "EnemyPoint 3"));

                Assert.That(definition.Entries.Count, Is.EqualTo(3));
                Assert.That(definition.Entries[2].ArchetypeId, Is.EqualTo("Enemy.Ak"));
                Assert.Throws<System.InvalidOperationException>(() => definition.Configure(
                    new EnemyRosterEntry(501, "Enemy.Pistol", "EnemyPoint 1"),
                    new EnemyRosterEntry(501, "Enemy.Rifle", "EnemyPoint 2"),
                    new EnemyRosterEntry(503, "Enemy.Ak", "EnemyPoint 3")));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }
    }
}
