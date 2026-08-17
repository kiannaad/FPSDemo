using System;
using CGame.Animation.Rig;
using UnityEngine;

namespace CGame.Animation
{
    [CreateAssetMenu(menuName = "CGame/Animation/Procedural/IK Motion Layer", fileName = "IkMotionLayerSettings")]
    public sealed class IkMotionLayerSettings : AnimationLayerSettings
    {
        [SerializeField] private KRigElement targetBone;
        [SerializeField] private VectorCurve rotationCurves = default;
        [SerializeField] private VectorCurve translationCurves = default;
        [SerializeField] private Vector3 rotationScale = Vector3.one;
        [SerializeField] private Vector3 translationScale = Vector3.one;
        [SerializeField, Min(0f)] private float blendTime;
        [SerializeField, Min(0.0001f)] private float playRate = 1f;
        [SerializeField] private bool autoBlendOut = true;

        public KRigElement TargetBone => targetBone;
        public VectorCurve RotationCurves => rotationCurves;
        public VectorCurve TranslationCurves => translationCurves;
        public Vector3 RotationScale => rotationScale;
        public Vector3 TranslationScale => translationScale;
        public float BlendTime => blendTime;
        public float PlayRate => playRate;
        public bool AutoBlendOut => autoBlendOut;
        public float Duration => Mathf.Max(rotationCurves.Length, translationCurves.Length);
        public override IAnimationLayerJob CreateAnimationJob() => new IkMotionLayerJob();

        public override void Validate(KRig expectedRig)
        {
            base.Validate(expectedRig);
            RigHandleUtility.ResolveElement(expectedRig, targetBone, name + " target bone");
            if (!rotationCurves.IsValid || !translationCurves.IsValid)
            {
                throw new InvalidOperationException(name + " IK motion curves must contain X, Y and Z curves.");
            }
            if (Duration <= 0f || playRate <= 0f || blendTime < 0f)
            {
                throw new InvalidOperationException(name + " IK motion duration and play rate must be positive and blend time non-negative.");
            }
        }

        protected override void OnRigUpdated()
        {
            SynchronizeRigElement(ref targetBone);
        }
    }
}
