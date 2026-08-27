using System;
using CGame;
using CGame.Ability;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;

namespace CGame.InventoryEquipment.Tests
{
    public sealed class AimWeaponAbilityTests
    {
        // Exercises the production WhileInputActive contract without scene input plumbing.
        [Test]
        public void AimAbility_HoldsAimingTagUntilInputRelease_AndHonorsBlockedState()
        {
            GameplayTag weaponTag = CreateTag("Weapon.Rifle.AK12");
            GameplayTag abilityTag = CreateTag("Ability.Weapon.Aim");
            GameplayTag inputTag = CreateTag("InputTag.Weapon.Aim");
            GameplayTag aimingTag = CreateTag("State.Weapon.Aiming");
            GameplayTag blockedTag = CreateTag("State.Weapon.Action");
            GameplayTagSource source = CreateTagSource();
            GameplayTagManager.Instance.Initialize(new[] { source });

            GameObject root = new GameObject("AimAbilityPawn");
            Pawn pawn = new Pawn(root);
            AbilitySystemComponent abilitySystem = new AbilitySystemComponent(pawn);
            WeaponDefinition definition = null;
            WeaponItemDefinition itemDefinition = null;
            InventoryComponent inventory = null;
            WeaponInstance weapon = null;
            try
            {
                AimWeaponAbilityDefinition aim = new AimWeaponAbilityDefinition();
                aim.Configure(abilityTag, inputTag);
                aim.ConfigureAimingState(aimingTag, new[] { blockedTag });
                definition = WeaponDefinition.CreateRuntime(
                    weaponTag,
                    30,
                    new AbilitySet(new[] { aim.CreateGrant() }));
                itemDefinition = WeaponItemDefinition.CreateRuntime(definition, 30, 90);
                inventory = new InventoryComponent();
                ItemInstanceHandle handle = inventory.Add(itemDefinition);
                weapon = (WeaponInstance)definition.CreateInstance(
                    new EquipmentCreateContext(inventory.AcquireLease(handle), abilitySystem));
                weapon.Arm();

                abilitySystem.AbilityInputTagPressed(inputTag);
                abilitySystem.ProcessAbilityInput();
                Assert.That(pawn.IsAiming, Is.True);
                Assert.That(abilitySystem.HasOwnedTag(aimingTag), Is.True);

                abilitySystem.AbilityInputTagReleased(inputTag);
                abilitySystem.ProcessAbilityInput();
                Assert.That(pawn.IsAiming, Is.False);
                Assert.That(abilitySystem.HasOwnedTag(aimingTag), Is.False);

                GameplayTagGrantHandle blockedHandle = abilitySystem.AddOwnedTag(blockedTag);
                abilitySystem.AbilityInputTagPressed(inputTag);
                abilitySystem.ProcessAbilityInput();
                Assert.That(pawn.IsAiming, Is.False);
                Assert.That(abilitySystem.HasOwnedTag(aimingTag), Is.False);
                Assert.That(abilitySystem.RemoveOwnedTag(blockedHandle), Is.True);

                abilitySystem.ProcessAbilityInput();
                Assert.That(
                    pawn.IsAiming,
                    Is.True,
                    "A held Aim input must activate automatically once a temporary block, such as weapon switching, is removed.");
                weapon.Dispose();
                weapon = null;
                Assert.That(pawn.IsAiming, Is.False);
                Assert.That(abilitySystem.HasOwnedTag(aimingTag), Is.False);
            }
            finally
            {
                weapon?.Dispose();
                abilitySystem.Dispose();
                inventory?.Dispose();
                UnityEngine.Object.DestroyImmediate(itemDefinition);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(source);
                GameplayTagManager.Instance.Shutdown();
            }
        }

        private static GameplayTagSource CreateTagSource()
        {
            GameplayTagSource source = ScriptableObject.CreateInstance<GameplayTagSource>();
            source.SetDefinition("AimAbilityTest", new[]
            {
                new GameplayTagSourceNode("Ability", false, children: new[]
                {
                    new GameplayTagSourceNode("Weapon", false, children: new[]
                    {
                        new GameplayTagSourceNode("Aim", true)
                    })
                }),
                new GameplayTagSourceNode("InputTag", false, children: new[]
                {
                    new GameplayTagSourceNode("Weapon", false, children: new[]
                    {
                        new GameplayTagSourceNode("Aim", true)
                    })
                }),
                new GameplayTagSourceNode("State", false, children: new[]
                {
                    new GameplayTagSourceNode("Weapon", false, children: new[]
                    {
                        new GameplayTagSourceNode("Action", true),
                        new GameplayTagSourceNode("Aiming", true)
                    })
                }),
                new GameplayTagSourceNode("Weapon", false, children: new[]
                {
                    new GameplayTagSourceNode("Rifle", false, children: new[]
                    {
                        new GameplayTagSourceNode("AK12", true)
                    })
                })
            });
            return source;
        }

        private static GameplayTag CreateTag(string value)
        {
            Assert.That(GameplayTag.TryCreateSerialized(value, out GameplayTag tag), Is.True);
            return tag;
        }
    }
}
