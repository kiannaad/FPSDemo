using System;
using System.Collections.Generic;
using System.Linq;
using CGame.Ability;
using CGame.Ability.Animation;
using CGame.Animation;
using CGame.GameplayTags;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.Weapon.Equipment.Tests
{
    public sealed class WeaponSwitchAbilityTests
    {
        private readonly List<UnityEngine.Object> objectsToDestroy =
            new List<UnityEngine.Object>();

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
        public void SuccessfulSwitch_PromotesPreparedEquipmentAndPresentationBeforeReleasingOldOwner()
        {
            InitializeTags();
            WeaponAnimationDefinition rifle = CreateDefinition("rifle", supportsFire: true);
            WeaponAnimationDefinition knife = CreateDefinition("knife", supportsFire: false);
            var oldLease = new FakeLease(rifle);
            var targetLease = new FakeLease(knife);
            var loader = new FakeDefinitionLoader(targetLease);
            var presentation = new FakeSwitchPresentation();
            var pawn = new Pawn();
            var controller = new Controller();
            controller.PossessingPawn(pawn);
            Assert.That(controller.InitializeWeapon(rifle.WeaponId, rifle.Capabilities), Is.True);
            var abilitySystem = new AbilitySystemComponent(pawn);
            pawn.BindingAbilitySystem(abilitySystem);
            var slot = new EquipmentSlot(
                abilitySystem,
                controller.WeaponRuntime,
                presentation,
                loader,
                presentation);
            Assert.That(
                slot.TryEquip(
                    oldLease,
                    new[]
                    {
                        WeaponActionAbilitySetFactory.Create(rifle),
                        WeaponSwitchAbilitySetFactory.Create()
                    },
                    out EquipmentInstance oldEquipment),
                Is.EqualTo(EquipmentEquipResult.Equipped));
            WeaponSwitchAbilityInstance switchAbility = GetSwitchAbility(
                abilitySystem,
                oldEquipment);

            WeaponSwitchRequestResult request = controller.RequestSwitchWeapon(
                knife.WeaponId,
                out WeaponSwitchFact started);

            Assert.That(request, Is.EqualTo(WeaponSwitchRequestResult.Started));
            Assert.That(started.ToWeaponId, Is.EqualTo(knife.WeaponId));
            Assert.That(slot.Current, Is.SameAs(oldEquipment));
            Assert.That(oldLease.ReleaseCount, Is.Zero);
            presentation.CompleteNextAnimation();
            Assert.That(slot.Current, Is.SameAs(oldEquipment));
            Assert.That(presentation.PreparedReplacement.CommitCount, Is.Zero);

            presentation.CompleteNextAnimation();

            Assert.That(controller.WeaponRuntime.Snapshot.EquippedWeaponId,
                Is.EqualTo(knife.WeaponId));
            Assert.That(controller.WeaponRuntime.Snapshot.Generation, Is.EqualTo(2u));
            Assert.That(slot.Current, Is.Not.SameAs(oldEquipment));
            Assert.That(slot.Current.WeaponId, Is.EqualTo(knife.WeaponId));
            Assert.That(oldEquipment.IsDisposed, Is.True);
            Assert.That(oldLease.ReleaseCount, Is.EqualTo(1));
            Assert.That(targetLease.ReleaseCount, Is.Zero);
            Assert.That(presentation.PreparedReplacement.CommitCount, Is.EqualTo(1));
            Assert.That(presentation.PromotedPoseCount, Is.EqualTo(1));
            Assert.That(switchAbility.LastEndReason, Is.EqualTo(AbilityEndReason.Completed));
            Assert.That(controller.WeaponRuntime.ActiveSwitch.IsValid, Is.False);

            slot.Dispose();
            Assert.That(targetLease.ReleaseCount, Is.EqualTo(1));
            Assert.That(oldLease.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public void TargetPlaybackFailure_RestoresOldPoseBeforeReportingFailure()
        {
            InitializeTags();
            WeaponAnimationDefinition rifle = CreateDefinition("rifle", supportsFire: true);
            WeaponAnimationDefinition knife = CreateDefinition("knife", supportsFire: false);
            var oldLease = new FakeLease(rifle);
            var targetLease = new FakeLease(knife);
            var loader = new FakeDefinitionLoader(targetLease);
            var presentation = new FakeSwitchPresentation();
            var pawn = new Pawn();
            var controller = new Controller();
            controller.PossessingPawn(pawn);
            Assert.That(controller.InitializeWeapon(rifle.WeaponId, rifle.Capabilities), Is.True);
            var abilitySystem = new AbilitySystemComponent(pawn);
            pawn.BindingAbilitySystem(abilitySystem);
            var slot = new EquipmentSlot(
                abilitySystem,
                controller.WeaponRuntime,
                presentation,
                loader,
                presentation);
            Assert.That(slot.TryEquip(
                    oldLease,
                    new[]
                    {
                        WeaponActionAbilitySetFactory.Create(rifle),
                        WeaponSwitchAbilitySetFactory.Create()
                    },
                    out EquipmentInstance oldEquipment),
                Is.EqualTo(EquipmentEquipResult.Equipped));
            WeaponSwitchFact terminal = default;
            controller.WeaponRuntime.SwitchChanged += fact =>
            {
                if (fact.Phase != WeaponSwitchPhase.Started)
                {
                    terminal = fact;
                }
            };

            Assert.That(controller.RequestSwitchWeapon(
                    knife.WeaponId,
                    out _),
                Is.EqualTo(WeaponSwitchRequestResult.Started));
            presentation.FailNextAnimation = true;
            presentation.CompleteNextAnimation();

            Assert.That(controller.WeaponRuntime.ActiveSwitch.IsValid, Is.True,
                "The failure fact must wait until the old pose and equip animation are restored.");
            Assert.That(slot.Current, Is.SameAs(oldEquipment));
            Assert.That(oldLease.ReleaseCount, Is.Zero);
            Assert.That(presentation.PlayedPoseCount, Is.EqualTo(2),
                "Target pose and old restore pose must both be requested.");

            presentation.CompleteNextAnimation();

            Assert.That(controller.WeaponRuntime.ActiveSwitch.IsValid, Is.False);
            Assert.That(terminal.Phase, Is.EqualTo(WeaponSwitchPhase.Failed));
            Assert.That(terminal.EndReason,
                Is.EqualTo(WeaponSwitchEndReason.TargetPlaybackFailed));
            Assert.That(slot.Current, Is.SameAs(oldEquipment));
            Assert.That(oldEquipment.IsDisposed, Is.False);
            Assert.That(oldLease.ReleaseCount, Is.Zero);
            Assert.That(targetLease.ReleaseCount, Is.EqualTo(1));
            Assert.That(presentation.PromotedPoseCount, Is.EqualTo(1));

            slot.Dispose();
            Assert.That(oldLease.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public void CancelAfterTargetStarted_RestoresOldPresentationAndKeepsOldEquipment()
        {
            InitializeTags();
            WeaponAnimationDefinition rifle = CreateDefinition("rifle", supportsFire: true);
            WeaponAnimationDefinition knife = CreateDefinition("knife", supportsFire: false);
            var oldLease = new FakeLease(rifle);
            var targetLease = new FakeLease(knife);
            var presentation = new FakeSwitchPresentation();
            var pawn = new Pawn();
            var controller = new Controller();
            controller.PossessingPawn(pawn);
            Assert.That(controller.InitializeWeapon(rifle.WeaponId, rifle.Capabilities), Is.True);
            var abilitySystem = new AbilitySystemComponent(pawn);
            pawn.BindingAbilitySystem(abilitySystem);
            var slot = new EquipmentSlot(
                abilitySystem,
                controller.WeaponRuntime,
                presentation,
                new FakeDefinitionLoader(targetLease),
                presentation);
            Assert.That(slot.TryEquip(
                    oldLease,
                    new[]
                    {
                        WeaponActionAbilitySetFactory.Create(rifle),
                        WeaponSwitchAbilitySetFactory.Create()
                    },
                    out EquipmentInstance oldEquipment),
                Is.EqualTo(EquipmentEquipResult.Equipped));
            WeaponSwitchAbilityInstance switchAbility = GetSwitchAbility(
                abilitySystem,
                oldEquipment);
            WeaponSwitchFact terminal = default;
            controller.WeaponRuntime.SwitchChanged += fact =>
            {
                if (fact.Phase != WeaponSwitchPhase.Started)
                {
                    terminal = fact;
                }
            };

            Assert.That(controller.RequestSwitchWeapon(knife.WeaponId, out _),
                Is.EqualTo(WeaponSwitchRequestResult.Started));
            presentation.CompleteNextAnimation();
            Assert.That(presentation.PlayedPoseCount, Is.EqualTo(1));

            Assert.That(switchAbility.EndAbility(AbilityEndReason.Cancelled), Is.True);

            Assert.That(controller.WeaponRuntime.ActiveSwitch.IsValid, Is.False);
            Assert.That(terminal.Phase, Is.EqualTo(WeaponSwitchPhase.Cancelled));
            Assert.That(terminal.EndReason, Is.EqualTo(WeaponSwitchEndReason.Cancelled));
            Assert.That(slot.Current, Is.SameAs(oldEquipment));
            Assert.That(oldEquipment.IsDisposed, Is.False);
            Assert.That(oldLease.ReleaseCount, Is.Zero);
            Assert.That(targetLease.ReleaseCount, Is.EqualTo(1));
            Assert.That(presentation.PlayedPoseCount, Is.EqualTo(2));
            Assert.That(presentation.PlayedAnimationCount, Is.EqualTo(3));
            Assert.That(presentation.PromotedPoseCount, Is.EqualTo(1));

            slot.Dispose();
            Assert.That(oldLease.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public void MissingDefinitionLoader_ReportsTargetLoadFailureAndKeepsOldEquipment()
        {
            InitializeTags();
            WeaponAnimationDefinition rifle = CreateDefinition("rifle", supportsFire: true);
            WeaponAnimationDefinition knife = CreateDefinition("knife", supportsFire: false);
            var oldLease = new FakeLease(rifle);
            var presentation = new FakeSwitchPresentation();
            var pawn = new Pawn();
            var controller = new Controller();
            controller.PossessingPawn(pawn);
            Assert.That(controller.InitializeWeapon(rifle.WeaponId, rifle.Capabilities), Is.True);
            var abilitySystem = new AbilitySystemComponent(pawn);
            pawn.BindingAbilitySystem(abilitySystem);
            var slot = new EquipmentSlot(
                abilitySystem,
                controller.WeaponRuntime,
                presentation,
                null,
                presentation);
            Assert.That(slot.TryEquip(
                    oldLease,
                    new[]
                    {
                        WeaponActionAbilitySetFactory.Create(rifle),
                        WeaponSwitchAbilitySetFactory.Create()
                    },
                    out EquipmentInstance oldEquipment),
                Is.EqualTo(EquipmentEquipResult.Equipped));
            WeaponSwitchFact terminal = default;
            controller.WeaponRuntime.SwitchChanged += fact =>
            {
                if (fact.Phase != WeaponSwitchPhase.Started)
                {
                    terminal = fact;
                }
            };

            Assert.That(controller.RequestSwitchWeapon(knife.WeaponId, out _),
                Is.EqualTo(WeaponSwitchRequestResult.Started));

            Assert.That(controller.WeaponRuntime.ActiveSwitch.IsValid, Is.False);
            Assert.That(terminal.Phase, Is.EqualTo(WeaponSwitchPhase.Failed));
            Assert.That(terminal.EndReason,
                Is.EqualTo(WeaponSwitchEndReason.TargetLoadFailed));
            Assert.That(slot.Current, Is.SameAs(oldEquipment));
            Assert.That(oldEquipment.IsDisposed, Is.False);
            Assert.That(oldLease.ReleaseCount, Is.Zero);
            Assert.That(presentation.PlayedAnimationCount, Is.Zero);
            Assert.That(presentation.PlayedPoseCount, Is.Zero);

            slot.Dispose();
            Assert.That(oldLease.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public void UnequipFailure_RestoresOldPresentationBeforeReportingFailure()
        {
            InitializeTags();
            WeaponAnimationDefinition rifle = CreateDefinition("rifle", supportsFire: true);
            WeaponAnimationDefinition knife = CreateDefinition("knife", supportsFire: false);
            var oldLease = new FakeLease(rifle);
            var targetLease = new FakeLease(knife);
            var presentation = new FakeSwitchPresentation
            {
                FailNextAnimation = true
            };
            var pawn = new Pawn();
            var controller = new Controller();
            controller.PossessingPawn(pawn);
            Assert.That(controller.InitializeWeapon(rifle.WeaponId, rifle.Capabilities), Is.True);
            var abilitySystem = new AbilitySystemComponent(pawn);
            pawn.BindingAbilitySystem(abilitySystem);
            var slot = new EquipmentSlot(
                abilitySystem,
                controller.WeaponRuntime,
                presentation,
                new FakeDefinitionLoader(targetLease),
                presentation);
            Assert.That(slot.TryEquip(
                    oldLease,
                    new[]
                    {
                        WeaponActionAbilitySetFactory.Create(rifle),
                        WeaponSwitchAbilitySetFactory.Create()
                    },
                    out EquipmentInstance oldEquipment),
                Is.EqualTo(EquipmentEquipResult.Equipped));
            WeaponSwitchFact terminal = default;
            controller.WeaponRuntime.SwitchChanged += fact =>
            {
                if (fact.Phase != WeaponSwitchPhase.Started)
                {
                    terminal = fact;
                }
            };

            Assert.That(controller.RequestSwitchWeapon(knife.WeaponId, out _),
                Is.EqualTo(WeaponSwitchRequestResult.Started));

            Assert.That(controller.WeaponRuntime.ActiveSwitch.IsValid, Is.True,
                "The switch must remain active until the old presentation is restored.");
            Assert.That(presentation.PlayedAnimationCount, Is.EqualTo(2));
            Assert.That(presentation.PlayedPoseCount, Is.EqualTo(1));
            Assert.That(slot.Current, Is.SameAs(oldEquipment));

            presentation.CompleteNextAnimation();

            Assert.That(controller.WeaponRuntime.ActiveSwitch.IsValid, Is.False);
            Assert.That(terminal.Phase, Is.EqualTo(WeaponSwitchPhase.Failed));
            Assert.That(terminal.EndReason,
                Is.EqualTo(WeaponSwitchEndReason.UnequipFailed));
            Assert.That(slot.Current, Is.SameAs(oldEquipment));
            Assert.That(oldEquipment.IsDisposed, Is.False);
            Assert.That(oldLease.ReleaseCount, Is.Zero);
            Assert.That(targetLease.ReleaseCount, Is.EqualTo(1));
            Assert.That(presentation.PromotedPoseCount, Is.EqualTo(1));

            slot.Dispose();
            Assert.That(oldLease.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public void SwitchAbilityActivationFailure_ReportsCorrelatedFailureReason()
        {
            InitializeTags();
            WeaponAnimationDefinition rifle = CreateDefinition("rifle", supportsFire: true);
            var pawn = new Pawn();
            var controller = new Controller();
            controller.PossessingPawn(pawn);
            Assert.That(controller.InitializeWeapon(rifle.WeaponId, rifle.Capabilities), Is.True);
            var abilitySystem = new AbilitySystemComponent(pawn);
            pawn.BindingAbilitySystem(abilitySystem);
            WeaponSwitchFact started = default;
            WeaponSwitchFact terminal = default;
            controller.WeaponRuntime.SwitchChanged += fact =>
            {
                if (fact.Phase == WeaponSwitchPhase.Started)
                {
                    started = fact;
                }
                else
                {
                    terminal = fact;
                }
            };

            WeaponSwitchRequestResult result = controller.RequestSwitchWeapon(
                new WeaponId("knife"),
                out WeaponSwitchFact returned);

            Assert.That(result,
                Is.EqualTo(WeaponSwitchRequestResult.AbilityActivationFailed));
            Assert.That(returned.IsValid, Is.False);
            Assert.That(started.IsValid, Is.True);
            Assert.That(terminal.SwitchId, Is.EqualTo(started.SwitchId));
            Assert.That(terminal.FromWeaponId, Is.EqualTo(started.FromWeaponId));
            Assert.That(terminal.ToWeaponId, Is.EqualTo(started.ToWeaponId));
            Assert.That(terminal.Phase, Is.EqualTo(WeaponSwitchPhase.Failed));
            Assert.That(terminal.EndReason,
                Is.EqualTo(WeaponSwitchEndReason.AbilityActivationFailed));
            Assert.That(controller.WeaponRuntime.ActiveSwitch.IsValid, Is.False);
            Assert.That(controller.WeaponRuntime.Snapshot.EquippedWeaponId,
                Is.EqualTo(rifle.WeaponId));
            Assert.That(controller.WeaponRuntime.Snapshot.Generation, Is.EqualTo(1u));
        }

        [Test]
        public void PresentationReplacement_RollsBackOrCommitsWithoutDestroyingOldEarly()
        {
            WeaponAnimationDefinition rifle = CreateDefinition("rifle", supportsFire: true);
            WeaponAnimationDefinition knife = CreateDefinition("knife", supportsFire: false);
            var character = new GameObject("character");
            objectsToDestroy.Add(character);
            Animator animator = character.AddComponent<Animator>();
            var rightHand = new GameObject("Right_Hand");
            rightHand.transform.SetParent(character.transform, false);

            using (var controller = new CharacterWeaponPresentationController(animator))
            {
                Assert.That(controller.TryEquip(rifle, 1u), Is.True);
                WeaponPresentationInstance oldPresentation =
                    controller.CurrentPresentation;

                CharacterWeaponPresentationReplacement rollback =
                    controller.PrepareReplacement(knife, 2u);

                Assert.That(rollback, Is.Not.Null);
                Assert.That(rollback.IsValid, Is.True);
                Assert.That(controller.CurrentPresentation,
                    Is.SameAs(oldPresentation));
                Assert.That(oldPresentation.gameObject.activeSelf, Is.False);
                Assert.That(rollback.Candidate.gameObject.activeSelf, Is.True);

                WeaponPresentationInstance rolledBackCandidate = rollback.Candidate;
                rollback.Dispose();

                Assert.That(controller.CurrentPresentation,
                    Is.SameAs(oldPresentation));
                Assert.That(oldPresentation.gameObject.activeSelf, Is.True);
                Assert.That(rolledBackCandidate == null, Is.True);

                CharacterWeaponPresentationReplacement commit =
                    controller.PrepareReplacement(knife, 2u);
                WeaponPresentationInstance committedCandidate = commit.Candidate;
                commit.Commit();

                Assert.That(commit.IsValid, Is.False);
                Assert.That(controller.CurrentPresentation,
                    Is.SameAs(committedCandidate));
                Assert.That(controller.CurrentDefinition, Is.SameAs(knife));
                Assert.That(controller.CurrentGeneration, Is.EqualTo(2u));
                Assert.That(oldPresentation == null, Is.True);
            }
        }

        [Test]
        public void YooAssetLoader_UnresolvedLocation_CompletesWithoutLease()
        {
            var loader = new YooAssetEquipmentDefinitionLoader(
                new MissingDefinitionLocationResolver());

            using (IEquipmentDefinitionLoadOperation operation =
                   loader.BeginLoad(new WeaponId("missing")))
            {
                Assert.That(operation, Is.Not.Null);
                Assert.That(operation.IsDone, Is.True);
                Assert.That(operation.TryTakeLease(out _), Is.False);
                Assert.That(operation.TryTakeLease(out _), Is.False);
            }
        }

        [Test]
        public void DefaultGameplayTags_ContainsWeaponSwitchAbilityAndStateTags()
        {
            GameplayTagSource source =
                AssetDatabase.LoadAssetAtPath<GameplayTagSource>(
                    "Assets/Data/GamePlayTag/Sources/DefaultGameplayTags.asset");
            Assert.That(source, Is.Not.Null);
            GameplayTagRegistryBuildResult build =
                GameplayTagManager.Instance.Initialize(new[] { source });

            Assert.That(build.Succeeded, Is.True);
            Assert.That(GameplayTagManager.Instance.IsExplicitTag(
                WeaponSwitchGameplayTags.SwitchAbility), Is.True);
            Assert.That(GameplayTagManager.Instance.IsExplicitTag(
                WeaponSwitchGameplayTags.SwitchState), Is.True);
        }

        [Test]
        public void RestorePlaybackFailure_ReportsRestoreFailedAndKeepsOldEquipment()
        {
            InitializeTags();
            WeaponAnimationDefinition rifle = CreateDefinition("rifle", supportsFire: true);
            WeaponAnimationDefinition knife = CreateDefinition("knife", supportsFire: false);
            var oldLease = new FakeLease(rifle);
            var targetLease = new FakeLease(knife);
            var presentation = new FakeSwitchPresentation();
            presentation.FailedAnimationNumbers.Add(2);
            presentation.FailedAnimationNumbers.Add(3);
            var pawn = new Pawn();
            var controller = new Controller();
            controller.PossessingPawn(pawn);
            Assert.That(controller.InitializeWeapon(rifle.WeaponId, rifle.Capabilities), Is.True);
            var abilitySystem = new AbilitySystemComponent(pawn);
            pawn.BindingAbilitySystem(abilitySystem);
            var slot = new EquipmentSlot(
                abilitySystem,
                controller.WeaponRuntime,
                presentation,
                new FakeDefinitionLoader(targetLease),
                presentation);
            Assert.That(slot.TryEquip(
                    oldLease,
                    new[]
                    {
                        WeaponActionAbilitySetFactory.Create(rifle),
                        WeaponSwitchAbilitySetFactory.Create()
                    },
                    out EquipmentInstance oldEquipment),
                Is.EqualTo(EquipmentEquipResult.Equipped));
            WeaponSwitchFact terminal = default;
            controller.WeaponRuntime.SwitchChanged += fact =>
            {
                if (fact.Phase != WeaponSwitchPhase.Started)
                {
                    terminal = fact;
                }
            };

            Assert.That(controller.RequestSwitchWeapon(knife.WeaponId, out _),
                Is.EqualTo(WeaponSwitchRequestResult.Started));
            presentation.CompleteNextAnimation();

            Assert.That(controller.WeaponRuntime.ActiveSwitch.IsValid, Is.False);
            Assert.That(terminal.Phase, Is.EqualTo(WeaponSwitchPhase.Failed));
            Assert.That(terminal.EndReason,
                Is.EqualTo(WeaponSwitchEndReason.RestoreFailed));
            Assert.That(slot.Current, Is.SameAs(oldEquipment));
            Assert.That(oldEquipment.IsDisposed, Is.False);
            Assert.That(oldLease.ReleaseCount, Is.Zero);
            Assert.That(targetLease.ReleaseCount, Is.EqualTo(1));
            Assert.That(presentation.PreparedReplacement.IsDisposed, Is.True);
            Assert.That(presentation.PromotedPoseCount, Is.Zero);

            slot.Dispose();
            Assert.That(oldLease.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public void SameAndDuplicateRequests_DoNotStartASecondLoadOrSwitch()
        {
            InitializeTags();
            WeaponAnimationDefinition rifle = CreateDefinition("rifle", supportsFire: true);
            WeaponAnimationDefinition knife = CreateDefinition("knife", supportsFire: false);
            var oldLease = new FakeLease(rifle);
            var targetLease = new FakeLease(knife);
            var loader = new FakeDefinitionLoader(targetLease);
            var presentation = new FakeSwitchPresentation();
            var pawn = new Pawn();
            var controller = new Controller();
            controller.PossessingPawn(pawn);
            Assert.That(controller.InitializeWeapon(rifle.WeaponId, rifle.Capabilities), Is.True);
            var abilitySystem = new AbilitySystemComponent(pawn);
            pawn.BindingAbilitySystem(abilitySystem);
            var slot = new EquipmentSlot(
                abilitySystem,
                controller.WeaponRuntime,
                presentation,
                loader,
                presentation);
            Assert.That(slot.TryEquip(
                    oldLease,
                    new[]
                    {
                        WeaponActionAbilitySetFactory.Create(rifle),
                        WeaponSwitchAbilitySetFactory.Create()
                    },
                    out EquipmentInstance oldEquipment),
                Is.EqualTo(EquipmentEquipResult.Equipped));

            Assert.That(controller.RequestSwitchWeapon(rifle.WeaponId, out _),
                Is.EqualTo(WeaponSwitchRequestResult.AlreadyEquipped));
            Assert.That(loader.BeginLoadCount, Is.Zero);

            Assert.That(controller.RequestSwitchWeapon(
                    knife.WeaponId,
                    out WeaponSwitchFact first),
                Is.EqualTo(WeaponSwitchRequestResult.Started));
            Assert.That(loader.BeginLoadCount, Is.EqualTo(1));

            Assert.That(controller.RequestSwitchWeapon(
                    rifle.WeaponId,
                    out WeaponSwitchFact rejected),
                Is.EqualTo(WeaponSwitchRequestResult.AlreadySwitching));
            Assert.That(rejected.IsValid, Is.False);
            Assert.That(loader.BeginLoadCount, Is.EqualTo(1));
            Assert.That(controller.WeaponRuntime.ActiveSwitch.SwitchId,
                Is.EqualTo(first.SwitchId));
            Assert.That(controller.WeaponRuntime.ActiveSwitch.ToWeaponId,
                Is.EqualTo(knife.WeaponId));

            Assert.That(GetSwitchAbility(abilitySystem, oldEquipment)
                    .EndAbility(AbilityEndReason.Cancelled),
                Is.True);
            slot.Dispose();
            Assert.That(oldLease.ReleaseCount, Is.EqualTo(1));
            Assert.That(targetLease.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public void AbilitySystemDisposedDuringTargetPlayback_ReleasesCandidateAndReportsOwnerDisposed()
        {
            InitializeTags();
            WeaponAnimationDefinition rifle = CreateDefinition("rifle", supportsFire: true);
            WeaponAnimationDefinition knife = CreateDefinition("knife", supportsFire: false);
            var oldLease = new FakeLease(rifle);
            var targetLease = new FakeLease(knife);
            var presentation = new FakeSwitchPresentation();
            var pawn = new Pawn();
            var controller = new Controller();
            controller.PossessingPawn(pawn);
            Assert.That(controller.InitializeWeapon(rifle.WeaponId, rifle.Capabilities), Is.True);
            var abilitySystem = new AbilitySystemComponent(pawn);
            pawn.BindingAbilitySystem(abilitySystem);
            var slot = new EquipmentSlot(
                abilitySystem,
                controller.WeaponRuntime,
                presentation,
                new FakeDefinitionLoader(targetLease),
                presentation);
            Assert.That(slot.TryEquip(
                    oldLease,
                    new[]
                    {
                        WeaponActionAbilitySetFactory.Create(rifle),
                        WeaponSwitchAbilitySetFactory.Create()
                    },
                    out EquipmentInstance oldEquipment),
                Is.EqualTo(EquipmentEquipResult.Equipped));
            WeaponSwitchAbilityInstance switchAbility = GetSwitchAbility(
                abilitySystem,
                oldEquipment);
            var terminalFacts = new List<WeaponSwitchFact>();
            controller.WeaponRuntime.SwitchChanged += fact =>
            {
                if (fact.Phase != WeaponSwitchPhase.Started)
                {
                    terminalFacts.Add(fact);
                }
            };

            Assert.That(controller.RequestSwitchWeapon(knife.WeaponId, out _),
                Is.EqualTo(WeaponSwitchRequestResult.Started));
            presentation.CompleteNextAnimation();
            Assert.That(presentation.PreparedReplacement.IsValid, Is.True);

            abilitySystem.Dispose();

            Assert.That(switchAbility.LastEndReason,
                Is.EqualTo(AbilityEndReason.SourceRemoved));
            Assert.That(controller.WeaponRuntime.ActiveSwitch.IsValid, Is.False);
            Assert.That(terminalFacts.Count, Is.EqualTo(1));
            Assert.That(terminalFacts[0].Phase,
                Is.EqualTo(WeaponSwitchPhase.Cancelled));
            Assert.That(terminalFacts[0].EndReason,
                Is.EqualTo(WeaponSwitchEndReason.OwnerDisposed));
            Assert.That(slot.Current, Is.SameAs(oldEquipment));
            Assert.That(oldEquipment.IsDisposed, Is.False);
            Assert.That(oldLease.ReleaseCount, Is.Zero);
            Assert.That(targetLease.ReleaseCount, Is.EqualTo(1));
            Assert.That(presentation.PreparedReplacement.IsDisposed, Is.True);

            slot.Dispose();
            Assert.That(oldLease.ReleaseCount, Is.EqualTo(1));
            Assert.That(targetLease.ReleaseCount, Is.EqualTo(1));
        }

        private WeaponSwitchAbilityInstance GetSwitchAbility(
            AbilitySystemComponent abilitySystem,
            EquipmentInstance equipment)
        {
            return equipment.GrantReceipts
                .SelectMany(receipt => receipt.SpecHandles)
                .Select(handle => abilitySystem.TryGetSpec(handle, out AbilitySpec spec)
                    ? spec.PrimaryInstance
                    : null)
                .OfType<WeaponSwitchAbilityInstance>()
                .Single();
        }

        private void InitializeTags()
        {
            GameplayTagSource source = ScriptableObject.CreateInstance<GameplayTagSource>();
            source.SetDefinition("WeaponSwitchTests", new[]
            {
                Node("Ability", false, Node("Weapon", false,
                    Node("Fire", true),
                    Node("Reload", true),
                    Node("Melee", true),
                    Node("Switch", true))),
                Node("Event", false, Node("Weapon", false,
                    Node("Fire", true),
                    Node("Reload", true),
                    Node("Melee", true))),
                Node("State", false, Node("Weapon", false,
                    Node("Action", true),
                    Node("Switch", true)))
            });
            objectsToDestroy.Add(source);
            GameplayTagRegistryBuildResult result =
                GameplayTagManager.Instance.Initialize(new[] { source });
            Assert.That(result.Succeeded, Is.True,
                string.Join(Environment.NewLine,
                    result.Errors.Select(error => error.ToString())));
        }

        private WeaponAnimationDefinition CreateDefinition(
            string weaponId,
            bool supportsFire)
        {
            AnimationClipAsset clipAsset = CreateClipAsset();
            var prefab = new GameObject($"{weaponId}-presentation");
            objectsToDestroy.Add(prefab);
            var mount = new GameObject("RightHandMount");
            mount.transform.SetParent(prefab.transform, false);
            WeaponPresentationInstance presentation =
                prefab.AddComponent<WeaponPresentationInstance>();
            var serializedPresentation = new SerializedObject(presentation);
            serializedPresentation.FindProperty("rightHandMount")
                .objectReferenceValue = mount.transform;
            serializedPresentation.ApplyModifiedPropertiesWithoutUndo();
            var definition = ScriptableObject.CreateInstance<WeaponAnimationDefinition>();
            objectsToDestroy.Add(definition);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("weaponId").stringValue = weaponId;
            serialized.FindProperty("weaponPrefab").objectReferenceValue = prefab;
            serialized.FindProperty("overlayPose").objectReferenceValue = clipAsset;
            serialized.FindProperty("equip").objectReferenceValue = clipAsset;
            serialized.FindProperty("unequip").objectReferenceValue = clipAsset;
            serialized.FindProperty("supportsFire").boolValue = supportsFire;
            serialized.FindProperty("supportsMeleeAttack").boolValue = !supportsFire;
            serialized.FindProperty(supportsFire ? "fire" : "meleeAttack")
                .objectReferenceValue = clipAsset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(definition.IsValid, Is.True);
            return definition;
        }

        private AnimationClipAsset CreateClipAsset()
        {
            var clip = new AnimationClip();
            objectsToDestroy.Add(clip);
            var asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            Assert.That(asset.TryInitialize(clip), Is.True);
            objectsToDestroy.Add(asset);
            return asset;
        }

        private static GameplayTagSourceNode Node(
            string segment,
            bool explicitTag,
            params GameplayTagSourceNode[] children)
        {
            return new GameplayTagSourceNode(
                segment,
                explicitTag,
                children: children);
        }

        private sealed class FakeDefinitionLoader : IEquipmentDefinitionLoader
        {
            private readonly FakeLease lease;

            public FakeDefinitionLoader(FakeLease lease)
            {
                this.lease = lease;
            }

            public int BeginLoadCount { get; private set; }

            public IEquipmentDefinitionLoadOperation BeginLoad(WeaponId weaponId)
            {
                BeginLoadCount++;
                return new FakeLoadOperation(weaponId == lease.WeaponId ? lease : null);
            }
        }

        private sealed class MissingDefinitionLocationResolver :
            IWeaponAnimationDefinitionLocationResolver
        {
            public bool TryResolveLocation(
                WeaponId weaponId,
                out string location)
            {
                location = string.Empty;
                return false;
            }
        }

        private sealed class FakeLoadOperation : IEquipmentDefinitionLoadOperation
        {
            private IEquipmentDefinitionLease lease;

            public FakeLoadOperation(IEquipmentDefinitionLease lease)
            {
                this.lease = lease;
            }

            public bool IsDone => true;

            public bool TryTakeLease(out IEquipmentDefinitionLease definitionLease)
            {
                definitionLease = lease;
                lease = null;
                return definitionLease != null;
            }

            public void Dispose()
            {
                lease?.Dispose();
                lease = null;
            }
        }

        private sealed class FakeSwitchPresentation : IWeaponSwitchPresentation
        {
            private readonly Queue<FakePlayback> animations =
                new Queue<FakePlayback>();

            public event Action Updated;
            public FakePresentationReplacement PreparedReplacement { get; private set; }
            public int PromotedPoseCount { get; private set; }
            public int PlayedPoseCount { get; private set; }
            public int PlayedAnimationCount { get; private set; }
            public bool FailNextAnimation { get; set; }
            public HashSet<int> FailedAnimationNumbers { get; } =
                new HashSet<int>();

            public IAbilityAnimationPlayback PlayAnimation(
                AnimationClipAsset asset,
                long requestId)
            {
                PlayedAnimationCount++;
                bool shouldFail = FailNextAnimation
                    || FailedAnimationNumbers.Contains(PlayedAnimationCount);
                var playback = new FakePlayback(
                    shouldFail
                        ? AbilityAnimationPlaybackState.Failed
                        : AbilityAnimationPlaybackState.Playing);
                FailNextAnimation = false;
                animations.Enqueue(playback);
                return playback;
            }

            public IAbilityAnimationPlayback PlayPose(
                AnimationClipAsset asset,
                long requestId)
            {
                PlayedPoseCount++;
                return new FakePlayback(AbilityAnimationPlaybackState.Playing);
            }

            public bool StopAnimation(IAbilityAnimationPlayback playback)
            {
                if (playback is FakePlayback fake && !fake.IsTerminal)
                {
                    fake.State = AbilityAnimationPlaybackState.Cancelled;
                    return true;
                }

                return false;
            }

            public IWeaponSwitchPresentationReplacement PrepareReplacement(
                WeaponAnimationDefinition definition,
                uint generation)
            {
                PreparedReplacement = new FakePresentationReplacement();
                return PreparedReplacement;
            }

            public void PromotePose(IAbilityAnimationPlayback playback)
            {
                PromotedPoseCount++;
            }

            public void CompleteNextAnimation()
            {
                FakePlayback playback;
                do
                {
                    playback = animations.Dequeue();
                }
                while (playback.IsTerminal);

                playback.State = AbilityAnimationPlaybackState.Completed;
                Updated?.Invoke();
            }
        }

        private sealed class FakePresentationReplacement :
            IWeaponSwitchPresentationReplacement
        {
            public bool IsValid => !IsDisposed;
            public bool IsDisposed { get; private set; }
            public int CommitCount { get; private set; }

            public void Commit()
            {
                CommitCount++;
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        private sealed class FakePlayback : IAbilityAnimationPlayback
        {
            public FakePlayback(AbilityAnimationPlaybackState state)
            {
                State = state;
            }

            public AbilityAnimationPlaybackState State { get; set; }
            public bool IsTerminal => State == AbilityAnimationPlaybackState.Completed
                || State == AbilityAnimationPlaybackState.Cancelled
                || State == AbilityAnimationPlaybackState.Interrupted
                || State == AbilityAnimationPlaybackState.Failed;
        }

        private sealed class FakeLease : IEquipmentDefinitionLease
        {
            public FakeLease(WeaponAnimationDefinition definition)
            {
                Definition = definition;
            }

            public WeaponAnimationDefinition Definition { get; }
            public WeaponId WeaponId => Definition.WeaponId;
            public bool IsValid => !IsDisposed;
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
    }
}
