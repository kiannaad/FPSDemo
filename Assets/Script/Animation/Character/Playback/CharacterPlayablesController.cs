using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class CharacterPlayablesController : IDisposable
    {
        private readonly struct SynchronizedOverride
        {
            public SynchronizedOverride(
                AnimationPlaybackHandle slotHandle,
                AnimationPlaybackHandle overrideHandle)
            {
                SlotHandle = slotHandle;
                OverrideHandle = overrideHandle;
            }

            public AnimationPlaybackHandle SlotHandle { get; }
            public AnimationPlaybackHandle OverrideHandle { get; }
        }

        private static long nextPlaybackId;
        private static long nextRequestId;

        private readonly Pawn pawn;
        private readonly Animator animator;
        private readonly AvatarMask upperBodyMask;
        private readonly List<SynchronizedOverride> synchronizedOverrides =
            new List<SynchronizedOverride>();
        private PlayableGraph graph;
        private Playable nativeControllerSource;
        private RuntimeAnimatorController runtimeController;
        private CharacterAnimationChannelMixer overlayMixer;
        private CharacterAnimationChannelMixer slotMixer;
        private CharacterAnimationChannelMixer overrideMixer;
        private AnimationLayerMixerPlayable masterMixer;
        private PlayableOutput projectOutput;
        private bool isDisposed;

        public CharacterPlayablesController(
            Pawn pawn,
            Animator animator,
            AvatarMask upperBodyMask = null)
        {
            this.pawn = pawn ?? throw new ArgumentNullException(nameof(pawn));
            this.animator = animator ?? throw new ArgumentNullException(nameof(animator));
            this.upperBodyMask = upperBodyMask;
        }

        internal Playable NativeControllerSource => nativeControllerSource;
        internal AnimationLayerMixerPlayable MasterMixer => masterMixer;
        internal AnimationLayerMixerPlayable OverlayMixer => overlayMixer?.Mixer
            ?? AnimationLayerMixerPlayable.Null;
        internal AnimationLayerMixerPlayable SlotMixer => slotMixer?.Mixer
            ?? AnimationLayerMixerPlayable.Null;
        internal AnimationLayerMixerPlayable OverrideMixer => overrideMixer?.Mixer
            ?? AnimationLayerMixerPlayable.Null;
        internal int OverlayActiveSlotCount => overlayMixer?.ActiveSlotCount ?? 0;
        internal int SlotActiveSlotCount => slotMixer?.ActiveSlotCount ?? 0;
        internal int OverrideActiveSlotCount => overrideMixer?.ActiveSlotCount ?? 0;

        public bool IsValid()
        {
            if (isDisposed
                || animator == null
                || !animator.enabled
                || !graph.IsValid()
                || graph.GetOutputCount() != 2
                || !nativeControllerSource.IsValid()
                || !masterMixer.IsValid()
                || overlayMixer == null
                || !overlayMixer.Mixer.IsValid()
                || slotMixer == null
                || !slotMixer.Mixer.IsValid()
                || overrideMixer == null
                || !overrideMixer.Mixer.IsValid()
                || !projectOutput.IsOutputValid()
                || animator.runtimeAnimatorController != runtimeController)
            {
                return false;
            }

            PlayableOutput currentNativeOutput = graph.GetOutput(0);
            return currentNativeOutput.IsOutputValid()
                && currentNativeOutput.GetSourcePlayable().Equals(nativeControllerSource)
                && masterMixer.GetInput(0).Equals(nativeControllerSource)
                && masterMixer.GetInput(1).Equals(overrideMixer.Mixer)
                && slotMixer.Mixer.GetInput(0).Equals(overlayMixer.Mixer)
                && overrideMixer.Mixer.GetInput(0).Equals(slotMixer.Mixer)
                && Mathf.Approximately(currentNativeOutput.GetWeight(), 0f)
                && Mathf.Approximately(projectOutput.GetWeight(), 1f)
                && graph.GetOutput(1).Equals(projectOutput);
        }

        public bool TryRebuild()
        {
            ReleaseOwnedResources();
            if (isDisposed
                || animator == null
                || !animator.enabled)
            {
                return false;
            }

            graph = animator.playableGraph;
            if (!graph.IsValid() || graph.GetOutputCount() != 1)
            {
                return false;
            }

            PlayableOutput nativeOutput = graph.GetOutput(0);
            Playable source = nativeOutput.GetSourcePlayable();
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (!nativeOutput.IsOutputValid()
                || !source.IsValid()
                || controller == null)
            {
                return false;
            }

            try
            {
                overlayMixer = new CharacterAnimationChannelMixer(
                    graph,
                    0,
                    Playable.Null,
                    pawn);
                slotMixer = new CharacterAnimationChannelMixer(
                    graph,
                    1,
                    overlayMixer.Mixer,
                    pawn);
                overrideMixer = new CharacterAnimationChannelMixer(
                    graph,
                    1,
                    slotMixer.Mixer,
                    pawn);
                masterMixer = AnimationLayerMixerPlayable.Create(graph, 2);
                masterMixer.ConnectInput(0, source, 0, 1f);
                masterMixer.ConnectInput(1, overrideMixer.Mixer, 0, 0f);
                if (upperBodyMask != null)
                {
                    masterMixer.SetLayerMaskFromAvatarMask(1, upperBodyMask);
                }

                PlayableOutput output = AnimationPlayableOutput.Create(
                    graph,
                    "CharacterUpperBody",
                    animator);
                output.SetSourcePlayable(masterMixer);
                output.SetWeight(1f);
                nativeOutput.SetWeight(0f);
                nativeControllerSource = source;
                runtimeController = controller;
                projectOutput = output;
                graph.Play();
                return IsValid();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ReleaseOwnedResources();
                return false;
            }
        }

        public AnimationPlaybackHandle PlayPose(
            AnimationClipAsset asset,
            long requestId = 0)
        {
            if (!EnsureReady())
            {
                return CreateFailedHandle(asset, requestId);
            }

            long resolvedRequestId = ResolveRequestId(requestId);
            return overlayMixer.Play(
                asset,
                NextPlaybackId(),
                resolvedRequestId,
                false,
                false);
        }

        public AnimationPlaybackHandle PlayPoseImmediate(
            AnimationClipAsset asset,
            long requestId = 0)
        {
            if (!EnsureReady())
            {
                return CreateFailedHandle(asset, requestId);
            }

            long resolvedRequestId = ResolveRequestId(requestId);
            AnimationPlaybackHandle handle = overlayMixer.Play(
                asset,
                NextPlaybackId(),
                resolvedRequestId,
                false,
                false,
                true);
            if (handle.State != AnimationPlaybackState.Failed)
            {
                masterMixer.SetInputWeight(1, 1f);
            }

            return handle;
        }

        public AnimationPlaybackHandle PlayAnimation(
            AnimationClipAsset asset,
            long requestId = 0)
        {
            if (!EnsureReady())
            {
                return CreateFailedHandle(asset, requestId);
            }

            long resolvedRequestId = ResolveRequestId(requestId);
            AnimationPlaybackHandle slotHandle = slotMixer.Play(
                asset,
                NextPlaybackId(),
                resolvedRequestId,
                true,
                false);
            if (slotHandle.State == AnimationPlaybackState.Failed
                || asset == null
                || asset.OverrideMask == null)
            {
                return slotHandle;
            }

            AnimationPlaybackHandle overrideHandle = overrideMixer.Play(
                asset,
                NextPlaybackId(),
                resolvedRequestId,
                true,
                true,
                false,
                false);
            if (overrideHandle.State == AnimationPlaybackState.Failed)
            {
                slotMixer.Stop(slotHandle);
                slotHandle.State = AnimationPlaybackState.Failed;
                return slotHandle;
            }

            synchronizedOverrides.Add(
                new SynchronizedOverride(slotHandle, overrideHandle));
            return slotHandle;
        }

        public bool Stop(AnimationPlaybackHandle handle)
        {
            if (handle == null)
            {
                return false;
            }

            bool stopped = overlayMixer != null && overlayMixer.Stop(handle);
            stopped = (slotMixer != null && slotMixer.Stop(handle)) || stopped;
            stopped = (overrideMixer != null && overrideMixer.Stop(handle)) || stopped;
            for (int i = synchronizedOverrides.Count - 1; i >= 0; i--)
            {
                SynchronizedOverride pair = synchronizedOverrides[i];
                if (!ReferenceEquals(pair.SlotHandle, handle))
                {
                    continue;
                }

                overrideMixer?.Stop(pair.OverrideHandle);
                synchronizedOverrides.RemoveAt(i);
            }

            return stopped;
        }

        public float GetCurveValue(string curveName)
        {
            return slotMixer != null ? slotMixer.GetCurveValue(curveName) : 0f;
        }

        public void Update(float deltaTime)
        {
            if (!IsValid() || deltaTime <= 0f)
            {
                return;
            }

            overlayMixer.Update();
            slotMixer.Update();
            overrideMixer.Update();
            for (int i = synchronizedOverrides.Count - 1; i >= 0; i--)
            {
                SynchronizedOverride pair = synchronizedOverrides[i];
                if (!pair.SlotHandle.IsTerminal)
                {
                    continue;
                }

                if (!pair.OverrideHandle.IsTerminal)
                {
                    overrideMixer.Stop(pair.OverrideHandle);
                }

                synchronizedOverrides.RemoveAt(i);
            }

            bool hasUpperBodyOutput = overlayMixer.HasActivePlayback
                || slotMixer.HasActivePlayback
                || overrideMixer.HasActivePlayback;
            masterMixer.SetInputWeight(1, hasUpperBodyOutput ? 1f : 0f);
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            ReleaseOwnedResources();
            isDisposed = true;
        }

        internal bool TrySetPlaybackTime(
            AnimationPlaybackHandle handle,
            double time)
        {
            return (overlayMixer != null
                    && overlayMixer.TrySetPlaybackTime(handle, time))
                || (slotMixer != null
                    && slotMixer.TrySetPlaybackTime(handle, time))
                || (overrideMixer != null
                    && overrideMixer.TrySetPlaybackTime(handle, time));
        }

        internal void RestoreNativeOutput()
        {
            ReleaseOwnedResources();
        }

        private bool EnsureReady()
        {
            return IsValid() || TryRebuild();
        }

        private AnimationPlaybackHandle CreateFailedHandle(
            AnimationClipAsset asset,
            long requestId)
        {
            return AnimationPlaybackHandle.CreateFailed(
                NextPlaybackId(),
                ResolveRequestId(requestId),
                asset != null ? asset.AnimationClip : null);
        }

        private static long ResolveRequestId(long requestId)
        {
            return requestId > 0
                ? requestId
                : Interlocked.Increment(ref nextRequestId);
        }

        private static long NextPlaybackId()
        {
            return Interlocked.Increment(ref nextPlaybackId);
        }

        private void ReleaseOwnedResources()
        {
            if (graph.IsValid() && graph.GetOutputCount() > 0)
            {
                PlayableOutput nativeOutput = graph.GetOutput(0);
                if (nativeOutput.IsOutputValid())
                {
                    nativeOutput.SetWeight(1f);
                }
            }

            if (graph.IsValid() && projectOutput.IsOutputValid())
            {
                graph.DestroyOutput(projectOutput);
            }

            if (graph.IsValid() && masterMixer.IsValid())
            {
                graph.DestroyPlayable(masterMixer);
            }

            overrideMixer?.Dispose();
            slotMixer?.Dispose();
            overlayMixer?.Dispose();
            synchronizedOverrides.Clear();
            nativeControllerSource = Playable.Null;
            runtimeController = null;
            masterMixer = AnimationLayerMixerPlayable.Null;
            projectOutput = PlayableOutput.Null;
            overrideMixer = null;
            slotMixer = null;
            overlayMixer = null;
        }
    }
}
