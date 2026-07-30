using System;
using System.Collections.Generic;

namespace CGame.Animation
{
    public sealed class AnimationNotifySnapshot
    {
        public AnimationNotifySnapshot(
            string trackName,
            string notifyType,
            string displayName,
            string eventTag,
            IEnumerable<string> contextTags,
            AnimationNotifyDispatchPolicy dispatchPolicy,
            int startFrame,
            int durationFrames,
            float minTriggerWeight)
        {
            TrackName = trackName ?? string.Empty;
            NotifyType = notifyType ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            EventTag = eventTag ?? string.Empty;
            ContextTags = contextTags == null
                ? Array.Empty<string>()
                : new List<string>(contextTags).ToArray();
            DispatchPolicy = dispatchPolicy;
            StartFrame = startFrame;
            DurationFrames = durationFrames;
            MinTriggerWeight = minTriggerWeight;
        }

        public string TrackName { get; }
        public string NotifyType { get; }
        public string DisplayName { get; }
        public string EventTag { get; }
        public IReadOnlyList<string> ContextTags { get; }
        public AnimationNotifyDispatchPolicy DispatchPolicy { get; }
        public int StartFrame { get; }
        public int DurationFrames { get; }
        public float MinTriggerWeight { get; }
    }
}
