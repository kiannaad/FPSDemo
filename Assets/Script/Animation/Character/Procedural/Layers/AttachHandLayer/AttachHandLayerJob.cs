using System;
using System.Collections.Generic;
using CGame.Animation.Rig;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class AttachHandLayerJob : IAnimationLayerJob
    {
        private LayerJobData jobData;
        private AttachHandLayerSettings settings;
        private NativeArray<AttachHandPoseData> chain;
        private AttachHandJob job;

        public Type SettingsType => typeof(AttachHandLayerSettings);

        public void Initialize(LayerJobData data, AnimationLayerSettings layerSettings)
        {
            jobData = data;
            settings = RequireSettings(layerSettings);
            Rebuild();
        }

        public AnimationScriptPlayable CreatePlayable(PlayableGraph graph)
        {
            job.Chain = chain;
            return AnimationScriptPlayable.Create(graph, job, 1);
        }

        public AnimationLayerSettings GetSettings() => settings;

        public void OnPreAnimationUpdate() { }

        public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight)
        {
            job.Weight = weight;
            job.HandPoseOffset = settings.HandPoseOffset;
            job.Chain = chain;
            playable.SetJobData(job);
        }

        public void OnPostAnimationUpdate() { }

        public void Dispose()
        {
            if (chain.IsCreated) chain.Dispose();
        }

        private void Rebuild()
        {
            if (chain.IsCreated) chain.Dispose();
            Transform hand = RigHandleUtility.ResolveTransform(jobData.RigComponent, settings.HandBone, settings.name);
            Transform weapon = RigHandleUtility.ResolveTransform(jobData.RigComponent, settings.WeaponBone, settings.name);
            Transform ikWeapon = RigHandleUtility.ResolveTransform(jobData.RigComponent, settings.IkWeaponBone, settings.name);
            Transform ikHand = RigHandleUtility.ResolveTransform(jobData.RigComponent, settings.IkHandBone, settings.name);
            IReadOnlyList<KRigElement> elements = RigHandleUtility.ResolveChain(
                jobData.RigComponent.Rig,
                settings.ElementChainName,
                settings.name);

            jobData.RigComponent.RestoreInitializedHierarchyPose();

            if (settings.CustomHandPose != null)
            {
                settings.CustomHandPose.SampleAnimation(jobData.Animator.gameObject, 0f);
            }

            KTransform relativeHandPose = new KTransform(weapon).GetRelativeTransform(new KTransform(hand), false);
            chain = new NativeArray<AttachHandPoseData>(elements.Count, Allocator.Persistent);
            for (int index = 0; index < elements.Count; index++)
            {
                Transform transform = RigHandleUtility.ResolveTransform(jobData.RigComponent, elements[index], settings.name);
                chain[index] = new AttachHandPoseData
                {
                    Handle = jobData.Animator.BindStreamTransform(transform),
                    LocalRotation = transform.localRotation
                };
            }

            jobData.RigComponent.RestoreInitializedHierarchyPose();

            job = new AttachHandJob
            {
                IkHand = jobData.Animator.BindStreamTransform(ikHand),
                IkWeapon = jobData.Animator.BindStreamTransform(ikWeapon),
                RelativeHandPose = relativeHandPose,
                HandPoseOffset = settings.HandPoseOffset,
                Chain = chain
            };
        }

        private static AttachHandLayerSettings RequireSettings(AnimationLayerSettings value)
        {
            return value as AttachHandLayerSettings
                ?? throw new ArgumentException("Attach hand job requires AttachHandLayerSettings.", nameof(value));
        }
    }
}
