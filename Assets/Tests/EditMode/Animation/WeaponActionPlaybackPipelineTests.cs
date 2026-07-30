using System;
using System.Collections.Generic;
using CGame.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.Tests
{
    public sealed class WeaponActionPlaybackPipelineTests
    {
        [Test]
        public void Runtime_ResolvesPrimaryFromCapabilitiesAndKeepsOneActiveAction()
        {
            var meleeRuntime = new WeaponRuntime();
            Assert.IsTrue(meleeRuntime.Initialize(
                new WeaponId("knife"),
                new WeaponRuntimeCapabilities(false, false, true)));
            Assert.IsTrue(
                meleeRuntime.RequestPrimaryAction(
                    out WeaponActionFact melee));
            Assert.AreEqual(WeaponActionKind.MeleeAttack, melee.Kind);
            Assert.IsFalse(meleeRuntime.RequestReload(out _));

            var rifleRuntime = new WeaponRuntime();
            Assert.IsTrue(rifleRuntime.Initialize(
                new WeaponId("rifle"),
                new WeaponRuntimeCapabilities(true, true, false)));
            Assert.IsTrue(
                rifleRuntime.RequestPrimaryAction(
                    out WeaponActionFact fire));
            Assert.AreEqual(WeaponActionKind.Fire, fire.Kind);
            Assert.IsTrue(
                rifleRuntime.RequestReload(
                    out WeaponActionFact reload));
            Assert.AreEqual(WeaponActionKind.Reload, reload.Kind);
            Assert.AreEqual(
                reload.ActionId,
                rifleRuntime.ActiveAction.ActionId);
        }

        [Test]
        public void Adapter_BuffersOrderedFactsAndSeedsOnlyTheCurrentRuntime()
        {
            var firstRuntime = CreateKnifeRuntime();
            var secondRuntime = CreateKnifeRuntime();
            using (var adapter =
                   new CharacterWeaponAnimationAdapter())
            {
                adapter.BindRuntime(firstRuntime);
                firstRuntime.RequestPrimaryAction(
                    out WeaponActionFact first);
                Assert.IsFalse(firstRuntime.RequestPrimaryAction(out _));

                Assert.AreEqual(1, adapter.PendingActionCount);
                Assert.IsTrue(adapter.TryDequeue(out WeaponActionFact fact0));
                Assert.AreEqual(first.ActionId, fact0.ActionId);

                secondRuntime.RequestPrimaryAction(
                    out WeaponActionFact seeded);
                adapter.BindRuntime(secondRuntime);
                Assert.AreEqual(1, adapter.PendingActionCount);
                Assert.IsTrue(adapter.TryDequeue(out WeaponActionFact seed));
                Assert.AreEqual(seeded.ActionId, seed.ActionId);

                firstRuntime.RequestPrimaryAction(out _);
                Assert.AreEqual(0, adapter.PendingActionCount);
            }
        }

        [Test]
        public void Sequencer_IsIdempotentSamplesSlotCurveAndCompletesMatchingAction()
        {
            using (var fixture = new PlayablesFixture())
            {
                var runtime = CreateKnifeRuntime();
                var facts = new List<WeaponActionFact>();
                runtime.ActionChanged += facts.Add;
                using (var sequencer = new WeaponAnimationSequencer(
                           fixture.Controller,
                           fixture.Definition))
                {
                    sequencer.BindRuntime(runtime);
                    runtime.RequestPrimaryAction(
                        out WeaponActionFact melee);

                    Assert.IsTrue(sequencer.Consume(melee));
                    AnimationPlaybackHandle handle =
                        sequencer.CurrentHandle;
                    Assert.NotNull(handle);
                    Assert.AreEqual(1, fixture.Controller.SlotActiveSlotCount);
                    Assert.IsTrue(sequencer.Consume(melee));
                    Assert.AreSame(handle, sequencer.CurrentHandle);
                    Assert.AreEqual(1, fixture.Controller.SlotActiveSlotCount);

                    Assert.IsTrue(fixture.Controller.TrySetPlaybackTime(
                        handle,
                        fixture.Definition.MeleeAttack.AnimationClip.length
                        * 0.426239));
                    fixture.Controller.Update(0.016f);
                    Assert.Greater(
                        fixture.Controller.GetCurveValue(
                            "PelvisYawOffset"),
                        1f);

                    double terminalTime =
                        fixture.Definition.MeleeAttack.AnimationClip.length
                        + fixture.Definition.MeleeAttack.BlendOutTime;
                    Assert.IsTrue(fixture.Controller.TrySetPlaybackTime(
                        handle,
                        terminalTime));
                    fixture.Controller.Update(0.016f);
                    sequencer.Update();

                    Assert.IsFalse(runtime.ActiveAction.IsValid);
                    Assert.AreEqual(
                        WeaponActionPhase.Completed,
                        facts[facts.Count - 1].Phase);
                    Assert.AreEqual(
                        WeaponActionEndReason.Completed,
                        facts[facts.Count - 1].EndReason);
                }
            }
        }

        [Test]
        public void Sequencer_RepeatedMeleeKeepsTheCurrentBusinessAction()
        {
            using (var fixture = new PlayablesFixture())
            using (var adapter = new CharacterWeaponAnimationAdapter())
            using (var sequencer = new WeaponAnimationSequencer(
                       fixture.Controller,
                       fixture.Definition))
            {
                var runtime = CreateKnifeRuntime();
                adapter.BindRuntime(runtime);
                sequencer.BindRuntime(runtime);
                runtime.RequestPrimaryAction(out WeaponActionFact first);
                Drain(adapter, sequencer);
                AnimationPlaybackHandle firstHandle =
                    sequencer.CurrentHandle;

                Assert.IsFalse(runtime.RequestPrimaryAction(out _));
                Drain(adapter, sequencer);

                Assert.AreEqual(
                    first.ActionId,
                    runtime.ActiveAction.ActionId);
                Assert.AreEqual(
                    first.ActionId,
                    sequencer.CurrentAction.ActionId);
                Assert.AreSame(
                    firstHandle,
                    sequencer.CurrentHandle);
            }
        }

        [Test]
        public void Sequencer_PlaybackFailureAndDisposeCancelOnlyMatchingAction()
        {
            using (var fixture = new PlayablesFixture())
            {
                var runtime = CreateKnifeRuntime();
                var facts = new List<WeaponActionFact>();
                runtime.ActionChanged += facts.Add;
                var sequencer = new WeaponAnimationSequencer(
                    fixture.Controller,
                    fixture.Definition);
                sequencer.BindRuntime(runtime);
                runtime.RequestPrimaryAction(
                    out WeaponActionFact action);
                fixture.Controller.Dispose();

                Assert.IsFalse(sequencer.Consume(action));
                Assert.IsFalse(runtime.ActiveAction.IsValid);
                Assert.AreEqual(
                    WeaponActionEndReason.AnimationFailed,
                    facts[facts.Count - 1].EndReason);

                var secondRuntime = CreateKnifeRuntime();
                using (var secondFixture = new PlayablesFixture())
                {
                    var secondSequencer =
                        new WeaponAnimationSequencer(
                            secondFixture.Controller,
                            secondFixture.Definition);
                    secondSequencer.BindRuntime(secondRuntime);
                    secondRuntime.RequestPrimaryAction(
                        out WeaponActionFact second);
                    Assert.IsTrue(secondSequencer.Consume(second));
                    secondSequencer.Dispose();
                    Assert.IsFalse(secondRuntime.ActiveAction.IsValid);
                }

                sequencer.Dispose();
            }
        }

        [Test]
        public void AnimInstance_CallbackOnlyBuffersUntilAnimationUpdate()
        {
            GameObject visual = null;
            CharacterAnimInstance instance = null;
            try
            {
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        PlayablesFixture.CharacterPrefabPath);
                visual = UnityEngine.Object.Instantiate(prefab);
                Animator animator =
                    visual.GetComponentInChildren<Animator>();
                instance = new CharacterAnimInstance(
                    new StaticCharacterSource(visual.transform),
                    animator,
                    Resources.Load<CharacterAnimationConfig>(
                        "CharacterAnimationConfig").UpperBodyMask,
                    PlayablesFixture.LoadDefinition());
                var runtime = CreateKnifeRuntime();
                instance.UpdateAnimation(0.016f, runtime);
                Assert.AreEqual(
                    0,
                    instance.PlayablesController.SlotActiveSlotCount);

                runtime.RequestPrimaryAction(out _);
                Assert.AreEqual(
                    0,
                    instance.PlayablesController.SlotActiveSlotCount);

                instance.UpdateAnimation(0.016f, runtime);
                Assert.AreEqual(
                    1,
                    instance.PlayablesController.SlotActiveSlotCount);
            }
            finally
            {
                instance?.Dispose();
                if (visual != null)
                {
                    UnityEngine.Object.DestroyImmediate(visual);
                }
            }
        }

        private static WeaponRuntime CreateKnifeRuntime()
        {
            var runtime = new WeaponRuntime();
            Assert.IsTrue(runtime.Initialize(
                new WeaponId("knife"),
                new WeaponRuntimeCapabilities(false, false, true)));
            return runtime;
        }

        private static void Drain(
            CharacterWeaponAnimationAdapter adapter,
            WeaponAnimationSequencer sequencer)
        {
            while (adapter.TryDequeue(out WeaponActionFact fact))
            {
                sequencer.Consume(fact);
            }
        }

        private sealed class PlayablesFixture : IDisposable
        {
            public const string CharacterPrefabPath =
                "Assets/Art/Character/Kinemation/KinemationVisualCharacter.prefab";
            private readonly GameObject visual;

            public PlayablesFixture()
            {
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        CharacterPrefabPath);
                Assert.NotNull(prefab);
                visual = UnityEngine.Object.Instantiate(prefab);
                Animator animator =
                    visual.GetComponentInChildren<Animator>();
                Assert.NotNull(animator);
                Definition = LoadDefinition();
                Controller = new CharacterPlayablesController(
                    animator,
                    Resources.Load<CharacterAnimationConfig>(
                        "CharacterAnimationConfig").UpperBodyMask);
            }

            public CharacterPlayablesController Controller { get; }
            public WeaponAnimationDefinition Definition { get; }

            public static WeaponAnimationDefinition LoadDefinition()
            {
                WeaponAnimationDefinition definition =
                    Resources.Load<WeaponAnimationDefinition>(
                        "FistsWeaponAnimationDefinition");
                Assert.NotNull(definition);
                return definition;
            }

            public void Dispose()
            {
                Controller.Dispose();
                UnityEngine.Object.DestroyImmediate(visual);
            }
        }

        private sealed class StaticCharacterSource :
            IAnimationCharacterSource
        {
            public StaticCharacterSource(Transform transform)
            {
                Transform = transform;
            }

            public Transform Transform { get; }
            public Vector3 Velocity => Vector3.zero;
            public bool IsGrounded => true;
        }
    }
}
