using NUnit.Framework;
using UnityEngine;

namespace CGame.Network.Tests
{
    [Category("Network052")]
    public sealed class DedicatedEnemyCombatStateTests
    {
        [Test]
        public void DamageEndpoint_LethalReplaySurvivesEnemyRetirement()
        {
            var root = new GameObject("DamageEndpointReplay");
            try
            {
                var runtime = root.AddComponent<DedicatedServerRuntime>();
                var type = typeof(DedicatedServerRuntime);
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                type.GetProperty("IsPhysicsReady").SetValue(runtime, true);
                type.GetField("launch", flags).SetValue(runtime, new DedicatedServerLaunchConfiguration(
                    1, 1, 2, "test", "Lab", "v1", System.Array.Empty<DedicatedAuthorityPawnConfiguration>()));
                var owners = (System.Collections.Generic.Dictionary<long, AuthorityMoveProcessor>)type.GetField("moveProcessors", flags).GetValue(runtime);
                owners.Add(1, null);
                var results = (System.Collections.Generic.Dictionary<(long, long, long), DedicatedDamageResult>)
                    type.GetField("enemyDamageResults", flags).GetValue(runtime);
                results.Add((101, 1, 7), new DedicatedDamageResult(101, 0, 100, 5, true, true, false));
                // No enemy entity exists: its authority root was already retired.
                var task = (System.Threading.Tasks.Task<DedicatedEnemyDamageResult>)type.GetMethod("QueueEnemyDamageAsync", flags)
                    .Invoke(runtime, new object[] { new DedicatedEnemyDamageRequest
                    { MatchId = 1, EnemyId = 101, CausingPawnId = 1, CausingShotSequence = 7, Damage = 20 } });
                type.GetMethod("ProcessEnemyDamage", flags).Invoke(runtime, null);
                Assert.That(task.IsCompleted, Is.True);
                Assert.That(task.Result.Accepted, Is.True);
                Assert.That(task.Result.IsReplay, Is.True);
                Assert.That(task.Result.IsDead, Is.True);
                Assert.That(task.Result.VitalsRevision, Is.EqualTo(5));
            }
            finally { Object.DestroyImmediate(root); }
        }

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
                Assert.That(first.PresentationActionKind, Is.EqualTo(EnemyActionKind.Hit));
                Assert.That(first.IsReplay, Is.False);
                Assert.That(replay.IsReplay, Is.True);
                Assert.That(replay.Health, Is.EqualTo(40));
                Assert.That(replay.PresentationActionKind, Is.Null);
                Assert.That(lethal.IsDead, Is.True);
                Assert.That(lethal.DiedThisHit, Is.True);
                Assert.That(lethal.PresentationActionKind, Is.EqualTo(EnemyActionKind.Death));
                Assert.That(lethal.WithReplay().PresentationActionKind, Is.Null);
                Assert.That(entity.ApplyDamage(1, 9, 1).PresentationActionKind, Is.Null);
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
