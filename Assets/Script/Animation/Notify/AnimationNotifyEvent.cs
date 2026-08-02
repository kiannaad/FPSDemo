using System;
using UnityEngine;

namespace CGame.Animation
{
    [Serializable]
    public class AnimationNotifyEvent
    {
        [SerializeReference] private AnimationNotify notify = new AnimationInstantNotify();
        [SerializeField, Min(0)] private int startFrame;
        [SerializeField, Min(0)] private int durationFrames;
        [SerializeField, Min(0f)] private float minTriggerWeight = 0.00001f;

        public AnimationNotify Notify
        {
            get => notify;
            set => notify = value ?? new AnimationInstantNotify();
        }

        public int StartFrame
        {
            get => startFrame;
            set => startFrame = Mathf.Max(0, value);
        }

        public int DurationFrames
        {
            get => durationFrames;
            set => durationFrames = Mathf.Max(0, value);
        }

        public float MinTriggerWeight
        {
            get => minTriggerWeight;
            set => minTriggerWeight = Mathf.Max(0f, value);
        }

        public bool IsDuration => durationFrames > 0 || notify is AnimationDurationNotify;

        public int EndFrame => startFrame + durationFrames;

        public void SetFrameRange(int newStartFrame, int newDurationFrames, int maxFrame)
        {
            int clampedMaxFrame = Mathf.Max(0, maxFrame);
            startFrame = Mathf.Clamp(newStartFrame, 0, clampedMaxFrame);
            durationFrames = Mathf.Clamp(newDurationFrames, 0, clampedMaxFrame - startFrame);
        }

        public void MoveToFrame(int newStartFrame, int maxFrame)
        {
            SetFrameRange(newStartFrame, durationFrames, maxFrame);
        }

        public void ResizeStartFrame(int newStartFrame, int maxFrame)
        {
            int endFrame = Mathf.Clamp(EndFrame, 0, Mathf.Max(0, maxFrame));
            int clampedStart = Mathf.Clamp(newStartFrame, 0, endFrame);
            SetFrameRange(clampedStart, endFrame - clampedStart, maxFrame);
        }

        public void ResizeEndFrame(int newEndFrame, int maxFrame)
        {
            int clampedEnd = Mathf.Clamp(newEndFrame, startFrame, Mathf.Max(0, maxFrame));
            SetFrameRange(startFrame, clampedEnd - startFrame, maxFrame);
        }

        public bool TryValidateForRuntime(int maxFrame, out string error)
        {
            if (notify == null)
            {
                error = "Animation Notify event requires a Notify definition.";
                return false;
            }

            int clampedMaxFrame = Mathf.Max(0, maxFrame);
            if (startFrame > clampedMaxFrame || EndFrame > clampedMaxFrame)
            {
                error = $"Animation Notify '{notify.DisplayName}' is outside the clip frame range.";
                return false;
            }

            if (notify is AnimationDurationNotify && durationFrames <= 0)
            {
                error = $"Duration Notify '{notify.DisplayName}' requires DurationFrames greater than zero.";
                return false;
            }

            if (notify is AnimationInstantNotify && durationFrames != 0)
            {
                error = $"Instant Notify '{notify.DisplayName}' requires DurationFrames equal to zero.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
