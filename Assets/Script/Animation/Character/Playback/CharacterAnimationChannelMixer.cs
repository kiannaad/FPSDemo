using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    internal sealed class CharacterAnimationChannelMixer : IDisposable
    {
        private const int SlotCount = 3;

        private sealed class Slot
        {
            public int InputIndex;
            public CharacterAnimationPlayable Animation;
            public float CachedWeight;
            public bool IsManualBlendOut;
            public float ManualBlendOutStart;
            public float ManualBlendOutDuration;
            public AnimationPlaybackState BlendOutTerminalState;
            public bool StartsAtFullWeight;
        }

        private readonly PlayableGraph graph;
        private readonly int firstUserInput;
        private readonly List<Slot> slots = new List<Slot>(SlotCount);
        private AnimationLayerMixerPlayable mixer;
        private Slot activeSlot;
        private bool isDisposed;

        public CharacterAnimationChannelMixer(
            PlayableGraph graph,
            int baseInputCount,
            Playable baseInput)
        {
            if (!graph.IsValid())
            {
                throw new ArgumentException("A valid PlayableGraph is required.", nameof(graph));
            }

            this.graph = graph;
            firstUserInput = baseInputCount;
            mixer = AnimationLayerMixerPlayable.Create(graph, baseInputCount + SlotCount);
            if (baseInputCount > 0)
            {
                if (!baseInput.IsValid())
                {
                    throw new ArgumentException("A valid base input is required.", nameof(baseInput));
                }

                mixer.ConnectInput(0, baseInput, 0, 1f);
            }
        }

        public AnimationLayerMixerPlayable Mixer => mixer;
        public int ActiveSlotCount => slots.Count;
        public bool HasActivePlayback => slots.Count > 0;

        public AnimationPlaybackHandle Play(
            AnimationClipAsset asset,
            long playbackId,
            long requestId,
            bool autoBlendOut,
            bool useOverrideMask,
            bool startAtFullWeight = false)
        {
            if (isDisposed || !mixer.IsValid())
            {
                return AnimationPlaybackHandle.CreateFailed(
                    playbackId,
                    requestId,
                    asset != null ? asset.AnimationClip : null);
            }

            for (int i = 0; i < slots.Count; i++)
            {
                AnimationPlaybackHandle existing = slots[i].Animation.Handle;
                if (existing.RequestId == requestId
                    && existing.Clip == asset?.AnimationClip
                    && !existing.IsTerminal)
                {
                    return existing;
                }
            }

            if (!CharacterAnimationPlayable.TryCreate(
                    graph,
                    asset,
                    playbackId,
                    requestId,
                    autoBlendOut,
                    out CharacterAnimationPlayable animation,
                    out string error))
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    Debug.LogError(error);
                }

                return AnimationPlaybackHandle.CreateFailed(
                    playbackId,
                    requestId,
                    asset != null ? asset.AnimationClip : null);
            }

            if (slots.Count == SlotCount)
            {
                ReleaseSlot(slots[0], AnimationPlaybackState.Interrupted);
            }

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].CachedWeight = mixer.GetInputWeight(slots[i].InputIndex);
            }

            int inputIndex = FindUnusedInputIndex();
            var slot = new Slot
            {
                InputIndex = inputIndex,
                Animation = animation,
                StartsAtFullWeight = startAtFullWeight,
            };
            slots.Add(slot);
            activeSlot = slot;
            mixer.ConnectInput(
                inputIndex,
                animation.Playable,
                0,
                startAtFullWeight ? 1f : 0f);

            AvatarMask selectedMask = useOverrideMask
                ? animation.OverrideMask
                : animation.Mask;
            if (selectedMask != null)
            {
                mixer.SetLayerMaskFromAvatarMask((uint)inputIndex, selectedMask);
            }
            mixer.SetLayerAdditive((uint)inputIndex, animation.Additive);
            animation.Handle.State = startAtFullWeight
                ? AnimationPlaybackState.Playing
                : AnimationPlaybackState.BlendingIn;
            return animation.Handle;
        }

        public void Update()
        {
            if (isDisposed || !mixer.IsValid() || activeSlot == null)
            {
                return;
            }

            if (activeSlot.IsManualBlendOut)
            {
                UpdateManualBlendOut(activeSlot);
                return;
            }

            CharacterAnimationPlayable active = activeSlot.Animation;
            float blendInWeight = CalculateBlendInWeight(active);
            mixer.SetInputWeight(activeSlot.InputIndex, blendInWeight);
            active.Handle.State = blendInWeight < 1f
                ? AnimationPlaybackState.BlendingIn
                : AnimationPlaybackState.Playing;

            for (int i = slots.Count - 1; i >= 0; i--)
            {
                Slot slot = slots[i];
                if (slot == activeSlot)
                {
                    continue;
                }

                float weight = Mathf.Lerp(slot.CachedWeight, 0f, blendInWeight);
                mixer.SetInputWeight(slot.InputIndex, weight);
                if (Mathf.Approximately(blendInWeight, 1f))
                {
                    ReleaseSlot(slot, AnimationPlaybackState.Interrupted);
                }
            }

            if (!active.AutoBlendOut || active.LocalTime < active.Length)
            {
                if (!active.AutoBlendOut
                    && !active.Handle.Clip.isLooping
                    && active.LocalTime > active.Length)
                {
                    active.Playable.SetTime(active.Length);
                }

                return;
            }

            BeginBlendOut(
                activeSlot,
                active.Length,
                active.BlendOutTime,
                AnimationPlaybackState.Completed);
            UpdateManualBlendOut(activeSlot);
        }

        public bool Stop(AnimationPlaybackHandle handle)
        {
            Slot slot = FindSlot(handle);
            if (slot == null)
            {
                return false;
            }

            if (slot != activeSlot)
            {
                ReleaseSlot(slot, AnimationPlaybackState.Cancelled);
                return true;
            }

            BeginBlendOut(
                slot,
                slot.Animation.LocalTime,
                slot.Animation.BlendOutTime,
                AnimationPlaybackState.Cancelled);
            return true;
        }

        public float GetCurveValue(string curveName)
        {
            float value = 0f;
            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                if (slot.Animation.TryEvaluateCurve(curveName, out float rawValue))
                {
                    value += rawValue * mixer.GetInputWeight(slot.InputIndex);
                }
            }

            return value;
        }

        public bool TrySetPlaybackTime(AnimationPlaybackHandle handle, double time)
        {
            Slot slot = FindSlot(handle);
            if (slot == null || !slot.Animation.Playable.IsValid())
            {
                return false;
            }

            slot.Animation.Playable.SetTime(time);
            return true;
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            for (int i = slots.Count - 1; i >= 0; i--)
            {
                ReleaseSlot(slots[i], AnimationPlaybackState.Cancelled);
            }

            if (graph.IsValid() && mixer.IsValid())
            {
                graph.DestroyPlayable(mixer);
            }

            mixer = AnimationLayerMixerPlayable.Null;
            activeSlot = null;
            isDisposed = true;
        }

        private float CalculateBlendInWeight(CharacterAnimationPlayable animation)
        {
            if (activeSlot != null && activeSlot.StartsAtFullWeight)
            {
                return 1f;
            }

            if (Mathf.Approximately(animation.BlendInTime, 0f))
            {
                return 1f;
            }

            return Mathf.Clamp01(
                (animation.LocalTime - animation.StartTime) / animation.BlendInTime);
        }

        private void BeginBlendOut(
            Slot slot,
            float startTime,
            float duration,
            AnimationPlaybackState terminalState)
        {
            if (slot.IsManualBlendOut)
            {
                return;
            }

            slot.IsManualBlendOut = true;
            slot.ManualBlendOutStart = startTime;
            slot.ManualBlendOutDuration = Mathf.Max(0f, duration);
            slot.BlendOutTerminalState = terminalState;
            slot.CachedWeight = mixer.GetInputWeight(slot.InputIndex);
            slot.Animation.Handle.State = AnimationPlaybackState.BlendingOut;
        }

        private void UpdateManualBlendOut(Slot slot)
        {
            float alpha = Mathf.Approximately(slot.ManualBlendOutDuration, 0f)
                ? 1f
                : Mathf.Clamp01(
                    (slot.Animation.LocalTime - slot.ManualBlendOutStart)
                    / slot.ManualBlendOutDuration);
            mixer.SetInputWeight(
                slot.InputIndex,
                Mathf.Lerp(slot.CachedWeight, 0f, alpha));
            if (Mathf.Approximately(alpha, 1f))
            {
                ReleaseSlot(slot, slot.BlendOutTerminalState);
            }
        }

        private int FindUnusedInputIndex()
        {
            for (int inputIndex = firstUserInput;
                 inputIndex < firstUserInput + SlotCount;
                 inputIndex++)
            {
                bool used = false;
                for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
                {
                    if (slots[slotIndex].InputIndex == inputIndex)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    return inputIndex;
                }
            }

            throw new InvalidOperationException("No animation channel input is available.");
        }

        private Slot FindSlot(AnimationPlaybackHandle handle)
        {
            if (handle == null)
            {
                return null;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (ReferenceEquals(slots[i].Animation.Handle, handle))
                {
                    return slots[i];
                }
            }

            return null;
        }

        private void ReleaseSlot(Slot slot, AnimationPlaybackState terminalState)
        {
            if (slot == null)
            {
                return;
            }

            if (mixer.IsValid())
            {
                mixer.DisconnectInput(slot.InputIndex);
            }

            slot.Animation.Handle.State = terminalState;
            slot.Animation.Dispose();
            slots.Remove(slot);
            if (ReferenceEquals(activeSlot, slot))
            {
                activeSlot = slots.Count > 0 ? slots[slots.Count - 1] : null;
            }
        }
    }
}
