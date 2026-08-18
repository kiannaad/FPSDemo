using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class IkLayerJob : IAnimationLayerJob
    {
        private AnimationUpdateContext updateContext;
        private IkLayerSettings settings;
        private IkJob job;

        public Type SettingsType => typeof(IkLayerSettings);

        public void Initialize(LayerJobData jobData, AnimationLayerSettings layerSettings)
        {
            settings = RequireSettings(layerSettings);
            updateContext = jobData.UpdateContext;
            job = new IkJob
            {
                Root = jobData.CharacterRootHandle,
                RightHand = CreateHandle(jobData, settings.RightHand, settings.RightHandIk, settings.RightHandHint, "right hand"),
                LeftHand = CreateHandle(jobData, settings.LeftHand, settings.LeftHandIk, settings.LeftHandHint, "left hand"),
                RightFoot = CreateHandle(jobData, settings.RightFoot, settings.RightFootIk, settings.RightFootHint, "right foot"),
                LeftFoot = CreateHandle(jobData, settings.LeftFoot, settings.LeftFootIk, settings.LeftFootHint, "left foot"),
                IsHuman = jobData.Animator.isHuman,
                OffsetFeetTargets = settings.OffsetFeetTargets,
                RightHandWeight = settings.RightHandWeight,
                LeftHandWeight = settings.LeftHandWeight,
                RightFootWeight = settings.RightFootWeight,
                LeftFootWeight = settings.LeftFootWeight
            };
        }

        public AnimationScriptPlayable CreatePlayable(PlayableGraph graph) => AnimationScriptPlayable.Create(graph, job, 1);
        public AnimationLayerSettings GetSettings() => settings;
        public void OnPreAnimationUpdate(float deltaTime, float weight) { }

        public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight)
        {
            job.Weight = weight;
            // Feet must compensate the visual ModelRoot offset produced by Turn.
            // Rotation.YawDelta is only the physical root's frame-to-frame delta and
            // has a different lifetime and sign from the visual turn offset.
            job.TurnOffset = updateContext.TurnOffsetDegrees;
            job.OffsetFeetTargets = settings.OffsetFeetTargets;
            job.RightHandWeight = settings.RightHandWeight;
            job.LeftHandWeight = settings.LeftHandWeight;
            job.RightFootWeight = settings.RightFootWeight;
            job.LeftFootWeight = settings.LeftFootWeight;
            playable.SetJobData(job);
        }

        public void OnPostAnimationUpdate() { }
        public void Dispose() { }

        private static IkHandle CreateHandle(
            LayerJobData data,
            CGame.Animation.Rig.KRigElement tipElement,
            CGame.Animation.Rig.KRigElement targetElement,
            CGame.Animation.Rig.KRigElement hintElement,
            string label)
        {
            Transform tip = RigHandleUtility.ResolveTransform(data.RigComponent, tipElement, label + " tip");
            Transform target = RigHandleUtility.ResolveTransform(data.RigComponent, targetElement, label + " target");
            Transform hint = RigHandleUtility.ResolveTransform(data.RigComponent, hintElement, label + " hint");
            if (tip.parent == null || tip.parent.parent == null)
            {
                throw new InvalidOperationException(label + " must have a two-bone parent chain.");
            }

            return new IkHandle(data.Animator, tip, target, hint);
        }

        private static IkLayerSettings RequireSettings(AnimationLayerSettings value)
        {
            return value as IkLayerSettings
                ?? throw new ArgumentException("IK job requires IkLayerSettings.", nameof(value));
        }
    }
}
