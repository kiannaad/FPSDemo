using System.Collections.Generic;
using CGame.Ability;
using CGame.Ability.Attributes;
using CGame.Ability.Cues;
using CGame.Ability.Effects;
using CGame.Ability.Targeting;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;

namespace CGame.InventoryEquipment.Tests
{
    public sealed class GameplayDamageIntegrationTests
    {
        [Test]
        public void Apply_WallAndCharacterEntriesDispatchInOrderAndOnlyCharacterTakesDamage()
        {
            var trace = new List<string>();
            var router = new RecordingRouter(trace);
            GameplayCueRouter.Register(router);
            var wallRoot = new GameObject("Wall");
            BoxCollider wall = wallRoot.AddComponent<BoxCollider>();
            var targetRoot = new GameObject("Target");
            BoxCollider targetCollider = targetRoot.AddComponent<BoxCollider>();
            var source = new AbilitySystemComponent(new object(), null, null);
            source.AddAttributeSet(new CombatSet(20f));
            var target = new AbilitySystemComponent(new object(), null, null);
            var health = new HealthSet(60f, 60f);
            target.AddAttributeSet(health);
            var resolver = new MappingResolver(targetCollider, target);
            GameplayEffectDefinition damageEffect = CreateDamageEffect(20f);
            try
            {
                var targetData = new GameplayAbilityTargetDataHandle(9UL, new[]
                {
                    Hit(0, wall),
                    Hit(1, targetCollider)
                });

                WeaponDamageApplicationResult result = new WeaponDamageApplication(resolver).Apply(
                    source,
                    damageEffect,
                    new object(),
                    new object(),
                    Vector3.zero,
                    targetData);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        "GameplayCue.Weapon.Fire",
                        "GameplayCue.Weapon.Impact",
                        "GameplayCue.Weapon.Impact",
                        "GameplayCue.Weapon.DamageTaken"
                    },
                    trace);
                Assert.That(result.ImpactCount, Is.EqualTo(2));
                Assert.That(result.AppliedCount, Is.EqualTo(1));
                Assert.That(result.Failures.Count, Is.Zero);
                Assert.That(health.Health.CurrentValue, Is.EqualTo(40f));
                Assert.That(health.Damage.CurrentValue, Is.Zero);
            }
            finally
            {
                GameplayCueRouter.Unregister(router);
                source.Dispose();
                target.Dispose();
                Object.DestroyImmediate(damageEffect);
                Object.DestroyImmediate(wallRoot);
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void Apply_FatalHitDispatchesDamageTakenBeforeOutOfHealth()
        {
            var trace = new List<string>();
            var router = new RecordingRouter(trace);
            GameplayCueRouter.Register(router);
            var targetRoot = new GameObject("Target");
            BoxCollider targetCollider = targetRoot.AddComponent<BoxCollider>();
            var source = new AbilitySystemComponent(new object(), null, null);
            source.AddAttributeSet(new CombatSet(100f));
            var target = new AbilitySystemComponent(new object(), null, null);
            var health = new HealthSet(60f, 60f);
            health.OutOfHealth += () => trace.Add("OutOfHealth");
            target.AddAttributeSet(health);
            GameplayEffectDefinition damageEffect = CreateDamageEffect(100f);
            try
            {
                new WeaponDamageApplication(new MappingResolver(targetCollider, target)).Apply(
                    source,
                    damageEffect,
                    new object(),
                    new object(),
                    Vector3.zero,
                    new GameplayAbilityTargetDataHandle(10UL, new[] { Hit(0, targetCollider) }));

                CollectionAssert.AreEqual(
                    new[]
                    {
                        "GameplayCue.Weapon.Fire",
                        "GameplayCue.Weapon.Impact",
                        "GameplayCue.Weapon.DamageTaken",
                        "OutOfHealth"
                    },
                    trace);
            }
            finally
            {
                GameplayCueRouter.Unregister(router);
                source.Dispose();
                target.Dispose();
                Object.DestroyImmediate(damageEffect);
                Object.DestroyImmediate(targetRoot);
            }
        }

        private static GameplayEffectDefinition CreateDamageEffect(float ignoredMagnitude)
        {
            GameplayTag.TryCreateSerialized("GameplayCue.Weapon.DamageTaken", out GameplayTag damageTaken);
            var definition = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            definition.Configure(
                GameplayEffectDurationPolicy.Instant,
                damageTaken,
                GameplayEffectModifierDefinition.SourceAttribute(
                    HealthSet.DamageAttribute,
                    GameplayEffectModifierOperation.Add,
                    CombatSet.BaseDamageAttribute));
            return definition;
        }

        private static SingleTargetHitData Hit(int traceIndex, Collider collider)
        {
            return new SingleTargetHitData(
                traceIndex,
                new GameplayHitResult(
                    collider.transform.position,
                    Vector3.back,
                    collider,
                    Vector3.zero,
                    collider.transform.position));
        }

        private sealed class MappingResolver : IAbilitySystemTargetResolver
        {
            private readonly Collider collider;
            private readonly AbilitySystemComponent target;

            public MappingResolver(Collider collider, AbilitySystemComponent target)
            {
                this.collider = collider;
                this.target = target;
            }

            public bool TryResolve(Collider candidate, out AbilitySystemComponent abilitySystem)
            {
                abilitySystem = ReferenceEquals(candidate, collider) ? target : null;
                return abilitySystem != null;
            }
        }

        private sealed class RecordingRouter : IGameplayCueRouter
        {
            private readonly List<string> trace;

            public RecordingRouter(List<string> trace)
            {
                this.trace = trace;
            }

            public void Execute(GameplayTag cueTag, GameplayCueParameters parameters)
            {
                trace.Add(cueTag.Name);
            }

            public GameplayCueHandle Add(GameplayTag cueTag, GameplayCueParameters parameters)
            {
                return default;
            }

            public bool Remove(GameplayCueHandle handle)
            {
                return false;
            }

            public void RemoveForTarget(object target)
            {
            }
        }
    }
}
