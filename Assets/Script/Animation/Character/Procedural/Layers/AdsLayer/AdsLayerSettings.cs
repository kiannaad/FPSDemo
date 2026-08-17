using System;
using CGame.Animation.Rig;
using UnityEngine;

namespace CGame.Animation
{
    [Serializable]
    public struct AdsAxisBlend
    {
        [Range(0f, 1f)] public float X;
        [Range(0f, 1f)] public float Y;
        [Range(0f, 1f)] public float Z;
        public Vector3 Value => new Vector3(X, Y, Z);
    }

    [CreateAssetMenu(menuName = "CGame/Animation/Procedural/ADS Layer", fileName = "AdsLayerSettings")]
    public sealed class AdsLayerSettings : WeaponLayerSettings
    {
        [SerializeField] private KRigElement aimTargetBone;
        [SerializeField] private EaseMode aimingEaseMode = EaseMode.EaseInOut;
        [SerializeField] private AdsAxisBlend positionBlend;
        [SerializeField] private AdsAxisBlend rotationBlend;
        [SerializeField, Min(0.01f)] private float aimingSpeed = 6f;
        [SerializeField] private EaseMode aimPointEaseMode = EaseMode.EaseInOut;
        [SerializeField, Min(0.01f)] private float aimPointSpeed = 8f;
        [SerializeField, Range(0f, 1f)] private float cameraBlend;

        public KRigElement AimTargetBone => aimTargetBone;
        public EaseMode AimingEaseMode => aimingEaseMode;
        public Vector3 PositionBlend => positionBlend.Value;
        public Vector3 RotationBlend => rotationBlend.Value;
        public float AimingSpeed => aimingSpeed;
        public EaseMode AimPointEaseMode => aimPointEaseMode;
        public float AimPointSpeed => aimPointSpeed;
        public float CameraBlend => cameraBlend;
        public override IAnimationLayerJob CreateAnimationJob() => new AdsLayerJob();

        public override void Validate(KRig expectedRig)
        {
            base.Validate(expectedRig);
            RigHandleUtility.ResolveElement(expectedRig, aimTargetBone, name + " aim target");
            if (aimingSpeed < 0.01f || aimPointSpeed < 0.01f)
            {
                throw new InvalidOperationException(name + " ADS speeds must be positive.");
            }
        }

        protected override void OnRigUpdated()
        {
            base.OnRigUpdated();
            SynchronizeRigElement(ref aimTargetBone);
        }
    }
}
