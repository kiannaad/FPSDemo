using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    internal sealed class CharacterAnimationPlayable : IDisposable
    {
        private readonly Dictionary<string, AnimationCurve> namedCurves;
        private bool isDisposed;

        private CharacterAnimationPlayable(
            AnimationClipPlayable playable,
            AnimationPlaybackHandle handle,
            AvatarMask mask,
            AvatarMask overrideMask,
            bool additive,
            float blendInTime,
            float blendOutTime,
            float startTime,
            bool autoBlendOut,
            Dictionary<string, AnimationCurve> namedCurves)
        {
            Playable = playable;
            Handle = handle;
            Mask = mask;
            OverrideMask = overrideMask;
            Additive = additive;
            BlendInTime = blendInTime;
            BlendOutTime = blendOutTime;
            StartTime = startTime;
            AutoBlendOut = autoBlendOut;
            this.namedCurves = namedCurves;
        }

        public AnimationClipPlayable Playable { get; private set; }
        public AnimationPlaybackHandle Handle { get; }
        public AvatarMask Mask { get; }
        public AvatarMask OverrideMask { get; }
        public bool Additive { get; }
        public float BlendInTime { get; }
        public float BlendOutTime { get; }
        public float StartTime { get; }
        public bool AutoBlendOut { get; }
        public float LocalTime => Playable.IsValid() ? (float)Playable.GetTime() : 0f;
        public float Length => Handle.Clip != null ? Handle.Clip.length : 0f;

        public static bool TryCreate(
            PlayableGraph graph,
            AnimationClipAsset asset,
            long playbackId,
            long requestId,
            bool autoBlendOut,
            out CharacterAnimationPlayable animationPlayable,
            out string error)
        {
            animationPlayable = null;
            if (!graph.IsValid())
            {
                error = "A valid PlayableGraph is required.";
                return false;
            }

            if (asset == null)
            {
                error = "An AnimationClipAsset is required.";
                return false;
            }

            if (!asset.TryValidateForPlayback(out error))
            {
                return false;
            }

            AnimationClip clip = asset.AnimationClip;
            var curveSnapshots = new Dictionary<string, AnimationCurve>(StringComparer.Ordinal);
            for (int i = 0; i < asset.NamedCurves.Count; i++)
            {
                AnimationCurveChannel source = asset.NamedCurves[i];
                AnimationCurve sourceCurve = source.Curve;
                var curve = sourceCurve == null
                    ? new AnimationCurve()
                    : new AnimationCurve(sourceCurve.keys)
                    {
                        preWrapMode = sourceCurve.preWrapMode,
                        postWrapMode = sourceCurve.postWrapMode,
                    };
                curveSnapshots.Add(source.Name, curve);
            }

            IReadOnlyList<AnimationNotifySnapshot> notifySnapshots = CreateNotifySnapshots(asset);
            AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
            if (!playable.IsValid())
            {
                error = $"Unable to create an AnimationClipPlayable for '{clip.name}'.";
                return false;
            }

            float normalizedStart = asset.OverrideNormalizedStartTime
                ? Mathf.Clamp01(asset.NormalizedStartTime)
                : 0f;
            float startTime = normalizedStart * clip.length;
            playable.SetTime(startTime);
            playable.SetSpeed(asset.Speed);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);

            var handle = new AnimationPlaybackHandle(
                playbackId,
                requestId,
                clip,
                notifySnapshots,
                AnimationPlaybackState.Pending);
            animationPlayable = new CharacterAnimationPlayable(
                playable,
                handle,
                asset.Mask,
                asset.OverrideMask,
                asset.Additive,
                asset.BlendInTime,
                asset.BlendOutTime,
                startTime,
                autoBlendOut,
                curveSnapshots);
            error = string.Empty;
            return true;
        }

        public bool TryEvaluateCurve(string curveName, out float value)
        {
            if (!string.IsNullOrWhiteSpace(curveName)
                && namedCurves.TryGetValue(curveName, out AnimationCurve curve))
            {
                float normalizedTime = Mathf.Approximately(Length, 0f)
                    ? 0f
                    : Mathf.Clamp01(LocalTime / Length);
                value = curve.Evaluate(normalizedTime);
                return true;
            }

            value = 0f;
            return false;
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            if (Playable.IsValid())
            {
                Playable.Destroy();
            }

            Playable = default;
            isDisposed = true;
        }

        private static IReadOnlyList<AnimationNotifySnapshot> CreateNotifySnapshots(
            AnimationClipAsset asset)
        {
            var snapshots = new List<AnimationNotifySnapshot>();
            IReadOnlyList<AnimationNotifyTrack> tracks = asset.NotifyTracks;
            for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
            {
                AnimationNotifyTrack track = tracks[trackIndex];
                if (track == null)
                {
                    continue;
                }

                List<AnimationNotifyEvent> events = track.Events;
                for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
                {
                    AnimationNotifyEvent notifyEvent = events[eventIndex];
                    AnimationNotify notify = notifyEvent?.Notify;
                    if (notify == null)
                    {
                        continue;
                    }

                    snapshots.Add(new AnimationNotifySnapshot(
                        track.Name,
                        notify.GetType().FullName,
                        notify.DisplayName,
                        notify.EventTag,
                        notify.ContextTags,
                        notify.DispatchPolicy,
                        notifyEvent.StartFrame,
                        notifyEvent.DurationFrames,
                        notifyEvent.MinTriggerWeight));
                }
            }

            return snapshots.ToArray();
        }
    }
}
