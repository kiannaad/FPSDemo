using System;
using CGame.Ability.Attributes;
using CGame.Ability.Cues;
using CGame.Ability.Effects;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;

namespace CGame.Ability.Tests
{
    public sealed class GameplayEffectAttributeTests
    {
        [Test]
        public void AddAttributeSet_DuplicateConcreteType_Throws()
        {
            var abilitySystem = new AbilitySystemComponent(new object(), null);
            abilitySystem.AddAttributeSet(new HealthSet());

            Assert.Throws<InvalidOperationException>(() => abilitySystem.AddAttributeSet(new HealthSet()));
        }

        [Test]
        public void GetSet_ReturnsRegisteredInstance()
        {
            var abilitySystem = new AbilitySystemComponent(new object(), null);
            var healthSet = new HealthSet();
            abilitySystem.AddAttributeSet(healthSet);

            Assert.That(abilitySystem.GetSet<HealthSet>(), Is.SameAs(healthSet));
        }

        [Test]
        public void GetSet_Missing_ReturnsNull()
        {
            var abilitySystem = new AbilitySystemComponent(new object(), null);

            Assert.That(abilitySystem.GetSet<HealthSet>(), Is.Null);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void GameplayAttributeData_NonFiniteInitialValue_Throws(float value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameplayAttributeData(value));
        }

        [Test]
        public void GameplayAttribute_RejectsDifferentSetType()
        {
            Assert.Throws<ArgumentException>(() => HealthSet.HealthAttribute.GetData(new CombatSet()));
        }

        [Test]
        public void ApplyInstantConstantAdd_ChangesTargetAttribute()
        {
            var source = new AbilitySystemComponent(new object(), null);
            var target = new AbilitySystemComponent(new object(), null);
            var healthSet = new HealthSet(60f, 100f);
            target.AddAttributeSet(healthSet);
            var definition = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            definition.ConfigureForTests(
                GameplayEffectDurationPolicy.Instant,
                GameplayEffectModifierDefinition.Constant(
                    HealthSet.HealthAttribute,
                    GameplayEffectModifierOperation.Add,
                    10f));

            GameplayEffectSpec spec = source.MakeOutgoingSpec(
                definition,
                1f,
                new GameplayEffectContext(source.Avatar, source.Avatar, definition));
            GameplayEffectApplyResult result = source.ApplyGameplayEffectSpecToTarget(spec, target);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(healthSet.Health.CurrentValue, Is.EqualTo(70f));
        }

        [Test]
        public void ApplyInstantOverride_ReplacesTargetAttribute()
        {
            var source = new AbilitySystemComponent(new object(), null);
            var target = new AbilitySystemComponent(new object(), null);
            var healthSet = new HealthSet(60f, 100f);
            target.AddAttributeSet(healthSet);
            var definition = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            definition.ConfigureForTests(
                GameplayEffectDurationPolicy.Instant,
                GameplayEffectModifierDefinition.Constant(
                    HealthSet.HealthAttribute,
                    GameplayEffectModifierOperation.Override,
                    25f));

            GameplayEffectSpec spec = source.MakeOutgoingSpec(
                definition,
                1f,
                new GameplayEffectContext(source.Avatar, source.Avatar, definition));
            GameplayEffectApplyResult result = source.ApplyGameplayEffectSpecToTarget(spec, target);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(healthSet.Health.CurrentValue, Is.EqualTo(25f));
        }

