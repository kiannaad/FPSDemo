using UnityEngine.Animations;

namespace CGame.Animation
{
    public struct ViewJob : IAnimationJob
    {
        public TransformStreamHandle Root;
        public TransformStreamHandle Weapon;
        public TransformStreamHandle RightHand;
        public TransformStreamHandle LeftHand;
        public PoseOffsetJobData WeaponPose;
        public PoseOffsetJobData RightHandPose;
        public PoseOffsetJobData LeftHandPose;
        public float Weight;

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!KCurves.IsWeightRelevant(Weight)) return;
            AnimationLayerJobUtility.ModifyTransform(stream, Root, Weapon, WeaponPose.ToPose(), Weight);
            AnimationLayerJobUtility.ModifyTransform(stream, Root, RightHand, RightHandPose.ToPose(), Weight);
            AnimationLayerJobUtility.ModifyTransform(stream, Root, LeftHand, LeftHandPose.ToPose(), Weight);
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }
}
