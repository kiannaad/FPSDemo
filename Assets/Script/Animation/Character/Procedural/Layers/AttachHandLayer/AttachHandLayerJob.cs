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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private NativeArray<AttachHandDebugSample> debugSamples;
        private bool hasLoggedDebugSample;
#endif

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

        public void OnPreAnimationUpdate(float deltaTime, float weight) { }

        public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight)
        {
            job = playable.GetJobData<AttachHandJob>();
            job.Weight = weight;
            job.HandPoseOffset = settings.HandPoseOffset;
            job.Chain = chain;
            playable.SetJobData(job);
        }

        public void OnPostAnimationUpdate()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (hasLoggedDebugSample || !debugSamples.IsCreated || debugSamples[0].Captured == 0)
            {
                return;
            }

            AttachHandDebugSample sample = debugSamples[0];
            Debug.Log(
                $"[WeaponIkProbe][AttachHand] Layer={settings.name}; "
                + $"Weapon={sample.WeaponPosition:F3}; Hand={sample.HandPosition:F3}; "
                + $"IkWeapon={sample.IkWeaponPosition:F3}; IkHandBeforeAttach={sample.IkHandPosition:F3}; "
                + $"RelativeHandPos={sample.RelativeHandPosition:F3}; "
                + $"RelativeHandRotAngle={sample.RelativeHandRotationAngle:F2}; Weight={job.Weight:F3}");
            hasLoggedDebugSample = true;
#endif
        }

        public void Dispose()
        {
            if (chain.IsCreated) chain.Dispose();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugSamples.IsCreated) debugSamples.Dispose();
            hasLoggedDebugSample = false;
#endif
        }

        private void Rebuild()
        {
            if (chain.IsCreated) chain.Dispose();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugSamples.IsCreated) debugSamples.Dispose();
            hasLoggedDebugSample = false;
#endif
            Transform hand = RigHandleUtility.ResolveTransform(jobData.RigComponent, settings.HandBone, settings.name);
            Transform weapon = RigHandleUtility.ResolveTransform(jobData.RigComponent, settings.WeaponBone, settings.name);
            Transform ikWeapon = RigHandleUtility.ResolveTransform(jobData.RigComponent, settings.IkWeaponBone, settings.name);
            Transform ikHand = RigHandleUtility.ResolveTransform(jobData.RigComponent, settings.IkHandBone, settings.name);
            IReadOnlyList<KRigElement> elements = RigHandleUtility.ResolveChain(
                jobData.RigComponent.Rig,
                settings.ElementChainName,
                settings.name);

            KTransform relativeHandPose = default;
            Quaternion[] localRotations = new Quaternion[elements.Count];
            bool referenceInitialized = settings.CustomHandPose != null;
            if (referenceInitialized)
            {
                KTransform[] cachedHierarchyPose = CaptureHierarchyPose();
                try
                {
                    settings.CustomHandPose.SampleAnimation(jobData.Animator.gameObject, 0f);
                    relativeHandPose = new KTransform(weapon)
                        .GetRelativeTransform(new KTransform(hand), false);
                    for (int index = 0; index < elements.Count; index++)
                    {
                        Transform transform = RigHandleUtility.ResolveTransform(
                            jobData.RigComponent,
                            elements[index],
                            settings.name);
                        localRotations[index] = transform.localRotation;
                    }
                }
                finally
                {
                    RestoreHierarchyPose(cachedHierarchyPose);
                }
            }


            chain = new NativeArray<AttachHandPoseData>(elements.Count, Allocator.Persistent);
            for (int index = 0; index < elements.Count; index++)
            {
                Transform transform = RigHandleUtility.ResolveTransform(jobData.RigComponent, elements[index], settings.name);
                chain[index] = new AttachHandPoseData
                {
                    Handle = jobData.Animator.BindStreamTransform(transform),
                    LocalRotation = localRotations[index]
                };
            }

            job = new AttachHandJob
            {
                Hand = jobData.Animator.BindStreamTransform(hand),
                Weapon = jobData.Animator.BindStreamTransform(weapon),
                IkHand = jobData.Animator.BindStreamTransform(ikHand),
                IkWeapon = jobData.Animator.BindStreamTransform(ikWeapon),
                RelativeHandPose = relativeHandPose,
                HandPoseOffset = settings.HandPoseOffset,
                Chain = chain,
                ReferenceInitialized = referenceInitialized
            };
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugSamples = new NativeArray<AttachHandDebugSample>(1, Allocator.Persistent);
            job.DebugSamples = debugSamples;
            Debug.Log(
                $"[WeaponIkProbe][AttachHandInit] Layer={settings.name}; "
                + $"CustomHandPose={(settings.CustomHandPose != null ? settings.CustomHandPose.name : "<stream-first-frame>")}; "
                + $"ReferenceInitialized={referenceInitialized}; HandPoseOffset={settings.HandPoseOffset.Position:F3}");
#endif
        }

        private KTransform[] CaptureHierarchyPose()
        {
            int count = jobData.RigComponent.Rig.Hierarchy.Count;
            var pose = new KTransform[count];
            for (int index = 0; index < count; index++)
            {
                pose[index] = new KTransform(jobData.RigComponent.GetRigTransform(index), false);
            }

            return pose;
        }

        private void RestoreHierarchyPose(KTransform[] pose)
        {
            for (int index = 0; index < pose.Length; index++)
            {
                Transform target = jobData.RigComponent.GetRigTransform(index);
                target.localPosition = pose[index].Position;
                target.localRotation = pose[index].Rotation;
                target.localScale = pose[index].Scale;
            }
        }

        private static AttachHandLayerSettings RequireSettings(AnimationLayerSettings value)
        {
            return value as AttachHandLayerSettings
                ?? throw new ArgumentException("Attach hand job requires AttachHandLayerSettings.", nameof(value));
        }
    }
}
