using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CGame.Animation.Tests
{
    public sealed class AnimationNotifyPlaybackStateTests
    {
        private static readonly List<string> Trace = new List<string>();
        private Pawn pawn;

        [SetUp]
        public void SetUp()
        {
            Trace.Clear();
            pawn = new Pawn();
        }

        [Test]
        public void StartingPosition_DispatchesInstantThenDurationBeginAndTick()
        {
            AnimationNotifyPlaybackState state = CreateState(
                1d,
                false,
                1f,
                Instant("instant", 0f),
                Duration("duration", 0f, 0.5f));

            state.Update(0d, 1f);

            Assert.That(Trace, Is.EqualTo(new[] { "instant", "duration:begin", "duration:tick" }));
        }

        [Test]
        public void ForwardScan_UsesOpenClosedIntervalAndEndsBeforeSameFrameInstant()
        {
            AnimationNotifyPlaybackState state = CreateState(
                1d,
                false,
                1f,
                Duration("duration", 0.2f, 0.5f),
                Instant("instant", 0.5f));
            state.Update(0d, 1f);
            state.Update(0.2d, 1f);
            Trace.Clear();

            state.Update(0.5d, 1f);

            Assert.That(Trace, Is.EqualTo(new[] { "duration:end:NaturalEnd", "instant" }));
        }

        [Test]
        public void SameFrameBeginAndInstant_FollowTrackAndEventOrderBeforeTick()
        {
            AnimationNotifyRuntimeEntry duration = new AnimationNotifyRuntimeEntry(
                new RecordingDurationNotify("duration"), 10, 15, 0.5f, 0.75f, 0f, 0, 0);
            AnimationNotifyRuntimeEntry instant = new AnimationNotifyRuntimeEntry(
                new RecordingInstantNotify("instant"), 10, 10, 0.5f, 0.5f, 0f, 1, 0);
            AnimationNotifyPlaybackState state = CreateState(1d, false, 1f, instant, duration);
            state.Update(0d, 1f);

            state.Update(0.5d, 1f);

            Assert.That(Trace, Is.EqualTo(new[] { "duration:begin", "instant", "duration:tick" }));
        }

        [Test]
        public void ReverseScan_UsesClosedOpenIntervalAndReversesDurationLifecycle()
        {
            AnimationNotifyPlaybackState state = CreateState(
                1d,
                false,
                -1f,
                Duration("duration", 0.2f, 0.8f),
                Instant("instant", 0.5f));
            state.Update(1d, 1f);
            state.Update(0.8d, 1f);
            state.Update(0.5d, 1f);
            state.Update(0.2d, 1f);

            Assert.That(Trace, Is.EqualTo(new[]
            {
                "duration:begin",
                "duration:tick",
                "instant",
                "duration:tick",
                "duration:end:NaturalEnd",
            }));
        }

        [Test]
        public void LoopScan_DispatchesEveryCrossedCycle()
        {
            AnimationNotifyPlaybackState state = CreateState(1d, true, 1f, Instant("instant", 0.25f));
            state.Update(0d, 1f);

            state.Update(2.3d, 1f);

            Assert.That(Trace, Is.EqualTo(new[] { "instant", "instant", "instant" }));
        }

        [Test]
        public void DispatchStorm_StopsAtSafetyLimitAndReportsError()
        {
            AnimationNotifyPlaybackState state = CreateState(1d, true, 1f, Instant("instant", 0.25f));
            state.Update(0d, 1f);
            LogAssert.Expect(LogType.Error, "Animation Notify dispatch exceeded 64 callbacks in one update.");

            state.Update(100d, 1f);

            Assert.That(Trace.Count, Is.EqualTo(64));
        }

        [Test]
        public void StopImmediately_EndsActiveDurationWhenSlotIsReplaced()
        {
            RecordingDurationNotify notify = new RecordingDurationNotify("duration");
            notify.FadeOutPolicy = AnimationNotifyFadeOutPolicy.StopImmediately;
            AnimationNotifyPlaybackState state = CreateState(1d, false, 1f, Duration(notify, 0.1f, 0.9f));
            state.Update(0d, 1f);
            state.Update(0.2d, 1f);
            Trace.Clear();

            state.MarkReplaced();

            Assert.That(Trace, Is.EqualTo(new[] { "duration:end:Interrupted" }));
            Assert.That(state.ActiveDurationCount, Is.Zero);
        }

        [Test]
        public void FinishActiveDuration_DoesNotStartNewEventsButFinishesExistingDuration()
        {
            RecordingDurationNotify duration = new RecordingDurationNotify("duration");
            duration.FadeOutPolicy = AnimationNotifyFadeOutPolicy.FinishActiveDuration;
            RecordingInstantNotify instant = new RecordingInstantNotify("instant");
            instant.FadeOutPolicy = AnimationNotifyFadeOutPolicy.FinishActiveDuration;
            AnimationNotifyPlaybackState state = CreateState(
                1d,
                false,
                1f,
                Duration(duration, 0.1f, 0.9f),
                Instant(instant, 0.5f));
            state.Update(0d, 1f);
            state.Update(0.2d, 1f);
            Trace.Clear();
            state.MarkReplaced();

            state.Update(0.5d, 1f);
            state.Update(0.9d, 1f);

            Assert.That(Trace, Is.EqualTo(new[] { "duration:tick", "duration:end:NaturalEnd" }));
        }

        [Test]
        public void ContinueWhileWeighted_StartsEventsDuringVisualFade()
        {
            RecordingInstantNotify instant = new RecordingInstantNotify("instant");
            instant.FadeOutPolicy = AnimationNotifyFadeOutPolicy.ContinueWhileWeighted;
            AnimationNotifyPlaybackState state = CreateState(1d, false, 1f, Instant(instant, 0.5f));
            state.Update(0d, 1f);
            state.MarkReplaced();

            state.Update(0.5d, 0.5f);

            Assert.That(Trace, Is.EqualTo(new[] { "instant" }));
        }

        [Test]
        public void WeightDropAndSetTime_EndDurationWithExplicitReasons()
        {
            AnimationNotifyPlaybackState state = CreateState(1d, false, 1f, Duration("duration", 0.1f, 0.9f, 0.5f));
            state.Update(0d, 1f);
            state.Update(0.2d, 1f);
            Trace.Clear();
            state.Update(0.3d, 0.25f);
            Assert.That(Trace, Is.EqualTo(new[] { "duration:end:WeightBelowThreshold" }));
            state.Restore(0.4d, 1f);
            Trace.Clear();

            state.ResetTime(0.7d);

            Assert.That(Trace, Is.EqualTo(new[] { "duration:end:StateStopped" }));
        }

        [Test]
        public void CallbackException_IsolatedAndLaterInstantStillRuns()
        {
            AnimationNotifyPlaybackState state = CreateState(
                1d,
                false,
                1f,
                new AnimationNotifyRuntimeEntry(new ThrowingInstantNotify(), 10, 10, 0.5f, 0.5f, 0f, 0, 0),
                Instant("after", 0.5f, 1));
            state.Update(0d, 1f);
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: notify failure");

            state.Update(0.5d, 1f);

            Assert.That(Trace, Is.EqualTo(new[] { "after" }));
        }

        [Test]
        public void RuntimeEntry_SortsByStartFrameThenTrackThenEventWithoutAssetMutation()
        {
            AnimationNotifyRuntimeEntry[] entries =
            {
                Instant("third", 0.5f, 0, 1),
                Instant("second", 0.5f, 1, 0),
                Instant("first", 0.25f, 0, 2),
            };

            Array.Sort(entries);

            Assert.That(((RecordingInstantNotify)entries[0].Notify).Name, Is.EqualTo("first"));
            Assert.That(((RecordingInstantNotify)entries[1].Notify).Name, Is.EqualTo("second"));
            Assert.That(((RecordingInstantNotify)entries[2].Notify).Name, Is.EqualTo("third"));
        }

        private AnimationNotifyPlaybackState CreateState(
            double length,
            bool looping,
            float speed,
            params AnimationNotifyRuntimeEntry[] entries)
        {
            Array.Sort(entries);
            return new AnimationNotifyPlaybackState(pawn, entries, length, looping, speed);
        }

        private static AnimationNotifyRuntimeEntry Instant(
            string name,
            float time,
            int eventIndex = 0,
            int trackIndex = 0,
            int startFrame = -1)
        {
            return Instant(new RecordingInstantNotify(name), time, eventIndex, trackIndex, startFrame);
        }

        private static AnimationNotifyRuntimeEntry Instant(
            AnimationInstantNotify notify,
            float time,
            int eventIndex = 0,
            int trackIndex = 0,
            int startFrame = -1)
        {
            int frame = startFrame >= 0 ? startFrame : Mathf.RoundToInt(time * 20f);
            return new AnimationNotifyRuntimeEntry(notify, frame, frame, time, time, 0f, trackIndex, eventIndex);
        }

        private static AnimationNotifyRuntimeEntry Duration(
            string name,
            float start,
            float end,
            float minWeight = 0f)
        {
            return Duration(new RecordingDurationNotify(name), start, end, minWeight);
        }

        private static AnimationNotifyRuntimeEntry Duration(
            AnimationDurationNotify notify,
            float start,
            float end,
            float minWeight = 0f)
        {
            return new AnimationNotifyRuntimeEntry(
                notify,
                Mathf.RoundToInt(start * 20f),
                Mathf.RoundToInt(end * 20f),
                start,
                end,
                minWeight,
                0,
                0);
        }

        [Serializable]
        private sealed class RecordingInstantNotify : AnimationInstantNotify
        {
            public RecordingInstantNotify(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public override void OnNotify(Pawn owner)
            {
                Assert.That(owner, Is.Not.Null);
                Trace.Add(Name);
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

            public override void OnBegin(Pawn owner)
            {
                Trace.Add($"{name}:begin");
            }

            public override void OnTick(Pawn owner)
            {
                Trace.Add($"{name}:tick");
            }

            public override void OnEnd(Pawn owner, AnimationNotifyEndReason reason)
            {
                Trace.Add($"{name}:end:{reason}");
            }
        }

        [Serializable]
        private sealed class ThrowingInstantNotify : AnimationInstantNotify
        {
            public override void OnNotify(Pawn owner)
            {
                throw new InvalidOperationException("notify failure");
            }
        }
    }
}
