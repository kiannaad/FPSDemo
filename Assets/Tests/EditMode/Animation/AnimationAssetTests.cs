using CGame.Animation;
using CGame.Animation.Editor;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CGame.Tests
{
    public class AnimationAssetTests
    {
        [Test]
        public void ClipAsset_ExposesPlayableClipData()
        {
            AnimationClip clip = new AnimationClip();
            AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            asset.TryInitialize(clip);

            Assert.IsTrue(asset.IsValid);
            Assert.AreSame(clip, asset.MainClip);
        }

        [Test]
        public void ClipAsset_PreservesPlaybackNotifyAndNamedCurveFacts()
        {
            var clip = new AnimationClip { frameRate = 30f };
            AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            asset.TryInitialize(clip);
            asset.FadeDuration = 0.12f;
            asset.Speed = 1.25f;
            asset.OverrideNormalizedStartTime = true;
            asset.NormalizedStartTime = 0.4f;
            asset.AddNotifyTrack("Footsteps");
            asset.SetNamedCurve("MaskLeftHandIK", AnimationCurve.Linear(0f, 0f, 1f, 1f));

            Assert.AreEqual(0.12f, asset.FadeDuration);
            Assert.AreEqual(1.25f, asset.Speed);
            Assert.IsTrue(asset.OverrideNormalizedStartTime);
            Assert.AreEqual(0.4f, asset.NormalizedStartTime);
            Assert.AreEqual(1, asset.NotifyTracks.Count);
            Assert.IsTrue(asset.TryGetNamedCurve("MaskLeftHandIK", out AnimationCurve curve));
            Assert.AreEqual(0.5f, curve.Evaluate(0.5f), 0.001f);
        }

        [Test]
        public void SequenceAsset_ExposesFirstClipAsMainClip()
        {
            AnimationClip clip = new AnimationClip();
            AnimationClipAsset clipAsset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            clipAsset.TryInitialize(clip);
            AnimationSequenceAsset asset = ScriptableObject.CreateInstance<AnimationSequenceAsset>();
            asset.Clips = new[]
            {
                new AnimationSequenceAsset.SequenceEntry
                {
                    ClipAsset = clipAsset,
                },
            };

            Assert.IsTrue(asset.IsValid);
            Assert.AreSame(clip, asset.MainClip);
        }

        [Test]
        public void ClipAsset_DoesNotRebindAnimationClipAfterInitialization()
        {
            AnimationClip firstClip = new AnimationClip();
            AnimationClip secondClip = new AnimationClip();
            AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();

            bool initialized = asset.TryInitialize(firstClip);
            bool rebound = asset.TryInitialize(secondClip);

            Assert.IsTrue(initialized);
            Assert.IsFalse(rebound);
            Assert.AreSame(firstClip, asset.AnimationClip);
        }

        [Test]
        public void SequenceAsset_PreservesClipAssetsAndPlaybackSpeeds()
        {
            AnimationClip firstClip = new AnimationClip();
            AnimationClip secondClip = new AnimationClip();
            AnimationClipAsset firstAsset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            AnimationClipAsset secondAsset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            firstAsset.TryInitialize(firstClip);
            secondAsset.TryInitialize(secondClip);
            secondAsset.Speed = 1.5f;
            AnimationSequenceAsset sequence = ScriptableObject.CreateInstance<AnimationSequenceAsset>();
            sequence.Clips = new[]
            {
                new AnimationSequenceAsset.SequenceEntry
                {
                    ClipAsset = firstAsset,
                    Speed = 0.5f,
                },
                new AnimationSequenceAsset.SequenceEntry
                {
                    ClipAsset = secondAsset,
                    Speed = 2f,
                },
            };

            Assert.IsTrue(sequence.IsValid);
            Assert.AreSame(firstClip, sequence.MainClip);
            Assert.AreSame(firstAsset, sequence.Clips[0].ClipAsset);
            Assert.AreSame(secondAsset, sequence.Clips[1].ClipAsset);
            Assert.AreEqual(0.5f, sequence.Clips[0].Speed);
            Assert.AreEqual(2f, sequence.Clips[1].Speed);
        }

        [Test]
        public void SequenceAsset_DoesNotAllowDirectNotifyEditing()
        {
            AnimationClipAsset clipAsset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            clipAsset.TryInitialize(new AnimationClip());
            AnimationSequenceAsset sequence = ScriptableObject.CreateInstance<AnimationSequenceAsset>();
            sequence.Clips = new[]
            {
                new AnimationSequenceAsset.SequenceEntry
                {
                    ClipAsset = clipAsset,
                },
            };

            AnimationEditorWindow window = AnimationEditorWindow.Open(sequence);

            try
            {
                Assert.IsFalse(sequence.CanEditNotifies);
                Assert.IsFalse(window.CanEditSelectedAsset);
                Assert.IsNull(window.AddNotifyTrackToAsset("Sequence Notify Track"));
                Assert.AreEqual(0, sequence.NotifyTracks.Count);
            }
            finally
            {
                window.Close();
            }
        }

#if ANIMANCER
        [Test]
        public void AnimationAssetPlayer_PlaysAssetThroughAnimancerComponent()
        {
            GameObject gameObject = new GameObject("AnimationAssetPlayerTest");
            try
            {
                gameObject.AddComponent<Animator>();
                AnimancerComponent animancer = gameObject.AddComponent<AnimancerComponent>();
                AnimationAssetPlayer player = gameObject.AddComponent<AnimationAssetPlayer>();
                AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
                asset.TryInitialize(new AnimationClip());
                player.Animancer = animancer;
                player.AnimationAsset = asset;

                AnimancerState state = player.Play();

                Assert.IsNotNull(state);
                Assert.AreSame(asset.AnimationClip, state.MainObject);
                Assert.IsNotNull(player.NotifyRuntime);
                Assert.AreSame(asset, player.NotifyRuntime.ClipAsset);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AnimationAssetPlayer_DispatchesNotifyAroundManualEvaluate()
        {
            GameObject gameObject = new GameObject("AnimationAssetPlayerNotifyRuntimeTickTest");
            try
            {
                gameObject.AddComponent<Animator>();
                AnimancerComponent animancer = gameObject.AddComponent<AnimancerComponent>();
                AnimationAssetPlayer player = gameObject.AddComponent<AnimationAssetPlayer>();
                AnimationClipAsset asset = CreateEditableClipAsset();
                asset.FadeDuration = 0f;
                var notify = new CountingInstantNotify();
                asset.AddNotifyTrack("Footsteps").AddEvent(notify, 2);
                player.Animancer = animancer;
                player.AnimationAsset = asset;

                AnimancerState state = player.Play();
                player.CaptureNotifyBeforeEvaluateForTesting();
                state.Time = 0.25f;
                animancer.Evaluate(0f);
                player.DispatchNotifyAfterEvaluateForTesting(0.25f);

                Assert.AreEqual(1, notify.NotifyCount);
                Assert.AreSame(animancer, notify.LastContext.AnimancerComponent);
                Assert.AreSame(gameObject, notify.LastContext.OwnerGameObject);
                Assert.AreSame(player.NotifyRuntime.AnimancerState, notify.LastContext.AnimancerState);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AnimationAssetPlayer_DoesNotRepeatNotifyWhenEvaluateDoesNotAdvance()
        {
            GameObject gameObject = new GameObject("AnimationAssetPlayerNotifyRuntimeNoAdvanceTest");
            try
            {
                gameObject.AddComponent<Animator>();
                AnimancerComponent animancer = gameObject.AddComponent<AnimancerComponent>();
                AnimationAssetPlayer player = gameObject.AddComponent<AnimationAssetPlayer>();
                AnimationClipAsset asset = CreateEditableClipAsset();
                asset.FadeDuration = 0f;
                var notify = new CountingInstantNotify();
                asset.AddNotifyTrack("Footsteps").AddEvent(notify, 2);
                player.Animancer = animancer;
                player.AnimationAsset = asset;

                AnimancerState state = player.Play();
                player.CaptureNotifyBeforeEvaluateForTesting();
                state.Time = 0.25f;
                animancer.Evaluate(0f);
                player.DispatchNotifyAfterEvaluateForTesting(0.25f);

                player.CaptureNotifyBeforeEvaluateForTesting();
                animancer.Evaluate(0f);
                player.DispatchNotifyAfterEvaluateForTesting(0f);

                Assert.AreEqual(1, notify.NotifyCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AnimationAssetPlayer_EndsActiveDurationWhenStateStopsWithoutTimeAdvance()
        {
            GameObject gameObject = new GameObject("AnimationAssetPlayerNotifyRuntimeStoppedNoAdvanceTest");
            try
            {
                gameObject.AddComponent<Animator>();
                AnimancerComponent animancer = gameObject.AddComponent<AnimancerComponent>();
                AnimationAssetPlayer player = gameObject.AddComponent<AnimationAssetPlayer>();
                AnimationClipAsset asset = CreateEditableClipAsset();
                asset.FadeDuration = 0f;
                var notify = new CountingDurationNotify();
                asset.AddNotifyTrack("Trail").AddEvent(notify, 2, 5);
                player.Animancer = animancer;
                player.AnimationAsset = asset;

                AnimancerState state = player.Play();
                player.CaptureNotifyBeforeEvaluateForTesting();
                state.Time = 0.25f;
                animancer.Evaluate(0f);
                player.DispatchNotifyAfterEvaluateForTesting(0.25f);

                player.CaptureNotifyBeforeEvaluateForTesting();
                state.IsPlaying = false;
                animancer.Evaluate(0f);
                player.DispatchNotifyAfterEvaluateForTesting(0f);

                Assert.AreEqual(1, notify.BeginCount);
                Assert.AreEqual(1, notify.EndCount);
                Assert.AreEqual(AnimationNotifyEndReason.StateStopped, notify.LastEndReason);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AnimationAssetPlayer_DispatchesOwnerReceiverNotifyThroughRuntime()
        {
            GameObject gameObject = new GameObject("AnimationAssetPlayerOwnerReceiverTest");
            try
            {
                gameObject.AddComponent<Animator>();
                var receiver = gameObject.AddComponent<RecordingNotifyReceiver>();
                AnimancerComponent animancer = gameObject.AddComponent<AnimancerComponent>();
                AnimationAssetPlayer player = gameObject.AddComponent<AnimationAssetPlayer>();
                AnimationClipAsset asset = CreateEditableClipAsset();
                asset.FadeDuration = 0f;
                var notify = new CountingInstantNotify
                {
                    DispatchPolicy = AnimationNotifyDispatchPolicy.OwnerReceiver,
                    EventTag = "AnimEvent.Footstep",
                };
                asset.AddNotifyTrack("Footsteps").AddEvent(notify, 2);
                player.Animancer = animancer;
                player.AnimationAsset = asset;

                AnimancerState state = player.Play();
                player.CaptureNotifyBeforeEvaluateForTesting();
                state.Time = 0.25f;
                animancer.Evaluate(0f);
                player.DispatchNotifyAfterEvaluateForTesting(0.25f);

                Assert.AreEqual(0, notify.NotifyCount);
                Assert.AreEqual(1, receiver.ReceiveCount);
                Assert.AreEqual("AnimEvent.Footstep", receiver.LastContext.EventTag);
                Assert.AreSame(gameObject, receiver.LastContext.OwnerGameObject);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AnimationAssetPlayer_EndsActiveDurationNotifyWhenDisabled()
        {
            GameObject gameObject = new GameObject("AnimationAssetPlayerDisableCleanupTest");
            try
            {
                gameObject.AddComponent<Animator>();
                AnimancerComponent animancer = gameObject.AddComponent<AnimancerComponent>();
                AnimationAssetPlayer player = gameObject.AddComponent<AnimationAssetPlayer>();
                AnimationClipAsset asset = CreateEditableClipAsset();
                asset.FadeDuration = 0f;
                var notify = new CountingDurationNotify();
                asset.AddNotifyTrack("Trail").AddEvent(notify, 2, 5);
                player.Animancer = animancer;
                player.AnimationAsset = asset;

                AnimancerState state = player.Play();
                player.CaptureNotifyBeforeEvaluateForTesting();
                state.Time = 0.25f;
                animancer.Evaluate(0f);
                player.DispatchNotifyAfterEvaluateForTesting(0.25f);

                typeof(AnimationAssetPlayer)
                    .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(player, null);

                Assert.AreEqual(1, notify.BeginCount);
                Assert.AreEqual(1, notify.EndCount);
                Assert.AreEqual(AnimationNotifyEndReason.OwnerDisabled, notify.LastEndReason);
                Assert.IsNull(player.NotifyRuntime);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AnimationAssetPlayer_PlayingNewAssetInterruptsOldDurationNotify()
        {
            GameObject gameObject = new GameObject("AnimationAssetPlayerSwitchCleanupTest");
            try
            {
                gameObject.AddComponent<Animator>();
                AnimancerComponent animancer = gameObject.AddComponent<AnimancerComponent>();
                AnimationAssetPlayer player = gameObject.AddComponent<AnimationAssetPlayer>();
                AnimationClipAsset firstAsset = CreateEditableClipAsset();
                AnimationClipAsset secondAsset = CreateEditableClipAsset();
                firstAsset.FadeDuration = 0f;
                secondAsset.FadeDuration = 0f;
                var notify = new CountingDurationNotify();
                firstAsset.AddNotifyTrack("Trail").AddEvent(notify, 2, 5);
                player.Animancer = animancer;
                player.AnimationAsset = firstAsset;

                AnimancerState state = player.Play();
                player.CaptureNotifyBeforeEvaluateForTesting();
                state.Time = 0.25f;
                animancer.Evaluate(0f);
                player.DispatchNotifyAfterEvaluateForTesting(0.25f);
                player.AnimationAsset = secondAsset;
                player.Play();

                Assert.AreEqual(1, notify.BeginCount);
                Assert.AreEqual(1, notify.EndCount);
                Assert.AreEqual(AnimationNotifyEndReason.Interrupted, notify.LastEndReason);
                Assert.AreSame(secondAsset, player.NotifyRuntime.ClipAsset);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AnimancerComponent_CreateNotifyRuntimeProvidesManualIntegrationPath()
        {
            GameObject gameObject = new GameObject("AnimancerComponentNotifyRuntimeExtensionTest");
            try
            {
                gameObject.AddComponent<Animator>();
                AnimancerComponent animancer = gameObject.AddComponent<AnimancerComponent>();
                AnimationClipAsset asset = CreateEditableClipAsset();
                asset.FadeDuration = 0f;
                AnimancerState state = animancer.Play(asset);

                AnimationNotifyRuntime runtime = animancer.CreateNotifyRuntime(gameObject, asset, state);

                Assert.IsNotNull(runtime);
                Assert.AreSame(gameObject, runtime.Owner);
                Assert.AreSame(asset, runtime.ClipAsset);
                Assert.AreSame(state, runtime.AnimancerState);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

#endif

        [Test]
        public void AnimationClipAssetFactory_RegistersCreateMenuPath()
        {
            MethodInfo createMenuMethod = typeof(AnimationClipAssetFactory).GetMethod(nameof(AnimationClipAssetFactory.CreateFromSelectedClipInCreateMenu));
            MenuItem menuItem = createMenuMethod.GetCustomAttributes<MenuItem>().FirstOrDefault();

            Assert.IsNotNull(menuItem);
            Assert.AreEqual("Assets/Create/CGame/Animation/Animation Clip Asset", menuItem.menuItem);
        }

        [Test]
        public void AnimationClipAssetFactory_CreatesPersistentAssetBoundToSourceClip()
        {
            const string folderPath = "Assets/Temp/AnimationAssetTests";
            EnsureAssetFolder("Assets", "Temp");
            EnsureAssetFolder("Assets/Temp", "AnimationAssetTests");
            string clipPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/FactorySource.anim");
            string createdPath = null;

            try
            {
                var clip = new AnimationClip
                {
                    frameRate = 24f,
                };
                AssetDatabase.CreateAsset(clip, clipPath);

                AnimationClipAsset createdAsset = AnimationClipAssetFactory.CreateFromClip(clip);
                createdPath = AssetDatabase.GetAssetPath(createdAsset);
                AnimationClipAsset loadedAsset = AssetDatabase.LoadAssetAtPath<AnimationClipAsset>(createdPath);

                Assert.IsNotNull(loadedAsset);
                Assert.AreSame(clip, loadedAsset.AnimationClip);
                Assert.IsFalse(loadedAsset.TryInitialize(new AnimationClip()));
            }
            finally
            {
                if (!string.IsNullOrEmpty(createdPath))
                {
                AssetDatabase.DeleteAsset(createdPath);
                }

                AssetDatabase.DeleteAsset(clipPath);
                AssetDatabase.DeleteAsset(folderPath);
                DeleteAssetFolderIfEmpty("Assets/Temp");
            }
        }

        [Test]
        public void AnimationClipAssetFactory_CreateOrUpdateIsIdempotent()
        {
            const string folderPath = "Assets/Temp/AnimationAssetTests";
            const string assetPath = folderPath + "/IdempotentClipAsset.asset";
            EnsureAssetFolder("Assets", "Temp");
            EnsureAssetFolder("Assets/Temp", "AnimationAssetTests");
            string clipPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/IdempotentSource.anim");

            try
            {
                var clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);

                AnimationClipAsset first = AnimationClipAssetFactory.CreateOrUpdateFromClip(clip, assetPath);
                AnimationClipAsset second = AnimationClipAssetFactory.CreateOrUpdateFromClip(clip, assetPath);

                Assert.IsNotNull(first);
                Assert.AreSame(first, second);
                Assert.AreSame(clip, second.AnimationClip);
                Assert.AreEqual(1, AssetDatabase.FindAssets("t:AnimationClipAsset", new[] { folderPath }).Length);
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.DeleteAsset(clipPath);
                AssetDatabase.DeleteAsset(folderPath);
                DeleteAssetFolderIfEmpty("Assets/Temp");
            }
        }

        [Test]
        public void AnimationClipAsset_PersistsEditedNotifyData()
        {
            const string folderPath = "Assets/Temp/AnimationAssetTests";
            EnsureAssetFolder("Assets", "Temp");
            EnsureAssetFolder("Assets/Temp", "AnimationAssetTests");
            string clipPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/PersistentSource.anim");
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/PersistentClipAsset.asset");

            try
            {
                var clip = new AnimationClip
                {
                    frameRate = 30f,
                };
                clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
                AssetDatabase.CreateAsset(clip, clipPath);

                AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
                asset.TryInitialize(clip);
                AssetDatabase.CreateAsset(asset, assetPath);
                AnimationNotifyTrack track = asset.AddNotifyTrack("Footsteps");
                AnimationNotifyEvent notifyEvent = track.AddEvent(new AnimationInstantNotify { DisplayName = "Foot Plant" }, 12);
                notifyEvent.MinTriggerWeight = 0.25f;
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();

                AnimationClipAsset loadedAsset = AssetDatabase.LoadAssetAtPath<AnimationClipAsset>(assetPath);

                Assert.IsNotNull(loadedAsset);
                Assert.AreEqual(1, loadedAsset.NotifyTracks.Count);
                Assert.AreEqual("Footsteps", loadedAsset.NotifyTracks[0].Name);
                Assert.AreEqual(1, loadedAsset.NotifyTracks[0].Events.Count);
                Assert.AreEqual(12, loadedAsset.NotifyTracks[0].Events[0].StartFrame);
                Assert.AreEqual(0.25f, loadedAsset.NotifyTracks[0].Events[0].MinTriggerWeight);
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.DeleteAsset(clipPath);
                AssetDatabase.DeleteAsset(folderPath);
                DeleteAssetFolderIfEmpty("Assets/Temp");
            }
        }

        [Test]
        public void ProjectIdleClipAsset_IsEditableAndPlayable()
        {
            const string sampleAssetPath = "Assets/Art/Animation/LocomotionAsset/InPlace/A_INP_IdleClipAsset.asset";
            AnimationClipAsset asset = AssetDatabase.LoadAssetAtPath<AnimationClipAsset>(sampleAssetPath);
            Assert.IsNotNull(asset);
            Assert.IsTrue(asset.IsValid);
            Assert.IsTrue(asset.CanEditNotifies);

            GameObject gameObject = new GameObject("ProjectIdleClipAssetPlaybackTest");
            try
            {
                Animator animator = gameObject.AddComponent<Animator>();
                AnimationAssetPlayer player = gameObject.AddComponent<AnimationAssetPlayer>();
                player.Animator = animator;
                player.AnimationAsset = asset;

                Assert.IsTrue(player.Play());
                Assert.IsTrue(player.IsPlaying);
                Assert.AreSame(asset, player.NotifyRuntime.ClipAsset);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

#if ANIMANCER
        [Test]
        public void QuickPlaySampleScript_PlaysProjectIdleClipAssetOnEnable()
        {
            const string sampleAssetPath = "Assets/Art/Animation/LocomotionAsset/InPlace/A_INP_IdleClipAsset.asset";
            Type sampleType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Animancer.Samples.Basics.PlayAnimationOnEnable"))
                .FirstOrDefault(type => type != null);
            AnimationClipAsset asset = AssetDatabase.LoadAssetAtPath<AnimationClipAsset>(sampleAssetPath);
            Assert.IsNotNull(sampleType);
            Assert.IsNotNull(asset);

            GameObject gameObject = new GameObject("QuickPlaySampleScriptPlaybackTest");
            try
            {
                gameObject.AddComponent<Animator>();
                AnimancerComponent animancer = gameObject.AddComponent<AnimancerComponent>();
                Component sample = gameObject.AddComponent(sampleType);
                sampleType.GetField("animation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sample, asset);
                sampleType.GetField("animancer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sample, animancer);

                sampleType.GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sample, null);

                Assert.IsNotNull(animancer.States.Current);
                Assert.AreSame(asset.AnimationClip, animancer.States.Current.MainObject);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AnimancerComponent_PlayAcceptsAnimationAssetDirectly()
        {
            GameObject gameObject = new GameObject("AnimancerComponentAnimationAssetExtensionTest");
            try
            {
                gameObject.AddComponent<Animator>();
                AnimancerComponent animancer = gameObject.AddComponent<AnimancerComponent>();
                AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
                asset.TryInitialize(new AnimationClip());

                AnimancerState state = animancer.Play(asset);

                Assert.IsNotNull(state);
                Assert.AreSame(asset.AnimationClip, state.MainObject);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
#endif

        [Test]
        public void AnimationAsset_AddsNotifyTracksAndEvents()
        {
            AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            AnimationNotifyTrack track = asset.AddNotifyTrack("Footsteps");

            AnimationNotifyEvent instant = track.AddEvent(new AnimationInstantNotify(), 3);
            AnimationNotifyEvent duration = track.AddEvent(new AnimationDurationNotify(), 5, 8);

            Assert.AreEqual(1, asset.NotifyTracks.Count);
            Assert.AreEqual("Footsteps", asset.NotifyTracks[0].Name);
            Assert.IsFalse(instant.IsDuration);
            Assert.IsTrue(duration.IsDuration);
            Assert.AreEqual(13, duration.EndFrame);
        }

        [Test]
        public void AnimationNotify_StoresSemanticTagsAndExplicitDispatchPolicy()
        {
            var notify = new AnimationInstantNotify
            {
                EventTag = " AnimEvent.Footstep ",
                DispatchPolicy = AnimationNotifyDispatchPolicy.OwnerReceiver,
            };
            notify.SetContextTags(new[] { " Surface.Concrete ", "", "MoveState.Run" });

            string json = JsonUtility.ToJson(notify);
            AnimationInstantNotify loaded = JsonUtility.FromJson<AnimationInstantNotify>(json);

            Assert.AreEqual("AnimEvent.Footstep", loaded.EventTag);
            Assert.AreEqual(AnimationNotifyDispatchPolicy.OwnerReceiver, loaded.DispatchPolicy);
            Assert.AreEqual(2, loaded.ContextTags.Count);
            Assert.AreEqual("Surface.Concrete", loaded.ContextTags[0]);
            Assert.AreEqual("MoveState.Run", loaded.ContextTags[1]);
        }

        [Test]
        public void AnimationNotify_DefaultsToDirectNotifyWithoutImplicitBroadcast()
        {
            var notify = new AnimationInstantNotify();

            Assert.AreEqual(string.Empty, notify.EventTag);
            Assert.AreEqual(0, notify.ContextTags.Count);
            Assert.AreEqual(AnimationNotifyDispatchPolicy.DirectNotify, notify.DispatchPolicy);
        }

        [Test]
        public void AnimationEventContext_CapturesOwnerAnimationEventAndSpatialData()
        {
            GameObject gameObject = new GameObject("AnimationEventContextTest");
            GameObject attachObject = new GameObject("AnimationEventAttachPoint");

            try
            {
                Animator animator = gameObject.AddComponent<Animator>();
                gameObject.transform.position = new Vector3(1f, 2f, 3f);
                attachObject.transform.position = new Vector3(4f, 5f, 6f);
                attachObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
                asset.TryInitialize(new AnimationClip());
                var notify = new AnimationInstantNotify
                {
                    EventTag = "AnimEvent.Weapon.Swing",
                };
                notify.SetContextTags(new[] { "Weapon.Sword" });
                var notifyEvent = new AnimationNotifyEvent
                {
                    Notify = notify,
                    StartFrame = 8,
                };

                var context = new AnimationEventContext(
                    gameObject,
                    asset,
                    notifyEvent,
                    0.5f,
                    0.016f,
                    0.8f,
                    attachObject.transform,
                    "RightHand");

                Assert.AreSame(gameObject, context.Owner);
                Assert.AreSame(gameObject, context.OwnerGameObject);
                Assert.AreSame(gameObject.transform, context.OwnerTransform);
                Assert.AreSame(animator, context.Animator);
                Assert.AreSame(asset, context.AnimationAsset);
                Assert.AreSame(notifyEvent, context.NotifyEvent);
                Assert.AreEqual("AnimEvent.Weapon.Swing", context.EventTag);
                Assert.AreEqual("Weapon.Sword", context.ContextTags[0]);
                Assert.AreEqual(0.5f, context.NormalizedTime);
                Assert.AreEqual(0.016f, context.DeltaTime);
                Assert.AreEqual(0.8f, context.Weight);
                Assert.AreEqual(attachObject.transform.position, context.Position);
                Assert.AreEqual(attachObject.transform.rotation, context.Rotation);
                Assert.AreSame(attachObject.transform, context.AttachPoint);
                Assert.AreEqual("RightHand", context.BoneOrSocketName);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(attachObject);
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AnimationNotifyLifecycleEntries_AreRuntimeHooksWithoutAssetState()
        {
            var context = new AnimationEventContext(null, null, null, 0.25f, 0.016f, 1f);
            var instant = new CountingInstantNotify();
            var duration = new CountingDurationNotify();

            instant.OnNotify(context);
            duration.OnBegin(context);
            duration.OnTick(context);
            duration.OnEnd(context, AnimationNotifyEndReason.Interrupted);

            Assert.AreEqual(1, instant.NotifyCount);
            Assert.AreEqual(1, duration.BeginCount);
            Assert.AreEqual(1, duration.TickCount);
            Assert.AreEqual(1, duration.EndCount);
            Assert.AreEqual(AnimationNotifyEndReason.Interrupted, duration.LastEndReason);
            Assert.IsFalse(typeof(AnimationNotifyEvent).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Any(field => field.Name.Contains("active", StringComparison.OrdinalIgnoreCase) || field.Name.Contains("runtime", StringComparison.OrdinalIgnoreCase)));
        }

        [Test]
        public void AnimationNotifyEndReason_ContainsRequiredCleanupReasons()
        {
            Assert.IsTrue(Enum.IsDefined(typeof(AnimationNotifyEndReason), AnimationNotifyEndReason.NaturalEnd));
            Assert.IsTrue(Enum.IsDefined(typeof(AnimationNotifyEndReason), AnimationNotifyEndReason.Interrupted));
            Assert.IsTrue(Enum.IsDefined(typeof(AnimationNotifyEndReason), AnimationNotifyEndReason.WeightBelowThreshold));
            Assert.IsTrue(Enum.IsDefined(typeof(AnimationNotifyEndReason), AnimationNotifyEndReason.OwnerDisabled));
            Assert.IsTrue(Enum.IsDefined(typeof(AnimationNotifyEndReason), AnimationNotifyEndReason.StateStopped));
        }

        [Test]
        public void AnimationNotifyRuntime_DispatchesInstantNotifyWhenPlaybackCrossesEventFrame()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            AnimationNotifyTrack track = asset.AddNotifyTrack("Footsteps");
            var notify = new CountingInstantNotify
            {
                EventTag = "AnimEvent.Footstep",
            };
            track.AddEvent(notify, 2);
            var runtime = new AnimationNotifyRuntime(null, asset);

            runtime.Tick(0.1f, 0.1f, 1f);
            runtime.Tick(0.2f, 0.1f, 1f);

            Assert.AreEqual(1, notify.NotifyCount);
            Assert.AreSame(asset, runtime.ClipAsset);
            Assert.AreEqual(0.1f, runtime.LastTime);
            Assert.AreEqual(0.2f, runtime.CurrentTime);
            Assert.AreEqual("AnimEvent.Footstep", notify.LastContext.EventTag);
            Assert.AreEqual(0.2f, notify.LastContext.NormalizedTime);
        }

        [Test]
        public void AnimationNotifyRuntime_DispatchesInstantNotifyWhenSingleTickSkipsAcrossFrame()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            var notify = new CountingInstantNotify();
            asset.AddNotifyTrack("Weapon").AddEvent(notify, 5);
            var runtime = new AnimationNotifyRuntime(null, asset);

            runtime.Tick(0.6f, 0.6f, 1f);

            Assert.AreEqual(1, notify.NotifyCount);
        }

        [Test]
        public void AnimationNotifyRuntime_DoesNotRepeatInstantNotifyOutsideCrossedInterval()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            var notify = new CountingInstantNotify();
            asset.AddNotifyTrack("Weapon").AddEvent(notify, 3);
            var runtime = new AnimationNotifyRuntime(null, asset);

            runtime.Tick(0.4f, 0.4f, 1f);
            runtime.Tick(0.5f, 0.1f, 1f);

            Assert.AreEqual(1, notify.NotifyCount);
        }

        [Test]
        public void AnimationNotifyRuntime_DoesNotRepeatInstantNotifyWhenTimeStopsAfterEvaluate()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            var notify = new CountingInstantNotify();
            asset.AddNotifyTrack("Weapon").AddEvent(notify, 3);
            var runtime = new AnimationNotifyRuntime(null, asset);

            runtime.Tick(0.4f, 0.4f, 1f);
            runtime.Tick(0.4f, 0f, 1f);

            Assert.AreEqual(1, notify.NotifyCount);
        }

        [Test]
        public void AnimationNotifyRuntime_RepeatsInstantNotifyOnForwardLoop()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            var notify = new CountingInstantNotify();
            asset.AddNotifyTrack("Footsteps").AddEvent(notify, 2);
            var runtime = new AnimationNotifyRuntime(null, asset);

            runtime.Tick(0.25f, 0.25f, 1f);
            runtime.Tick(1.25f, 1f, 1f);
            runtime.Tick(2.25f, 1f, 1f);

            Assert.AreEqual(3, notify.NotifyCount);
        }

        [Test]
        public void AnimationNotifyRuntime_SkipsInstantNotifyBelowMinTriggerWeight()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            var notify = new CountingInstantNotify();
            AnimationNotifyEvent notifyEvent = asset.AddNotifyTrack("Footsteps").AddEvent(notify, 3);
            notifyEvent.MinTriggerWeight = 0.5f;
            var runtime = new AnimationNotifyRuntime(null, asset);

            runtime.Tick(0.4f, 0.4f, 0.25f);

            Assert.AreEqual(0, notify.NotifyCount);
        }

        [Test]
        public void AnimationNotifyRuntime_DispatchesOwnerReceiverPolicyToOwner()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            var notify = new CountingInstantNotify
            {
                DispatchPolicy = AnimationNotifyDispatchPolicy.OwnerReceiver,
                EventTag = "AnimEvent.Weapon.Swing",
            };
            asset.AddNotifyTrack("Weapon").AddEvent(notify, 4);
            GameObject gameObject = new GameObject("AnimationNotifyRuntimeOwnerReceiverTest");

            try
            {
                var receiver = gameObject.AddComponent<RecordingNotifyReceiver>();
                var runtime = new AnimationNotifyRuntime(gameObject, asset);

                runtime.Tick(0.5f, 0.5f, 1f);

                Assert.AreEqual(0, notify.NotifyCount);
                Assert.AreEqual(1, receiver.ReceiveCount);
                Assert.AreSame(gameObject, receiver.LastContext.OwnerGameObject);
                Assert.AreEqual("AnimEvent.Weapon.Swing", receiver.LastContext.EventTag);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AnimationNotifyRuntime_IgnoresEmptyTracksAndMissingAsset()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            asset.AddNotifyTrack("Empty");
            var runtime = new AnimationNotifyRuntime(null, asset);
            var missingAssetRuntime = new AnimationNotifyRuntime(null, null);

            Assert.DoesNotThrow(() => runtime.Tick(0.5f, 0.5f, 1f));
            Assert.DoesNotThrow(() => missingAssetRuntime.Tick(0.5f, 0.5f, 1f));
            Assert.AreEqual(0, runtime.ActiveNotifyCount);
            Assert.AreEqual(0, missingAssetRuntime.ActiveNotifyCount);
        }

        [Test]
        public void AnimationNotifyRuntime_RunsDurationNotifyBeginTickAndNaturalEnd()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            var notify = new CountingDurationNotify();
            asset.AddNotifyTrack("Trail").AddEvent(notify, 2, 3);
            var runtime = new AnimationNotifyRuntime(null, asset);

            runtime.Tick(0.25f, 0.25f, 1f);
            runtime.Tick(0.4f, 0.15f, 1f);
            runtime.Tick(0.5f, 0.1f, 1f);

            Assert.AreEqual(1, notify.BeginCount);
            Assert.AreEqual(2, notify.TickCount);
            Assert.AreEqual(1, notify.EndCount);
            Assert.AreEqual(AnimationNotifyEndReason.NaturalEnd, notify.LastEndReason);
            Assert.AreEqual(0, runtime.ActiveNotifyCount);
            Assert.AreSame(asset, notify.LastBeginContext.AnimationAsset);
            Assert.AreSame(asset, notify.LastEndContext.AnimationAsset);
        }

        [Test]
        public void AnimationNotifyRuntime_RepeatsDurationNotifyOnForwardLoop()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            var notify = new CountingDurationNotify();
            asset.AddNotifyTrack("Trail").AddEvent(notify, 2, 3);
            var runtime = new AnimationNotifyRuntime(null, asset);

            runtime.Tick(0.25f, 0.25f, 1f);
            runtime.Tick(0.5f, 0.25f, 1f);
            runtime.Tick(1.25f, 0.75f, 1f);
            runtime.Tick(1.5f, 0.25f, 1f);

            Assert.AreEqual(2, notify.BeginCount);
            Assert.AreEqual(2, notify.EndCount);
            Assert.AreEqual(AnimationNotifyEndReason.NaturalEnd, notify.LastEndReason);
            Assert.AreEqual(0, runtime.ActiveNotifyCount);
        }

        [Test]
        public void AnimationNotifyRuntime_DoesNotBeginDurationNotifyBelowMinTriggerWeight()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            var notify = new CountingDurationNotify();
            AnimationNotifyEvent notifyEvent = asset.AddNotifyTrack("Trail").AddEvent(notify, 2, 3);
            notifyEvent.MinTriggerWeight = 0.6f;
            var runtime = new AnimationNotifyRuntime(null, asset);

            runtime.Tick(0.25f, 0.25f, 0.5f);

            Assert.AreEqual(0, notify.BeginCount);
            Assert.AreEqual(0, notify.TickCount);
            Assert.AreEqual(0, runtime.ActiveNotifyCount);
        }

        [Test]
        public void AnimationNotifyRuntime_EndsActiveDurationNotifyWhenWeightDropsBelowThreshold()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            var notify = new CountingDurationNotify();
            AnimationNotifyEvent notifyEvent = asset.AddNotifyTrack("Trail").AddEvent(notify, 2, 5);
            notifyEvent.MinTriggerWeight = 0.6f;
            var runtime = new AnimationNotifyRuntime(null, asset);

            runtime.Tick(0.25f, 0.25f, 0.8f);
            runtime.Tick(0.35f, 0.1f, 0.5f);

            Assert.AreEqual(1, notify.BeginCount);
            Assert.AreEqual(1, notify.TickCount);
            Assert.AreEqual(1, notify.EndCount);
            Assert.AreEqual(AnimationNotifyEndReason.WeightBelowThreshold, notify.LastEndReason);
            Assert.AreEqual(0, runtime.ActiveNotifyCount);
        }

        [Test]
        public void AnimationNotifyRuntime_EndAllInterruptsActiveDurationNotify()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            var notify = new CountingDurationNotify();
            asset.AddNotifyTrack("Trail").AddEvent(notify, 2, 5);
            var runtime = new AnimationNotifyRuntime(null, asset);

            runtime.Tick(0.25f, 0.25f, 1f);
            runtime.EndAll(AnimationNotifyEndReason.Interrupted);

            Assert.AreEqual(1, notify.BeginCount);
            Assert.AreEqual(1, notify.EndCount);
            Assert.AreEqual(AnimationNotifyEndReason.Interrupted, notify.LastEndReason);
            Assert.AreEqual(0, runtime.ActiveNotifyCount);
        }

        [Test]
        public void AnimationNotifyRuntime_EndsActiveDurationNotifyWhenOwnerDisabled()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            var notify = new CountingDurationNotify();
            asset.AddNotifyTrack("Trail").AddEvent(notify, 2, 5);
            GameObject gameObject = new GameObject("AnimationNotifyRuntimeOwnerDisabledTest");

            try
            {
                var runtime = new AnimationNotifyRuntime(gameObject, asset);
                runtime.Tick(0.25f, 0.25f, 1f);

                gameObject.SetActive(false);
                runtime.Tick(0.35f, 0.1f, 1f);

                Assert.AreEqual(1, notify.BeginCount);
                Assert.AreEqual(1, notify.EndCount);
                Assert.AreEqual(AnimationNotifyEndReason.OwnerDisabled, notify.LastEndReason);
                Assert.AreEqual(0, runtime.ActiveNotifyCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

#if ANIMANCER
        [Test]
        public void AnimationNotifyRuntime_EndsActiveDurationNotifyWhenStateStopped()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            var notify = new CountingDurationNotify();
            asset.AddNotifyTrack("Trail").AddEvent(notify, 2, 5);
            GameObject gameObject = new GameObject("AnimationNotifyRuntimeStateStoppedTest");

            try
            {
                gameObject.AddComponent<Animator>();
                AnimancerComponent animancer = gameObject.AddComponent<AnimancerComponent>();
                AnimancerState state = animancer.Play(asset);
                var runtime = new AnimationNotifyRuntime(gameObject, asset, state, animancer);
                runtime.Tick(0.25f, 0.25f, 1f);

                state.IsPlaying = false;
                runtime.Tick(0.35f, 0.1f, 1f);

                Assert.AreEqual(1, notify.BeginCount);
                Assert.AreEqual(1, notify.EndCount);
                Assert.AreEqual(AnimationNotifyEndReason.StateStopped, notify.LastEndReason);
                Assert.AreEqual(0, runtime.ActiveNotifyCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
#endif

        [Test]
        public void AnimationNotifyRuntime_KeepsDurationStateIsolatedPerRuntimeInstance()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            var notify = new CountingDurationNotify();
            asset.AddNotifyTrack("Trail").AddEvent(notify, 2, 5);
            var firstRuntime = new AnimationNotifyRuntime(null, asset);
            var secondRuntime = new AnimationNotifyRuntime(null, asset);

            firstRuntime.Tick(0.25f, 0.25f, 1f);
            secondRuntime.Tick(0.25f, 0.25f, 1f);
            firstRuntime.EndAll(AnimationNotifyEndReason.Interrupted);

            Assert.AreEqual(2, notify.BeginCount);
            Assert.AreEqual(1, notify.EndCount);
            Assert.AreEqual(0, firstRuntime.ActiveNotifyCount);
            Assert.AreEqual(1, secondRuntime.ActiveNotifyCount);

            secondRuntime.EndAll(AnimationNotifyEndReason.Interrupted);

            Assert.AreEqual(2, notify.EndCount);
            Assert.AreEqual(0, secondRuntime.ActiveNotifyCount);
        }

        [Test]
        public void NotifyEvent_ClampsMoveAndResizeToClipFrameRange()
        {
            AnimationNotifyEvent notifyEvent = new AnimationNotifyEvent
            {
                Notify = new AnimationDurationNotify(),
                StartFrame = 10,
                DurationFrames = 5,
            };

            notifyEvent.MoveToFrame(28, 30);
            Assert.AreEqual(28, notifyEvent.StartFrame);
            Assert.AreEqual(2, notifyEvent.DurationFrames);

            notifyEvent.ResizeStartFrame(20, 30);
            Assert.AreEqual(20, notifyEvent.StartFrame);
            Assert.AreEqual(10, notifyEvent.DurationFrames);

            notifyEvent.ResizeEndFrame(50, 30);
            Assert.AreEqual(20, notifyEvent.StartFrame);
            Assert.AreEqual(10, notifyEvent.DurationFrames);
        }

        [Test]
        public void AnimationEditorWindow_DisablesEditingWhenClipIsMissing()
        {
            AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            AnimationEditorWindow window = AnimationEditorWindow.Open(asset);

            try
            {
                Assert.AreSame(asset, window.EditingAsset);
                Assert.IsFalse(window.CanEditSelectedAsset);

                asset.TryInitialize(new AnimationClip());

                Assert.IsTrue(window.CanEditSelectedAsset);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void OpenHandler_OpensProjectAnimationAssets()
        {
            AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            int instanceId = asset.GetInstanceID();

            bool handled = AnimationAssetOpenHandler.OnOpenAsset(instanceId, 0);

            Assert.IsTrue(handled);
            EditorWindow.GetWindow<AnimationEditorWindow>().Close();
        }

        [Test]
        public void AnimationEditorWindow_ClampsPreviewFrameToClipLength()
        {
            AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            AnimationClip clip = new AnimationClip
            {
                frameRate = 10f,
            };
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            asset.TryInitialize(clip);
            AnimationEditorWindow window = AnimationEditorWindow.Open(asset);

            try
            {
                window.SetPreviewFrame(500);

                Assert.AreEqual(10, window.PreviewFrame);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void AnimationAsset_ManagesNotifyTracks()
        {
            AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            asset.AddNotifyTrack("Default");

            bool renamed = asset.RenameNotifyTrack(0, "Footstep");
            bool removed = asset.RemoveNotifyTrackAt(0);

            Assert.IsTrue(renamed);
            Assert.IsTrue(removed);
            Assert.AreEqual(0, asset.NotifyTracks.Count);
        }

        [Test]
        public void AnimationEditorWindow_AddsNotifyEventsToSelectedTrack()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            AnimationEditorWindow window = AnimationEditorWindow.Open(asset);

            try
            {
                window.AddNotifyTrackToAsset("Footstep");
                window.AddNotifyTrackToAsset("Weapon");
                window.SelectNotifyTrack(1);
                window.SetPreviewFrame(4);

                AnimationNotifyEvent notifyEvent = window.AddNotifyToSelectedTrack();

                Assert.IsNotNull(notifyEvent);
                Assert.AreEqual(2, asset.NotifyTracks.Count);
                Assert.AreEqual(0, asset.NotifyTracks[0].Events.Count);
                Assert.AreEqual(1, asset.NotifyTracks[1].Events.Count);
                Assert.IsInstanceOf<AnimationInstantNotify>(asset.NotifyTracks[1].Events[0].Notify);
                Assert.AreEqual(4, asset.NotifyTracks[1].Events[0].StartFrame);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void AnimationEditorWindow_AddsNotifyStateWithDefaultDuration()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            AnimationEditorWindow window = AnimationEditorWindow.Open(asset);

            try
            {
                window.AddNotifyTrackToAsset("ContextFX");
                window.SetPreviewFrame(5);

                AnimationNotifyEvent notifyEvent = window.AddNotifyStateToSelectedTrack();

                Assert.IsNotNull(notifyEvent);
                Assert.IsInstanceOf<AnimationDurationNotify>(notifyEvent.Notify);
                Assert.AreEqual(5, notifyEvent.StartFrame);
                Assert.Greater(notifyEvent.DurationFrames, 0);
                Assert.LessOrEqual(notifyEvent.EndFrame, 10);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void AnimationEditorWindow_AddsNotifyItemToRequestedTrackAndFrame()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            AnimationEditorWindow window = AnimationEditorWindow.Open(asset);

            try
            {
                window.AddNotifyTrackToAsset("Footstep");
                window.AddNotifyTrackToAsset("Weapon");

                AnimationNotifyEvent notifyEvent = window.AddNotifyToTrackAtFrameForTesting(0, 7, false);

                Assert.IsNotNull(notifyEvent);
                Assert.AreEqual(1, asset.NotifyTracks[0].Events.Count);
                Assert.AreEqual(0, asset.NotifyTracks[1].Events.Count);
                Assert.AreEqual(7, asset.NotifyTracks[0].Events[0].StartFrame);
                Assert.AreEqual(0, asset.NotifyTracks[0].Events[0].DurationFrames);
                Assert.AreEqual(0, window.SelectedNotifyTrackIndex);
                Assert.AreEqual(7, window.PreviewFrame);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void AnimationEditorWindow_InsertsNotifyTrackAfterRequestedTrack()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            AnimationEditorWindow window = AnimationEditorWindow.Open(asset);

            try
            {
                window.AddNotifyTrackToAsset("Footstep");
                window.AddNotifyTrackToAsset("Weapon");

                AnimationNotifyTrack insertedTrack = window.InsertNotifyTrackAfterForTesting(0);

                Assert.IsNotNull(insertedTrack);
                Assert.AreEqual(3, asset.NotifyTracks.Count);
                Assert.AreEqual("Footstep", asset.NotifyTracks[0].Name);
                Assert.AreSame(insertedTrack, asset.NotifyTracks[1]);
                Assert.AreEqual("Weapon", asset.NotifyTracks[2].Name);
                Assert.AreEqual(1, window.SelectedNotifyTrackIndex);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void AnimationEditorWindow_DoesNotResizeInstantNotifyEvents()
        {
            Assert.AreEqual(AnimationEditorDragMode.MoveEvent, AnimationEditorWindow.GetTimelineEventDragModeForTesting(0f, 12f, false));
            Assert.AreEqual(AnimationEditorDragMode.MoveEvent, AnimationEditorWindow.GetTimelineEventDragModeForTesting(11.9f, 12f, false));
            Assert.AreEqual(AnimationEditorDragMode.ResizeStart, AnimationEditorWindow.GetTimelineEventDragModeForTesting(0f, 24f, true));
            Assert.AreEqual(AnimationEditorDragMode.ResizeEnd, AnimationEditorWindow.GetTimelineEventDragModeForTesting(23.9f, 24f, true));
        }

        [Test]
        public void AnimationEditorWindow_SplitsNotifyMenuTypesByDuration()
        {
            var notifyTypes = AnimationEditorWindow.GetNotifyMenuTypesForTesting(false);
            var notifyStateTypes = AnimationEditorWindow.GetNotifyMenuTypesForTesting(true);

            Assert.Contains(typeof(AnimationInstantNotify), notifyTypes);
            Assert.IsFalse(notifyTypes.Contains(typeof(AnimationDurationNotify)));
            Assert.Contains(typeof(AnimationDurationNotify), notifyStateTypes);
            Assert.IsFalse(notifyStateTypes.Contains(typeof(AnimationInstantNotify)));
        }

        [Test]
        public void AnimationEditorWindow_UsesCustomStringFieldForNotifyLabel()
        {
            var notify = new LabelledInstantNotify();

            string label = AnimationEditorWindow.GetNotifyLabelForTesting(notify);

            Assert.AreEqual("Foot Plant Left", label);
        }

        [Test]
        public void AnimationEditorWindow_DisablesTrackAndEventCreationWithoutClip()
        {
            AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            AnimationEditorWindow window = AnimationEditorWindow.Open(asset);

            try
            {
                Assert.IsNull(window.AddNotifyTrackToAsset("Invalid"));
                Assert.IsNull(window.AddNotifyToSelectedTrack());
                Assert.IsNull(window.AddNotifyStateToSelectedTrack());
                Assert.AreEqual(0, asset.NotifyTracks.Count);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void AnimationEditorWindow_TimelineZoomChangesFrameCoordinateMapping()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            AnimationEditorWindow window = AnimationEditorWindow.Open(asset);

            try
            {
                window.SetTimelineFrameWidth(8f);
                float zoomedOutX = window.FrameToTimelineXForTesting(5);
                int zoomedOutFrame = window.TimelineXToFrameForTesting(zoomedOutX, 10);

                window.SetTimelineFrameWidth(24f);
                float zoomedInX = window.FrameToTimelineXForTesting(5);
                int zoomedInFrame = window.TimelineXToFrameForTesting(zoomedInX, 10);

                Assert.AreEqual(40f, zoomedOutX);
                Assert.AreEqual(120f, zoomedInX);
                Assert.AreEqual(5, zoomedOutFrame);
                Assert.AreEqual(5, zoomedInFrame);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void AnimationWindowState_StoresEditingContextAndClampsPreviewFrame()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            asset.AddNotifyTrack("Default");
            var state = new AnimationWindowState();

            state.SetEditingAsset(asset);
            state.SetPreviewFrame(500);
            state.SetSelectedNotifyTrackIndex(20);
            state.BeginDrag(0, 1, AnimationEditorDragMode.MoveEvent);

            Assert.AreSame(asset, state.EditingAsset);
            Assert.AreEqual(10, state.PreviewFrame);
            Assert.AreEqual(0, state.SelectedNotifyTrackIndex);
            Assert.IsTrue(state.IsDragging);
            Assert.AreEqual(AnimationEditorDragMode.MoveEvent, state.ActiveDragMode);

            state.EndDrag();

            Assert.IsFalse(state.IsDragging);
        }

        [Test]
        public void TimeUtility_ConvertsBetweenFramesTimeAndPixels()
        {
            Rect rect = new Rect(12f, 0f, 200f, 20f);

            float x = TimeUtility.FrameToX(rect, 5, 8f);
            int frame = TimeUtility.XToFrame(rect, x, 10, 8f);
            double time = TimeUtility.FrameToTime(12, 24f);
            int roundTripFrame = TimeUtility.TimeToFrame(time, 24f);

            Assert.AreEqual(52f, x);
            Assert.AreEqual(5, frame);
            Assert.AreEqual(0.5d, time);
            Assert.AreEqual(12, roundTripFrame);
        }

        [Test]
        public void AnimationEventService_AddsMovesAndTrimsEvents()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            var service = new AnimationEventService();
            service.AddTrack(asset, "Default");

            AnimationNotifyEvent notifyEvent = service.AddNotify(asset, 0, new AnimationDurationNotify(), 3, 4);
            bool moved = service.MoveEvent(asset, notifyEvent, 8);
            bool trimmed = service.TrimEvent(asset, notifyEvent, 10, AnimationEditorDragMode.ResizeEnd);

            Assert.IsNotNull(notifyEvent);
            Assert.IsTrue(moved);
            Assert.IsTrue(trimmed);
            Assert.AreEqual(8, notifyEvent.StartFrame);
            Assert.AreEqual(2, notifyEvent.DurationFrames);
        }

        [Test]
        public void AnimationEventService_EditsTriggerWeightAndDeletesEvents()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            var service = new AnimationEventService();
            service.AddTrack(asset, "Default");
            AnimationNotifyEvent notifyEvent = service.AddNotify(asset, 0, new AnimationInstantNotify(), 3, 0);

            bool weighted = service.SetEventMinTriggerWeight(asset, notifyEvent, 0.5f);
            bool moved = service.SetEventStartFrame(asset, notifyEvent, 8);
            bool deleted = service.RemoveEvent(asset, 0, 0);

            Assert.IsTrue(weighted);
            Assert.AreEqual(0.5f, notifyEvent.MinTriggerWeight);
            Assert.IsTrue(moved);
            Assert.AreEqual(8, notifyEvent.StartFrame);
            Assert.IsTrue(deleted);
            Assert.AreEqual(0, asset.NotifyTracks[0].Events.Count);
        }

        [Test]
        public void AnimationEventService_AddTrackSupportsUndoRedo()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            var service = new AnimationEventService();

            try
            {
                service.AddTrack(asset, "Context Track");

                Assert.AreEqual(1, asset.NotifyTracks.Count);

                Undo.PerformUndo();

                Assert.AreEqual(0, asset.NotifyTracks.Count);

                Undo.PerformRedo();

                Assert.AreEqual(1, asset.NotifyTracks.Count);
                Assert.AreEqual("Context Track", asset.NotifyTracks[0].Name);
            }
            finally
            {
                Undo.ClearUndo(asset);
            }
        }

        [Test]
        public void EventLayoutUtility_AssignsSeparateLanesForOverlappingDurationEvents()
        {
            var events = new[]
            {
                new AnimationNotifyEvent
                {
                    Notify = new AnimationDurationNotify(),
                    StartFrame = 1,
                    DurationFrames = 5,
                },
                new AnimationNotifyEvent
                {
                    Notify = new AnimationDurationNotify(),
                    StartFrame = 3,
                    DurationFrames = 5,
                },
            };

            var layouts = EventLayoutUtility.LayoutEvents(events, new Rect(0f, 0f, 200f, 32f), 10f);

            Assert.AreEqual(0, layouts[0].Lane);
            Assert.AreEqual(1, layouts[1].Lane);
            Assert.AreNotEqual(layouts[0].Rect.y, layouts[1].Rect.y);
        }

        [Test]
        public void EventLayoutUtility_SeparatesInstantEventsOnSameFrame()
        {
            var events = new[]
            {
                new AnimationNotifyEvent
                {
                    Notify = new AnimationInstantNotify(),
                    StartFrame = 2,
                },
                new AnimationNotifyEvent
                {
                    Notify = new AnimationInstantNotify(),
                    StartFrame = 2,
                },
            };

            var layouts = EventLayoutUtility.LayoutEvents(events, new Rect(0f, 0f, 200f, 38f), 10f);

            Assert.AreEqual(0, layouts[0].Lane);
            Assert.AreEqual(1, layouts[1].Lane);
            Assert.AreNotEqual(layouts[0].Rect.y, layouts[1].Rect.y);
        }

        [Test]
        public void EventLayoutUtility_AvoidsVisibleInstantEventLabelOverlap()
        {
            var events = new[]
            {
                new AnimationNotifyEvent
                {
                    Notify = new AnimationInstantNotify(),
                    StartFrame = 12,
                },
                new AnimationNotifyEvent
                {
                    Notify = new AnimationDurationNotify(),
                    StartFrame = 24,
                    DurationFrames = 8,
                },
            };

            var layouts = EventLayoutUtility.LayoutEvents(events, new Rect(0f, 0f, 200f, 38f), 4f, notifyEvent => 80f);

            Assert.AreEqual(0, layouts[0].Lane);
            Assert.AreEqual(1, layouts[1].Lane);
            Assert.AreNotEqual(layouts[0].Rect.y, layouts[1].Rect.y);
        }

        [Test]
        public void TimelineLayoutUtility_AlignsRulerContentAndTrackRows()
        {
            TimelineLayout layout = TimelineLayoutUtility.Calculate(new Rect(0f, 0f, 1000f, 720f), 2, 100);
            TrackLayout groupTrack = TimelineLayoutUtility.GetNotifyGroupLayout(layout);
            TrackLayout firstTrack = TimelineLayoutUtility.GetTrackLayout(layout, 0);

            Assert.AreEqual(layout.RulerRect.x, layout.ContentRect.x);
            Assert.AreEqual(layout.RulerRect.width, layout.ContentRect.width);
            Assert.AreEqual(layout.HeaderRect.xMax, layout.RulerRect.x);
            Assert.AreEqual(layout.ContentRect.y, groupTrack.LaneRect.y);
            Assert.AreEqual(groupTrack.RowRect.yMax + AnimationEditorConstants.TrackGap, firstTrack.LaneRect.y);
            Assert.AreEqual(layout.HeaderRect.x, firstTrack.HeaderRect.x);
            Assert.AreEqual(layout.RulerRect.x, firstTrack.LaneRect.x);
        }

        [Test]
        public void AnimationEditorWindow_TogglesNotifyTrackTreeExpansionWithoutChangingAsset()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            AnimationEditorWindow window = AnimationEditorWindow.Open(asset);

            try
            {
                window.AddNotifyTrackToAsset("Footstep");

                Assert.IsTrue(window.NotifyTracksExpanded);

                window.SetNotifyTracksExpanded(false);

                Assert.IsFalse(window.NotifyTracksExpanded);
                Assert.AreEqual(1, asset.NotifyTracks.Count);

                window.SetNotifyTracksExpanded(true);

                Assert.IsTrue(window.NotifyTracksExpanded);
                Assert.AreEqual(1, asset.NotifyTracks.Count);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void TimelineLayoutUtility_ConservesPreviewHeightAndFillsRemainingTimelineArea()
        {
            TimelineLayout layout = TimelineLayoutUtility.Calculate(new Rect(0f, 0f, 1180f, 760f), 2, 177);

            Assert.GreaterOrEqual(layout.PreviewRect.height, AnimationEditorConstants.MinPreviewHeight);
            Assert.LessOrEqual(layout.PreviewRect.height, AnimationEditorConstants.MaxPreviewHeight);
            Assert.AreEqual(layout.RootRect.yMax, layout.TimelineRect.yMax);
            Assert.Greater(layout.TimelineRect.height, AnimationEditorConstants.TimelineHeaderHeight + AnimationEditorConstants.TimelineRulerHeight + AnimationEditorConstants.TrackHeight * 2f);
        }

        [Test]
        public void TimelineRulerUtility_AdaptsMinorTickStyleToFrameWidth()
        {
            Assert.AreEqual(TimelineRulerTickStyle.Hidden, TimelineRulerUtility.GetMinorTickStyle(AnimationEditorConstants.MinorTickDotFrameWidth - 0.1f));
            Assert.AreEqual(TimelineRulerTickStyle.Dot, TimelineRulerUtility.GetMinorTickStyle(AnimationEditorConstants.MinorTickDotFrameWidth));
            Assert.AreEqual(TimelineRulerTickStyle.Bar, TimelineRulerUtility.GetMinorTickStyle(AnimationEditorConstants.MinorTickBarFrameWidth));
        }

        [Test]
        public void AnimationEditorWindow_RemoveSelectedTrackClampsSelection()
        {
            AnimationClipAsset asset = CreateEditableClipAsset();
            AnimationEditorWindow window = AnimationEditorWindow.Open(asset);

            try
            {
                window.AddNotifyTrackToAsset("Footstep");
                window.AddNotifyTrackToAsset("Weapon");
                window.SelectNotifyTrack(1);

                bool removed = window.RemoveSelectedNotifyTrack();

                Assert.IsTrue(removed);
                Assert.AreEqual(1, asset.NotifyTracks.Count);
                Assert.AreEqual(0, window.SelectedNotifyTrackIndex);
            }
            finally
            {
                window.Close();
            }
        }

        private static AnimationClipAsset CreateEditableClipAsset()
        {
            AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            AnimationClip clip = new AnimationClip
            {
                frameRate = 10f,
            };
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            asset.TryInitialize(clip);
            return asset;
        }

        private static void DeleteAssetFolderIfEmpty(string folderPath)
        {
            string absoluteFolderPath = Path.GetFullPath(folderPath);
            if (!Directory.Exists(absoluteFolderPath) || Directory.EnumerateFileSystemEntries(absoluteFolderPath).Any())
            {
                return;
            }

            AssetDatabase.DeleteAsset(folderPath);
        }

        private static void EnsureAssetFolder(string parentPath, string folderName)
        {
            string folderPath = $"{parentPath}/{folderName}";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder(parentPath, folderName);
            }
        }

        private class LabelledInstantNotify : AnimationInstantNotify
        {
            [SerializeField] private string eventName = "Foot Plant Left";
        }

        private class CountingInstantNotify : AnimationInstantNotify
        {
            public int NotifyCount { get; private set; }
            public AnimationEventContext LastContext { get; private set; }

            public override void OnNotify(AnimationEventContext context)
            {
                NotifyCount++;
                LastContext = context;
            }
        }

        private class CountingDurationNotify : AnimationDurationNotify
        {
            public int BeginCount { get; private set; }
            public int TickCount { get; private set; }
            public int EndCount { get; private set; }
            public AnimationNotifyEndReason LastEndReason { get; private set; }
            public AnimationEventContext LastBeginContext { get; private set; }
            public AnimationEventContext LastTickContext { get; private set; }
            public AnimationEventContext LastEndContext { get; private set; }

            public override void OnBegin(AnimationEventContext context)
            {
                BeginCount++;
                LastBeginContext = context;
            }

            public override void OnTick(AnimationEventContext context)
            {
                TickCount++;
                LastTickContext = context;
            }

            public override void OnEnd(AnimationEventContext context, AnimationNotifyEndReason reason)
            {
                EndCount++;
                LastEndReason = reason;
                LastEndContext = context;
            }
        }

        private class RecordingNotifyReceiver : MonoBehaviour, IAnimationNotifyReceiver
        {
            public int ReceiveCount { get; private set; }
            public AnimationEventContext LastContext { get; private set; }

            public void OnAnimationNotify(AnimationEventContext context)
            {
                ReceiveCount++;
                LastContext = context;
            }
        }
    }
}
