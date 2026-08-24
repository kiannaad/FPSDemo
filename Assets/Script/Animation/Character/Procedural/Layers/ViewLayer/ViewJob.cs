using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace CGame.Animation
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public struct ViewDebugSample
    {
        public Vector3 WeaponBefore;
        public Vector3 WeaponAfter;
        public float Weight;
        public byte Captured;
    }
#endif

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
        public float AimingWeight;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public NativeArray<ViewDebugSample> DebugSamples;
#endif

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!KCurves.IsWeightRelevant(Weight)) return;
            KTransform weaponBefore = AnimationLayerJobUtility.GetTransform(stream, Weapon);
            AnimationLayerJobUtility.ModifyTransform(
                stream,
                Root,
                Weapon,
                WeaponPose.ToPose(),
                Weight * (1f - AimingWeight));
            AnimationLayerJobUtility.ModifyTransform(stream, Root, RightHand, RightHandPose.ToPose(), Weight);
            AnimationLayerJobUtility.ModifyTransform(stream, Root, LeftHand, LeftHandPose.ToPose(), Weight);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugSamples.IsCreated && DebugSamples[0].Captured == 0)
            {
                DebugSamples[0] = new ViewDebugSample
                {
                    WeaponBefore = weaponBefore.Position,
                    WeaponAfter = AnimationLayerJobUtility.GetTransform(stream, Weapon).Position,
                    Weight = Weight,
                    Captured = 1
                };
            }
#endif
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }
}
