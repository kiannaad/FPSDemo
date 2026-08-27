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
            public AnimationNotifyPlaybackState NotifyState;
            public AnimationNotifyGeneration NotifyGeneration;
        }

        private readonly PlayableGraph graph;
        private readonly Pawn pawn;
        private readonly int firstUserInput;
        private readonly List<Slot> slots = new List<Slot>(SlotCount);
        private readonly AnimationNotifyDispatchQueue notifyQueue;
        private readonly bool ownsNotifyQueue;
        private AnimationLayerMixerPlayable mixer;
        private Slot activeSlot;
        private bool isDisposed;
        private bool isUpdating;
        private float lastMaskAttachHandLog = float.NaN;
        private float lastReloadWeaponBoneWeightLog = float.NaN;

        public CharacterAnimationChannelMixer(
            PlayableGraph graph,
            int baseInputCount,
            Playable baseInput,
            Pawn pawn = null,
            AnimationNotifyDispatchQueue notifyQueue = null)
        {
            if (!graph.IsValid())
            {
                throw new ArgumentException("A valid PlayableGraph is required.", nameof(graph));
            }

            this.graph = graph;
            this.pawn = pawn;
            ownsNotifyQueue = notifyQueue == null;
            this.notifyQueue = notifyQueue ?? new AnimationNotifyDispatchQueue();
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
            bool startAtFullWeight = false,
            bool dispatchNotifies = true)
        {
            if (isDisposed || !mixer.IsValid() || isUpdating)
            {
                if (isUpdating)
                {
                    Debug.LogError("Synchronous Play during CharacterAnimationChannelMixer.Update is not allowed.");
                }

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
                slots[0].NotifyGeneration?.Invalidate();
                ReleaseSlot(
                    slots[0],
                    AnimationPlaybackState.Interrupted,
                    AnimationNotifyEndReason.Interrupted);
            }

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].CachedWeight = mixer.GetInputWeight(slots[i].InputIndex);
            }

            int inputIndex = FindUnusedInputIndex();
            AnimationNotifyGeneration notifyGeneration = new AnimationNotifyGeneration();
            var slot = new Slot
            {
                InputIndex = inputIndex,
                Animation = animation,
                StartsAtFullWeight = startAtFullWeight,
                NotifyGeneration = notifyGeneration,
                NotifyState = dispatchNotifies
                    ? new AnimationNotifyPlaybackState(
                        pawn,
                        animation.NotifyEntries,
                        animation.Length,
                        animation.Handle.Clip.isLooping,
                        animation.Speed,
                        this.notifyQueue,
                        notifyGeneration)
                    : null,
            };
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].NotifyGeneration?.Invalidate();
                slots[i].NotifyGeneration = new AnimationNotifyGeneration();
                slots[i].NotifyState?.ReplaceGeneration(slots[i].NotifyGeneration);
                slots[i].NotifyState?.MarkReplaced();
            }

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
            if (isDisposed || !mixer.IsValid() || activeSlot == null || isUpdating)
            {
                return;
            }

            isUpdating = true;
            try
            {
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
                UpdateNotifyState(activeSlot);

                for (int i = slots.Count - 1; i >= 0; i--)
                {
                    Slot slot = slots[i];
                    if (slot == activeSlot)
                    {
                        continue;
                    }

                    float weight = Mathf.Lerp(slot.CachedWeight, 0f, blendInWeight);
                    mixer.SetInputWeight(slot.InputIndex, weight);
                    UpdateNotifyState(slot);
                    if (Mathf.Approximately(blendInWeight, 1f))
                    {
                        ReleaseSlot(
                            slot,
                            AnimationPlaybackState.Interrupted,
                            AnimationNotifyEndReason.Interrupted);
                    }
                }

                bool reachedEnd = active.Speed >= 0f
                    ? active.LocalTime >= active.Length
                    : active.LocalTime <= 0f;
                if (!active.AutoBlendOut || !reachedEnd)
                {
                    if (!active.AutoBlendOut && !active.Handle.Clip.isLooping && reachedEnd)
                    {
                        active.Playable.SetTime(active.Speed >= 0f ? active.Length : 0f);
                    }

                    return;
                }

                BeginBlendOut(
                    activeSlot,
                    active.LocalTime,
                    active.BlendOutTime,
                    AnimationPlaybackState.Completed);
                UpdateManualBlendOut(activeSlot);
            }
            finally
            {
                isUpdating = false;
                if (ownsNotifyQueue)
                {
                    notifyQueue.Dispatch();
                }
            }
        }

        public bool Stop(AnimationPlaybackHandle handle)
        {
            if (isUpdating)
            {
                Debug.LogError("Synchronous Stop during CharacterAnimationChannelMixer.Update is not allowed.");
                return false;
            }

            Slot slot = FindSlot(handle);
            if (slot == null)
            {
                return false;
            }

            slot.NotifyGeneration?.Invalidate();
            slot.NotifyState?.EndAll(AnimationNotifyEndReason.StateStopped);

            if (slot != activeSlot)
            {
                ReleaseSlot(slot, AnimationPlaybackState.Cancelled, AnimationNotifyEndReason.StateStopped);
                DispatchIfOwned();
                return true;
            }

            BeginBlendOut(
                slot,
                slot.Animation.LocalTime,
                slot.Animation.BlendOutTime,
                AnimationPlaybackState.Cancelled);
            DispatchIfOwned();
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
                    float inputWeight = mixer.GetInputWeight(slot.InputIndex);
                    value += rawValue * inputWeight;
                    if (curveName == "MaskAttachHand")
                    {
                        float resolved = rawValue * inputWeight;
                        if (float.IsNaN(lastMaskAttachHandLog)
                            || Mathf.Abs(resolved - lastMaskAttachHandLog) > 0.02f)
                        {
                            lastMaskAttachHandLog = resolved;
                            Debug.Log($"[ReloadTrace] MaskAttachHand source: clip={slot.Animation.Handle.Clip?.name}, raw={rawValue:F2}, inputWeight={inputWeight:F2}, resolved={resolved:F2}");
                        }
                    }

                    string clipName = slot.Animation.Handle.Clip?.name;
                    if (curveName == "WeaponBoneWeight"
                        && !string.IsNullOrEmpty(clipName)
                        && clipName.IndexOf("Reload", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        float resolved = rawValue * inputWeight;
                        if (float.IsNaN(lastReloadWeaponBoneWeightLog)
                            || Mathf.Abs(resolved - lastReloadWeaponBoneWeightLog) > 0.20f)
                        {
                            lastReloadWeaponBoneWeightLog = resolved;
                            float normalizedTime = Mathf.Approximately(slot.Animation.Length, 0f)
                                ? 0f
                                : Mathf.Clamp01(slot.Animation.LocalTime / slot.Animation.Length);
                            Debug.Log($"[ReloadTrace] WeaponBoneWeight source: clip={clipName}, normalizedTime={normalizedTime:F3}, raw={rawValue:F2}, inputWeight={inputWeight:F2}, resolved={resolved:F2}");
                        }
                    }
                }
            }

            return value;
        }

        public bool TryGetCurveValue(string curveName, out float value)
        {
            value = 0f;
            bool found = false;
            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                if (slot.Animation.TryEvaluateCurve(curveName, out float rawValue))
                {
                    value += rawValue * mixer.GetInputWeight(slot.InputIndex);
                    found = true;
                }
            }

            return found;
        }

        public bool TrySetPlaybackTime(AnimationPlaybackHandle handle, double time)
        {
            if (isUpdating)
            {
                Debug.LogError("Synchronous SetTime during CharacterAnimationChannelMixer.Update is not allowed.");
                return false;
            }

            Slot slot = FindSlot(handle);
            if (slot == null || !slot.Animation.Playable.IsValid())
            {
                return false;
            }

            slot.Animation.Playable.SetTime(time);
            slot.NotifyState?.ResetTime(time);
            DispatchIfOwned();
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
                ReleaseSlot(slots[i], AnimationPlaybackState.Cancelled, AnimationNotifyEndReason.OwnerDisabled);
            }

            if (graph.IsValid() && mixer.IsValid())
            {
                graph.DestroyPlayable(mixer);
            }

            mixer = AnimationLayerMixerPlayable.Null;
            activeSlot = null;
            notifyQueue.Dispatch();
            isDisposed = true;
        }

        public void DispatchNotifies()
        {
            notifyQueue.Dispatch();
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
                Mathf.Abs(animation.LocalTime - animation.StartTime) / animation.BlendInTime);
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
                    Mathf.Abs(slot.Animation.LocalTime - slot.ManualBlendOutStart)
                    / slot.ManualBlendOutDuration);
            mixer.SetInputWeight(
                slot.InputIndex,
                Mathf.Lerp(slot.CachedWeight, 0f, alpha));
            if (Mathf.Approximately(alpha, 1f))
            {
                AnimationNotifyEndReason reason = slot.BlendOutTerminalState == AnimationPlaybackState.Completed
                    ? AnimationNotifyEndReason.NaturalEnd
                    : AnimationNotifyEndReason.StateStopped;
                ReleaseSlot(slot, slot.BlendOutTerminalState, reason);
                return;
            }

            UpdateNotifyState(slot);
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

        private void UpdateNotifyState(Slot slot)
        {
            slot.NotifyState?.Update(
                slot.Animation.LocalTime,
                mixer.GetInputWeight(slot.InputIndex));
        }

        private void ReleaseSlot(
            Slot slot,
            AnimationPlaybackState terminalState,
            AnimationNotifyEndReason notifyEndReason)
        {
            if (slot == null)
            {
                return;
            }

            if (mixer.IsValid())
            {
                mixer.DisconnectInput(slot.InputIndex);
            }

            slot.NotifyState?.EndAll(notifyEndReason);
            if (notifyEndReason == AnimationNotifyEndReason.NaturalEnd)
            {
                notifyQueue.RetireAfterCurrentDispatch(slot.NotifyGeneration);
            }
            else
            {
                slot.NotifyGeneration?.Invalidate();
            }
            slot.Animation.Handle.State = terminalState;
            slot.Animation.Dispose();
            slots.Remove(slot);
            if (ReferenceEquals(activeSlot, slot))
            {
                activeSlot = slots.Count > 0 ? slots[slots.Count - 1] : null;
                if (activeSlot != null)
                {
                    activeSlot.NotifyState?.Restore(
                        activeSlot.Animation.LocalTime,
                        mixer.GetInputWeight(activeSlot.InputIndex));
                }
            }
        }

        private void DispatchIfOwned()
        {
            if (ownsNotifyQueue && !notifyQueue.IsDispatching)
            {
                notifyQueue.Dispatch();
            }
        }
    }
}
