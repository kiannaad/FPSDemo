using System;
using System.Collections.Generic;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.Tests
{
    public sealed class WeaponSwitchPipelineTests
    {
        [Test]
        public void Runtime_SwitchIsTransactionalAndLocksAllWeaponRequests()
        {
            var runtime = CreateKnifeRuntime();
            var switchFacts = new List<WeaponSwitchFact>();
            var equipmentFacts =
                new List<WeaponEquipmentSnapshot>();
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
            Assert.IsFalse(runtime.CompleteSwitch(
                started.SwitchId + 1,
                new WeaponRuntimeCapabilities(
                    true,
                    true,
                    false)));

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
        public void Runtime_SwitchCancelsActionAndFailureKeepsOldTruth()
        {
            var runtime = CreateKnifeRuntime();
            var actionFacts = new List<WeaponActionFact>();
            var switchFacts = new List<WeaponSwitchFact>();
            runtime.ActionChanged += actionFacts.Add;
            runtime.SwitchChanged += switchFacts.Add;
            Assert.IsTrue(runtime.RequestPrimaryAction(
                out WeaponActionFact action));

            Assert.AreEqual(
                WeaponSwitchRequestResult.Started,
                runtime.RequestSwitchWeapon(
                    new WeaponId("rifle"),
                    out WeaponSwitchFact started));
            Assert.IsFalse(runtime.ActiveAction.IsValid);
            Assert.AreEqual(
                action.ActionId,
                actionFacts[actionFacts.Count - 1].ActionId);
            Assert.AreEqual(
                WeaponActionPhase.Cancelled,
                actionFacts[actionFacts.Count - 1].Phase);

            Assert.IsTrue(runtime.FailSwitch(
                started.SwitchId,
                WeaponSwitchEndReason.TargetLoadFailed));
            Assert.AreEqual(
                new WeaponId("knife"),
                runtime.Snapshot.EquippedWeaponId);
            Assert.AreEqual(1u, runtime.Snapshot.Generation);
            Assert.IsTrue(runtime.Capabilities.SupportsMeleeAttack);
            Assert.AreEqual(
                WeaponSwitchPhase.Failed,
                switchFacts[switchFacts.Count - 1].Phase);
        }

        [Test]
        public void Adapter_PreservesActionCancellationBeforeSwitchAndSeedsSwitch()
        {
            var firstRuntime = CreateKnifeRuntime();
            var secondRuntime = CreateKnifeRuntime();
            using (var adapter =
                   new CharacterWeaponAnimationAdapter())
            {
                adapter.BindRuntime(firstRuntime);
                firstRuntime.RequestPrimaryAction(
                    out WeaponActionFact action);
                firstRuntime.RequestSwitchWeapon(
                    new WeaponId("rifle"),
                    out WeaponSwitchFact weaponSwitch);

                Assert.IsTrue(adapter.TryDequeueEvent(
                    out WeaponAnimationEvent first));
                Assert.AreEqual(
                    WeaponAnimationEventKind.Action,
                    first.Kind);
                Assert.AreEqual(
                    action.ActionId,
                    first.Action.ActionId);
                Assert.IsTrue(adapter.TryDequeueEvent(
                    out WeaponAnimationEvent second));
                Assert.AreEqual(
                    WeaponActionPhase.Cancelled,
                    second.Action.Phase);
                Assert.IsTrue(adapter.TryDequeueEvent(
                    out WeaponAnimationEvent third));
                Assert.AreEqual(
                    WeaponAnimationEventKind.Switch,
                    third.Kind);
                Assert.AreEqual(
                    weaponSwitch.SwitchId,
                    third.Switch.SwitchId);

                secondRuntime.RequestSwitchWeapon(
                    new WeaponId("rifle"),
                    out WeaponSwitchFact seeded);
                adapter.BindRuntime(secondRuntime);
                Assert.AreEqual(1, adapter.PendingEventCount);
                Assert.IsTrue(adapter.TryDequeueEvent(
                    out WeaponAnimationEvent seed));
                Assert.AreEqual(
                    seeded.SwitchId,
                    seed.Switch.SwitchId);
            }
        }

        [Test]
        public void Sequencer_LoadsUnequipsCrossfadesEquipsAndCommitsAtomically()
        {
            int knifeReleases = 0;
            int rifleReleases = 0;
            using (var fixture = new SwitchFixture())
            {
                WeaponAnimationDefinition rifle =
                    CreateRifleDefinition(fixture.Knife);
                var provider =
                    new InMemoryWeaponAnimationDefinitionProvider(
                        new[] { fixture.Knife, rifle },
                        weaponId =>
                        {
                            if (weaponId == new WeaponId("rifle"))
                            {
                                rifleReleases++;
                            }
                        });
                var knifeLease =
                    new ResolvedWeaponAnimationDefinitionLease(
                        fixture.Knife,
                        () => knifeReleases++);
                var sequencer = new WeaponAnimationSequencer(
                    fixture.Controller,
                    provider,
                    knifeLease,
                    fixture.InitialOverlay);
                var runtime = CreateKnifeRuntime();
                sequencer.BindRuntime(runtime);

                Assert.AreEqual(
                    WeaponSwitchRequestResult.Started,
                    runtime.RequestSwitchWeapon(
                        new WeaponId("rifle"),
                        out WeaponSwitchFact weaponSwitch));
                Assert.IsTrue(sequencer.Consume(weaponSwitch));
                Assert.AreEqual(
                    WeaponSwitchAnimationStage.Loading,
                    sequencer.SwitchStage);
                sequencer.Update();
                Assert.AreEqual(
                    WeaponSwitchAnimationStage.Unequipping,
                    sequencer.SwitchStage);
                Assert.AreEqual(
                    new WeaponId("knife"),
                    runtime.Snapshot.EquippedWeaponId);

                Complete(
                    fixture.Controller,
                    sequencer.UnequipHandle,
                    fixture.Knife.Unequip);
                sequencer.Update();
                Assert.AreEqual(
                    WeaponSwitchAnimationStage.WaitingForTarget,
                    sequencer.SwitchStage);
                Assert.AreEqual(
                    new WeaponId("knife"),
                    runtime.Snapshot.EquippedWeaponId);

                MakePlaying(
                    fixture.Controller,
                    sequencer.TargetOverlayHandle,
                    rifle.OverlayPose);
                Complete(
                    fixture.Controller,
                    sequencer.TargetEquipHandle,
                    rifle.Equip);
                sequencer.Update();

                Assert.AreEqual(
                    new WeaponId("rifle"),
                    runtime.Snapshot.EquippedWeaponId);
                Assert.IsTrue(runtime.Capabilities.SupportsFire);
                Assert.IsFalse(runtime.IsSwitching);
                Assert.AreEqual(
                    AnimationPlaybackState.Interrupted,
                    fixture.InitialOverlay.State,
                    "The old overlay must stop before its definition lease is released.");
                Assert.AreEqual(1, knifeReleases);
                Assert.AreEqual(0, rifleReleases);
                Assert.AreSame(
                    rifle,
                    sequencer.CurrentDefinition);

                sequencer.Dispose();
                provider.Dispose();
                Assert.AreEqual(1, rifleReleases);
                UnityEngine.Object.DestroyImmediate(rifle);
            }
        }

        [Test]
        public void Sequencer_LoadFailureDoesNotPlayUnequipOrChangeOldWeapon()
        {
            int knifeReleases = 0;
            using (var fixture = new SwitchFixture())
            using (var provider =
                   new InMemoryWeaponAnimationDefinitionProvider(
                       new[] { fixture.Knife }))
            {
                var sequencer = new WeaponAnimationSequencer(
                    fixture.Controller,
                    provider,
                    new ResolvedWeaponAnimationDefinitionLease(
                        fixture.Knife,
                        () => knifeReleases++),
                    fixture.InitialOverlay);
                var runtime = CreateKnifeRuntime();
                var facts = new List<WeaponSwitchFact>();
                runtime.SwitchChanged += facts.Add;
                sequencer.BindRuntime(runtime);
                runtime.RequestSwitchWeapon(
                    new WeaponId("missing"),
                    out WeaponSwitchFact weaponSwitch);
                Assert.IsTrue(sequencer.Consume(weaponSwitch));

                sequencer.Update();

                Assert.AreEqual(
                    new WeaponId("knife"),
                    runtime.Snapshot.EquippedWeaponId);
                Assert.AreEqual(
                    WeaponSwitchEndReason.TargetLoadFailed,
                    facts[facts.Count - 1].EndReason);
                Assert.AreEqual(
                    AnimationPlaybackState.Playing,
                    fixture.InitialOverlay.State);
                Assert.IsNull(sequencer.UnequipHandle);
                sequencer.Dispose();
                Assert.AreEqual(1, knifeReleases);
            }
        }

        [Test]
        public void Sequencer_TargetPlaybackFailureRestoresOldOverlayBeforeFailing()
        {
            int rifleReleases = 0;
            using (var fixture = new SwitchFixture())
            {
                WeaponAnimationDefinition rifle =
                    CreateRifleDefinition(fixture.Knife);
                using (var provider =
                       new InMemoryWeaponAnimationDefinitionProvider(
                           new[] { fixture.Knife, rifle },
                           weaponId =>
                           {
                               if (weaponId
                                   == new WeaponId("rifle"))
                               {
                                   rifleReleases++;
                               }
                           }))
                {
                    var sequencer =
                        new WeaponAnimationSequencer(
                            fixture.Controller,
                            provider,
                            new ResolvedWeaponAnimationDefinitionLease(
                                fixture.Knife),
                            fixture.InitialOverlay);
                    var runtime = CreateKnifeRuntime();
                    var facts = new List<WeaponSwitchFact>();
                    runtime.SwitchChanged += facts.Add;
                    sequencer.BindRuntime(runtime);
                    runtime.RequestSwitchWeapon(
                        new WeaponId("rifle"),
                        out WeaponSwitchFact weaponSwitch);
                    Assert.IsTrue(sequencer.Consume(weaponSwitch));
                    sequencer.Update();

                    SetAsset(rifle, "equip", null);
                    LogAssert.Expect(
                        LogType.Error,
                        "An AnimationClipAsset is required.");
                    Complete(
                        fixture.Controller,
                        sequencer.UnequipHandle,
                        fixture.Knife.Unequip);
                    sequencer.Update();
                    Assert.AreEqual(
                        WeaponSwitchAnimationStage.Restoring,
                        sequencer.SwitchStage);
                    Assert.IsTrue(runtime.IsSwitching);

                    MakePlaying(
                        fixture.Controller,
                        sequencer.RestoreOverlayHandle,
                        fixture.Knife.OverlayPose);
                    Complete(
                        fixture.Controller,
                        sequencer.RestoreEquipHandle,
                        fixture.Knife.Equip);
                    sequencer.Update();

                    Assert.IsFalse(runtime.IsSwitching);
                    Assert.AreEqual(
                        new WeaponId("knife"),
                        runtime.Snapshot.EquippedWeaponId);
                    Assert.AreEqual(
                        WeaponSwitchEndReason.TargetPlaybackFailed,
                        facts[facts.Count - 1].EndReason);
                    Assert.AreEqual(1, rifleReleases);
                    Assert.AreEqual(
                        AnimationPlaybackState.Playing,
                        sequencer.CurrentOverlayHandle.State);
                    sequencer.Dispose();
                }

                UnityEngine.Object.DestroyImmediate(rifle);
            }
        }

        [Test]
        public void Sequencer_DisposeCancelsLoadSwitchAndReleasesCurrentLease()
        {
            int currentReleases = 0;
            var provider = new DeferredProvider();
            using (var fixture = new SwitchFixture())
            {
                var sequencer = new WeaponAnimationSequencer(
                    fixture.Controller,
                    provider,
                    new ResolvedWeaponAnimationDefinitionLease(
                        fixture.Knife,
                        () => currentReleases++),
                    fixture.InitialOverlay);
                var runtime = CreateKnifeRuntime();
                var facts = new List<WeaponSwitchFact>();
                runtime.SwitchChanged += facts.Add;
                sequencer.BindRuntime(runtime);
                runtime.RequestSwitchWeapon(
                    new WeaponId("rifle"),
                    out WeaponSwitchFact weaponSwitch);
                Assert.IsTrue(sequencer.Consume(weaponSwitch));

                sequencer.Dispose();

                Assert.IsTrue(provider.Operation.IsDisposed);
                Assert.AreEqual(1, currentReleases);
                Assert.IsFalse(runtime.IsSwitching);
                Assert.AreEqual(
                    WeaponSwitchEndReason.OwnerDisposed,
                    facts[facts.Count - 1].EndReason);
            }

            provider.Dispose();
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

        private static WeaponAnimationDefinition
            CreateRifleDefinition(
                WeaponAnimationDefinition source)
        {
            var definition =
                ScriptableObject.CreateInstance<
                    WeaponAnimationDefinition>();
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("weaponId").stringValue =
                "rifle";
            serialized.FindProperty("weaponPrefab")
                .objectReferenceValue = source.WeaponPrefab;
            serialized.FindProperty("overlayPose")
                .objectReferenceValue = source.OverlayPose;
            serialized.FindProperty("equip")
                .objectReferenceValue = source.Equip;
            serialized.FindProperty("unequip")
                .objectReferenceValue = source.Unequip;
            serialized.FindProperty("supportsFire").boolValue =
                true;
            serialized.FindProperty("fire")
                .objectReferenceValue = source.MeleeAttack;
            serialized.FindProperty("supportsReload").boolValue =
                false;
            serialized.FindProperty("reload")
                .objectReferenceValue = null;
            serialized.FindProperty("supportsMeleeAttack")
                .boolValue = false;
            serialized.FindProperty("meleeAttack")
                .objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.AreEqual(
                WeaponAnimationDefinitionError.None,
                definition.Validate(new WeaponId("rifle")));
            return definition;
        }

        private static void SetAsset(
            WeaponAnimationDefinition definition,
            string propertyName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty(propertyName)
                .objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void MakePlaying(
            CharacterPlayablesController controller,
            AnimationPlaybackHandle handle,
            AnimationClipAsset asset)
        {
            Assert.NotNull(handle);
            Assert.IsTrue(controller.TrySetPlaybackTime(
                handle,
                asset.BlendInTime + 0.05f));
            controller.Update(0.016f);
            Assert.AreEqual(
                AnimationPlaybackState.Playing,
                handle.State);
        }

        private static void Complete(
            CharacterPlayablesController controller,
            AnimationPlaybackHandle handle,
            AnimationClipAsset asset)
        {
            Assert.NotNull(handle);
            Assert.IsTrue(controller.TrySetPlaybackTime(
                handle,
                asset.AnimationClip.length
                + asset.BlendOutTime
                + 0.05f));
            controller.Update(0.016f);
            Assert.AreEqual(
                AnimationPlaybackState.Completed,
                handle.State);
        }

        private sealed class SwitchFixture : IDisposable
        {
            private const string CharacterPrefabPath =
                "Assets/Art/Character/Kinemation/"
                + "KinemationVisualCharacter.prefab";
            private readonly GameObject visual;

            public SwitchFixture()
            {
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        CharacterPrefabPath);
                Assert.NotNull(prefab);
                visual = UnityEngine.Object.Instantiate(prefab);
                Animator animator =
                    visual.GetComponentInChildren<Animator>();
                Assert.NotNull(animator);
                Knife =
                    Resources.Load<WeaponAnimationDefinition>(
                        "FistsWeaponAnimationDefinition");
                Assert.NotNull(Knife);
                Controller = new CharacterPlayablesController(
                    animator,
                    Resources.Load<CharacterAnimationConfig>(
                        "CharacterAnimationConfig")
                        .UpperBodyMask);
                InitialOverlay =
                    Controller.PlayPoseImmediate(
                        Knife.OverlayPose,
                        100);
                Assert.AreEqual(
                    AnimationPlaybackState.Playing,
                    InitialOverlay.State);
            }

            public CharacterPlayablesController Controller {
                get;
            }
            public WeaponAnimationDefinition Knife { get; }
            public AnimationPlaybackHandle InitialOverlay { get; }

            public void Dispose()
            {
                Controller.Dispose();
                UnityEngine.Object.DestroyImmediate(visual);
            }
        }

        [Test]
        public void Sequencer_RebuildsCurrentOverlayAfterGraphCancellation()
        {
            using (var fixture = new SwitchFixture())
            {
                var sequencer = new WeaponAnimationSequencer(
                    fixture.Controller,
                    null,
                    new ResolvedWeaponAnimationDefinitionLease(
                        fixture.Knife),
                    fixture.InitialOverlay);
                var runtime = CreateKnifeRuntime();
                sequencer.BindRuntime(runtime);
                AnimationPlaybackHandle initial =
                    fixture.InitialOverlay;

                fixture.Controller.RestoreNativeOutput();
                Assert.AreEqual(
                    AnimationPlaybackState.Cancelled,
                    initial.State);

                sequencer.Update();

                Assert.NotNull(
                    sequencer.CurrentOverlayHandle);
                Assert.AreNotSame(
                    initial,
                    sequencer.CurrentOverlayHandle);
                Assert.AreEqual(
                    AnimationPlaybackState.Playing,
                    sequencer.CurrentOverlayHandle.State);
                Assert.AreEqual(
                    new WeaponId("knife"),
                    sequencer.CurrentDefinition.WeaponId);
                sequencer.Dispose();
            }
        }

        private sealed class DeferredProvider :
            IWeaponAnimationDefinitionProvider
        {
            public DeferredOperation Operation { get; private set; }

            public IWeaponAnimationDefinitionResolveOperation
                BeginResolve(WeaponId weaponId)
            {
                Operation = new DeferredOperation();
                return Operation;
            }

            public void Dispose()
            {
                Operation?.Dispose();
            }
        }

        private sealed class DeferredOperation :
            IWeaponAnimationDefinitionResolveOperation
        {
            public bool IsCompleted => false;
            public bool IsDisposed { get; private set; }
            public WeaponAnimationDefinitionResolveResult Result =>
                throw new InvalidOperationException();

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}
