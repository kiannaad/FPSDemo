using System;
using System.Collections;
using System.Collections.Generic;
using CGame.Ability;
using CGame.Ability.Animation;
using CGame.GameplayTags;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
namespace CGame.Animation.PlayMode.Tests
{
    public sealed class CharacterAnimInstanceNotifyPlayModeTests
    {
        private static readonly List<string> Trace = new List<string>();

        private GameObject ownerObject;
        private Animator animator;
        private AnimatorController runtimeController;
        private Pawn pawn;
        private CharacterAnimInstance animInstance;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Trace.Clear();
            RecordingNotify.ExpectedPawn = null;
            ownerObject = new GameObject("NotifyPlayModeOwner");
            animator = ownerObject.AddComponent<Animator>();
            runtimeController = CreateController(CreateClip("NativeIdle", 0.5f));
            animator.runtimeAnimatorController = runtimeController;
            pawn = new Pawn();
            RecordingNotify.ExpectedPawn = pawn;

            yield return null;

            Assert.That(animator.playableGraph.IsValid(), Is.True);
            Assert.That(animator.playableGraph.GetOutputCount(), Is.EqualTo(1));
            animInstance = new CharacterAnimInstance(
                pawn,
                new CharacterSource(ownerObject.transform),
                animator);
            animInstance.UpdateAnimation(Mathf.Max(Time.deltaTime, 0.001f));
            animInstance.DispatchAnimationNotifies();
            Assert.That(animInstance.PlayablesController.IsValid(), Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            animInstance?.Dispose();
            animInstance = null;
            RecordingNotify.ExpectedPawn = null;
            if (ownerObject != null)
            {
                UnityEngine.Object.Destroy(ownerObject);
            }

            if (runtimeController != null)
            {
                UnityEngine.Object.Destroy(runtimeController);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator PreAnimationCollectsAndPostAnimationDispatchesAfterPoseFrame()
        {
            AnimationClipAsset asset = CreateAsset("TwoPhase", 1f, 0.4f);
            asset.AddNotifyTrack().AddEvent(new RecordingNotify("post-pose"), 0);
            animInstance.PlayablesController.PlayAnimation(asset);

            animInstance.UpdateAnimation(Mathf.Max(Time.deltaTime, 0.001f));

            Assert.That(Trace, Is.Empty);
            yield return null;
            animInstance.DispatchAnimationNotifies();
            Assert.That(Trace, Is.EqualTo(new[] { "post-pose" }));
            UnityEngine.Object.Destroy(asset);
        }

        [UnityTest]
        public IEnumerator UpdateAnimation_RealGraphDispatchesForwardAndReverseToSamePawn()
        {
            AnimationClipAsset forward = CreateAsset("Forward", 1f, 0.4f);
            forward.AddNotifyTrack().AddEvent(new RecordingNotify("forward"), 2);
            AnimationPlaybackHandle forwardHandle =
                animInstance.PlayablesController.PlayAnimation(forward);

            yield return PumpUntil(() => Trace.Contains("forward"));

            Assert.That(forwardHandle.State, Is.Not.EqualTo(AnimationPlaybackState.Failed));
            Assert.That(Trace, Is.EqualTo(new[] { "forward" }));

            Trace.Clear();
            AnimationClipAsset reverse = CreateAsset("Reverse", -10f, 0.4f);
            reverse.AddNotifyTrack().AddEvent(new RecordingNotify("reverse"), 7);
            AnimationPlaybackHandle reverseHandle =
                animInstance.PlayablesController.PlayAnimation(reverse);

            yield return PumpUntil(() => Trace.Contains("reverse"));

            Assert.That(reverseHandle.State, Is.Not.EqualTo(AnimationPlaybackState.Failed));
            Assert.That(Trace, Is.EqualTo(new[] { "reverse" }));
            UnityEngine.Object.Destroy(forward);
            UnityEngine.Object.Destroy(reverse);
        }

        [UnityTest]
        public IEnumerator OverlayAndOverrideMask_DispatchOnceAndStopEndsDuration()
        {
            AnimationClipAsset overlay = CreateAsset("Overlay", 1f, 0.4f);
            overlay.AddNotifyTrack().AddEvent(new RecordingNotify("overlay"), 0);

            animInstance.PlayablesController.PlayPoseImmediate(overlay);
            animInstance.UpdateAnimation(Mathf.Max(Time.deltaTime, 0.001f));
            animInstance.DispatchAnimationNotifies();

            Assert.That(Trace, Is.EqualTo(new[] { "overlay" }));
            Trace.Clear();

            AnimationClipAsset action = CreateAsset("OverrideAction", 1f, 0.4f);
            action.OverrideMask = new AvatarMask();
            action.AddNotifyTrack().AddEvent(new RecordingDurationNotify("duration"), 0, 6);
            AnimationPlaybackHandle handle =
                animInstance.PlayablesController.PlayAnimation(action);
            animInstance.UpdateAnimation(Mathf.Max(Time.deltaTime, 0.001f));
            animInstance.DispatchAnimationNotifies();

            Assert.That(Trace, Is.EqualTo(new[] { "duration:begin", "duration:tick" }));
            Trace.Clear();
            Assert.That(animInstance.PlayablesController.Stop(handle), Is.True);
            yield return PumpFrames(2);

            Assert.That(Trace, Is.EqualTo(new[] { "duration:end:StateStopped" }));
            UnityEngine.Object.Destroy(action.OverrideMask);
            UnityEngine.Object.Destroy(overlay);
            UnityEngine.Object.Destroy(action);
        }

        [UnityTest]
        public IEnumerator SetTimeAndGraphRebuild_DoNotBackfillAndCleanActiveDurationOnce()
        {
            AnimationClipAsset asset = CreateAsset("SetTime", 1f, 0.5f);
            AnimationNotifyTrack track = asset.AddNotifyTrack();
            track.AddEvent(new RecordingDurationNotify("duration"), 0, 8);
            track.AddEvent(new RecordingNotify("jumped"), 4);
            AnimationPlaybackHandle handle =
                animInstance.PlayablesController.PlayAnimation(asset);
            animInstance.UpdateAnimation(Mathf.Max(Time.deltaTime, 0.001f));
            animInstance.DispatchAnimationNotifies();
            Trace.Clear();

            Assert.That(
                animInstance.PlayablesController.TrySetPlaybackTime(handle, 0.3d),
                Is.True);
            animInstance.UpdateAnimation(Mathf.Max(Time.deltaTime, 0.001f));
            animInstance.DispatchAnimationNotifies();

            Assert.That(Trace, Is.EqualTo(new[] { "duration:end:StateStopped" }));
            Trace.Clear();

            AnimationClipAsset active = CreateAsset("GraphRebuild", 1f, 0.5f);
            active.AddNotifyTrack().AddEvent(
                new RecordingDurationNotify("rebuild"),
                0,
                8);
            animInstance.PlayablesController.PlayAnimation(active);
            animInstance.UpdateAnimation(Mathf.Max(Time.deltaTime, 0.001f));
            animInstance.DispatchAnimationNotifies();
            Trace.Clear();

            AnimatorController replacement = CreateController(
                CreateClip("ReplacementNative", 0.5f));
            animator.runtimeAnimatorController = replacement;
            yield return null;
            animInstance.UpdateAnimation(Mathf.Max(Time.deltaTime, 0.001f));
            animInstance.DispatchAnimationNotifies();

            Assert.That(Trace, Is.EqualTo(new[] { "rebuild:end:OwnerDisabled" }));
            Assert.That(animInstance.PlayablesController.IsValid(), Is.True);
            UnityEngine.Object.Destroy(runtimeController);
            runtimeController = replacement;
            UnityEngine.Object.Destroy(asset);
            UnityEngine.Object.Destroy(active);
        }

        [UnityTest]
        public IEnumerator Dispose_EndsEveryActiveDurationWithOwnerDisabledOnce()
        {
            AnimationClipAsset asset = CreateAsset("Shutdown", 1f, 0.5f);
            asset.AddNotifyTrack().AddEvent(
                new RecordingDurationNotify("shutdown"),
                0,
                8);
            animInstance.PlayablesController.PlayAnimation(asset);
            animInstance.UpdateAnimation(Mathf.Max(Time.deltaTime, 0.001f));
            animInstance.DispatchAnimationNotifies();
            Trace.Clear();

            animInstance.Dispose();
            animInstance = null;
            yield return null;

            Assert.That(Trace, Is.EqualTo(new[] { "shutdown:end:OwnerDisabled" }));
            UnityEngine.Object.Destroy(asset);
        }

        [UnityTest]
        public IEnumerator UpdateAnimation_RealGraphRoutesGameEventToPawnAscOnceWithoutOverrideDuplication()
        {
            GameplayTagSource tagSource = ScriptableObject.CreateInstance<GameplayTagSource>();
            tagSource.SetDefinition(
                "AbilityGameEventPlayMode",
                new[]
                {
                    new GameplayTagSourceNode(
                        "Event",
                        false,
                        children: new[]
                        {
                            new GameplayTagSourceNode(
                                "Weapon",
                                false,
                                children: new[] { new GameplayTagSourceNode("Reload", true) })
                        })
                });
            GameplayTagRegistryBuildResult tagResult = GameplayTagManager.Instance.Initialize(new[] { tagSource });
            Assert.That(tagResult.Succeeded, Is.True, string.Join(Environment.NewLine, tagResult.Errors));
            GameplayTag eventTag = GameplayTagManager.Instance.RequestTag("Event.Weapon.Reload");
            var abilitySystem = new AbilitySystemComponent(pawn);
            pawn.BindingAbilitySystem(abilitySystem);
            int eventCount = 0;
            AbilityGameEventRegistration registration = abilitySystem.RegisterGameEvent(
                eventTag,
                AbilityGameEventMatchPolicy.Exact,
                payload => eventCount++);
            AnimationClipAsset asset = CreateAsset("AbilityEvent", 1f, 0.4f);
            asset.OverrideMask = new AvatarMask();
            asset.AddNotifyTrack().AddEvent(
                new global::CGame.Ability.Animation.AnimationGameEventNotify { EventTag = eventTag },
                2);

            try
            {
                AnimationPlaybackHandle handle = animInstance.PlayablesController.PlayAnimation(asset);
                yield return PumpUntil(() => eventCount == 1);

                Assert.That(handle.State, Is.Not.EqualTo(AnimationPlaybackState.Failed));
                Assert.That(eventCount, Is.EqualTo(1));
                Assert.That(abilitySystem.AbilityCount, Is.Zero);
            }
            finally
            {
                registration.Dispose();
                pawn.ClearingAbilitySystem(abilitySystem);
                abilitySystem.Dispose();
                GameplayTagManager.Instance.Shutdown();
                UnityEngine.Object.Destroy(asset.OverrideMask);
                UnityEngine.Object.Destroy(asset);
                UnityEngine.Object.Destroy(tagSource);
            }
        }

        [UnityTest]
        public IEnumerator UpdateAnimation_RealGraphEventCompletesActiveWaitTaskAndAbility()
        {
            GameplayTagSource tagSource = ScriptableObject.CreateInstance<GameplayTagSource>();
            tagSource.SetDefinition(
                "AbilityTaskPlayMode",
                new[]
                {
                    new GameplayTagSourceNode(
                        "Ability",
                        false,
                        children: new[]
                        {
                            new GameplayTagSourceNode(
                                "Weapon",
                                false,
                                children: new[] { new GameplayTagSourceNode("Reload", true) })
                        }),
                    new GameplayTagSourceNode(
                        "Event",
                        false,
                        children: new[]
                        {
                            new GameplayTagSourceNode(
                                "Weapon",
                                false,
                                children: new[] { new GameplayTagSourceNode("Reload", true) })
                        })
                });
            GameplayTagRegistryBuildResult tagResult = GameplayTagManager.Instance.Initialize(new[] { tagSource });
            Assert.That(tagResult.Succeeded, Is.True, string.Join(Environment.NewLine, tagResult.Errors));
            GameplayTag abilityTag = GameplayTagManager.Instance.RequestTag("Ability.Weapon.Reload");
            GameplayTag eventTag = GameplayTagManager.Instance.RequestTag("Event.Weapon.Reload");
            object source = new object();
            var abilitySystem = new AbilitySystemComponent(pawn);
            pawn.BindingAbilitySystem(abilitySystem);
            AbilitySpecHandle specHandle = abilitySystem.GiveAbility(
                new EventEndingAbilityDefinition(abilityTag, eventTag),
                source);
            AbilityActivationResult activation = abilitySystem.TryActivateAbilityByTag(abilityTag);
            Assert.That(activation.Succeeded, Is.True);
            Assert.That(abilitySystem.TryGetSpec(specHandle, out AbilitySpec spec), Is.True);
            var instance = (EventEndingAbilityInstance)spec.PrimaryInstance;
            AnimationClipAsset asset = CreateAsset("AbilityTaskEvent", 1f, 0.4f);
            asset.AddNotifyTrack().AddEvent(
                new global::CGame.Ability.Animation.AnimationGameEventNotify { EventTag = eventTag },
                2);

            try
            {
                AnimationPlaybackHandle handle = animInstance.PlayablesController.PlayAnimation(asset);
                yield return PumpUntil(() => instance.State == AbilityInstanceState.Inactive);

                Assert.That(handle.State, Is.Not.EqualTo(AnimationPlaybackState.Failed));
                Assert.That(instance.EventCount, Is.EqualTo(1));
                Assert.That(instance.CommitCount, Is.EqualTo(1));
                Assert.That(instance.LastEndReason, Is.EqualTo(AbilityEndReason.Completed));
                Assert.That(instance.WaitTask.State, Is.EqualTo(AbilityTaskState.Completed));
                Assert.That(instance.WaitTask.IsListening, Is.False);
                Assert.That(instance.ActiveTaskCount, Is.Zero);
            }
            finally
            {
                pawn.ClearingAbilitySystem(abilitySystem);
                abilitySystem.Dispose();
                GameplayTagManager.Instance.Shutdown();
                UnityEngine.Object.Destroy(asset);
                UnityEngine.Object.Destroy(tagSource);
            }
        }

        private IEnumerator PumpUntil(Func<bool> condition, int maximumFrames = 30)
        {
            for (int frame = 0; frame < maximumFrames && !condition(); frame++)
            {
                animInstance.UpdateAnimation(Mathf.Max(Time.deltaTime, 0.001f));
                yield return null;
                animInstance.DispatchAnimationNotifies();
            }

            Assert.That(condition(), Is.True, "Expected Notify was not dispatched by the real Animator graph.");
        }

        private IEnumerator PumpFrames(int frameCount)
        {
            for (int frame = 0; frame < frameCount; frame++)
            {
                animInstance.UpdateAnimation(Mathf.Max(Time.deltaTime, 0.001f));
                yield return null;
                animInstance.DispatchAnimationNotifies();
            }
        }

        private static AnimationClipAsset CreateAsset(
            string clipName,
            float speed,
            float length)
        {
            AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            Assert.That(asset.TryInitialize(CreateClip(clipName, length)), Is.True);
            asset.Speed = speed;
            asset.BlendInTime = 0f;
            asset.BlendOutTime = 0f;
            return asset;
        }

        private static AnimationClip CreateClip(string name, float length)
        {
            var clip = new AnimationClip
            {
                frameRate = 20f,
                name = name,
            };
            clip.SetCurve(
                string.Empty,
                typeof(Transform),
                "localPosition.x",
                AnimationCurve.Linear(0f, 0f, length, 1f));
            return clip;
        }

        private static AnimatorController CreateController(AnimationClip clip)
        {
            var controller = new AnimatorController
            {
                name = $"{clip.name}Controller",
            };
            controller.AddLayer("Base Layer");
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            controller.AddParameter("Velocity", AnimatorControllerParameterType.Float);
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("InAir", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Sprinting", AnimatorControllerParameterType.Float);
            AnimatorState state =
                controller.layers[0].stateMachine.AddState("Idle");
            state.motion = clip;
            return controller;
        }

        private sealed class CharacterSource : IAnimationCharacterSource
        {
            public CharacterSource(Transform transform)
            {
                Transform = transform;
            }

            public Transform Transform { get; }
            public Vector3 Velocity => Vector3.zero;
            public bool IsGrounded => true;
        }

        private sealed class EventEndingAbilityDefinition : AbilityDefinition
        {
            private readonly GameplayTag eventTag;

            public EventEndingAbilityDefinition(GameplayTag abilityTag, GameplayTag eventTag)
                : base(abilityTag)
            {
                this.eventTag = eventTag;
            }

            protected override AbilityInstance CreateInstance()
            {
                return new EventEndingAbilityInstance(eventTag);
            }
        }

        private sealed class EventEndingAbilityInstance : AbilityInstance
        {
            private readonly GameplayTag eventTag;

            public EventEndingAbilityInstance(GameplayTag eventTag)
            {
                this.eventTag = eventTag;
            }

            public WaitGameEventTask WaitTask { get; private set; }
            public int EventCount { get; private set; }
            public int CommitCount { get; private set; }

            protected override void OnActivate()
            {
                WaitTask = StartTask(new WaitGameEventTask(
                    eventTag,
                    AbilityGameEventMatchPolicy.Exact,
                    onlyTriggerOnce: true,
                    OnEvent));
            }

            protected override void OnCommit()
            {
                CommitCount++;
            }

            private void OnEvent(AbilityGameEventPayload payload)
            {
                EventCount++;
                TryCommit();
                EndAbility(AbilityEndReason.Completed);
            }
        }

        [Serializable]
        private sealed class RecordingNotify : AnimationInstantNotify
        {
            public RecordingNotify(string label)
            {
                this.label = label;
            }

            public static Pawn ExpectedPawn { get; set; }

            private readonly string label;

            public override void OnNotify(Pawn owner)
            {
                Assert.That(owner, Is.SameAs(ExpectedPawn));
                Trace.Add(label);
            }
        }

        [Serializable]
        private sealed class RecordingDurationNotify : AnimationDurationNotify
        {
            public RecordingDurationNotify(string label)
            {
                this.label = label;
            }

            private readonly string label;

            public override void OnBegin(Pawn owner)
            {
                Assert.That(owner, Is.SameAs(RecordingNotify.ExpectedPawn));
                Trace.Add($"{label}:begin");
            }

            public override void OnTick(Pawn owner)
            {
                Assert.That(owner, Is.SameAs(RecordingNotify.ExpectedPawn));
                Trace.Add($"{label}:tick");
            }

            public override void OnEnd(Pawn owner, AnimationNotifyEndReason reason)
            {
                Assert.That(owner, Is.SameAs(RecordingNotify.ExpectedPawn));
                Trace.Add($"{label}:end:{reason}");
            }
        }
    }
}
#endif
