using CGame.Ability;
using CGame.Ability.Attributes;
using CGame.Ability.Effects;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Tests.Gameplay
{
    public sealed class AbilitySystemHealthOwnershipTests
    {
        [Test]
        public void EnemyPlayerState_OwnsInitializedAbilitySystem()
        {
            var healthSetDefinition = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            healthSetDefinition.ConfigureForTests(AttributeSetKind.Health);
            var combatSetDefinition = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            combatSetDefinition.ConfigureForTests(AttributeSetKind.Combat);
            var initializationEffect = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            initializationEffect.ConfigureForTests(
                GameplayEffectDurationPolicy.Instant,
                GameplayEffectModifierDefinition.Constant(HealthSet.MaxHealthAttribute, GameplayEffectModifierOperation.Override, 60f),
                GameplayEffectModifierDefinition.Constant(HealthSet.HealthAttribute, GameplayEffectModifierOperation.Override, 60f),
                GameplayEffectModifierDefinition.Constant(CombatSet.BaseDamageAttribute, GameplayEffectModifierOperation.Override, 10f));
            var initialization = ScriptableObject.CreateInstance<AbilitySystemInitializationDefinition>();
            initialization.ConfigureForTests(
                new[] { healthSetDefinition, combatSetDefinition },
                new[] { initializationEffect });
            var definition = ScriptableObject.CreateInstance<EnemyPlayerStateDefinition>();
            definition.Configure(new GameObject("EnemyPrefab"), initialization);

            var playerState = new EnemyPlayerState(definition);

            Assert.That(playerState.AbilitySystem.Owner, Is.SameAs(playerState));
            Assert.That(playerState.AbilitySystem.Avatar, Is.Null);
            Assert.That(playerState.AbilitySystem.GetSet<HealthSet>().Health.CurrentValue, Is.EqualTo(60f));
            Assert.That(playerState.AbilitySystem.GetSet<CombatSet>().BaseDamage.CurrentValue, Is.EqualTo(10f));
        }

        [Test]
        public void HealthComponent_ReflectsHealthSetAndPublishesDeathOnce()
        {
            var source = new AbilitySystemComponent(new object(), null);
            var target = new AbilitySystemComponent(new object(), null);
            target.AddAttributeSet(new HealthSet(60f, 60f));
            var healthComponent = new HealthComponent();
            healthComponent.Bind(target);
            int deathStartedCount = 0;
            int deathFinishedCount = 0;
            healthComponent.DeathStarted += () => deathStartedCount++;
            healthComponent.DeathFinished += () => deathFinishedCount++;
            var damageEffect = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            damageEffect.ConfigureForTests(
                GameplayEffectDurationPolicy.Instant,
                GameplayEffectModifierDefinition.Constant(
                    HealthSet.DamageAttribute,
                    GameplayEffectModifierOperation.Add,
                    100f));
            GameplayEffectSpec spec = source.MakeOutgoingSpec(
                damageEffect,
                1f,
                new GameplayEffectContext(source.Avatar, source.Avatar, damageEffect));

            Assert.That(source.ApplyGameplayEffectSpecToTarget(spec, target).Succeeded, Is.True);
            Assert.That(source.ApplyGameplayEffectSpecToTarget(spec, target).Succeeded, Is.True);

            Assert.That(healthComponent.Health, Is.Zero);
            Assert.That(healthComponent.MaxHealth, Is.EqualTo(60f));
            Assert.That(healthComponent.IsDead, Is.True);
            Assert.That(deathStartedCount, Is.EqualTo(1));
            Assert.That(deathFinishedCount, Is.EqualTo(1));
        }
    }
}
