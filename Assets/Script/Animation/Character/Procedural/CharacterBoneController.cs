using System;
using CGame.Animation.Rig;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class CharacterBoneController : IDisposable
    {
        private readonly Animator animator;
        private readonly KRigComponent rigComponent;
        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private AnimationMixerPlayable passthroughMixer;
        private Playable previousSource;
        private CharacterProceduralAnimationProfile profile;
        private bool isDisposed;

        public CharacterBoneController(Animator animator, KRigComponent rigComponent)
        {
            this.animator = animator ?? throw new ArgumentNullException(nameof(animator));
            this.rigComponent = rigComponent ?? throw new ArgumentNullException(nameof(rigComponent));
        }

        public bool IsValid()
        {
            return !isDisposed
                && animator != null
                && graph.IsValid()
                && output.IsOutputValid()
                && passthroughMixer.IsValid();
        }

        public bool TryRebuild(PlayableGraph targetGraph, PlayableOutput previousOutput)
        {
            ReleaseOutput();
            if (isDisposed || !targetGraph.IsValid() || !previousOutput.IsOutputValid())
            {
                return false;
            }

            Playable source = previousOutput.GetSourcePlayable();
            if (!source.IsValid())
            {
                return false;
            }

            try
            {
                passthroughMixer = AnimationMixerPlayable.Create(targetGraph, 1);
                passthroughMixer.ConnectInput(0, source, 0, 1f);
                previousOutput.SetSourcePlayable(passthroughMixer);
                output = (AnimationPlayableOutput)previousOutput;
                previousSource = source;
                graph = targetGraph;
                return IsValid();
            }
            catch
            {
                ReleaseOutput();
                throw;
            }
        }

        public CharacterProceduralAnimationProfile Profile => profile;

        public void RequestProfile(CharacterProceduralAnimationProfile requestedProfile)
        {
            if (requestedProfile != null && requestedProfile.Rig != rigComponent.Rig)
            {
                throw new InvalidOperationException("Procedural animation profile must reference the Pawn KRig.");
            }

            profile = requestedProfile;
        }

        public void Update(float deltaTime)
        {
            if (IsValid())
            {
                KVirtualElement[] virtualElements = rigComponent.GetComponentsInChildren<KVirtualElement>(true);
                foreach (KVirtualElement virtualElement in virtualElements)
                {
                    if (virtualElement.TargetBone != null)
                    {
                        virtualElement.transform.SetPositionAndRotation(
                            virtualElement.TargetBone.position,
                            virtualElement.TargetBone.rotation);
                    }
                }
            }
        }

        public void ReleaseOutput()
        {
            if (graph.IsValid() && output.IsOutputValid() && previousSource.IsValid())
            {
                output.SetSourcePlayable(previousSource);
            }

            if (graph.IsValid() && passthroughMixer.IsValid())
            {
                graph.DestroyPlayable(passthroughMixer);
            }

            output = AnimationPlayableOutput.Null;
            passthroughMixer = AnimationMixerPlayable.Null;
            previousSource = Playable.Null;
            graph = default;
        }

        public void Dispose()
        {
            ReleaseOutput();
            isDisposed = true;
        }
    }
}
