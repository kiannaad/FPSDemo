using System;
using CGame.Ability.Attributes;
using CGame.Ability.Cues;
using CGame.Ability.Effects;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Ability.Tests
{
    public sealed class AbilitySystemInitializationTests
    {
        [Test]
        public void Initialize_RegistersSetsThenAppliesInitializationEffects()
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
            var owner = new object();
            var abilitySystem = new AbilitySystemComponent(owner, null, null);

            initialization.Initialize(abilitySystem);

            Assert.That(abilitySystem.Owner, Is.SameAs(owner));
            Assert.That(abilitySystem.GetSet<HealthSet>().Health.CurrentValue, Is.EqualTo(60f));
            Assert.That(abilitySystem.GetSet<HealthSet>().MaxHealth.CurrentValue, Is.EqualTo(60f));
            Assert.That(abilitySystem.GetSet<CombatSet>().BaseDamage.CurrentValue, Is.EqualTo(10f));
        }

        [Test]
        public void Initialize_SecondEffectFails_DisposesAbilitySystemWithoutResidualSets()
        {
            var healthSetDefinition = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            healthSetDefinition.ConfigureForTests(AttributeSetKind.Health);
            var healthEffect = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            healthEffect.name = "InitializeHealth";
            healthEffect.ConfigureForTests(
                GameplayEffectDurationPolicy.Instant,
                GameplayEffectModifierDefinition.Constant(HealthSet.HealthAttribute, GameplayEffectModifierOperation.Override, 60f));
            var missingCombatEffect = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            missingCombatEffect.name = "InitializeMissingCombat";
            missingCombatEffect.ConfigureForTests(
                GameplayEffectDurationPolicy.Instant,
                GameplayEffectModifierDefinition.Constant(CombatSet.BaseDamageAttribute, GameplayEffectModifierOperation.Override, 10f));
            var initialization = ScriptableObject.CreateInstance<AbilitySystemInitializationDefinition>();
            initialization.name = "FailingInitialization";
            initialization.ConfigureForTests(
                new[] { healthSetDefinition },
                new[] { healthEffect, missingCombatEffect });
            var abilitySystem = new AbilitySystemComponent(new object(), null, null);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => initialization.Initialize(abilitySystem));

            StringAssert.Contains("InitializeMissingCombat", exception.Message);
            Assert.That(abilitySystem.IsDisposed, Is.True);
            Assert.That(abilitySystem.GetSet<HealthSet>(), Is.Null);
            Assert.Throws<ObjectDisposedException>(() => abilitySystem.AddAttributeSet(new HealthSet()));
        }
    }
}
