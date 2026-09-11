using CGame.Ability;
using CGame.Ability.Targeting;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEngine;

namespace CGame.InventoryEquipment.Tests
{
    public sealed class WeaponTargetDataQueryTests
    {
        [Test]
        public void QueryTargetData_ReturnsZeroOrOneEntryWithStableShotAndTraceIdentity()
        {
            Assert.That(GameplayTag.TryCreateSerialized("Weapon.Knife", out GameplayTag weaponTag), Is.True);
            WeaponDefinition definition = WeaponDefinition.CreateRuntime(weaponTag, 30, new AbilitySet());
            definition.ConfigureBulletData(100f, ~0);
            WeaponItemDefinition itemDefinition = WeaponItemDefinition.CreateRuntime(definition, 30, 0);
            var inventory = new InventoryComponent();
            var abilitySystem = new AbilitySystemComponent(new object());
            var presentation = new GameObject("WeaponPresentation");
            presentation.transform.position = Vector3.right * 1000f;
            var muzzle = new GameObject("MuzzlePoint");
            muzzle.transform.SetParent(presentation.transform);
            var target = new GameObject("Target");
            target.transform.position = Vector3.right * 1000f + Vector3.forward * 5f;
            BoxCollider targetCollider = target.AddComponent<BoxCollider>();
            try
            {
                InventoryLease lease = inventory.AcquireLease(inventory.Add(itemDefinition));
                var weapon = (WeaponInstance)definition.CreateInstance(new EquipmentCreateContext(lease, abilitySystem));
                weapon.PreparePresentation(presentation);
                Physics.SyncTransforms();
                Vector3 cameraOrigin = Vector3.right * 1000f;
                GameplayAbilityTargetDataHandle hit = weapon.QueryTargetData(cameraOrigin, Vector3.forward);
                Assert.That(hit.Count, Is.EqualTo(1));
                Assert.That(hit.ShotId, Is.EqualTo(1UL));
                Assert.That(hit[0].TraceIndex, Is.Zero);
                Assert.That(hit[0].HitResult.Collider, Is.SameAs(targetCollider));
                Assert.That(hit[0].HitResult.TraceStart, Is.EqualTo(cameraOrigin));

                target.SetActive(false);
                GameplayAbilityTargetDataHandle miss = weapon.QueryTargetData(cameraOrigin, Vector3.forward);
                Assert.That(miss.Count, Is.Zero);
                weapon.Dispose();
            }
            finally
            {
                abilitySystem.Dispose();
                inventory.Dispose();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(presentation);
                Object.DestroyImmediate(itemDefinition);
                Object.DestroyImmediate(definition);
            }
        }
    }
}
