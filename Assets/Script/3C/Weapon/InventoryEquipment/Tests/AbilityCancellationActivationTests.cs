using System.Collections.Generic;
using CGame.Ability;
using CGame.GameplayTags;
using NUnit.Framework;
using RuntimeAbilityDefinition = CGame.Ability.AbilityDefinition;

namespace CGame.InventoryEquipment.Tests
{
    public sealed class AbilityCancellationActivationTests
    {
        [Test]
        public void CanActivateFailure_DoesNotCancelActiveTarget()
        {
            GameplayTag fireTag = CreateTag("Ability.Weapon.Fire");
            GameplayTag reloadTag = CreateTag("Ability.Weapon.Reload");
            var abilitySystem = new AbilitySystemComponent(new object());
            try
            {
                AbilitySpecHandle fireHandle = abilitySystem.GiveAbility(
                    new TestAbilityDefinition(fireTag),
                    new object());
                AbilitySpecHandle reloadHandle = abilitySystem.GiveAbility(
                    new TestAbilityDefinition(reloadTag, false, new[] { fireTag }),
                    new object());

                Assert.That(abilitySystem.TryActivateAbility(fireHandle).Succeeded, Is.True);

                AbilityActivationResult reloadResult = abilitySystem.TryActivateAbility(reloadHandle);

                Assert.That(reloadResult.Succeeded, Is.False);
                Assert.That(GetInstance(abilitySystem, fireHandle).State, Is.EqualTo(AbilityInstanceState.Active));
                Assert.That(GetInstance(abilitySystem, fireHandle).LastEndReason, Is.Null);
                Assert.That(GetInstance(abilitySystem, reloadHandle).CanActivateCallCount, Is.EqualTo(1));
            }
            finally
            {
                abilitySystem.Dispose();
            }
        }

        [Test]
        public void CancelAbilityTags_CancelsOnlyExactActiveAbilityTag_AndRunsTargetCleanup()
        {
            GameplayTag fireTag = CreateTag("Ability.Weapon.Fire");
            GameplayTag automaticFireTag = CreateTag("Ability.Weapon.Fire.Automatic");
            GameplayTag reloadTag = CreateTag("Ability.Weapon.Reload");
            GameplayTag firingStateTag = CreateTag("State.Weapon.Firing");
            var abilitySystem = new AbilitySystemComponent(new object());
            try
            {
                AbilitySpecHandle fireHandle = abilitySystem.GiveAbility(
                    new TestAbilityDefinition(fireTag, true, null, new[] { firingStateTag }),
                    new object());
                AbilitySpecHandle automaticFireHandle = abilitySystem.GiveAbility(
                    new TestAbilityDefinition(automaticFireTag),
                    new object());
                AbilitySpecHandle reloadHandle = abilitySystem.GiveAbility(
                    new TestAbilityDefinition(reloadTag, true, new[] { fireTag }),
                    new object());

                Assert.That(abilitySystem.TryActivateAbility(fireHandle).Succeeded, Is.True);
                Assert.That(abilitySystem.TryActivateAbility(automaticFireHandle).Succeeded, Is.True);
                Assert.That(abilitySystem.HasOwnedTagExact(firingStateTag), Is.True);

                Assert.That(abilitySystem.TryActivateAbility(reloadHandle).Succeeded, Is.True);

                Assert.That(GetInstance(abilitySystem, fireHandle).State, Is.EqualTo(AbilityInstanceState.Inactive));
                Assert.That(GetInstance(abilitySystem, fireHandle).LastEndReason, Is.EqualTo(AbilityEndReason.Cancelled));
                Assert.That(abilitySystem.HasOwnedTagExact(firingStateTag), Is.False);
                Assert.That(GetInstance(abilitySystem, automaticFireHandle).State, Is.EqualTo(AbilityInstanceState.Active));
                Assert.That(GetInstance(abilitySystem, reloadHandle).State, Is.EqualTo(AbilityInstanceState.Active));
            }
            finally
            {
                abilitySystem.Dispose();
            }
        }

        private static TestAbilityInstance GetInstance(AbilitySystemComponent abilitySystem, AbilitySpecHandle handle)
        {
            Assert.That(abilitySystem.TryGetSpec(handle, out AbilitySpec spec), Is.True);
            return (TestAbilityInstance)spec.PrimaryInstance;
        }

        private static GameplayTag CreateTag(string value)
        {
            Assert.That(GameplayTag.TryCreateSerialized(value, out GameplayTag tag), Is.True);
            return tag;
        }

        private sealed class TestAbilityDefinition : RuntimeAbilityDefinition
        {
            private readonly bool canActivate;

            public TestAbilityDefinition(
                GameplayTag abilityTag,
                bool canActivate = true,
                IEnumerable<GameplayTag> cancelAbilityTags = null,
                IEnumerable<GameplayTag> activationOwnedTags = null)
                : base(
                    abilityTag,
                    activationOwnedTags: activationOwnedTags,
                    cancelAbilityTags: cancelAbilityTags)
            {
                this.canActivate = canActivate;
            }

            protected override AbilityInstance CreateInstance()
            {
                return new TestAbilityInstance(canActivate);
            }
        }

        private sealed class TestAbilityInstance : AbilityInstance
        {
            private readonly bool canActivate;

            public TestAbilityInstance(bool canActivate)
            {
                this.canActivate = canActivate;
            }

            public int CanActivateCallCount { get; private set; }

            public override bool CanActivate(AbilityActivationContext context)
            {
                CanActivateCallCount++;
                return canActivate;
            }
        }
    }
}
