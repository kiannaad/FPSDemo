using System;
using CGame.Animation.Rig;
using UnityEngine;

namespace CGame.Animation
{
    [CreateAssetMenu(menuName = "CGame/Animation/Procedural/Turn Layer", fileName = "TurnLayerSettings")]
    public sealed class TurnLayerSettings : AnimationLayerSettings
    {
        [SerializeField] private KRigElement characterRootBone = new KRigElement(-1, "Skeleton", -1);
        [SerializeField] private KRigElement characterHipBone = new KRigElement(-1, "Hips", -1);
        [SerializeField] private KRigElement upperBodyRootBone = new KRigElement(-1, "Spine", -1);
        [SerializeField, Range(0f, 180f)] private float angleThreshold = 90f;
        [SerializeField] private AnimationCurve turnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Min(0f)] private float turnSpeed = 1f;
        [SerializeField] private string animatorTurnRightTrigger = "TurnRight";
        [SerializeField] private string animatorTurnLeftTrigger = "TurnLeft";

        public KRigElement CharacterRootBone => characterRootBone;
        public KRigElement CharacterHipBone => characterHipBone;
        public KRigElement UpperBodyRootBone => upperBodyRootBone;
        public float AngleThreshold => angleThreshold;
        public AnimationCurve TurnCurve => turnCurve;
        public float TurnSpeed => turnSpeed;
        public string AnimatorTurnRightTrigger => animatorTurnRightTrigger;
        public string AnimatorTurnLeftTrigger => animatorTurnLeftTrigger;
        public override IAnimationLayerJob CreateAnimationJob() => new TurnLayerJob();

        public override void Validate(KRig expectedRig)
        {
            base.Validate(expectedRig);
            RigHandleUtility.ResolveElement(expectedRig, characterRootBone, name + " root bone");
            RigHandleUtility.ResolveElement(expectedRig, characterHipBone, name + " hip bone");
            RigHandleUtility.ResolveElement(expectedRig, upperBodyRootBone, name + " upper body root bone");
            if (turnCurve == null) throw new InvalidOperationException(name + " turn curve is missing.");
            if (angleThreshold < 0f || turnSpeed < 0f) throw new InvalidOperationException(name + " turn values must be non-negative.");
            if (string.IsNullOrWhiteSpace(animatorTurnRightTrigger)
                || string.IsNullOrWhiteSpace(animatorTurnLeftTrigger))
            {
                throw new InvalidOperationException(name + " turn triggers are missing.");
            }
        }

        protected override void OnRigUpdated()
        {
            SynchronizeRigElement(ref characterRootBone);
            SynchronizeRigElement(ref characterHipBone);
            SynchronizeRigElement(ref upperBodyRootBone);
        }
    }
}
