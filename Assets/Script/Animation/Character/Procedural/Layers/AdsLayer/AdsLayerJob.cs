using System;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class AdsLayerJob : IAnimationLayerJob
    {
        private AdsLayerSettings settings;
        private AnimationUpdateContext context;
        private TransformStreamHandle root;
        private TransformStreamHandle weapon;
        private TransformStreamHandle aimTarget;
        private WeaponLayerJobData weaponData;
        private AdsRuntimeState state;

        public Type SettingsType => typeof(AdsLayerSettings);

        public void Initialize(LayerJobData jobData, AnimationLayerSettings layerSettings)
        {
            settings = layerSettings as AdsLayerSettings
                ?? throw new ArgumentException("ADS job requires AdsLayerSettings.", nameof(layerSettings));
            settings.Validate(jobData.RigComponent.Rig);
            context = jobData.UpdateContext;
            root = jobData.CharacterRootHandle;
            weapon = RigHandleUtility.Bind(jobData.Animator, jobData.RigComponent, settings.WeaponIkBone, settings.name);
            aimTarget = RigHandleUtility.Bind(jobData.Animator, jobData.RigComponent, settings.AimTargetBone, settings.name);
            weaponData.Initialize(jobData, settings);
        }

        public AnimationScriptPlayable CreatePlayable(PlayableGraph graph) => AnimationScriptPlayable.Create(graph, new AdsJob
        {
            Root = root,
            Weapon = weapon,
            AimTarget = aimTarget,
            WeaponData = weaponData,
            PositionBlend = settings.PositionBlend,
            RotationBlend = settings.RotationBlend,
            CameraBlend = settings.CameraBlend,
            AimingEase = settings.AimingEaseMode
        }, 1);

        public AnimationLayerSettings GetSettings() => settings;
        public void OnPreAnimationUpdate(float deltaTime, float weight)
        {
            state.Advance(context.IsAiming, context.AimPointOffset, deltaTime, settings.AimingSpeed, settings.AimPointSpeed, settings.AimPointEaseMode);
            context.SetAimingWeight(state.AimingWeight);
        }

        public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight)
        {
            AdsJob job = playable.GetJobData<AdsJob>();
            job.AimingWeight = state.AimingWeight;
            job.AimPointOffset = state.AimPoint;
            job.Weight = weight;
            playable.SetJobData(job);
        }

        public void OnPostAnimationUpdate() { }
        public void Dispose() { }
    }
}
