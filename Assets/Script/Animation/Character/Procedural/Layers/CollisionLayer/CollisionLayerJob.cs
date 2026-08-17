using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class CollisionLayerJob : IAnimationLayerJob
    {
        private CollisionLayerSettings settings;
        private AnimationUpdateContext context;
        private TransformStreamHandle root;
        private TransformStreamHandle weapon;
        private CollisionRuntimeState state;
        private AnimationScriptPlayable playable;

        public Type SettingsType => typeof(CollisionLayerSettings);
        public Vector3 ProbeOrigin { get; private set; }
        public Vector3 ProbeDirection { get; private set; }

        public void Initialize(LayerJobData jobData, AnimationLayerSettings layerSettings)
        {
            settings = layerSettings as CollisionLayerSettings
                ?? throw new ArgumentException("Collision job requires CollisionLayerSettings.", nameof(layerSettings));
            settings.Validate(jobData.RigComponent.Rig);
            context = jobData.UpdateContext;
            root = jobData.CharacterRootHandle;
            weapon = RigHandleUtility.Bind(jobData.Animator, jobData.RigComponent, settings.WeaponIkBone, settings.name);
        }

        public AnimationScriptPlayable CreatePlayable(PlayableGraph graph)
        {
            playable = AnimationScriptPlayable.Create(graph, new CollisionJob
            {
                Root = root,
                Weapon = weapon,
                TargetSpace = settings.TargetSpace,
                RayStartOffset = settings.RayStartOffset
            }, 1);
            return playable;
        }

        public AnimationLayerSettings GetSettings() => settings;
        public void OnPreAnimationUpdate(float deltaTime, float weight)
        {
            state.Advance(
                context.WeaponCollisionHasHit,
                context.WeaponCollisionDistance,
                deltaTime,
                settings);
        }

        public void UpdatePlayableJobData(AnimationScriptPlayable targetPlayable, float weight)
        {
            CollisionJob job = targetPlayable.GetJobData<CollisionJob>();
            job.BlockingPose = state.BlockingPose;
            job.Weight = weight;
            targetPlayable.SetJobData(job);
        }

        public void OnPostAnimationUpdate()
        {
            if (!playable.IsValid()) return;
            CollisionJob job = playable.GetJobData<CollisionJob>();
            ProbeOrigin = job.ProbeOrigin;
            ProbeDirection = job.ProbeDirection;
        }

        public void Dispose()
        {
            playable = AnimationScriptPlayable.Null;
        }
    }
}
