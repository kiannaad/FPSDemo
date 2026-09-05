using NUnit.Framework;
using UnityEngine;

namespace CGame.Network.Tests
{
    [Category("Network052")]
    public sealed class DedicatedEnemyCombatStateTests
    {
        [Test]
        public void ApplyDamage_SameShotReplay_DeductsOnceAndPublishesOneDeath()
        {
            var root = new GameObject("Enemy");
            try
            {
                var entity = root.AddComponent<DedicatedEnemyEntity>();
                entity.Initialize(new EnemyRosterEntry(101, "Enemy.Pistol", "EnemyPoint 1"));

                DedicatedDamageResult first = entity.ApplyDamage(1, 7, 60);
                DedicatedDamageResult replay = entity.ApplyDamage(1, 7, 60);
                DedicatedDamageResult lethal = entity.ApplyDamage(1, 8, 60);

                Assert.That(first.Health, Is.EqualTo(40));
                Assert.That(first.IsReplay, Is.False);
                Assert.That(replay.IsReplay, Is.True);
                Assert.That(replay.Health, Is.EqualTo(40));
                Assert.That(lethal.IsDead, Is.True);
                Assert.That(lethal.DiedThisHit, Is.True);
                Assert.That(entity.Health, Is.Zero);
                Assert.That(entity.VitalsRevision, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Resolve_EnemyAndPawnColliders_UsesTypedDedicatedIdentity()
        {
            var enemy = new GameObject("Enemy");
            var pawn = new GameObject("Pawn");
            try
            {
                var registry = new DedicatedCombatIdentityRegistry();
                BoxCollider enemyCollider = enemy.AddComponent<BoxCollider>();
                CapsuleCollider pawnCollider = pawn.AddComponent<CapsuleCollider>();

                registry.RegisterEnemy(101, enemy);
                registry.RegisterPawn(17, pawn);

                Assert.That(registry.Resolve(enemyCollider), Is.EqualTo(new DedicatedCombatIdentity(DedicatedCombatIdentityKind.Enemy, 101)));
                Assert.That(registry.Resolve(pawnCollider), Is.EqualTo(new DedicatedCombatIdentity(DedicatedCombatIdentityKind.Pawn, 17)));
                Assert.That(registry.Resolve(null).Kind, Is.EqualTo(DedicatedCombatIdentityKind.None));
            }
            finally
            {
                Object.DestroyImmediate(enemy);
                Object.DestroyImmediate(pawn);
            }
        }
    }
}
