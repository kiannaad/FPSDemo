using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class AimOffsetNode : AnimationNodeBase
    {
        private readonly IAnimationPlayableNode inputNode;
        private readonly Transform spine;
        private readonly Transform chest;
        private readonly Transform upperChest;
        private AnimationScriptPlayable scriptPlayable;
        private float yawRange = 90f;
        private float pitchUpRange = 90f;
        private float pitchDownRange = 90f;
        private float maxWeight = 1f;
        private float smoothingTime = 0.08f;
        private float currentYaw;
        private float currentPitch;
        private float currentWeight;

        public AimOffsetNode(IAnimationPlayableNode inputNode, Animator animator)
        {
            this.inputNode = inputNode ?? throw new ArgumentNullException(nameof(inputNode));
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            spine = CharacterBoneResolver.Resolve(animator, HumanBodyBones.Spine, "Spine");
            chest = CharacterBoneResolver.Resolve(animator, HumanBodyBones.Chest, "Chest");
            upperChest = CharacterBoneResolver.Resolve(
                animator,
                HumanBodyBones.UpperChest,
                "UpperChest");
        }

        public float CurrentYaw => currentYaw;
        public float CurrentPitch => currentPitch;
        public float CurrentWeight => currentWeight;
        public AnimationScriptPlayable ScriptPlayable => scriptPlayable;

        public void Configure(float yawRange, float pitchUpRange, float pitchDownRange, float maxWeight, float smoothingTime)
        {
            this.yawRange = Mathf.Max(0f, yawRange);
            this.pitchUpRange = Mathf.Max(0f, pitchUpRange);
            this.pitchDownRange = Mathf.Max(0f, pitchDownRange);
            this.maxWeight = Mathf.Clamp01(maxWeight);
            this.smoothingTime = Mathf.Max(0f, smoothingTime);
        }

        public override void Update(AnimationGraphContext context, float deltaTime)
        {
            inputNode.Update(context, deltaTime);
            float targetYaw = Mathf.Clamp(context.AimYaw, -yawRange, yawRange);
            float targetPitch = Mathf.Clamp(context.AimPitch, -pitchUpRange, pitchDownRange);
            float targetWeight = Mathf.Clamp01(context.AimWeight) * maxWeight;
            float blend = smoothingTime <= 0f ? 1f : 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / smoothingTime);
            currentYaw = Mathf.Lerp(currentYaw, targetYaw, blend);
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, blend);
            currentWeight = Mathf.Lerp(currentWeight, targetWeight, blend);
            AimOffsetJob job = scriptPlayable.GetJobData<AimOffsetJob>();
            job.Yaw = currentYaw;
            job.Pitch = currentPitch;
            job.Weight = currentWeight;
            scriptPlayable.SetJobData(job);
        }

        public override AnimationPoseHandle Evaluate(AnimationGraphContext context)
        {
            inputNode.Evaluate(context);
            return new AnimationPoseHandle(scriptPlayable, 1f, context.EvaluateFrameId, nameof(AimOffsetNode));
        }

        public override AnimationNodeDebugSnapshot GetDebugSnapshot()
        {
            return new AnimationNodeDebugSnapshot(nameof(AimOffsetNode), scriptPlayable.IsValid(), currentWeight, 1);
        }

        protected override void OnInitialize(AnimationGraphContext context)
        {
            inputNode.Initialize(context);
            var job = new AimOffsetJob
            {
                Spine = Bind(context, spine),
                Chest = Bind(context, chest),
                UpperChest = Bind(context, upperChest),
            };
            scriptPlayable = AnimationScriptPlayable.Create(context.Graph, job, 1);
            context.Graph.Connect(inputNode.Evaluate(context).Playable, 0, scriptPlayable, 0);
            scriptPlayable.SetInputWeight(0, 1f);
        }

        protected override void OnDestroy()
        {
            inputNode.Destroy();
        }

        private static TransformStreamHandle Bind(AnimationGraphContext context, Transform bone)
        {
            return bone != null ? context.Animator.BindStreamTransform(bone) : default;
        }
    }
}
