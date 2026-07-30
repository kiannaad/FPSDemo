using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CGame.Animation
{
    public sealed class RecoilReactionNode : AnimationNodeBase
    {
        private readonly IAnimationPlayableNode inputNode;
        private readonly Transform spine;
        private readonly Transform chest;
        private readonly Transform upperChest;
        private AnimationScriptPlayable scriptPlayable;
        private float impulse = 4f;
        private float maxPitch = 9f;
        private float decayTime = 0.12f;
        private float currentPitch;
        private ulong lastActionId;

        public RecoilReactionNode(IAnimationPlayableNode inputNode, Animator animator)
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

        public float CurrentPitch => currentPitch;
        public ulong LastActionId => lastActionId;

        public void Configure(float impulse, float maxPitch, float decayTime)
        {
            this.impulse = Mathf.Max(0f, impulse);
            this.maxPitch = Mathf.Max(0f, maxPitch);
            this.decayTime = Mathf.Max(0.001f, decayTime);
        }

        public bool Trigger(ulong actionId)
        {
            if (actionId == 0ul || actionId == lastActionId)
            {
                return false;
            }

            lastActionId = actionId;
            currentPitch = Mathf.Min(maxPitch, currentPitch + impulse);
            return true;
        }

        public void Cancel()
        {
            currentPitch = 0f;
        }

        public override void Update(AnimationGraphContext context, float deltaTime)
        {
            inputNode.Update(context, deltaTime);
            float decay = 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / decayTime);
            currentPitch = Mathf.Lerp(currentPitch, 0f, decay);
            RecoilReactionJob job = scriptPlayable.GetJobData<RecoilReactionJob>();
            job.Pitch = -currentPitch;
            scriptPlayable.SetJobData(job);
        }

        public override AnimationPoseHandle Evaluate(AnimationGraphContext context)
        {
            inputNode.Evaluate(context);
            return new AnimationPoseHandle(scriptPlayable, 1f, context.EvaluateFrameId, nameof(RecoilReactionNode));
        }

        public override AnimationNodeDebugSnapshot GetDebugSnapshot()
        {
            return new AnimationNodeDebugSnapshot(nameof(RecoilReactionNode), scriptPlayable.IsValid(), maxPitch > 0f ? currentPitch / maxPitch : 0f, 1);
        }

        protected override void OnInitialize(AnimationGraphContext context)
        {
            inputNode.Initialize(context);
            var job = new RecoilReactionJob
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
