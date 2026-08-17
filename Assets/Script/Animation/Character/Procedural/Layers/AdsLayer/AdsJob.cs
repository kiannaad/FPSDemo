using UnityEngine;
using UnityEngine.Animations;

namespace CGame.Animation
{
    public struct AdsRuntimeState
    {
        private KTransform previousAimPoint;
        private KTransform targetAimPoint;
        private float aimPointPlayback;
        public float AimingWeight { get; private set; }
        public KTransform AimPoint { get; private set; }

        public void Advance(bool isAiming, KTransform aimPoint, float deltaTime, float aimingSpeed, float aimPointSpeed, EaseMode ease)
        {
            AimingWeight = Mathf.MoveTowards(AimingWeight, isAiming ? 1f : 0f, Mathf.Max(0f, aimingSpeed) * Mathf.Max(0f, deltaTime));
            if (!targetAimPoint.Equals(aimPoint, false))
            {
                previousAimPoint = AimPoint;
                targetAimPoint = aimPoint;
                aimPointPlayback = 0f;
            }
            aimPointPlayback = Mathf.Clamp01(aimPointPlayback + Mathf.Max(0f, aimPointSpeed) * Mathf.Max(0f, deltaTime));
            AimPoint = KTransform.Lerp(previousAimPoint, targetAimPoint, KCurves.EvaluateEase(aimPointPlayback, ease));
        }
    }

    public struct AdsJob : IAnimationJob
    {
        public TransformStreamHandle Root;
        public TransformStreamHandle Weapon;
        public TransformStreamHandle AimTarget;
        public WeaponLayerJobData WeaponData;
        public KTransform AimPointOffset;
        public Vector3 PositionBlend;
        public Vector3 RotationBlend;
        public float AimingWeight;
        public float CameraBlend;
        public EaseMode AimingEase;
        public float Weight;

        public void ProcessAnimation(AnimationStream stream)
        {
            float weight = KCurves.EvaluateEase(AimingWeight, AimingEase) * Weight;
            if (!KCurves.IsWeightRelevant(weight)) return;
            WeaponData.Cache(stream);
            KTransform root = AnimationLayerJobUtility.GetTransform(stream, Root);
            KTransform weapon = root.GetRelativeTransform(AnimationLayerJobUtility.GetTransform(stream, Weapon), false);
            KTransform target = root.GetRelativeTransform(AnimationLayerJobUtility.GetTransform(stream, AimTarget), false);
            Vector3 rawPosition = target.Position - weapon.Position;
            Vector3 position = Vector3.Scale(rawPosition, Vector3.one - PositionBlend) + AimPointOffset.Position;
            Quaternion rawRotation = Quaternion.Inverse(weapon.Rotation);
            Vector3 rawEuler = Normalize(rawRotation.eulerAngles);
            Vector3 rotationEuler = Vector3.Scale(rawEuler, Vector3.one - RotationBlend) + Normalize(AimPointOffset.Rotation.eulerAngles);
            KTransform pose = new KTransform(position, Quaternion.Euler(rotationEuler));
            AnimationLayerJobUtility.ModifyTransform(stream, Root, Weapon, new KPose
            {
                Pose = pose,
                Space = TransformSpace.ComponentSpace,
                ModifyMode = TransformModifyMode.Add
            }, weight * (1f - CameraBlend));
            if (CameraBlend > 0f)
            {
                AnimationLayerJobUtility.ModifyTransform(stream, Root, AimTarget, new KPose
                {
                    Pose = new KTransform(-position, Quaternion.identity),
                    Space = TransformSpace.ComponentSpace,
                    ModifyMode = TransformModifyMode.Add
                }, weight * CameraBlend);
            }
            WeaponData.PostProcessPose(stream, weight);
        }

        public void ProcessRootMotion(AnimationStream stream) { }

        private static Vector3 Normalize(Vector3 value)
        {
            value.x = Normalize(value.x);
            value.y = Normalize(value.y);
            value.z = Normalize(value.z);
            return value;
        }

        private static float Normalize(float value) => value > 180f ? value - 360f : value;
    }
}
