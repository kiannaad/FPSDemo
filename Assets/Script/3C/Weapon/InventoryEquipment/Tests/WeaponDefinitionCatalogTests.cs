using System;
using CGame.Ability;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;

namespace CGame.InventoryEquipment.Tests
{
    public sealed class WeaponDefinitionCatalogTests
    {
        [Test]
        public void WeaponInstance_GrantsAbilitiesOnlyAfterArm_AndRevokesOnDispose()
        {
            GameplayTag weaponTag = CreateTag("Weapon.Knife");
            GameplayTag abilityTag = CreateTag("Ability.Weapon.Test");
            WeaponDefinition weaponDefinition = WeaponDefinition.CreateRuntime(
                weaponTag,
                30,
                new AbilitySet(new[] { new TestAbilityDefinition(abilityTag) }));
            WeaponItemDefinition itemDefinition = WeaponItemDefinition.CreateRuntime(weaponDefinition, 30, 90);
            var inventory = new InventoryComponent();
            var abilitySystem = new AbilitySystemComponent(new object());
            try
            {
                ItemInstanceHandle handle = inventory.Add(itemDefinition);
                InventoryLease lease = inventory.AcquireLease(handle);
                var weapon = (WeaponInstance)weaponDefinition.CreateInstance(
                    new EquipmentCreateContext(lease, abilitySystem));

                Assert.That(weapon.AbilityReceipts, Is.Empty);
                Assert.That(abilitySystem.AbilityCount, Is.EqualTo(0));

                weapon.Arm();
                Assert.That(weapon.IsArmed, Is.True);
                Assert.That(weapon.AbilityReceipts.Count, Is.EqualTo(1));
                Assert.That(abilitySystem.AbilityCount, Is.EqualTo(1));

                Assert.That(weapon.Fire(), Is.True);
                Assert.That(weapon.FireCount, Is.EqualTo(1));
                Assert.That(weapon.RecoilCount, Is.EqualTo(1));

                weapon.Dispose();
                Assert.That(abilitySystem.AbilityCount, Is.EqualTo(0));
            }
            finally
            {
                abilitySystem.Dispose();
                inventory.Dispose();
                UnityEngine.Object.DestroyImmediate(itemDefinition);
                UnityEngine.Object.DestroyImmediate(weaponDefinition);
            }
        }

        [Test]
        public void Catalog_RequiresExactUniqueWeaponTags()
        {
            GameplayTag tag = CreateTag("Weapon.Rifle.AK12");
            WeaponDefinition first = WeaponDefinition.CreateRuntime(tag, 30, new AbilitySet());
            WeaponDefinition second = WeaponDefinition.CreateRuntime(tag, 30, new AbilitySet());
            World world = null;
            try
            {
                var catalog = new WeaponCatalogSubSystem(new[] { first, second });
                world = World.Create(new WorldSubSystem[] { catalog });
                Assert.That(
                    () => world.InitializeAsync().GetAwaiter().GetResult(),
                    Throws.TypeOf<InvalidOperationException>());
            }
            finally
            {
                world?.ShutdownAsync().GetAwaiter().GetResult();
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
            }
        }

        [Test]
        public void Catalog_ResolvesOnlyTheExactRegisteredDefinition()
        {
            WeaponDefinition weapon = WeaponDefinition.CreateRuntime(
                CreateTag("Weapon.Knife"),
                30,
                new AbilitySet());
            WeaponItemDefinition item = WeaponItemDefinition.CreateRuntime(weapon, 30, 90);
            World world = null;
            try
            {
                var catalog = new WeaponCatalogSubSystem(new[] { weapon });
                world = World.Create(new WorldSubSystem[] { catalog });
                world.InitializeAsync().GetAwaiter().GetResult();

                Assert.That(catalog.ResolveExact(CreateTag("Weapon.Knife")), Is.SameAs(weapon));
                item.ValidateCatalog(catalog);
                Assert.That(
                    () => catalog.ResolveExact(CreateTag("Weapon")),
                    Throws.TypeOf<System.Collections.Generic.KeyNotFoundException>());
            }
            finally
            {
                world?.ShutdownAsync().GetAwaiter().GetResult();
                UnityEngine.Object.DestroyImmediate(item);
                UnityEngine.Object.DestroyImmediate(weapon);
            }
        }

        private static GameplayTag CreateTag(string value)
        {
            Assert.That(GameplayTag.TryCreateSerialized(value, out GameplayTag tag), Is.True);
            return tag;
        }

        private sealed class TestAbilityDefinition : AbilityDefinition
        {
            public TestAbilityDefinition(GameplayTag abilityTag)
                : base(abilityTag)
            {
            }

            protected override AbilityInstance CreateInstance()
            {
                return new TestAbilityInstance();
            }
        }

        private sealed class TestAbilityInstance : AbilityInstance
        {
            protected override void OnActivate()
            {
                EndAbility(AbilityEndReason.Completed);
            }
        }
    }
}