        [Test]
        public void SourceAttributeMagnitude_IsCapturedWhenSpecIsCreated()
        {
            var source = new AbilitySystemComponent(new object(), null);
            var sourceCombatSet = new CombatSet(20f);
            source.AddAttributeSet(sourceCombatSet);
            var target = new AbilitySystemComponent(new object(), null);
            var targetHealthSet = new HealthSet(60f, 100f);
            target.AddAttributeSet(targetHealthSet);
            var damageDefinition = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            damageDefinition.ConfigureForTests(
                GameplayEffectDurationPolicy.Instant,
                GameplayEffectModifierDefinition.SourceAttribute(
                    HealthSet.HealthAttribute,
                    GameplayEffectModifierOperation.Add,
                    CombatSet.BaseDamageAttribute));

            GameplayEffectSpec capturedSpec = source.MakeOutgoingSpec(
                damageDefinition,
                1f,
                new GameplayEffectContext(source.Avatar, source.Avatar, damageDefinition));

            var sourceMutationDefinition = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            sourceMutationDefinition.ConfigureForTests(
                GameplayEffectDurationPolicy.Instant,
                GameplayEffectModifierDefinition.Constant(
                    CombatSet.BaseDamageAttribute,
                    GameplayEffectModifierOperation.Override,
                    50f));
            GameplayEffectSpec sourceMutationSpec = source.MakeOutgoingSpec(
                sourceMutationDefinition,
                1f,
                new GameplayEffectContext(source.Avatar, source.Avatar, sourceMutationDefinition));
            Assert.That(source.ApplyGameplayEffectSpecToSelf(sourceMutationSpec).Succeeded, Is.True);

            GameplayEffectApplyResult result = source.ApplyGameplayEffectSpecToTarget(capturedSpec, target);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(sourceCombatSet.BaseDamage.CurrentValue, Is.EqualTo(50f));
            Assert.That(targetHealthSet.Health.CurrentValue, Is.EqualTo(80f));
        }

        [Test]
        public void MakeOutgoingSpec_NonInstant_ReturnsUnsupportedDuration()
        {
            var source = new AbilitySystemComponent(new object(), null);
            var definition = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            definition.ConfigureForTests(GameplayEffectDurationPolicy.Duration);

            GameplayEffectSpec spec = source.MakeOutgoingSpec(
                definition,
                1f,
                new GameplayEffectContext(source.Avatar, source.Avatar, definition));

            Assert.That(spec.IsValid, Is.False);
            Assert.That(spec.FailureReason, Is.EqualTo(GameplayEffectFailureReason.UnsupportedDuration));
        }

        [Test]
        public void ApplySpec_MissingThirdModifierSet_HasZeroSideEffects()
        {
            var source = new AbilitySystemComponent(new object(), null);
            var target = new AbilitySystemComponent(new object(), null);
            var healthSet = new HealthSet(60f, 100f);
            target.AddAttributeSet(healthSet);
            var definition = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            definition.ConfigureForTests(
                GameplayEffectDurationPolicy.Instant,
                GameplayEffectModifierDefinition.Constant(HealthSet.HealthAttribute, GameplayEffectModifierOperation.Add, 10f),
                GameplayEffectModifierDefinition.Constant(HealthSet.MaxHealthAttribute, GameplayEffectModifierOperation.Add, 10f),
                GameplayEffectModifierDefinition.Constant(CombatSet.BaseDamageAttribute, GameplayEffectModifierOperation.Add, 1f));
            GameplayEffectSpec spec = source.MakeOutgoingSpec(
                definition,
                1f,
                new GameplayEffectContext(source.Avatar, source.Avatar, definition));

            GameplayEffectApplyResult result = source.ApplyGameplayEffectSpecToTarget(spec, target);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(GameplayEffectFailureReason.MissingTargetAttributeSet));
            Assert.That(healthSet.Health.CurrentValue, Is.EqualTo(60f));
            Assert.That(healthSet.MaxHealth.CurrentValue, Is.EqualTo(100f));
        }

