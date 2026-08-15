using System;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class ViewLayerJob : IAnimationLayerJob
    {
        private ViewLayerSettings settings;
        private ViewJob job;

        public Type SettingsType => typeof(ViewLayerSettings);

        public void Initialize(LayerJobData jobData, AnimationLayerSettings layerSettings)
        {
            settings = RequireSettings(layerSettings);
            job = new ViewJob
            {
                Root = jobData.CharacterRootHandle,
                Weapon = RigHandleUtility.Bind(jobData.Animator, jobData.RigComponent, settings.IkWeaponBone.Element, settings.name),
                RightHand = RigHandleUtility.Bind(jobData.Animator, jobData.RigComponent, settings.IkRightHand.Element, settings.name),
                LeftHand = RigHandleUtility.Bind(jobData.Animator, jobData.RigComponent, settings.IkLeftHand.Element, settings.name),
                WeaponPose = new PoseOffsetJobData(settings.IkWeaponBone),
                RightHandPose = new PoseOffsetJobData(settings.IkRightHand),
                LeftHandPose = new PoseOffsetJobData(settings.IkLeftHand)
            };
        }

        public AnimationScriptPlayable CreatePlayable(PlayableGraph graph)
        {
            return AnimationScriptPlayable.Create(graph, job, 1);
        }

        public AnimationLayerSettings GetSettings() => settings;
        public void OnPreAnimationUpdate() { }

        public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight)
        {
            job.Weight = weight;
            playable.SetJobData(job);
        }

        public void OnPostAnimationUpdate() { }
        public void Dispose() { }

        private static ViewLayerSettings RequireSettings(AnimationLayerSettings value)
        {
            return value as ViewLayerSettings
                ?? throw new ArgumentException("View job requires ViewLayerSettings.", nameof(value));
        }
    }
}
