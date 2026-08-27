using UnityEngine;
using UnityEngine.Animations;
using Unity.Collections;

namespace CGame.Animation
{
    public struct IkDebugSample
    {
        public Vector3 TargetPosition;
        public Quaternion TargetRotation;
        public Vector3 HintPosition;
        public Vector3 PreSolveTipPosition;
        public Quaternion PreSolveTipRotation;
        public Vector3 PostSolveTipPosition;
        public Quaternion PostSolveTipRotation;
        public float RootToTargetDistance;
        public byte WasSolved;
    }

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

        public void Solve(AnimationStream stream, float weight, ref IkDebugSample debugSample)
        {
            Data.Tip = AnimationLayerJobUtility.GetTransform(stream, Tip);
            Data.Mid = AnimationLayerJobUtility.GetTransform(stream, Mid);
            Data.Root = AnimationLayerJobUtility.GetTransform(stream, Root);
            Data.Target = AnimationLayerJobUtility.GetTransform(stream, Target);
            Data.Hint = AnimationLayerJobUtility.GetTransform(stream, Hint);

            debugSample.TargetPosition = Data.Target.Position;
            debugSample.TargetRotation = Data.Target.Rotation;
            debugSample.HintPosition = Data.Hint.Position;
            debugSample.PreSolveTipPosition = Data.Tip.Position;
            debugSample.PreSolveTipRotation = Data.Tip.Rotation;
            debugSample.PostSolveTipPosition = Data.Tip.Position;
            debugSample.PostSolveTipRotation = Data.Tip.Rotation;
            debugSample.RootToTargetDistance = Vector3.Distance(Data.Root.Position, Data.Target.Position);
            debugSample.WasSolved = 0;

            if (!KCurves.IsWeightRelevant(weight) || Data.Tip.Equals(Data.Target, false)) return;
            KTwoBoneIK.Solve(ref Data);
            Root.SetRotation(stream, Quaternion.Slerp(Root.GetRotation(stream), Data.Root.Rotation, weight));
            Mid.SetRotation(stream, Quaternion.Slerp(Mid.GetRotation(stream), Data.Mid.Rotation, weight));
            Tip.SetRotation(stream, Quaternion.Slerp(Tip.GetRotation(stream), Data.Tip.Rotation, weight));
            debugSample.PostSolveTipPosition = Data.Tip.Position;
            debugSample.PostSolveTipRotation = Data.Tip.Rotation;
            debugSample.WasSolved = 1;
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
        public NativeArray<IkDebugSample> DebugSamples;

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!KCurves.IsWeightRelevant(Weight)) return;
            IkDebugSample rightHandSample = default;
            IkDebugSample leftHandSample = default;
            RightHand.Solve(stream, Weight * RightHandWeight, ref rightHandSample);
            LeftHand.Solve(stream, Weight * LeftHandWeight, ref leftHandSample);
            if (DebugSamples.IsCreated)
            {
                DebugSamples[0] = rightHandSample;
                DebugSamples[1] = leftHandSample;
            }
            if (!IsHuman || !stream.isHumanStream)
            {
                IkDebugSample rightFootSample = default;
                IkDebugSample leftFootSample = default;
                RightFoot.Solve(stream, Weight * RightFootWeight, ref rightFootSample);
                LeftFoot.Solve(stream, Weight * LeftFootWeight, ref leftFootSample);
                if (DebugSamples.IsCreated)
                {
                    DebugSamples[2] = rightFootSample;
                    DebugSamples[3] = leftFootSample;
                }
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
