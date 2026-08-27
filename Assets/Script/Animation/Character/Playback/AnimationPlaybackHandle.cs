using UnityEngine;

namespace CGame.Animation
{
    public sealed class AnimationPlaybackHandle
    {
        internal AnimationPlaybackHandle(
            long playbackId,
            long requestId,
            AnimationClip clip,
            AnimationPlaybackState state)
        {
            PlaybackId = playbackId;
            RequestId = requestId;
            Clip = clip;
            State = state;
        }

        public long PlaybackId { get; }
        public long RequestId { get; }
        public AnimationClip Clip { get; }
        public AnimationPlaybackState State { get; internal set; }
        public bool IsTerminal => State == AnimationPlaybackState.Completed
            || State == AnimationPlaybackState.Interrupted
            || State == AnimationPlaybackState.Cancelled
            || State == AnimationPlaybackState.Failed;

        internal static AnimationPlaybackHandle CreateFailed(
            long playbackId,
            long requestId,
            AnimationClip clip)
        {
            return new AnimationPlaybackHandle(
                playbackId,
                requestId,
                clip,
                AnimationPlaybackState.Failed);
        }
    }
}
