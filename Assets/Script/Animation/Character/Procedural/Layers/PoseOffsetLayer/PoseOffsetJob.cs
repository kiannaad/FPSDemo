using Unity.Collections;
using UnityEngine.Animations;

namespace CGame.Animation
{
    public struct PoseOffsetJobData
    {
        public KTransform Pose;
        public TransformSpace Space;
        public TransformModifyMode ModifyMode;

        public PoseOffsetJobData(KPose pose)
        {
            Pose = pose.Pose;
            Space = pose.Space;
            ModifyMode = pose.ModifyMode;
        }

        public KPose ToPose()
        {
            return new KPose { Pose = Pose, Space = Space, ModifyMode = ModifyMode };
        }
    }

    public struct PoseOffsetJob : IAnimationJob
    {
        public TransformStreamHandle Root;
        public NativeArray<TransformStreamHandle> Handles;
        public NativeArray<PoseOffsetJobData> Poses;
        public float Weight;

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!KCurves.IsWeightRelevant(Weight)) return;
            for (int index = 0; index < Handles.Length; index++)
            {
                AnimationLayerJobUtility.ModifyTransform(stream, Root, Handles[index], Poses[index].ToPose(), Weight);
            }
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }
}
