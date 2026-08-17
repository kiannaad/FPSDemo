using System;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class IkMotionLayerJob : IAnimationLayerJob
    {
        private IkMotionLayerSettings settings;
        private TransformStreamHandle root;
        private TransformStreamHandle target;
        private IkMotionRuntimeState state;

        public Type SettingsType => typeof(IkMotionLayerSettings);
        public bool IsPlaying => state.IsPlaying;
        public float Playback => state.Playback;
        public void Play() => state.Play();
        public void Stop() => state.Stop(settings.BlendTime);

        public void Initialize(LayerJobData jobData, AnimationLayerSettings layerSettings)
        {
            settings = layerSettings as IkMotionLayerSettings
                ?? throw new ArgumentException("IK motion job requires IkMotionLayerSettings.", nameof(layerSettings));
            settings.Validate(jobData.RigComponent.Rig);
            root = jobData.CharacterRootHandle;
            target = RigHandleUtility.Bind(jobData.Animator, jobData.RigComponent, settings.TargetBone, settings.name);
            state.Play();
        }

        public AnimationScriptPlayable CreatePlayable(PlayableGraph graph) => AnimationScriptPlayable.Create(graph, new IkMotionJob
        {
            Root = root,
            Target = target
        }, 1);

        public AnimationLayerSettings GetSettings() => settings;
        public void OnPreAnimationUpdate(float deltaTime, float weight) => state.Advance(deltaTime, settings);

        public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight)
        {
            IkMotionJob job = playable.GetJobData<IkMotionJob>();
            job.Motion = state.Result;
            job.Weight = weight;
            playable.SetJobData(job);
        }

        public void OnPostAnimationUpdate() { }
        public void Dispose() { }
    }
}
