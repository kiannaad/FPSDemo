using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;

namespace CGame.Animation.Tests
{
    public sealed class CharacterAnimationChannelMixerTests
    {
        private static readonly List<string> Trace = new List<string>();

        [SetUp]
        public void SetUp()
        {
            Trace.Clear();
            ReentrantInstantNotify.Action = null;
        }

        [Test]
        public void NegativeSpeed_DefaultsToClipEndAndExplicitStartOverridesIt()
        {
            PlayableGraph graph = PlayableGraph.Create("NegativeStartTest");
            try
            {
                AnimationClipAsset asset = CreateAsset(-1f);
                Assert.That(CharacterAnimationPlayable.TryCreate(graph, asset, 1, 1, false, out CharacterAnimationPlayable reverse, out string error), Is.True, error);
                Assert.That(reverse.StartTime, Is.EqualTo(1f).Within(0.0001f));
                reverse.Dispose();

                asset.OverrideNormalizedStartTime = true;
                asset.NormalizedStartTime = 0.25f;
                Assert.That(CharacterAnimationPlayable.TryCreate(graph, asset, 2, 2, false, out CharacterAnimationPlayable explicitStart, out error), Is.True, error);
                Assert.That(explicitStart.StartTime, Is.EqualTo(0.25f).Within(0.0001f));
                explicitStart.Dispose();
            }
            finally
            {
                graph.Destroy();
            }
        }

        [Test]
        public void ZeroSpeed_IsRejectedButNegativeSpeedIsValid()
        {
            AnimationClipAsset asset = CreateAsset(0f);
            Assert.That(asset.TryValidateForPlayback(out _), Is.False);

            asset.Speed = -2f;

            Assert.That(asset.TryValidateForPlayback(out string error), Is.True, error);
        }

        [Test]
        public void PlayableCreation_SortsCopiedEntriesWithoutMutatingAssetAndSkipsInvalidEvent()
        {
            PlayableGraph graph = PlayableGraph.Create("NotifyEntryCreationTest");
            CharacterAnimationPlayable playable = null;
            try
            {
                AnimationClipAsset asset = CreateAsset(1f);
                AnimationNotifyTrack track = asset.AddNotifyTrack();
                track.AddEvent(new RecordingInstantNotify("late"), 15);
                track.AddEvent(new AnimationInstantNotify(), 10, 1);
                track.AddEvent(new RecordingInstantNotify("early"), 5);
                LogAssert.Expect(LogType.Error, "Instant Notify 'Notify' requires DurationFrames equal to zero.");

                Assert.That(CharacterAnimationPlayable.TryCreate(graph, asset, 1, 1, false, out playable, out string error), Is.True, error);

                Assert.That(track.Events[0].StartFrame, Is.EqualTo(15));
                Assert.That(track.Events[1].StartFrame, Is.EqualTo(10));
                Assert.That(track.Events[2].StartFrame, Is.EqualTo(5));
                Assert.That(playable.NotifyEntries.Length, Is.EqualTo(2));
                Assert.That(playable.NotifyEntries[0].StartFrame, Is.EqualTo(5));
                Assert.That(playable.NotifyEntries[1].StartFrame, Is.EqualTo(15));
            }
            finally
            {
                playable?.Dispose();
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
            }
        }

        [Test]
        public void DispatchAuthority_AllowsLogicalSlotAndSuppressesSynchronizedCopy()
        {
            PlayableGraph graph = PlayableGraph.Create("NotifyAuthorityTest");
            CharacterAnimationChannelMixer logical = null;
            CharacterAnimationChannelMixer synchronizedCopy = null;
            try
            {
                AnimationClipAsset asset = CreateAsset(1f);
                asset.AddNotifyTrack().AddEvent(new RecordingInstantNotify("instant"), 0);
                logical = new CharacterAnimationChannelMixer(graph, 0, Playable.Null, new Pawn());
                synchronizedCopy = new CharacterAnimationChannelMixer(graph, 0, Playable.Null, new Pawn());
                logical.Play(asset, 1, 1, false, false, true, true);
                synchronizedCopy.Play(asset, 2, 1, false, false, true, false);

                logical.Update();
                synchronizedCopy.Update();

                Assert.That(Trace, Is.EqualTo(new[] { "instant" }));
            }
            finally
            {
                synchronizedCopy?.Dispose();
                logical?.Dispose();
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
            }
        }

