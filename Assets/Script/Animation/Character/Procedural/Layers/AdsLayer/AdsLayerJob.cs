using System;
using UnityEngine;
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
        private KTransform defaultAimPose;
        private Vector3 aimTargetDefaultLocalPosition;

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
            Transform rootTransform = jobData.Animator.transform;
            Transform weaponTransform = RigHandleUtility.ResolveTransform(jobData.RigComponent, settings.WeaponIkBone, settings.name + " weapon IK bone");
            Transform aimTargetTransform = RigHandleUtility.ResolveTransform(jobData.RigComponent, settings.AimTargetBone, settings.name + " aim target bone");
            KTransform rootPose = new KTransform(rootTransform);
            KTransform weaponPose = rootPose.GetRelativeTransform(new KTransform(weaponTransform), false);
            KTransform aimTargetPose = rootPose.GetRelativeTransform(new KTransform(aimTargetTransform), false);
            defaultAimPose = new KTransform(
                aimTargetPose.Position - weaponPose.Position,
                Quaternion.Inverse(weaponPose.Rotation));
            aimTargetDefaultLocalPosition = aimTargetTransform.localPosition;
            weaponData.Initialize(jobData, settings);
        }

        public AnimationScriptPlayable CreatePlayable(PlayableGraph graph) => AnimationScriptPlayable.Create(graph, new AdsJob
        {
            Root = root,
            Weapon = weapon,
            AimTarget = aimTarget,
            WeaponData = weaponData,
            DefaultAimPose = defaultAimPose,
            AimTargetDefaultLocalPosition = aimTargetDefaultLocalPosition,
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
