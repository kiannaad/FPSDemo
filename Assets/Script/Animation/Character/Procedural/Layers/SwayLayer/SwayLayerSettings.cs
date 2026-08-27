using System;
using CGame.Animation.Rig;
using UnityEngine;

namespace CGame.Animation
{
    [CreateAssetMenu(menuName = "CGame/Animation/Procedural/Sway Layer", fileName = "SwayLayerSettings")]
    public sealed class SwayLayerSettings : WeaponLayerSettings
    {
        [Header("Free Aim")]
        [SerializeField] private KRigElement headBone;
        [SerializeField, Min(0f)] private float freeAimClamp = 10f;
        [SerializeField, Min(0f)] private float freeAimInterpSpeed = 12f;
        [SerializeField, Min(0f)] private float freeAimInputScale = 1f;
        [SerializeField] private TransformSpace freeAimSpace = TransformSpace.ComponentSpace;
        [Header("Move Sway")]
        [SerializeField] private VectorSpring movePositionSpring = VectorSpring.Identity;
        [SerializeField] private VectorSpring moveRotationSpring = VectorSpring.Identity;
        [SerializeField, Min(0f)] private float moveTargetDamping = 12f;
        [SerializeField] private TransformSpace moveSpace = TransformSpace.ComponentSpace;
        [Header("Aim Sway")]
        [SerializeField] private VectorSpring aimPositionSpring = VectorSpring.Identity;
        [SerializeField] private VectorSpring aimRotationSpring = VectorSpring.Identity;
        [SerializeField, Min(0f)] private float aimTargetDamping = 12f;
        [SerializeField] private TransformSpace aimSpace = TransformSpace.ComponentSpace;

        public KRigElement HeadBone => headBone;
        public float FreeAimClamp => freeAimClamp;
        public float FreeAimInterpSpeed => freeAimInterpSpeed;
        public float FreeAimInputScale => freeAimInputScale;
        public TransformSpace FreeAimSpace => freeAimSpace;
        public VectorSpring MovePositionSpring => movePositionSpring;
        public VectorSpring MoveRotationSpring => moveRotationSpring;
        public float MoveTargetDamping => moveTargetDamping;
        public TransformSpace MoveSpace => moveSpace;
        public VectorSpring AimPositionSpring => aimPositionSpring;
        public VectorSpring AimRotationSpring => aimRotationSpring;
        public float AimTargetDamping => aimTargetDamping;
        public TransformSpace AimSpace => aimSpace;
        public override IAnimationLayerJob CreateAnimationJob() => new SwayLayerJob();

        public override void Validate(KRig expectedRig)
        {
            base.Validate(expectedRig);
            RigHandleUtility.ResolveElement(expectedRig, headBone, name + " head bone");
            if (freeAimClamp < 0f || freeAimInterpSpeed < 0f || freeAimInputScale < 0f
                || moveTargetDamping < 0f || aimTargetDamping < 0f)
            {
                throw new InvalidOperationException(name + " sway rates must be non-negative.");
            }
        }

        protected override void OnRigUpdated()
        {
            base.OnRigUpdated();
            SynchronizeRigElement(ref headBone);
        }
    }
}
