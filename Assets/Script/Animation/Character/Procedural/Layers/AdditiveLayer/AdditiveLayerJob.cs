using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class AdditiveLayerJob : IAnimationLayerJob
    {
        private AdditiveLayerSettings settings;
        private CharacterAnimInstance owner;
        private AnimationUpdateContext context;
        private TransformStreamHandle root;
        private TransformStreamHandle weapon;
        private TransformStreamHandle additiveBone;
        private WeaponLayerJobData weaponData;

        public Type SettingsType => typeof(AdditiveLayerSettings);

        public void Initialize(LayerJobData jobData, AnimationLayerSettings layerSettings)
        {
            settings = layerSettings as AdditiveLayerSettings
                ?? throw new ArgumentException("Additive job requires AdditiveLayerSettings.", nameof(layerSettings));
            settings.Validate(jobData.RigComponent.Rig);
            owner = jobData.Owner;
            context = jobData.UpdateContext;
            root = jobData.CharacterRootHandle;
            weapon = RigHandleUtility.Bind(jobData.Animator, jobData.RigComponent, settings.WeaponIkBone, settings.name);
            additiveBone = RigHandleUtility.Bind(jobData.Animator, jobData.RigComponent, settings.AdditiveBone, settings.name);
            weaponData.Initialize(jobData, settings);
        }

        public AnimationScriptPlayable CreatePlayable(PlayableGraph graph) => AnimationScriptPlayable.Create(graph, new AdditiveJob
        {
            Root = root,
            Weapon = weapon,
            AdditiveBone = additiveBone,
            WeaponData = weaponData
        }, 1);

        public AnimationLayerSettings GetSettings() => settings;
        public void OnPreAnimationUpdate(float deltaTime, float weight) { }

        public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight)
        {
            AdditiveJob job = playable.GetJobData<AdditiveJob>();
            float curve = string.IsNullOrWhiteSpace(settings.AimingCurve)
                ? 1f
                : owner.GetCurveValue(settings.AimingCurve);
            job.CurveScale = Mathf.Clamp01(curve) * Mathf.Lerp(1f, settings.AdsScalar, context.AimingWeight);
            job.RecoilOffset = context.RecoilOffset;
            job.Weight = weight;
            playable.SetJobData(job);
        }

        public void OnPostAnimationUpdate() { }
        public void Dispose() { }
    }
}
