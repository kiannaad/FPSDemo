using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGame.Animation
{
    public abstract class AnimationAssetBase : ScriptableObject, IAnimationAsset
    {
        [SerializeField] private List<AnimationNotifyTrack> notifyTracks = new List<AnimationNotifyTrack>();

        public abstract AnimationClip MainClip { get; }
        public abstract bool IsValid { get; }
        public IReadOnlyList<AnimationNotifyTrack> NotifyTracks => notifyTracks;

        public virtual bool CanEditNotifies => MainClip != null;

        public AnimationNotifyTrack AddNotifyTrack(string trackName = "Notify Track")
        {
            var track = new AnimationNotifyTrack
            {
                Name = trackName,
            };

            notifyTracks.Add(track);
            return track;
        }

        public AnimationNotifyTrack InsertNotifyTrack(int index, string trackName = "Notify Track")
        {
            int insertIndex = Mathf.Clamp(index, 0, notifyTracks.Count);
            var track = new AnimationNotifyTrack
            {
                Name = trackName,
            };

            notifyTracks.Insert(insertIndex, track);
            return track;
        }

        public bool RenameNotifyTrack(int index, string trackName)
        {
            if (!IsValidTrackIndex(index))
            {
                return false;
            }

            notifyTracks[index].Name = trackName;
            return true;
        }

        public bool RemoveNotifyTrackAt(int index)
        {
            if (!IsValidTrackIndex(index))
            {
                return false;
            }

            notifyTracks.RemoveAt(index);
            return true;
        }

        public AnimationNotifyEvent AddNotifyEvent(int trackIndex, AnimationNotify notify, int startFrame, int durationFrames = 0)
        {
            if (!IsValidTrackIndex(trackIndex))
            {
                return null;
            }

            return notifyTracks[trackIndex].AddEvent(notify, startFrame, durationFrames);
        }

        private bool IsValidTrackIndex(int index)
        {
            return index >= 0 && index < notifyTracks.Count;
        }
    }
}
