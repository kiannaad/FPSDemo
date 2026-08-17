using UnityEngine;
using UnityEngine.Animations;

namespace CGame.Animation
{
    public struct CollisionRuntimeState
    {
        public KTransform BlockingPose { get; private set; }

        public void Advance(bool hasHit, float hitDistance, float deltaTime, CollisionLayerSettings settings)
        {
            KTransform target = KTransform.Identity;
            if (hasHit)
            {
                float ratio = 1f - Mathf.Clamp01(hitDistance / settings.ProbeLength);
                target = KTransform.Lerp(
                    KTransform.Identity,
                    settings.UseSecondaryPose ? settings.SecondaryPose : settings.PrimaryPose,
                    ratio);
            }
            float alpha = settings.SmoothingSpeed <= 0f
                ? 1f
                : 1f - Mathf.Exp(-settings.SmoothingSpeed * Mathf.Max(0f, deltaTime));
            BlockingPose = KTransform.Lerp(Normalize(BlockingPose), target, alpha);
        }

        private static KTransform Normalize(KTransform value)
        {
            return value.Rotation == default ? KTransform.Identity : value;
        }
    }

    public struct CollisionJob : IAnimationJob
    {
        public TransformStreamHandle Root;
        public TransformStreamHandle Weapon;
        public KTransform BlockingPose;
        public TransformSpace TargetSpace;
        public float RayStartOffset;
        public float Weight;
        public Vector3 ProbeOrigin;
        public Vector3 ProbeDirection;

        public void ProcessAnimation(AnimationStream stream)
        {
            ProbeDirection = Weapon.GetRotation(stream) * Vector3.forward;
            ProbeOrigin = Weapon.GetPosition(stream) - ProbeDirection * RayStartOffset;
            if (!KCurves.IsWeightRelevant(Weight)) return;
            AnimationLayerJobUtility.ModifyTransform(stream, Root, Weapon, new KPose
            {
                Pose = BlockingPose,
                Space = TargetSpace,
                ModifyMode = TransformModifyMode.Add
            }, Weight);
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }
}
