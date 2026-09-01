using NUnit.Framework;
using CGame.Ability;
using CGame.GameplayTags;
using UnityEngine;

namespace CGame.InventoryEquipment.Tests
{
    public sealed class WeaponSpreadSamplerTests
    {
        private static readonly WeaponSpreadContext Context = new WeaponSpreadContext(
            Vector3.forward,
            Vector3.right,
            Vector3.up,
            false,
            true,
            0f);

        [Test]
        public void SameSeedAndContext_ProducesTheSameUnitDirectionInsideHalfAngle()
        {
            Vector3 first = WeaponSpreadSampler.SampleDirection(Context, 12f, 1f, 42UL);
            Vector3 second = WeaponSpreadSampler.SampleDirection(Context, 12f, 1f, 42UL);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(Vector3.Angle(Context.Forward, first), Is.LessThanOrEqualTo(12.001f));
        }

        [Test]
        public void LargerExponent_ConcentratesTheSameSeedsCloserToCenter()
        {
            float linearMean = MeanAngle(1f);
            float concentratedMean = MeanAngle(2f);

            Assert.That(concentratedMean, Is.LessThan(linearMean));
        }

        [Test]
        public void OneThousandSamples_StayWithinTheConfiguredSphericalHalfAngle()
        {
            for (ulong seed = 0; seed < 1000; seed++)
            {
                Vector3 direction = WeaponSpreadSampler.SampleDirection(Context, 20f, 1f, seed);
                Assert.That(direction.magnitude, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(Vector3.Angle(Context.Forward, direction), Is.LessThanOrEqualTo(20.001f));
            }
        }

        [Test]
        public void ZeroHalfAngle_ReturnsCameraForward()
        {
            Assert.That(WeaponSpreadSampler.SampleDirection(Context, 0f, 1f, 99UL), Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void SuccessfulShotHeat_AdvancesSequenceAndDisarmClearsOnlyHeat()
        {
            Assert.That(GameplayTag.TryCreateSerialized("Weapon.Knife", out GameplayTag weaponTag), Is.True);
            WeaponDefinition definition = WeaponDefinition.CreateRuntime(weaponTag, 30, new AbilitySet());
            WeaponItemDefinition item = WeaponItemDefinition.CreateRuntime(definition, 30, 0);
            var inventory = new InventoryComponent();
            var abilitySystem = new AbilitySystemComponent(new object());
            try
            {
                InventoryLease lease = inventory.AcquireLease(inventory.Add(item));
                var weapon = (WeaponInstance)definition.CreateInstance(new EquipmentCreateContext(lease, abilitySystem));
                weapon.Arm();
                weapon.CommitSuccessfulShot();

                Assert.That(weapon.Heat, Is.GreaterThan(0f));
                Assert.That(weapon.SpreadSequence, Is.EqualTo(1UL));
                float heated = weapon.Heat;
                weapon.AdvanceHeat(1f, Time.time + definition.HeatCooldownDelaySeconds + 1f);
                Assert.That(weapon.Heat, Is.LessThan(heated));
                weapon.Disarm();
                Assert.That(weapon.Heat, Is.Zero);
                Assert.That(weapon.SpreadSequence, Is.EqualTo(1UL));
                weapon.Dispose();
            }
            finally
            {
                abilitySystem.Dispose();
                inventory.Dispose();
                Object.DestroyImmediate(item);
                Object.DestroyImmediate(definition);
            }
        }

        private static float MeanAngle(float exponent)
        {
            float total = 0f;
            for (ulong seed = 0; seed < 1000; seed++)
            {
                total += Vector3.Angle(Context.Forward, WeaponSpreadSampler.SampleDirection(Context, 20f, exponent, seed));
            }

            return total / 1000f;
        }
    }
}
