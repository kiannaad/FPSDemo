using System;
using CGame.Animation.Rig;

namespace CGame.Animation
{
    public enum TransformSpace
    {
        BoneSpace,
        ParentBoneSpace,
        ComponentSpace,
        WorldSpace
    }

    public enum TransformModifyMode
    {
        Add,
        Replace,
        Ignore
    }

    [Serializable]
    public struct KPose
    {
        public KRigElement Element;
        public KTransform Pose;
        public TransformSpace Space;
        public TransformModifyMode ModifyMode;
    }
}
