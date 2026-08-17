using System;
using CGame.Animation.Rig;
using UnityEngine;

namespace CGame.Animation
{
    [CreateAssetMenu(menuName = "CGame/Animation/Procedural/Additive Layer", fileName = "AdditiveLayerSettings")]
    public sealed class AdditiveLayerSettings : WeaponLayerSettings
    {
        [SerializeField] private KRigElement additiveBone;
        [SerializeField, Min(0f)] private float interpolationSpeed;
        [SerializeField] private string aimingCurve;
        [SerializeField, Range(0f, 1f)] private float adsScalar = 1f;

        public KRigElement AdditiveBone => additiveBone;
        public float InterpolationSpeed => interpolationSpeed;
        public string AimingCurve => aimingCurve;
        public float AdsScalar => adsScalar;
        public override IAnimationLayerJob CreateAnimationJob() => new AdditiveLayerJob();

        public override void Validate(KRig expectedRig)
        {
            base.Validate(expectedRig);
            RigHandleUtility.ResolveElement(expectedRig, additiveBone, name + " additive bone");
            if (interpolationSpeed < 0f) throw new InvalidOperationException(name + " interpolation speed must be non-negative.");
        }

        protected override void OnRigUpdated()
        {
            base.OnRigUpdated();
            SynchronizeRigElement(ref additiveBone);
        }
    }
}
