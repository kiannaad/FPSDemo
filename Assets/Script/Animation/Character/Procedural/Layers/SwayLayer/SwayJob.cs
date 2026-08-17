using UnityEngine;
using UnityEngine.Animations;

namespace CGame.Animation
{
    public struct SwayRuntimeState
    {
        private Vector2 freeAimTarget;
        private Vector3 movePosition;
        private Vector3 moveRotation;
        private Vector3 aimPosition;
        private Vector3 aimRotation;
        private VectorSpringState movePositionState;
        private VectorSpringState moveRotationState;
        private VectorSpringState aimPositionState;
        private VectorSpringState aimRotationState;

        public Vector2 FreeAimValue { get; private set; }
        public KTransform MovePose => new KTransform(movePosition, Quaternion.Euler(moveRotation));
        public KTransform AimPose => new KTransform(aimPosition, Quaternion.Euler(aimRotation));

        public void Advance(
            Vector2 viewDelta,
            Vector2 moveInput,
            bool useFreeAim,
            float deltaTime,
            SwayLayerSettings settings)
        {
            if (useFreeAim)
            {
                freeAimTarget += viewDelta * settings.FreeAimInputScale;
                freeAimTarget.x = Mathf.Clamp(freeAimTarget.x, -settings.FreeAimClamp, settings.FreeAimClamp);
                freeAimTarget.y = Mathf.Clamp(freeAimTarget.y, -settings.FreeAimClamp, settings.FreeAimClamp);
            }
            else
            {
                freeAimTarget = Vector2.zero;
            }

            float freeAimAlpha = ExpDecay(settings.FreeAimInterpSpeed, deltaTime);
            FreeAimValue = Vector2.Lerp(FreeAimValue, freeAimTarget, freeAimAlpha);

            Vector3 movePositionTarget = new Vector3(moveInput.x, moveInput.y, moveInput.y) / 100f;
            Vector3 moveRotationTarget = new Vector3(moveInput.y, moveInput.x, moveInput.x);
            float moveAlpha = ExpDecay(settings.MoveTargetDamping, deltaTime);
            movePositionTarget = Vector3.Lerp(movePosition, movePositionTarget, moveAlpha);
            moveRotationTarget = Vector3.Lerp(moveRotation, moveRotationTarget, moveAlpha);
            movePosition = KSpringMath.Interpolate(
                movePosition, movePositionTarget, settings.MovePositionSpring, ref movePositionState, deltaTime);
            moveRotation = KSpringMath.Interpolate(
                moveRotation, moveRotationTarget, settings.MoveRotationSpring, ref moveRotationState, deltaTime);

            Vector3 aimPositionTarget = new Vector3(viewDelta.x, viewDelta.y, 0f) / 100f;
            Vector3 aimRotationTarget = new Vector3(viewDelta.y, viewDelta.x, viewDelta.x);
            float aimAlpha = ExpDecay(settings.AimTargetDamping, deltaTime);
            aimPositionTarget = Vector3.Lerp(aimPositionTarget, Vector3.zero, aimAlpha);
            aimRotationTarget = Vector3.Lerp(aimRotationTarget, Vector3.zero, aimAlpha);
            aimPosition = KSpringMath.Interpolate(
                aimPosition, aimPositionTarget, settings.AimPositionSpring, ref aimPositionState, deltaTime);
            aimRotation = KSpringMath.Interpolate(
                aimRotation, aimRotationTarget, settings.AimRotationSpring, ref aimRotationState, deltaTime);
        }

        private static float ExpDecay(float speed, float deltaTime)
        {
            return deltaTime <= 0f ? 0f : 1f - Mathf.Exp(-Mathf.Max(0f, speed) * deltaTime);
        }
    }

    public struct SwayJob : IAnimationJob
    {
        public TransformStreamHandle Root;
        public TransformStreamHandle Weapon;
        public TransformStreamHandle Head;
        public WeaponLayerJobData WeaponData;
        public Vector2 FreeAimAngles;
        public KTransform MovePose;
        public KTransform AimPose;
        public TransformSpace FreeAimSpace;
        public TransformSpace MoveSpace;
        public TransformSpace AimSpace;
        public float Weight;

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!KCurves.IsWeightRelevant(Weight)) return;
            WeaponData.Cache(stream);
            Quaternion freeAimRotation = Quaternion.Euler(FreeAimAngles.y, FreeAimAngles.x, 0f);
            Vector3 pivotOffset = Head.GetPosition(stream) - Weapon.GetPosition(stream);
            Vector3 positionOffset = -(freeAimRotation * pivotOffset - pivotOffset);
            Apply(stream, new KTransform(positionOffset, freeAimRotation), FreeAimSpace);
            Apply(stream, MovePose, MoveSpace);
            Apply(stream, AimPose, AimSpace);
            WeaponData.PostProcessPose(stream, Weight);
        }

        public void ProcessRootMotion(AnimationStream stream) { }

        private void Apply(AnimationStream stream, KTransform transform, TransformSpace space)
        {
            AnimationLayerJobUtility.ModifyTransform(stream, Root, Weapon, new KPose
            {
                Pose = transform,
                Space = space,
                ModifyMode = TransformModifyMode.Add
            }, Weight);
        }
    }
}
