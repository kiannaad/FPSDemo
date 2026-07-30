using System.Collections.Generic;
using CGame.Animation;
using NUnit.Framework;

namespace CGame.Tests
{
    public sealed class WeaponSwitchPipelineTests
    {
        [Test]
        public void Runtime_SwitchIsTransactionalAndLocksAllWeaponRequests()
        {
            var runtime = CreateKnifeRuntime();
            var switchFacts = new List<WeaponSwitchFact>();
            var equipmentFacts = new List<WeaponEquipmentSnapshot>();
            runtime.SwitchChanged += switchFacts.Add;
            runtime.EquipmentChanged += equipmentFacts.Add;

            Assert.AreEqual(
                WeaponSwitchRequestResult.AlreadyEquipped,
                runtime.RequestSwitchWeapon(
                    new WeaponId("knife"),
                    out _));
            Assert.AreEqual(
                WeaponSwitchRequestResult.Started,
                runtime.RequestSwitchWeapon(
                    new WeaponId("rifle"),
                    out WeaponSwitchFact started));
            Assert.IsTrue(runtime.IsSwitching);
            Assert.AreEqual(
                new WeaponId("knife"),
                runtime.Snapshot.EquippedWeaponId);
            Assert.IsTrue(runtime.Capabilities.SupportsMeleeAttack);
            Assert.IsFalse(runtime.RequestPrimaryAction(out _));
            Assert.IsFalse(runtime.RequestReload(out _));
            Assert.AreEqual(
                WeaponSwitchRequestResult.AlreadySwitching,
                runtime.RequestSwitchWeapon(
                    new WeaponId("pistol"),
                    out _));

            Assert.IsTrue(runtime.CompleteSwitch(
                started.SwitchId,
                new WeaponRuntimeCapabilities(
                    true,
                    true,
                    false)));

            Assert.IsFalse(runtime.IsSwitching);
            Assert.AreEqual(
                new WeaponId("rifle"),
                runtime.Snapshot.EquippedWeaponId);
            Assert.AreEqual(2u, runtime.Snapshot.Generation);
            Assert.IsTrue(runtime.Capabilities.SupportsFire);
            Assert.AreEqual(2, switchFacts.Count);
            Assert.AreEqual(
                WeaponSwitchPhase.Completed,
                switchFacts[1].Phase);
            Assert.AreEqual(1, equipmentFacts.Count);
        }

        [Test]
        public void Runtime_SwitchFailureKeepsOldEquipmentTruth()
        {
            var runtime = CreateKnifeRuntime();
            runtime.RequestPrimaryAction(out WeaponActionFact action);
            runtime.RequestSwitchWeapon(
                new WeaponId("rifle"),
                out WeaponSwitchFact started);

            Assert.IsFalse(runtime.ActiveAction.IsValid);
            Assert.IsTrue(runtime.FailSwitch(
                started.SwitchId,
                WeaponSwitchEndReason.TargetLoadFailed));
            Assert.AreEqual(
                new WeaponId("knife"),
                runtime.Snapshot.EquippedWeaponId);
            Assert.AreEqual(1u, runtime.Snapshot.Generation);
            Assert.IsTrue(runtime.Capabilities.SupportsMeleeAttack);
            Assert.IsTrue(action.IsValid);
        }

        [Test]
        public void Adapter_SeedsAnAlreadyActiveSwitchWhenBinding()
        {
            var runtime = CreateKnifeRuntime();
            runtime.RequestSwitchWeapon(
                new WeaponId("rifle"),
                out WeaponSwitchFact started);

            using (var adapter =
                   new CharacterWeaponAnimationAdapter())
            {
                adapter.BindRuntime(runtime);

                Assert.AreEqual(1, adapter.PendingEventCount);
                Assert.IsTrue(adapter.TryDequeueEvent(
                    out WeaponAnimationEvent animationEvent));
                Assert.AreEqual(
                    WeaponAnimationEventKind.Switch,
                    animationEvent.Kind);
                Assert.AreEqual(
                    started.SwitchId,
                    animationEvent.Switch.SwitchId);
            }
        }

        [TestCase("knife", "FistsWeaponAnimationDefinition")]
        [TestCase("rifle", "RifleAKAnimationDefinition")]
        public void DefinitionLocationResolver_MapsSupportedWeapons(
            string weaponId,
            string expectedLocation)
        {
            IWeaponAnimationDefinitionLocationResolver resolver =
                new WeaponAnimationDefinitionLocationResolver();

            Assert.IsTrue(resolver.TryResolveLocation(
                new WeaponId(weaponId),
                out string location));
            Assert.AreEqual(expectedLocation, location);
        }

        [TestCase("")]
        [TestCase("pistol")]
        public void DefinitionLocationResolver_RejectsUnsupportedWeapons(
            string weaponId)
        {
            IWeaponAnimationDefinitionLocationResolver resolver =
                new WeaponAnimationDefinitionLocationResolver();

            Assert.IsFalse(resolver.TryResolveLocation(
                new WeaponId(weaponId),
                out string location));
            Assert.IsTrue(string.IsNullOrEmpty(location));
        }

        private static WeaponRuntime CreateKnifeRuntime()
        {
            var runtime = new WeaponRuntime();
            Assert.IsTrue(runtime.Initialize(
                new WeaponId("knife"),
                new WeaponRuntimeCapabilities(
                    false,
                    false,
                    true)));
            return runtime;
        }
    }
}