        [Test]
        public void ReentrantPlay_IsRejectedWithoutBreakingMixerUpdate()
        {
            PlayableGraph graph = PlayableGraph.Create("NotifyReentryTest");
            CharacterAnimationChannelMixer mixer = null;
            try
            {
                AnimationClipAsset asset = CreateAsset(1f);
                asset.AddNotifyTrack().AddEvent(new ReentrantInstantNotify(), 0);
                mixer = new CharacterAnimationChannelMixer(graph, 0, Playable.Null, new Pawn());
                mixer.Play(asset, 1, 1, false, false, true);
                AnimationPlaybackHandle reentrantResult = null;
                ReentrantInstantNotify.Action = () =>
                    reentrantResult = mixer.Play(asset, 2, 2, false, false, true);
                LogAssert.Expect(LogType.Error, "Synchronous Play during CharacterAnimationChannelMixer.Update is not allowed.");

                mixer.Update();

                Assert.That(reentrantResult, Is.Not.Null);
                Assert.That(reentrantResult.State, Is.EqualTo(AnimationPlaybackState.Failed));
                Assert.That(mixer.ActiveSlotCount, Is.EqualTo(1));
            }
            finally
            {
                mixer?.Dispose();
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
            }
        }

        [Test]
        public void ReentrantStop_IsRejectedWithoutBreakingMixerUpdate()
        {
            PlayableGraph graph = PlayableGraph.Create("NotifyStopReentryTest");
            CharacterAnimationChannelMixer mixer = null;
            try
            {
                AnimationClipAsset asset = CreateAsset(1f);
                asset.AddNotifyTrack().AddEvent(new ReentrantInstantNotify(), 0);
                mixer = new CharacterAnimationChannelMixer(graph, 0, Playable.Null, new Pawn());
                AnimationPlaybackHandle handle = mixer.Play(asset, 1, 1, false, false, true);
                bool stopped = true;
                ReentrantInstantNotify.Action = () => stopped = mixer.Stop(handle);
                LogAssert.Expect(LogType.Error, "Synchronous Stop during CharacterAnimationChannelMixer.Update is not allowed.");

                mixer.Update();

                Assert.That(stopped, Is.False);
                Assert.That(handle.IsTerminal, Is.False);
                Assert.That(mixer.ActiveSlotCount, Is.EqualTo(1));
            }
            finally
            {
                mixer?.Dispose();
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
            }
        }

        [Test]
        public void SetPlaybackTime_EndsActiveDurationAndDoesNotBackfillJumpedInstant()
        {
            PlayableGraph graph = PlayableGraph.Create("NotifySetTimeTest");
            CharacterAnimationChannelMixer mixer = null;
            try
            {
                AnimationClipAsset asset = CreateAsset(1f);
                AnimationNotifyTrack track = asset.AddNotifyTrack();
                track.AddEvent(new RecordingDurationNotify("duration"), 0, 20);
                track.AddEvent(new RecordingInstantNotify("jumped"), 10);
                mixer = new CharacterAnimationChannelMixer(graph, 0, Playable.Null, new Pawn());
                AnimationPlaybackHandle handle = mixer.Play(asset, 1, 1, false, false, true);
                mixer.Update();
                Trace.Clear();

                Assert.That(mixer.TrySetPlaybackTime(handle, 0.75d), Is.True);
                mixer.Update();

                Assert.That(Trace, Is.EqualTo(new[] { "duration:end:StateStopped" }));
            }
            finally
            {
                mixer?.Dispose();
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
            }
        }

