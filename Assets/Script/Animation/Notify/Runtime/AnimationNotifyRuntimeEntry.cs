using System;

namespace CGame.Animation
{
    internal sealed class AnimationNotifyRuntimeEntry : IComparable<AnimationNotifyRuntimeEntry>
    {
        public AnimationNotifyRuntimeEntry(
            AnimationNotify notify,
            int startFrame,
            int endFrame,
            float startTime,
            float endTime,
            float minTriggerWeight,
            int trackIndex,
            int eventIndex)
        {
            Notify = notify;
            StartFrame = startFrame;
            EndFrame = endFrame;
            StartTime = startTime;
            EndTime = endTime;
            MinTriggerWeight = minTriggerWeight;
            TrackIndex = trackIndex;
            EventIndex = eventIndex;
        }

        public AnimationNotify Notify { get; }
        public int StartFrame { get; }
        public int EndFrame { get; }
        public float StartTime { get; }
        public float EndTime { get; }
        public float MinTriggerWeight { get; }
        public int TrackIndex { get; }
        public int EventIndex { get; }
        public bool IsDuration => Notify is AnimationDurationNotify;

        public int CompareTo(AnimationNotifyRuntimeEntry other)
        {
            if (other == null)
            {
                return 1;
            }

            int frameComparison = StartFrame.CompareTo(other.StartFrame);
            if (frameComparison != 0)
            {
                return frameComparison;
            }

            int trackComparison = TrackIndex.CompareTo(other.TrackIndex);
            return trackComparison != 0
                ? trackComparison
                : EventIndex.CompareTo(other.EventIndex);
        }
    }
}
