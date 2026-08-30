using CGame.Ability.Attributes;
using CGame.Ability.Effects;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;

namespace CGame.InventoryEquipment.Tests
{
    public sealed class WeaponDamageEffectDefinitionTests
    {
        [Test]
        public void ConfigureDamageEffect_ExposesSourceBaseDamageToTargetDamageContract()
        {
            Assert.That(GameplayTag.TryCreateSerialized("Weapon.Knife", out GameplayTag weaponTag), Is.True);
            Assert.That(GameplayTag.TryCreateSerialized("GameplayCue.Weapon.DamageTaken", out GameplayTag cueTag), Is.True);
            WeaponDefinition definition = WeaponDefinition.CreateRuntime(weaponTag, 30, new CGame.Ability.AbilitySet());
            var effect = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            effect.Configure(
                GameplayEffectDurationPolicy.Instant,
                cueTag,
                GameplayEffectModifierDefinition.SourceAttribute(
                    HealthSet.DamageAttribute,
                    GameplayEffectModifierOperation.Add,
                    CombatSet.BaseDamageAttribute));

            definition.ConfigureDamageEffect(effect);

            Assert.That(definition.DamageEffect, Is.SameAs(effect));
            Assert.That(effect.DurationPolicy, Is.EqualTo(GameplayEffectDurationPolicy.Instant));
            Assert.That(effect.ExecutedCueTag.Name, Is.EqualTo("GameplayCue.Weapon.DamageTaken"));
            Assert.That(effect.Modifiers.Count, Is.EqualTo(1));
            Assert.That(effect.Modifiers[0].TargetAttribute, Is.SameAs(HealthSet.DamageAttribute));
            Assert.That(effect.Modifiers[0].MagnitudeSource, Is.EqualTo(GameplayEffectMagnitudeSource.SourceAttribute));
            Assert.That(effect.Modifiers[0].SourceAttributeValue, Is.SameAs(CombatSet.BaseDamageAttribute));

            Object.DestroyImmediate(effect);
            Object.DestroyImmediate(definition);
        }
    }
}
