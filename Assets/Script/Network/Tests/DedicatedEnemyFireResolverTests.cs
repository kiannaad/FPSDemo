using NUnit.Framework;
using UnityEngine;

namespace CGame.Network.Tests
{
    [Category("Network059")]
    public sealed class DedicatedEnemyFireResolverTests
    {
        [Test]
        public void FireResolver_OnlyAppliesDamageForTargetHitAndEntersNoAmmoOnceMagazineIsEmpty()
        {
            var definition = new EnemyFireDefinition(2f, 10f, -1f, 10, 2, 15);
            var resolver = new DedicatedEnemyFireResolver(definition);
            var target = new DedicatedPawnCombatState(7, 100, 12, 30, "Default");
            var query = new FakeHitscanQuery(7);

            EnemyFireResolution first = resolver.TryResolve(1, Vector3.zero, Vector3.forward, Vector3.forward * 3f, 7, query);
            DedicatedPawnVitalsResult firstEffect = DedicatedGameplayEffectApplier.ApplyEnemyDamageEffect(target, 101, first.ActionSequence, first.Damage);
            EnemyFireResolution cooldown = resolver.TryResolve(2, Vector3.zero, Vector3.forward, Vector3.forward * 3f, 7, query);
            EnemyFireResolution second = resolver.TryResolve(11, Vector3.zero, Vector3.forward, Vector3.forward * 3f, 7, query);
            DedicatedPawnVitalsResult secondEffect = DedicatedGameplayEffectApplier.ApplyEnemyDamageEffect(target, 101, second.ActionSequence, second.Damage);
            EnemyFireResolution empty = resolver.TryResolve(21, Vector3.zero, Vector3.forward, Vector3.forward * 3f, 7, query);

            Assert.That(first.Fired, Is.True);
            Assert.That(first.HitTarget, Is.True);
            Assert.That(firstEffect.Health, Is.EqualTo(85));
            Assert.That(cooldown.Fired, Is.False);
            Assert.That(second.Fired, Is.True);
            Assert.That(second.EnteredNoAmmo, Is.True);
            Assert.That(secondEffect.Health, Is.EqualTo(70));
            Assert.That(empty.Fired, Is.False);
            Assert.That(target.VitalsRevision, Is.EqualTo(2));
        }

        [Test]
        public void FireResolver_ObstructionOrRangeFailureDoesNotProduceAnEffect()
        {
            var resolver = new DedicatedEnemyFireResolver(new EnemyFireDefinition(2f, 4f, -1f, 1, 3, 20));
            var target = new DedicatedPawnCombatState(7, 100, 12, 30, "Default");

            EnemyFireResolution blocked = resolver.TryResolve(1, Vector3.zero, Vector3.forward, Vector3.forward * 3f, 7, new FakeHitscanQuery(0));
            EnemyFireResolution outOfRange = resolver.TryResolve(2, Vector3.zero, Vector3.forward, Vector3.forward * 6f, 7, new FakeHitscanQuery(7));

            Assert.That(blocked.Fired, Is.True);
            Assert.That(blocked.HitTarget, Is.False);
            Assert.That(blocked.Damage, Is.Zero);
            Assert.That(outOfRange.Fired, Is.False);
            Assert.That(target.Health, Is.EqualTo(100));
            Assert.That(target.VitalsRevision, Is.Zero);
        }

        private sealed class FakeHitscanQuery : IEnemyHitscanQuery
        {
            private readonly long hitPawnId;
            public FakeHitscanQuery(long hitPawnId) => this.hitPawnId = hitPawnId;
            public bool TryHitPawn(Vector3 muzzleOrigin, Vector3 direction, float range, out long pawnId)
            {
                pawnId = hitPawnId;
                return hitPawnId > 0;
            }
        }
    }
}