        [Test]
        public void ApplySpec_PostExecuteThrows_RestoresEveryAttribute()
        {
            var source = new AbilitySystemComponent(new object(), null);
            var target = new AbilitySystemComponent(new object(), null);
            var healthSet = new HealthSet(60f, 100f);
            var throwingSet = new ThrowingAttributeSet(5f);
            target.AddAttributeSet(healthSet);
            target.AddAttributeSet(throwingSet);
            int healthChangedCount = 0;
            healthSet.HealthChanged += (oldHealth, newHealth) => healthChangedCount++;
            var definition = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            definition.ConfigureForTests(
                GameplayEffectDurationPolicy.Instant,
                GameplayEffectModifierDefinition.Constant(HealthSet.DamageAttribute, GameplayEffectModifierOperation.Add, 20f),
                GameplayEffectModifierDefinition.Constant(ThrowingAttributeSet.ValueAttribute, GameplayEffectModifierOperation.Add, 1f));
            GameplayEffectSpec spec = source.MakeOutgoingSpec(
                definition,
                1f,
                new GameplayEffectContext(source.Avatar, source.Avatar, definition));

            GameplayEffectApplyResult result = source.ApplyGameplayEffectSpecToTarget(spec, target);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(GameplayEffectFailureReason.ExecutionFailed));
            Assert.That(healthSet.Health.CurrentValue, Is.EqualTo(60f));
            Assert.That(healthSet.Damage.CurrentValue, Is.Zero);
            Assert.That(throwingSet.Value.CurrentValue, Is.EqualTo(5f));
            Assert.That(healthChangedCount, Is.Zero);
        }

        [Test]
        public void DamageMetaAttribute_ReducesHealthAndResetsDamage()
        {
            var source = new AbilitySystemComponent(new object(), null);
            var target = new AbilitySystemComponent(new object(), null);
            var healthSet = new HealthSet(60f, 100f);
            target.AddAttributeSet(healthSet);
            int healthChangedCount = 0;
            float observedOldHealth = 0f;
            float observedNewHealth = 0f;
            healthSet.HealthChanged += (oldHealth, newHealth) =>
            {
                healthChangedCount++;
                observedOldHealth = oldHealth;
                observedNewHealth = newHealth;
            };
            var definition = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            definition.ConfigureForTests(
                GameplayEffectDurationPolicy.Instant,
                GameplayEffectModifierDefinition.Constant(
                    HealthSet.DamageAttribute,
                    GameplayEffectModifierOperation.Add,
                    20f));
            GameplayEffectSpec spec = source.MakeOutgoingSpec(
                definition,
                1f,
                new GameplayEffectContext(source.Avatar, source.Avatar, definition));

            GameplayEffectApplyResult result = source.ApplyGameplayEffectSpecToTarget(spec, target);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(healthSet.Health.CurrentValue, Is.EqualTo(40f));
            Assert.That(healthSet.Damage.CurrentValue, Is.Zero);
            Assert.That(healthChangedCount, Is.EqualTo(1));
            Assert.That(observedOldHealth, Is.EqualTo(60f));
            Assert.That(observedNewHealth, Is.EqualTo(40f));
        }

        [Test]
        public void DamageMetaAttribute_OutOfHealthFiresOnlyOnFirstTransitionToZero()
        {
            var source = new AbilitySystemComponent(new object(), null);
            var target = new AbilitySystemComponent(new object(), null);
            var healthSet = new HealthSet(60f, 100f);
            target.AddAttributeSet(healthSet);
            int outOfHealthCount = 0;
            healthSet.OutOfHealth += () => outOfHealthCount++;
            var definition = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            definition.ConfigureForTests(
                GameplayEffectDurationPolicy.Instant,
                GameplayEffectModifierDefinition.Constant(
                    HealthSet.DamageAttribute,
                    GameplayEffectModifierOperation.Add,
                    100f));
            GameplayEffectSpec spec = source.MakeOutgoingSpec(
                definition,
                1f,
                new GameplayEffectContext(source.Avatar, source.Avatar, definition));

            Assert.That(source.ApplyGameplayEffectSpecToTarget(spec, target).Succeeded, Is.True);
            Assert.That(source.ApplyGameplayEffectSpecToTarget(spec, target).Succeeded, Is.True);

            Assert.That(healthSet.Health.CurrentValue, Is.Zero);
            Assert.That(healthSet.Damage.CurrentValue, Is.Zero);
            Assert.That(outOfHealthCount, Is.EqualTo(1));
        }

        [Test]
        public void DamageMetaAttribute_NegativeDamageDoesNotHeal()
        {
            var source = new AbilitySystemComponent(new object(), null);
            var target = new AbilitySystemComponent(new object(), null);
            var healthSet = new HealthSet(60f, 100f);
            target.AddAttributeSet(healthSet);
            var definition = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            definition.ConfigureForTests(
                GameplayEffectDurationPolicy.Instant,
                GameplayEffectModifierDefinition.Constant(
                    HealthSet.DamageAttribute,
                    GameplayEffectModifierOperation.Add,
                    -20f));
            GameplayEffectSpec spec = source.MakeOutgoingSpec(
                definition,
                1f,
                new GameplayEffectContext(source.Avatar, source.Avatar, definition));

            GameplayEffectApplyResult result = source.ApplyGameplayEffectSpecToTarget(spec, target);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(healthSet.Health.CurrentValue, Is.EqualTo(60f));
            Assert.That(healthSet.Damage.CurrentValue, Is.Zero);
        }

        [Test]
        public void ApplySpec_SuccessExecutesConfiguredCueAfterCommit()
        {
            Assert.That(GameplayTag.TryCreateSerialized("GameplayCue.Character.DamageTaken", out GameplayTag cueTag), Is.True);
            var router = new RecordingCueRouter();
            GameplayCueRouter.Register(router);
            try
            {
                var source = new AbilitySystemComponent(new object(), null);
                var targetAvatar = new object();
                var target = new AbilitySystemComponent(targetAvatar, null);
                var healthSet = new HealthSet(60f, 100f);
                target.AddAttributeSet(healthSet);
                var definition = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
                definition.ConfigureForTests(
                    GameplayEffectDurationPolicy.Instant,
                    cueTag,
                    GameplayEffectModifierDefinition.Constant(
                        HealthSet.DamageAttribute,
                        GameplayEffectModifierOperation.Add,
                        20f));
                GameplayEffectSpec spec = source.MakeOutgoingSpec(
                    definition,
                    1f,
                    new GameplayEffectContext(source.Avatar, source.Avatar, definition));

                GameplayEffectApplyResult result = source.ApplyGameplayEffectSpecToTarget(spec, target);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(router.ExecuteCount, Is.EqualTo(1));
                Assert.That(router.LastTag, Is.EqualTo(cueTag));
                Assert.That(router.LastParameters.Target, Is.SameAs(targetAvatar));
                Assert.That(healthSet.Health.CurrentValue, Is.EqualTo(40f));
            }
            finally
            {
                GameplayCueRouter.Unregister(router);
            }
        }

        [Test]
        public void ApplySpec_PostExecuteThrows_DoesNotExecuteCue()
        {
            Assert.That(GameplayTag.TryCreateSerialized("GameplayCue.Character.DamageTaken", out GameplayTag cueTag), Is.True);
            var router = new RecordingCueRouter();
            GameplayCueRouter.Register(router);
            try
            {
                var source = new AbilitySystemComponent(new object(), null);
                var target = new AbilitySystemComponent(new object(), null);
                target.AddAttributeSet(new ThrowingAttributeSet(5f));
                var definition = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
                definition.ConfigureForTests(
                    GameplayEffectDurationPolicy.Instant,
                    cueTag,
                    GameplayEffectModifierDefinition.Constant(
                        ThrowingAttributeSet.ValueAttribute,
                        GameplayEffectModifierOperation.Add,
                        1f));
                GameplayEffectSpec spec = source.MakeOutgoingSpec(
                    definition,
                    1f,
                    new GameplayEffectContext(source.Avatar, source.Avatar, definition));

                GameplayEffectApplyResult result = source.ApplyGameplayEffectSpecToTarget(spec, target);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(router.ExecuteCount, Is.Zero);
            }
            finally
            {
                GameplayCueRouter.Unregister(router);
            }
        }

        [Test]
        public void CloneWithContext_PreservesCapturedMagnitudesAndReplacesContext()
        {
            var source = new AbilitySystemComponent(new object(), null);
            source.AddAttributeSet(new CombatSet(20f));
            var target = new AbilitySystemComponent(new object(), null);
            var healthSet = new HealthSet(60f, 100f);
            target.AddAttributeSet(healthSet);
            var definition = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            definition.ConfigureForTests(
                GameplayEffectDurationPolicy.Instant,
                GameplayEffectModifierDefinition.SourceAttribute(
                    HealthSet.HealthAttribute,
                    GameplayEffectModifierOperation.Add,
                    CombatSet.BaseDamageAttribute));
            GameplayEffectSpec original = source.MakeOutgoingSpec(
                definition,
                1f,
                new GameplayEffectContext(source.Avatar, source.Avatar, definition));
            var replacementContext = new GameplayEffectContext(
                source.Avatar,
                source.Avatar,
                definition,
                ability: new object(),
                hitResult: null,
                origin: Vector3.one);

            GameplayEffectSpec clone = original.CloneWithContext(replacementContext);
            GameplayEffectApplyResult result = source.ApplyGameplayEffectSpecToTarget(clone, target);

            Assert.That(clone.Context, Is.SameAs(replacementContext));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(healthSet.Health.CurrentValue, Is.EqualTo(80f));
        }

        [Test]
        public void GameplayEffectDefinition_UnitySerialization_PreservesModifierContract()
        {
            Assert.That(GameplayTag.TryCreateSerialized("GameplayCue.Character.DamageTaken", out GameplayTag cueTag), Is.True);
            var definition = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            definition.ConfigureForTests(
                GameplayEffectDurationPolicy.Instant,
                cueTag,
                GameplayEffectModifierDefinition.SourceAttribute(
                    HealthSet.DamageAttribute,
                    GameplayEffectModifierOperation.Override,
                    CombatSet.BaseDamageAttribute));
            string json = JsonUtility.ToJson(definition);
            var clone = ScriptableObject.CreateInstance<GameplayEffectDefinition>();

            JsonUtility.FromJsonOverwrite(json, clone);

            Assert.That(clone.DurationPolicy, Is.EqualTo(GameplayEffectDurationPolicy.Instant));
            Assert.That(clone.ExecutedCueTag, Is.EqualTo(cueTag));
            Assert.That(clone.Modifiers.Count, Is.EqualTo(1));
            Assert.That(clone.Modifiers[0].TargetAttribute, Is.SameAs(HealthSet.DamageAttribute));
            Assert.That(clone.Modifiers[0].Operation, Is.EqualTo(GameplayEffectModifierOperation.Override));
            Assert.That(clone.Modifiers[0].MagnitudeSource, Is.EqualTo(GameplayEffectMagnitudeSource.SourceAttribute));
            Assert.That(clone.Modifiers[0].SourceAttributeValue, Is.SameAs(CombatSet.BaseDamageAttribute));
        }

        [Test]
        public void SetAvatar_ChangesAvatarWithoutChangingOwner()
        {
            var owner = new object();
            var firstAvatar = new object();
            var secondAvatar = new object();
            var abilitySystem = new AbilitySystemComponent(owner, firstAvatar, null);

            Assert.That(abilitySystem.SetAvatar(secondAvatar), Is.True);

            Assert.That(abilitySystem.Owner, Is.SameAs(owner));
            Assert.That(abilitySystem.Avatar, Is.SameAs(secondAvatar));
        }

        private sealed class ThrowingAttributeSet : AttributeSet
        {
            public static GameplayAttribute ValueAttribute { get; } =
                GameplayAttribute.Create<ThrowingAttributeSet>(nameof(Value), set => set.Value);

            public ThrowingAttributeSet(float value)
            {
                Value = new GameplayAttributeData(value);
            }

            public GameplayAttributeData Value { get; }

            protected override void PostGameplayEffectExecute(
                GameplayAttribute attribute,
                GameplayEffectContext context,
                IGameplayEffectExecution execution)
            {
                throw new InvalidOperationException("Expected test failure.");
            }
        }

        private sealed class RecordingCueRouter : IGameplayCueRouter
        {
            public int ExecuteCount { get; private set; }
            public GameplayTag LastTag { get; private set; }
            public GameplayCueParameters LastParameters { get; private set; }

            public void Execute(GameplayTag cueTag, GameplayCueParameters parameters)
            {
                ExecuteCount++;
                LastTag = cueTag;
                LastParameters = parameters;
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
