using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class TurnLayerJob : IAnimationLayerJob
    {
        private TurnLayerSettings settings;
        private AnimationUpdateContext context;
        private CharacterAnimInstance owner;
        private TransformStreamHandle root;
        private TransformStreamHandle modelRoot;
        private bool offsetPosition;
        private TurnRuntimeState state;

        public Type SettingsType => typeof(TurnLayerSettings);
        public float TurnOffsetDegrees => -state.Angle;
        public float Angle => state.Angle;
        public bool IsTurning => state.IsTurning;

        public TurnRuntimeState CaptureRuntimeState() => state;

        public void RestoreRuntimeState(TurnRuntimeState runtimeState)
        {
            state = runtimeState;
        }

        public void Initialize(LayerJobData jobData, AnimationLayerSettings layerSettings)
        {
            settings = layerSettings as TurnLayerSettings
                ?? throw new ArgumentException("Turn job requires TurnLayerSettings.", nameof(layerSettings));
            settings.Validate(jobData.RigComponent.Rig);
            context = jobData.UpdateContext;
            owner = jobData.Owner;
            root = jobData.VisualRootHandle;

            Transform model = RigHandleUtility.ResolveTransform(
                jobData.RigComponent, settings.CharacterRootBone, settings.name);
            Transform hip = RigHandleUtility.ResolveTransform(
                jobData.RigComponent, settings.CharacterHipBone, settings.name);
            modelRoot = jobData.Animator.BindStreamTransform(model);
            offsetPosition = model != hip;
            state.Angle = jobData.RigComponent.transform.localEulerAngles.y;
        }

        public AnimationScriptPlayable CreatePlayable(PlayableGraph graph) => AnimationScriptPlayable.Create(graph, new TurnJob
        {
            Root = root,
            ModelRoot = modelRoot,
            TurnAngleDegrees = 0f,
            Weight = 0f,
            OffsetPosition = offsetPosition
        }, 1);

        public AnimationLayerSettings GetSettings() => settings;

        public void OnPreAnimationUpdate(float deltaTime, float weight)
        {
            if (!KCurves.IsWeightRelevant(weight))
            {
                context.SetTurnOffsetDegrees(0f);
                return;
            }

            TurnRequest request = state.Advance(
                context.ViewDeltaDegrees.x,
                deltaTime,
                settings.AngleThreshold,
                settings.TurnSpeed,
                settings.TurnCurve,
                weight);
            context.SetTurnOffsetDegrees(TurnOffsetDegrees * weight);

            if (request == TurnRequest.None)
            {
                return;
            }

            string triggerName = request == TurnRequest.Left
                ? settings.AnimatorTurnLeftTrigger
                : settings.AnimatorTurnRightTrigger;
            owner.TrySetAnimatorTrigger(triggerName);
        }

        public void UpdatePlayableJobData(AnimationScriptPlayable playable, float weight)
        {
            TurnJob job = playable.GetJobData<TurnJob>();
            job.TurnAngleDegrees = state.Angle;
            job.Weight = weight;
            playable.SetJobData(job);
        }

        public void OnPostAnimationUpdate() { }
        public void Dispose() { }
    }
}
