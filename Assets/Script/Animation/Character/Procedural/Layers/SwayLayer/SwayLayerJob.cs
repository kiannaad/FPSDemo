using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class SwayLayerJob : IAnimationLayerJob
    {
        private SwayLayerSettings settings;
        private AnimationUpdateContext context;
        private TransformStreamHandle root;
        private TransformStreamHandle weapon;
        private TransformStreamHandle head;
        private WeaponLayerJobData weaponData;
        private SwayRuntimeState state;

        public Type SettingsType => typeof(SwayLayerSettings);

        public void Initialize(LayerJobData jobData, AnimationLayerSettings layerSettings)
        {
            settings = layerSettings as SwayLayerSettings
                ?? throw new ArgumentException("Sway job requires SwayLayerSettings.", nameof(layerSettings));
            settings.Validate(jobData.RigComponent.Rig);
            context = jobData.UpdateContext;
            root = jobData.VisualRootHandle;
            weapon = RigHandleUtility.Bind(jobData.Animator, jobData.RigComponent, settings.WeaponIkBone, settings.name);
            head = RigHandleUtility.Bind(jobData.Animator, jobData.RigComponent, settings.HeadBone, settings.name);
            weaponData.Initialize(jobData, settings);
        }

        public AnimationScriptPlayable CreatePlayable(PlayableGraph graph) => AnimationScriptPlayable.Create(graph, new SwayJob
        {
            Root = root,
            Weapon = weapon,
            Head = head,
            WeaponData = weaponData,
            FreeAimSpace = settings.FreeAimSpace,
            MoveSpace = settings.MoveSpace,
            AimSpace = settings.AimSpace
        }, 1);

        public AnimationLayerSettings GetSettings() => settings;
        public void OnPreAnimationUpdate(float deltaTime, float weight)
        {
            state.Advance(
                context.ViewDeltaDegrees,
                context.MoveInput,
                context.UseFreeAim,
                deltaTime,
                settings);
        }

        public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight)
        {
            SwayJob job = playable.GetJobData<SwayJob>();
            job.FreeAimAngles = state.FreeAimValue;
            job.MovePose = state.MovePose;
            job.AimPose = state.AimPose;
            job.Weight = weight;
            playable.SetJobData(job);
        }

        public void OnPostAnimationUpdate() { }
        public void Dispose() { }
    }
}