        [Test]
        public void NegativePlayback_UsesTravelDistanceForBlendCurveAndAutoCompletion()
        {
            PlayableGraph graph = PlayableGraph.Create("NegativePlaybackSymmetryTest");
            CharacterAnimationChannelMixer mixer = null;
            CharacterAnimationPlayable curvePlayable = null;
            try
            {
                AnimationClipAsset asset = CreateAsset(-1f);
                asset.BlendInTime = 0.5f;
                asset.SetNamedCurve("Progress", AnimationCurve.Linear(0f, 0f, 1f, 1f));
                mixer = new CharacterAnimationChannelMixer(graph, 0, Playable.Null, new Pawn());
                AnimationPlaybackHandle handle = mixer.Play(asset, 1, 1, true, false);
                Assert.That(mixer.TrySetPlaybackTime(handle, 0.75d), Is.True);

                mixer.Update();

                Assert.That(mixer.Mixer.GetInputWeight(0), Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(CharacterAnimationPlayable.TryCreate(graph, asset, 2, 2, false, out curvePlayable, out string error), Is.True, error);
                curvePlayable.Playable.SetTime(0.25d);
                Assert.That(curvePlayable.TryEvaluateCurve("Progress", out float curveValue), Is.True);
                Assert.That(curveValue, Is.EqualTo(0.25f).Within(0.001f));

                Assert.That(mixer.TrySetPlaybackTime(handle, 0d), Is.True);
                mixer.Update();
                Assert.That(handle.State, Is.EqualTo(AnimationPlaybackState.Completed));
                Assert.That(mixer.ActiveSlotCount, Is.Zero);
            }
            finally
            {
                curvePlayable?.Dispose();
                mixer?.Dispose();
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
            }
        }

        [Test]
        public void ReleasingReplacement_RestoresDurationWithoutBackfillingInstant()
        {
            PlayableGraph graph = PlayableGraph.Create("NotifySlotRestoreTest");
            CharacterAnimationChannelMixer mixer = null;
            try
            {
                AnimationClipAsset oldAsset = CreateAsset(1f, "OldClip");
                AnimationNotifyTrack oldTrack = oldAsset.AddNotifyTrack();
                oldTrack.AddEvent(new RecordingDurationNotify("oldDuration"), 0, 20);
                oldTrack.AddEvent(new RecordingInstantNotify("oldInstant"), 0);
                AnimationClipAsset replacementAsset = CreateAsset(1f, "ReplacementClip");
                replacementAsset.BlendOutTime = 0.1f;
                mixer = new CharacterAnimationChannelMixer(graph, 0, Playable.Null, new Pawn());
                mixer.Play(oldAsset, 1, 1, false, false, true);
                mixer.Update();
                AnimationPlaybackHandle replacement = mixer.Play(replacementAsset, 2, 2, false, false, true);
                Assert.That(mixer.Stop(replacement), Is.True);
                Trace.Clear();
                Assert.That(mixer.TrySetPlaybackTime(replacement, 0.1d), Is.True);

                mixer.Update();

                Assert.That(Trace, Is.EqualTo(new[] { "oldDuration:begin", "oldDuration:tick" }));
                Assert.That(mixer.ActiveSlotCount, Is.EqualTo(1));
            }
            finally
            {
                mixer?.Dispose();
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
            }
        }

        [Test]
        public void Dispose_EndsActiveDurationWithOwnerDisabled()
        {
            PlayableGraph graph = PlayableGraph.Create("NotifyOwnerDisabledTest");
            CharacterAnimationChannelMixer mixer = null;
            try
            {
                AnimationClipAsset asset = CreateAsset(1f);
                asset.AddNotifyTrack().AddEvent(new RecordingDurationNotify("duration"), 0, 20);
                mixer = new CharacterAnimationChannelMixer(graph, 0, Playable.Null, new Pawn());
                mixer.Play(asset, 1, 1, false, false, true);
                mixer.Update();
                Trace.Clear();

                mixer.Dispose();

                Assert.That(Trace, Is.EqualTo(new[] { "duration:end:OwnerDisabled" }));
                mixer = null;
            }
            finally
            {
                mixer?.Dispose();
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
            }
        }

        private static AnimationClipAsset CreateAsset(float speed, string clipName = "ChannelMixerTestClip")
        {
            AnimationClip clip = new AnimationClip
            {
                frameRate = 20f,
                name = clipName,
            };
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            AnimationClipAsset asset = ScriptableObject.CreateInstance<AnimationClipAsset>();
            Assert.That(asset.TryInitialize(clip), Is.True);
            asset.Speed = speed;
            asset.BlendInTime = 0f;
            asset.BlendOutTime = 0f;
            return asset;
        }

        [Serializable]
        private sealed class RecordingInstantNotify : AnimationInstantNotify
        {
            private readonly string name;

            public RecordingInstantNotify(string name)
            {
                this.name = name;
            }

            public override void OnNotify(Pawn pawn)
            {
                Trace.Add(name);
            }
        }

        [Serializable]
        private sealed class RecordingDurationNotify : AnimationDurationNotify
        {
            private readonly string name;

            public RecordingDurationNotify(string name)
            {
                this.name = name;
            }

            public override void OnBegin(Pawn pawn)
            {
                Trace.Add($"{name}:begin");
            }

            public override void OnTick(Pawn pawn)
            {
                Trace.Add($"{name}:tick");
            }

            public override void OnEnd(Pawn pawn, AnimationNotifyEndReason reason)
            {
                Trace.Add($"{name}:end:{reason}");
            }
        }

        [Serializable]
        private sealed class ReentrantInstantNotify : AnimationInstantNotify
        {
            public static Action Action { get; set; }

            public override void OnNotify(Pawn pawn)
            {
                Action?.Invoke();
            }
        }
    }
}
