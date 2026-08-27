using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Animation
{
    [Serializable]
    public class AnimationNotifyTrack
    {
        [SerializeField] private string name = "Notify Track";
        [SerializeField] private List<AnimationNotifyEvent> events = new List<AnimationNotifyEvent>();

        public string Name
        {
            get => name;
            set => name = string.IsNullOrWhiteSpace(value) ? "Notify Track" : value;
        }

        public List<AnimationNotifyEvent> Events => events;

        public AnimationNotifyEvent AddEvent(AnimationNotify notify, int startFrame, int durationFrames = 0)
        {
            var notifyEvent = new AnimationNotifyEvent
            {
                Notify = notify,
                StartFrame = startFrame,
                DurationFrames = durationFrames,
            };

            events.Add(notifyEvent);
            return notifyEvent;
        }
    }
}
