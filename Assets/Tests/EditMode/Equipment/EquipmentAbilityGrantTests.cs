using System;
using System.Collections.Generic;
using System.Linq;
using CGame.Ability;
using CGame.Animation;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.Weapon.Equipment.Tests
{
    public sealed class EquipmentAbilityGrantTests
    {
        private readonly List<UnityEngine.Object> objectsToDestroy = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            GameplayTagManager.Instance.Shutdown();
            foreach (UnityEngine.Object target in objectsToDestroy)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }

            objectsToDestroy.Clear();
        }

        [Test]
        public void Unequip_RevokesDeduplicatedSetsAndTasksBeforeReleasingDefinition()
        {
            InitializeTags(
                out GameplayTag abilityTag,
                out GameplayTag eventTag,
                out GameplayTag firstOwnedTag,
                out GameplayTag secondOwnedTag);
            var playerState = new PlayerState(new AbilitySet(), new object());
            var pawn = new Pawn();
            playerState.SetAvatar(pawn);
            var runtime = new WeaponRuntime();
            Assert.That(
                runtime.Initialize(
                    new WeaponId("rifle"),
                    new WeaponRuntimeCapabilities(true, true, false)),
                Is.True);
            var lease = new FakeDefinitionLease(CreateDefinition("rifle"));
            var definition = new CleanupTrackingAbilityDefinition(
                abilityTag,
                eventTag,
                () => lease.IsDisposed);
            var firstSet = new AbilitySet(new[] { definition }, new[] { firstOwnedTag });
            var secondSet = new AbilitySet(ownedTags: new[] { secondOwnedTag });
            var slot = new EquipmentSlot(playerState.AbilitySystem, runtime);

            EquipmentEquipResult result = slot.TryEquip(
                lease,
                new[] { firstSet, firstSet, secondSet },
                out EquipmentInstance equipment);

            Assert.That(result, Is.EqualTo(EquipmentEquipResult.Equipped));
            Assert.That(equipment.SourceObject, Is.SameAs(equipment));
            Assert.That(equipment.GrantReceipts.Count, Is.EqualTo(2));
            Assert.That(playerState.AbilitySystem.AbilityCount, Is.EqualTo(1));
            Assert.That(playerState.AbilitySystem.GetOwnedTagCount(firstOwnedTag), Is.EqualTo(1));
            Assert.That(playerState.AbilitySystem.GetOwnedTagCount(secondOwnedTag), Is.EqualTo(1));
            Assert.That(playerState.AbilitySystem.TryActivateAbilityByTag(abilityTag).Succeeded, Is.True);
            Assert.That(playerState.AbilitySystem.TryGetSpec(equipment.GrantReceipts[0].SpecHandles[0], out AbilitySpec spec), Is.True);
            var instance = (CleanupTrackingAbilityInstance)spec.PrimaryInstance;
            WaitGameEventTask task = instance.WaitTask;

            Assert.That(slot.Unequip(), Is.True);

            Assert.That(instance.LastEndReason, Is.EqualTo(AbilityEndReason.SourceRemoved));
            Assert.That(instance.LeaseWasDisposedDuringEnd, Is.False);
            Assert.That(task.State, Is.EqualTo(AbilityTaskState.Cancelled));
            Assert.That(task.IsListening, Is.False);
            Assert.That(playerState.AbilitySystem.AbilityCount, Is.Zero);
            Assert.That(playerState.AbilitySystem.HasOwnedTagExact(firstOwnedTag), Is.False);
            Assert.That(playerState.AbilitySystem.HasOwnedTagExact(secondOwnedTag), Is.False);
            Assert.That(lease.ReleaseCount, Is.EqualTo(1));
            Assert.That(slot.Unequip(), Is.False);
            equipment.Dispose();
            Assert.That(lease.ReleaseCount, Is.EqualTo(1));
            playerState.Dispose();
        }

        [Test]
        public void TryEquip_InvalidOrGrantFailureKeepsOldEquipmentAndSuccessfulReplacementReleasesOldOnce()
        {
            InitializeTags(
                out GameplayTag abilityTag,
                out _,
                out GameplayTag oldTag,
                out GameplayTag newTag);
            var component = new AbilitySystemComponent(new object());
            var runtime = new WeaponRuntime();
            runtime.Initialize(
                new WeaponId("rifle"),
                new WeaponRuntimeCapabilities(true, true, false));
            var slot = new EquipmentSlot(component, runtime);
            var oldLease = new FakeDefinitionLease(CreateDefinition("rifle"));
            var oldSet = new AbilitySet(ownedTags: new[] { oldTag });
            Assert.That(
                slot.TryEquip(oldLease, new[] { oldSet }, out EquipmentInstance oldEquipment),
                Is.EqualTo(EquipmentEquipResult.Equipped));

            var invalidLease = new FakeDefinitionLease(CreateDefinition("knife"), isValid: false);
            Assert.That(
                slot.TryEquip(invalidLease, new[] { new AbilitySet() }, out _),
                Is.EqualTo(EquipmentEquipResult.InvalidTarget));
            Assert.That(slot.Current, Is.SameAs(oldEquipment));
            Assert.That(invalidLease.ReleaseCount, Is.EqualTo(1));
            Assert.That(component.HasOwnedTagExact(oldTag), Is.True);

            var failingLease = new FakeDefinitionLease(CreateDefinition("knife"));
            var successfulCandidateSet = new AbilitySet(ownedTags: new[] { newTag });
            var throwingSet = new AbilitySet(
                new AbilityDefinition[]
                {
                    new PassiveAbilityDefinition(abilityTag),
                    new ThrowingAbilityDefinition(abilityTag)
                });
            Assert.That(
                slot.TryEquip(
                    failingLease,
                    new[] { successfulCandidateSet, throwingSet },
                    out _),
                Is.EqualTo(EquipmentEquipResult.GrantFailed));
            Assert.That(slot.Current, Is.SameAs(oldEquipment));
            Assert.That(failingLease.ReleaseCount, Is.EqualTo(1));
            Assert.That(component.AbilityCount, Is.Zero);
            Assert.That(component.HasOwnedTagExact(oldTag), Is.True);
            Assert.That(component.HasOwnedTagExact(newTag), Is.False);

            var replacementLease = new FakeDefinitionLease(CreateDefinition("knife"));
            Assert.That(
                slot.TryEquip(
                    replacementLease,
                    new[] { successfulCandidateSet },
                    out EquipmentInstance replacement),
                Is.EqualTo(EquipmentEquipResult.Equipped));

            Assert.That(slot.Current, Is.SameAs(replacement));
            Assert.That(oldEquipment.IsDisposed, Is.True);
            Assert.That(oldLease.ReleaseCount, Is.EqualTo(1));
            Assert.That(component.HasOwnedTagExact(oldTag), Is.False);
            Assert.That(component.HasOwnedTagExact(newTag), Is.True);
            slot.Dispose();
            Assert.That(replacementLease.ReleaseCount, Is.EqualTo(1));
            Assert.That(component.HasOwnedTagExact(newTag), Is.False);
        }

        [Test]
        public void TryEquip_SameWeaponAndDisposedSlotRejectIncomingLeaseWithoutChangingCurrent()
        {
            InitializeTags(out _, out _, out _, out _);
            var component = new AbilitySystemComponent(new object());
            var runtime = new WeaponRuntime();
            runtime.Initialize(
                new WeaponId("rifle"),
                new WeaponRuntimeCapabilities(true, false, false));
            var slot = new EquipmentSlot(component, runtime);
            var currentLease = new FakeDefinitionLease(CreateDefinition("rifle"));
            slot.TryEquip(currentLease, new[] { new AbilitySet() }, out EquipmentInstance current);

            var duplicateLease = new FakeDefinitionLease(CreateDefinition("rifle"));
            Assert.That(
                slot.TryEquip(duplicateLease, new[] { new AbilitySet() }, out _),
                Is.EqualTo(EquipmentEquipResult.AlreadyEquipped));
            Assert.That(slot.Current, Is.SameAs(current));
            Assert.That(duplicateLease.ReleaseCount, Is.EqualTo(1));
            Assert.That(currentLease.ReleaseCount, Is.Zero);

            slot.Dispose();
            slot.Dispose();
            var afterDisposeLease = new FakeDefinitionLease(CreateDefinition("knife"));
            Assert.That(
                slot.TryEquip(afterDisposeLease, new[] { new AbilitySet() }, out _),
                Is.EqualTo(EquipmentEquipResult.SlotDisposed));
            Assert.That(afterDisposeLease.ReleaseCount, Is.EqualTo(1));
            Assert.That(currentLease.ReleaseCount, Is.EqualTo(1));
        }

        [TestCase("Assets/Resources/KnifeWeaponAnimationDefinition.asset", "knife")]
        [TestCase("Assets/Art/Animation/Weapon/KINEMATION/AK/RifleAKAnimationDefinition.asset", "rifle")]
        public void LiveWeaponDefinitionSamples_MatchResolverIdsAndRemainValid(
            string assetPath,
            string weaponId)
        {
            WeaponAnimationDefinition definition =
                AssetDatabase.LoadAssetAtPath<WeaponAnimationDefinition>(assetPath);

            Assert.That(definition, Is.Not.Null, assetPath);
            Assert.That(definition.WeaponId, Is.EqualTo(new WeaponId(weaponId)));
            Assert.That(
                definition.Validate(new WeaponId(weaponId)),
                Is.EqualTo(WeaponAnimationDefinitionError.None));
        }

        private void InitializeTags(
            out GameplayTag abilityTag,
            out GameplayTag eventTag,
            out GameplayTag firstOwnedTag,
            out GameplayTag secondOwnedTag)
        {
            GameplayTagSource source = ScriptableObject.CreateInstance<GameplayTagSource>();
            source.SetDefinition(
                "EquipmentTests",
                new[]
                {
                    Node("Ability", false, Node("Weapon", false, Node("Test", true))),
                    Node("Event", false, Node("Weapon", false, Node("Test", true))),
                    Node("State", false, Node("Equipment", false, Node("First", true), Node("Second", true)))
                });
            objectsToDestroy.Add(source);
            GameplayTagRegistryBuildResult result = GameplayTagManager.Instance.Initialize(new[] { source });
            Assert.That(result.Succeeded, Is.True, string.Join(Environment.NewLine, result.Errors.Select(error => error.ToString())));
            abilityTag = GameplayTagManager.Instance.RequestTag("Ability.Weapon.Test");
            eventTag = GameplayTagManager.Instance.RequestTag("Event.Weapon.Test");
            firstOwnedTag = GameplayTagManager.Instance.RequestTag("State.Equipment.First");
            secondOwnedTag = GameplayTagManager.Instance.RequestTag("State.Equipment.Second");
        }

        private WeaponAnimationDefinition CreateDefinition(string weaponId)
        {
            WeaponAnimationDefinition definition = ScriptableObject.CreateInstance<WeaponAnimationDefinition>();
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("weaponId").stringValue = weaponId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            objectsToDestroy.Add(definition);
            return definition;
        }

        private static GameplayTagSourceNode Node(string segmentName, bool isExplicit, params GameplayTagSourceNode[] children)
        {
            return new GameplayTagSourceNode(segmentName, isExplicit, children: children);
        }

        private sealed class FakeDefinitionLease : IEquipmentDefinitionLease
        {
            private readonly bool isValid;

            public FakeDefinitionLease(WeaponAnimationDefinition definition, bool isValid = true)
            {
                Definition = definition;
                this.isValid = isValid;
            }

            public WeaponAnimationDefinition Definition { get; }
            public WeaponId WeaponId => Definition == null ? default : Definition.WeaponId;
            public bool IsValid => isValid && !IsDisposed && Definition != null && WeaponId.IsValid;
            public bool IsDisposed { get; private set; }
            public int ReleaseCount { get; private set; }

            public void Dispose()
            {
                if (IsDisposed)
                {
                    return;
                }

                IsDisposed = true;
                ReleaseCount++;
            }
        }

        private sealed class CleanupTrackingAbilityDefinition : AbilityDefinition
        {
            private readonly GameplayTag eventTag;
            private readonly Func<bool> isLeaseDisposed;

            public CleanupTrackingAbilityDefinition(
                GameplayTag abilityTag,
                GameplayTag eventTag,
                Func<bool> isLeaseDisposed)
                : base(abilityTag)
            {
                this.eventTag = eventTag;
                this.isLeaseDisposed = isLeaseDisposed;
            }

            protected override AbilityInstance CreateInstance()
            {
                return new CleanupTrackingAbilityInstance(eventTag, isLeaseDisposed);
            }
        }

        private sealed class CleanupTrackingAbilityInstance : AbilityInstance
        {
            private readonly GameplayTag eventTag;
            private readonly Func<bool> isLeaseDisposed;

            public CleanupTrackingAbilityInstance(GameplayTag eventTag, Func<bool> isLeaseDisposed)
            {
                this.eventTag = eventTag;
                this.isLeaseDisposed = isLeaseDisposed;
            }

            public WaitGameEventTask WaitTask { get; private set; }
            public bool LeaseWasDisposedDuringEnd { get; private set; }

            protected override void OnActivate()
            {
                WaitTask = StartTask(new WaitGameEventTask(
                    eventTag,
                    AbilityGameEventMatchPolicy.Exact,
                    onlyTriggerOnce: false,
                    payload => { }));
            }

            protected override void OnEnd(AbilityEndReason reason)
            {
                LeaseWasDisposedDuringEnd = isLeaseDisposed();
            }
        }

        private sealed class ThrowingAbilityDefinition : AbilityDefinition
        {
            public ThrowingAbilityDefinition(GameplayTag abilityTag)
                : base(abilityTag)
            {
            }

            protected override AbilityInstance CreateInstance()
            {
                throw new InvalidOperationException("Expected candidate grant failure.");
            }
        }

        private sealed class PassiveAbilityDefinition : AbilityDefinition
        {
            public PassiveAbilityDefinition(GameplayTag abilityTag)
                : base(abilityTag)
            {
            }

            protected override AbilityInstance CreateInstance()
            {
                return new PassiveAbilityInstance();
            }
        }

        private sealed class PassiveAbilityInstance : AbilityInstance
        {
        }
    }
}
