using UnityEngine;
using UnityEngine.Animations;

namespace CGame.Animation
{
    public struct IkHandle
    {
        public TransformStreamHandle Root;
        public TransformStreamHandle Mid;
        public TransformStreamHandle Tip;
        public TransformStreamHandle Target;
        public TransformStreamHandle Hint;
        public KTwoBoneIkData Data;

        public IkHandle(Animator animator, Transform tip, Transform target, Transform hint)
        {
            Tip = animator.BindStreamTransform(tip);
            Mid = animator.BindStreamTransform(tip.parent);
            Root = animator.BindStreamTransform(tip.parent.parent);
            Target = animator.BindStreamTransform(target);
            Hint = animator.BindStreamTransform(hint);
            Data = new KTwoBoneIkData
            {
                HasValidHint = true,
                HintWeight = 1f,
                RotationWeight = 1f,
                PositionWeight = 1f
            };
        }

        public void Solve(AnimationStream stream, float weight)
        {
            if (!KCurves.IsWeightRelevant(weight)) return;
            Data.Tip = AnimationLayerJobUtility.GetTransform(stream, Tip);
            Data.Mid = AnimationLayerJobUtility.GetTransform(stream, Mid);
            Data.Root = AnimationLayerJobUtility.GetTransform(stream, Root);
            Data.Target = AnimationLayerJobUtility.GetTransform(stream, Target);
            Data.Hint = AnimationLayerJobUtility.GetTransform(stream, Hint);
            if (Data.Tip.Equals(Data.Target, false)) return;
            KTwoBoneIK.Solve(ref Data);
            Root.SetRotation(stream, Quaternion.Slerp(Root.GetRotation(stream), Data.Root.Rotation, weight));
            Mid.SetRotation(stream, Quaternion.Slerp(Mid.GetRotation(stream), Data.Mid.Rotation, weight));
            Tip.SetRotation(stream, Quaternion.Slerp(Tip.GetRotation(stream), Data.Tip.Rotation, weight));
        }
    }

    public struct IkJob : IAnimationJob
    {
        public TransformStreamHandle Root;
        public IkHandle RightHand;
        public IkHandle LeftHand;
        public IkHandle RightFoot;
        public IkHandle LeftFoot;
        public float Weight;
        public float TurnOffset;
        public float RightHandWeight;
        public float LeftHandWeight;
        public float RightFootWeight;
        public float LeftFootWeight;
        public bool IsHuman;
        public bool OffsetFeetTargets;

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!KCurves.IsWeightRelevant(Weight)) return;
            RightHand.Solve(stream, Weight * RightHandWeight);
            LeftHand.Solve(stream, Weight * LeftHandWeight);
            if (!IsHuman || !stream.isHumanStream)
            {
                RightFoot.Solve(stream, Weight * RightFootWeight);
                LeftFoot.Solve(stream, Weight * LeftFootWeight);
                return;
            }

            AnimationHumanStream human = stream.AsHuman();
            KTransform rootTransform = AnimationLayerJobUtility.GetTransform(stream, Root);
            KTransform rightGoal = AnimationLayerJobUtility.GetTransform(stream, RightFoot.Target);
            KTransform leftGoal = AnimationLayerJobUtility.GetTransform(stream, LeftFoot.Target);
            rightGoal = rootTransform.GetRelativeTransform(rightGoal, false);
            leftGoal = rootTransform.GetRelativeTransform(leftGoal, false);
            if (OffsetFeetTargets) rootTransform.Rotation *= Quaternion.Euler(0f, -TurnOffset, 0f);
            rightGoal = rootTransform.GetWorldTransform(rightGoal, false);
            leftGoal = rootTransform.GetWorldTransform(leftGoal, false);
            SetFootGoal(human, AvatarIKGoal.RightFoot, rightGoal);
            SetFootGoal(human, AvatarIKGoal.LeftFoot, leftGoal);
        }

        public void ProcessRootMotion(AnimationStream stream) { }

        private static void SetFootGoal(AnimationHumanStream human, AvatarIKGoal goal, KTransform pose)
        {
            human.SetGoalWeightPosition(goal, 1f);
            human.SetGoalPosition(goal, pose.Position);
            human.SetGoalWeightRotation(goal, 1f);
            human.SetGoalRotation(goal, human.GetGoalRotationFromPose(goal));
        }
    }
}
