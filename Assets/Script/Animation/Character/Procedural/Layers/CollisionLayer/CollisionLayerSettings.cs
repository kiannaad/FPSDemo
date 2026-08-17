using System;
using CGame.Animation.Rig;
using UnityEngine;

namespace CGame.Animation
{
    [CreateAssetMenu(menuName = "CGame/Animation/Procedural/Collision Layer", fileName = "CollisionLayerSettings")]
    public sealed class CollisionLayerSettings : AnimationLayerSettings
    {
        [SerializeField] private KRigElement weaponIkBone;
        [SerializeField] private KTransform primaryPose = default;
        [SerializeField] private KTransform secondaryPose = default;
        [SerializeField] private TransformSpace targetSpace = TransformSpace.ComponentSpace;
        [SerializeField] private bool useSecondaryPose;
        [SerializeField, Min(0f)] private float smoothingSpeed;
        [SerializeField, Min(0f)] private float rayStartOffset;
        [SerializeField, Min(0f)] private float barrelLength = 1f;

        public KRigElement WeaponIkBone => weaponIkBone;
        public KTransform PrimaryPose => primaryPose;
        public KTransform SecondaryPose => secondaryPose;
        public TransformSpace TargetSpace => targetSpace;
        public bool UseSecondaryPose => useSecondaryPose;
        public float SmoothingSpeed => smoothingSpeed;
        public float RayStartOffset => rayStartOffset;
        public float BarrelLength => barrelLength;
        public float ProbeLength => rayStartOffset + barrelLength;
        public override IAnimationLayerJob CreateAnimationJob() => new CollisionLayerJob();

        public override void Validate(KRig expectedRig)
        {
            base.Validate(expectedRig);
            RigHandleUtility.ResolveElement(expectedRig, weaponIkBone, name + " weapon IK bone");
            if (smoothingSpeed < 0f || rayStartOffset < 0f || barrelLength <= 0f)
            {
                throw new InvalidOperationException(name + " collision distances and smoothing must be valid.");
            }
        }

        protected override void OnRigUpdated()
        {
            SynchronizeRigElement(ref weaponIkBone);
        }
    }
}
