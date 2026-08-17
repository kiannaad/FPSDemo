using UnityEngine;
using UnityEngine.Animations;

namespace CGame.Animation
{
    public struct WeaponLayerJobData
    {
        private TransformStreamHandle weaponHandle;
        private TransformStreamHandle rightElbowHandle;
        private TransformStreamHandle leftElbowHandle;
        private KTransform cachedWeapon;
        private KTransform cachedRightElbow;
        private KTransform cachedLeftElbow;
        private Vector3 animatedPivotOffset;
        private float hintTargetWeight;

        public void Initialize(LayerJobData jobData, WeaponLayerSettings settings)
        {
            weaponHandle = RigHandleUtility.Bind(
                jobData.Animator, jobData.RigComponent, settings.WeaponIkBone, settings.name + " weapon IK bone");
            rightElbowHandle = RigHandleUtility.Bind(
                jobData.Animator, jobData.RigComponent, settings.RightElbow, settings.name + " right elbow");
            leftElbowHandle = RigHandleUtility.Bind(
                jobData.Animator, jobData.RigComponent, settings.LeftElbow, settings.name + " left elbow");
            animatedPivotOffset = settings.AnimatedPivotOffset;
            hintTargetWeight = settings.HintTargetWeight;
        }

        public void Cache(AnimationStream stream)
        {
            cachedWeapon = AnimationLayerJobUtility.GetTransform(stream, weaponHandle);
            cachedRightElbow = AnimationLayerJobUtility.GetTransform(stream, rightElbowHandle);
            cachedLeftElbow = AnimationLayerJobUtility.GetTransform(stream, leftElbowHandle);
        }

        public void PostProcessPose(AnimationStream stream, float weight)
        {
            float alpha = Mathf.Clamp01(weight);
            if (!KCurves.IsWeightRelevant(alpha))
            {
                return;
            }

            KTransform weapon = AnimationLayerJobUtility.GetTransform(stream, weaponHandle);
            KTransform rightElbow = AnimationLayerJobUtility.GetTransform(stream, rightElbowHandle);
            KTransform leftElbow = AnimationLayerJobUtility.GetTransform(stream, leftElbowHandle);

            KTransform rightHint = KTransform.Lerp(cachedRightElbow, rightElbow, hintTargetWeight);
            KTransform leftHint = KTransform.Lerp(cachedLeftElbow, leftElbow, hintTargetWeight);
            rightHint = KTransform.Lerp(rightElbow, rightHint, alpha);
            leftHint = KTransform.Lerp(leftElbow, leftHint, alpha);
            rightElbowHandle.SetPosition(stream, rightHint.Position);
            rightElbowHandle.SetRotation(stream, rightHint.Rotation);
            leftElbowHandle.SetPosition(stream, leftHint.Position);
            leftElbowHandle.SetRotation(stream, leftHint.Rotation);

            KTransform delta = cachedWeapon.GetRelativeTransform(weapon, false);
            Vector3 pivotDelta = delta.Rotation * animatedPivotOffset - animatedPivotOffset;
            Vector3 desiredPosition = weapon.Position + weapon.Rotation * pivotDelta;
            weaponHandle.SetPosition(stream, Vector3.Lerp(weapon.Position, desiredPosition, alpha));
        }
    }
}
