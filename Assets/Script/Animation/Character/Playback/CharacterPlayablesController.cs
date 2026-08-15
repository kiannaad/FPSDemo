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
        private readonly AnimationNotifyDispatchQueue notifyQueue = new AnimationNotifyDispatchQueue();
        private PlayableGraph graph;
        private Playable nativeControllerSource;
        private AnimatorControllerPlayable animatorControllerSource;
        private RuntimeAnimatorController runtimeController;
        private CharacterAnimationChannelMixer overlayMixer;
        private CharacterAnimationChannelMixer slotMixer;
        private CharacterAnimationChannelMixer overrideMixer;
        private AnimationLayerMixerPlayable masterMixer;
        private PlayableOutput projectOutput;
        private AnimationClipAsset persistentPoseAsset;
        private AnimationPlaybackHandle persistentPoseReceipt;
        private AnimationPlaybackHandle currentPersistentPoseHandle;
        private long persistentPoseRequestId;
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
        internal PlayableGraph Graph => graph;
        internal PlayableOutput ProjectOutput => projectOutput;
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
                || graph.GetOutputCount() < 2
                || !nativeControllerSource.IsValid()
                || !animatorControllerSource.IsValid()
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

            PlayableOutput nativeOutput = graph.GetOutput(0);
            return nativeOutput.IsOutputValid()
                && nativeOutput.GetSourcePlayable().Equals(nativeControllerSource)
                && masterMixer.GetInput(0).Equals(animatorControllerSource)
                && masterMixer.GetInput(1).Equals(overrideMixer.Mixer)
                && slotMixer.Mixer.GetInput(0).Equals(overlayMixer.Mixer)
                && overrideMixer.Mixer.GetInput(0).Equals(slotMixer.Mixer)
                && Mathf.Approximately(nativeOutput.GetWeight(), 0f)
                && Mathf.Approximately(projectOutput.GetWeight(), 1f)
                && ContainsOutput(projectOutput);
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
                animatorControllerSource = AnimatorControllerPlayable.Create(graph, controller);
                overlayMixer = new CharacterAnimationChannelMixer(
                    graph,
                    0,
                    Playable.Null,
                    pawn,
                    notifyQueue);
                slotMixer = new CharacterAnimationChannelMixer(
                    graph,
                    1,
                    overlayMixer.Mixer,
                    pawn,
                    notifyQueue);
                overrideMixer = new CharacterAnimationChannelMixer(
                    graph,
                    1,
                    slotMixer.Mixer,
                    pawn,
                    notifyQueue);
                masterMixer = AnimationLayerMixerPlayable.Create(graph, 2);
                masterMixer.ConnectInput(0, animatorControllerSource, 0, 1f);
                masterMixer.ConnectInput(1, overrideMixer.Mixer, 0, 0f);
                if (upperBodyMask != null)
                {
                    masterMixer.SetLayerMaskFromAvatarMask(1, upperBodyMask);
                }

                PlayableOutput output = AnimationPlayableOutput.Create(
                    graph,
                    "CharacterPlayback",
                    animator);
                output.SetSourcePlayable(masterMixer);
                output.SetWeight(1f);
                nativeOutput.SetWeight(0f);
                nativeControllerSource = source;
                runtimeController = controller;
                projectOutput = output;
                graph.Play();
                SynchronizeAnimatorControllerParameters();
                RestorePersistentPose();
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
                if (upperBodyMask != null)
                {
                    masterMixer.SetInputWeight(1, 1f);
                }

                persistentPoseAsset = asset;
                persistentPoseReceipt = handle;
                currentPersistentPoseHandle = handle;
                persistentPoseRequestId = resolvedRequestId;
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

            AnimationPlaybackHandle resolvedHandle = ReferenceEquals(handle, persistentPoseReceipt)
                ? currentPersistentPoseHandle
                : handle;
            bool stopped = overlayMixer != null && overlayMixer.Stop(resolvedHandle);
            stopped = (slotMixer != null && slotMixer.Stop(handle)) || stopped;
            stopped = (overrideMixer != null && overrideMixer.Stop(handle)) || stopped;
            if (ReferenceEquals(handle, persistentPoseReceipt))
            {
                persistentPoseAsset = null;
                persistentPoseReceipt = null;
                currentPersistentPoseHandle = null;
                persistentPoseRequestId = 0;
            }
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

            SynchronizeAnimatorControllerParameters();
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

            bool hasUpperBodyOutput = upperBodyMask != null
                && (overlayMixer.HasActivePlayback
                    || slotMixer.HasActivePlayback
                    || overrideMixer.HasActivePlayback);
            masterMixer.SetInputWeight(1, hasUpperBodyOutput ? 1f : 0f);
        }

        public void DispatchNotifies()
        {
            if (!isDisposed)
            {
                notifyQueue.Dispatch();
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            ReleaseOwnedResources();
            persistentPoseAsset = null;
            persistentPoseReceipt = null;
            currentPersistentPoseHandle = null;
            persistentPoseRequestId = 0;
            notifyQueue.Dispatch();
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

        private bool ContainsOutput(PlayableOutput expectedOutput)
        {
            for (int index = 1; index < graph.GetOutputCount(); index++)
            {
                if (graph.GetOutput(index).Equals(expectedOutput))
                {
                    return true;
                }
            }

            return false;
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

            if (graph.IsValid() && animatorControllerSource.IsValid())
            {
                graph.DestroyPlayable(animatorControllerSource);
            }

            overrideMixer?.Dispose();
            slotMixer?.Dispose();
            overlayMixer?.Dispose();
            synchronizedOverrides.Clear();
            nativeControllerSource = Playable.Null;
            animatorControllerSource = AnimatorControllerPlayable.Null;
            runtimeController = null;
            masterMixer = AnimationLayerMixerPlayable.Null;
            projectOutput = PlayableOutput.Null;
            overrideMixer = null;
            slotMixer = null;
            overlayMixer = null;
        }

        private void RestorePersistentPose()
        {
            if (persistentPoseAsset == null || persistentPoseReceipt == null)
            {
                return;
            }

            currentPersistentPoseHandle = overlayMixer.Play(
                persistentPoseAsset,
                NextPlaybackId(),
                persistentPoseRequestId,
                false,
                false,
                true);
            if (currentPersistentPoseHandle.State == AnimationPlaybackState.Failed)
            {
                throw new InvalidOperationException("Unable to restore the persistent weapon pose after graph rebuild.");
            }

            if (upperBodyMask != null)
            {
                masterMixer.SetInputWeight(1, 1f);
            }
        }

        private void SynchronizeAnimatorControllerParameters()
        {
            if (!animatorControllerSource.IsValid())
            {
                return;
            }

            animatorControllerSource.SetFloat("MoveX", animator.GetFloat("MoveX"));
            animatorControllerSource.SetFloat("MoveY", animator.GetFloat("MoveY"));
            animatorControllerSource.SetFloat("Velocity", animator.GetFloat("Velocity"));
            animatorControllerSource.SetBool("Moving", animator.GetBool("Moving"));
            animatorControllerSource.SetBool("InAir", animator.GetBool("InAir"));
            animatorControllerSource.SetFloat("Sprinting", animator.GetFloat("Sprinting"));
        }
    }
}
