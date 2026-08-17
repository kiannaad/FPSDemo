using UnityEngine;
using UnityEngine.Animations;

namespace CGame.Animation
{
    public struct AdditiveJob : IAnimationJob
    {
        public TransformStreamHandle Root;
        public TransformStreamHandle Weapon;
        public TransformStreamHandle AdditiveBone;
        public WeaponLayerJobData WeaponData;
        public KTransform RecoilOffset;
        public float CurveScale;
        public float Weight;

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!KCurves.IsWeightRelevant(Weight)) return;
            WeaponData.Cache(stream);
            AnimationLayerJobUtility.ModifyTransform(stream, Weapon, Weapon, new KPose
            {
                Pose = RecoilOffset,
                Space = TransformSpace.BoneSpace,
                ModifyMode = TransformModifyMode.Add
            }, Weight);
            KTransform additive = AnimationLayerJobUtility.GetTransform(stream, AdditiveBone, false);
            AnimationLayerJobUtility.ModifyTransform(stream, Root, Weapon, new KPose
            {
                Pose = additive,
                Space = TransformSpace.ComponentSpace,
                ModifyMode = TransformModifyMode.Add
            }, Weight * CurveScale);
            WeaponData.PostProcessPose(stream, Weight);
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }
}
